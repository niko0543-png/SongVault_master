# ADR 0025 — Supervision en production : OpenTelemetry vers Application Insights, alertes par e-mail, page de statut publique

- Statut : accepté
- Date : 2026-10-17

## Contexte
Le backlog (#18) demande des traces et des métriques OpenTelemetry, un suivi des erreurs, des
alertes et une page de statut, avec trois critères : une alerte sur le taux d'erreurs 5xx,
chaque erreur reliée à sa trace, aucune donnée personnelle dans la télémétrie.
Jusqu'ici, la production n'avait que les journaux de la console des conteneurs, dans l'espace
Log Analytics `songvault-logs` (ADR 0012) : pas de trace d'une requête, et personne n'est
prévenu d'une panne. Le trafic est faible (quelques groupes) et le budget serré.

## Décision
- **Application Insights**, en mode espace de travail : `songvault-insights` range ses données
  dans `songvault-logs`, déjà payé à l'usage. L'API envoie traces, métriques et journaux par
  OpenTelemetry avec le paquet Microsoft `Azure.Monitor.OpenTelemetry.AspNetCore` (la
  « distribution » Azure Monitor : instrumentation d'ASP.NET Core, de HttpClient et de SqlClient,
  exportateur). C'est le seul paquet ajouté. Sentry a été écarté : un service de plus, hors
  d'Azure, avec ses propres comptes et son propre contrat de sous-traitance.
- **Activée par la chaîne de connexion**, `APPLICATIONINSIGHTS_CONNECTION_STRING`, posée par
  `infra/main.bicep` comme secret de la Container App. Sans elle (PC, tests, CI, Compose), rien
  n'est envoyé et rien ne change.
- **Échantillonnage** par défaut de la distribution : 5 traces par seconde au plus, donc tout à
  notre trafic. Les erreurs partent toujours, même hors échantillon
  (`EnableTraceBasedLogsSampler = false`). Live Metrics est coupé. Les sondes `/health/*`
  ne sont pas tracées.
- **Chaque erreur reliée à sa trace** : un journal écrit pendant une requête porte l'identifiant
  de sa trace, qui devient `operation_Id` dans Application Insights ; l'exception s'ouvre
  sur sa requête, ses appels SQL et HTTP. Une réponse 500 donne aussi cet identifiant à
  l'utilisateur, `reference` dans le ProblemDetails, que le front affiche (« Référence : … ») :
  on retrouve l'erreur d'après ce qu'il nous envoie.
- **Aucune donnée personnelle dans la télémétrie**, par construction :
  - deux processeurs OpenTelemetry nettoient traces et journaux avant l'export
    (`RedactingActivityProcessor`, `RedactingLogProcessor`) : adresses e-mail remplacées par
    `[e-mail]`, jetons des liens (`/api/share/…`, `/api/invitations/…`, `/s/…`, `/invite/…`)
    par `{token}`, valeurs de la chaîne de requête par `Redacted` ; une exception dont le texte
    contient une adresse est remplacée par une copie nettoyée ;
  - le navigateur (`user_agent.original`) et l'adresse IP ne partent pas ; Application Insights
    ne garde de toute façon pas les IP (`DisableIpMasking: false`) ;
  - les journaux ne nomment plus le destinataire d'un e-mail ; les journaux de `LogEmailSender`
    (le texte des e-mails, liens secrets compris) ne partent jamais ;
  - pas de télémétrie dans le navigateur : rien n'est ajouté au front ;
  - les requêtes SQL sont paramétrées par EF Core : leur texte ne contient pas de valeur.
  Les identifiants techniques (GUID d'un groupe, d'un morceau, d'un compte) restent : ce sont
  des pseudonymes, utiles pour corriger, sans lien avec une personne hors de la base.
- **Volume** : vers Application Insights, journaux à partir de `Warning`, sauf ceux de SongVault
  (`Information`) ; plafond de 0,3 Go par jour sur l'espace `songvault-logs` (9 Go par mois au pire,
  les 5 premiers Go du mois étant gratuits). Plafond atteint : plus rien n'est collecté jusqu'au
  lendemain, alertes comprises.
- **Alertes Azure Monitor par e-mail**, à l'adresse passée au déploiement (`-AlertEmail`,
  jamais dans le dépôt) :
  - **taux d'erreurs 5xx** : plus de 5 % des requêtes sur 15 minutes, dès 10 requêtes, vérifié
    toutes les 5 minutes (gravité 1) ;
  - **erreurs enregistrées** : au moins une exception dans le quart d'heure (gravité 2), pour une
    erreur isolée ou une tâche de fond en échec, qui ne fait pas de 5xx.
  Un second e-mail part quand c'est rentré dans l'ordre. Environ 2 € par mois les deux.
- **Page de statut publique** : `/statut` dans le front, qui lit `GET /api/status` (anonyme,
  30 requêtes par minute et par IP, calculée au plus toutes les 30 secondes). Trois parties :
  l'application, la base de données, les tâches de fond (en retard si une tâche attend depuis
  plus de 15 minutes : « ralenti »). Jamais de message d'erreur. Base injoignable : 503,
  « en panne ». API injoignable : le front s'affiche quand même et le dit.
- **Disponibilité vue de l'extérieur** : le workflow GitHub `uptime` lit `/api/status` toutes les
  15 minutes, à travers le front comme un visiteur. En panne, le run échoue et GitHub l'écrit par
  e-mail. Gratuit pour un dépôt public ; les tests de disponibilité d'Application Insights, payants,
  ne sont pas utilisés.

## Conséquences
- Un incident se lit dans Application Insights : « Échecs » pour les erreurs et leurs traces,
  « Recherche de transactions » pour une référence donnée par un utilisateur (runbook Azure,
  « Supervision »).
- Coût : environ 2 € par mois d'alertes ; données dans la part gratuite de Log Analytics au trafic
  actuel, une dizaine d'euros par mois au pire avec le plafond.
- Le nettoyage repose sur des motifs connus : une nouvelle route à jeton dans le chemin doit
  être ajoutée à `TelemetryRedactor` (et à ses tests).
- GitHub suspend les workflows programmés d'un dépôt sans activité depuis 60 jours : le
  réactiver depuis l'onglet Actions si besoin.
- Les journaux de la console (`ContainerAppConsoleLogs_CL`) restent tels quels ; tant que
  `Email:Provider=Log` en production (avant #27), ils contiennent le texte des e-mails.