# ADR 0011 — RGPD : tâches de fond en base, export asynchrone et suppression différée

- Statut : accepté
- Date : 2026-10-12

## Contexte
Avant une version commerciale, chaque utilisateur doit pouvoir récupérer ses données
(portabilité, article 20) et faire effacer son compte (article 17), sans écrire au support.
Un export avec les fichiers audio peut peser plusieurs centaines de mégaoctets, et une
suppression doit aussi effacer des fichiers du stockage : ni l'un ni l'autre ne tient dans
une requête HTTP. D'autres tâches à venir ont le même besoin d'exécution différée :
notifications (#12), conversion audio (#15), antivirus (#17), corbeille (#20), export d'une
sortie (#24).

## Décision
- **Tâches de fond en base** : une table `BackgroundJobs` (Type, Payload, Status, Attempts,
  RunAfter, Error) et un `BackgroundService` qui la consulte toutes les 5 secondes. Une tâche
  est ajoutée par `IBackgroundJobs.Enqueue` et enregistrée par le même `SaveChangesAsync`
  que la donnée qui la motive : jamais de tâche sans donnée, ni l'inverse.
- **Prise atomique** (`UPDLOCK, READPAST`) : plusieurs instances de l'API peuvent tourner
  sans prendre deux fois la même tâche. Trois essais (après 1 puis 5 minutes), puis
  `Failed` avec le message d'erreur ; une tâche restée `Running` plus de 30 minutes (API
  arrêtée pendant l'exécution) est reprise.
- **Pas de planificateur** : une tâche « programmée » est une tâche dont `RunAfter` est dans
  le futur (purge à J+30, expiration de l'export à J+7).
- **Export** : `POST /api/me/export` crée un `DataExport` et sa tâche. L'archive ZIP contient
  `data.json` (compte, adhésions, invitations envoyées et reçues, morceaux, versions et
  métadonnées des fichiers de tous les groupes du compte) et les fichiers. Elle se
  télécharge depuis « Mon compte », connexion exigée, pendant 7 jours ; un e-mail prévient
  quand elle est prête, sans lien secret. Un seul export en préparation à la fois.
- **Suppression** : `POST /api/me/delete`, mot de passe redemandé. Refusée (409) si le
  compte est le dernier Owner d'un groupe qui a d'autres membres. Sinon, dans une seule
  transaction : départ des groupes partagés, compte verrouillé sans fin (la connexion répond
  `Lockedout`), nouvelle empreinte de sécurité (sessions coupées en une minute), purge
  programmée à J+30.
- **Purge** : compte et tables d'Identity, groupes dont il était le seul membre avec leurs
  morceaux, versions et fichiers, exports, invitations adressées à son adresse. La base
  d'abord, en une transaction, puis les fichiers. Un compte réactivé entre-temps n'est pas
  purgé.

## Alternatives écartées
- **Hangfire ou Quartz.NET** : une dépendance et une dizaine de tables pour deux types de
  tâches aujourd'hui ; la table et le service tiennent en une centaine de lignes, testées.
- **File de messages (Service Bus, RabbitMQ)** : un service de plus à héberger et à
  superviser, sans besoin de débit.
- **Export pendant la requête** : requête de plusieurs minutes, coupée par nginx
  (`proxy_read_timeout`) et par le navigateur.
- **Lien de téléchargement signé dans l'e-mail** : un e-mail transféré ou une boîte
  compromise donnerait toutes les données ; la connexion est déjà là pour les protéger.
- **Suppression immédiate et définitive** : aucun retour possible après une erreur ou un
  compte piraté ; 30 jours restent dans le délai d'un mois de l'article 12.
- **Anonymiser le compte au lieu de le supprimer** : rien dans le modèle n'a besoin de
  garder un compte fantôme ; les morceaux appartiennent aux groupes, pas aux comptes.
- **E-mails d'Identity dans la table** : une écriture en base seulement pour un compte
  connu rallongerait « mot de passe oublié » et trahirait son existence ; la file en
  mémoire de l'ADR 0010 reste.

## Conséquences
- L'adresse d'un compte supprimé reste prise pendant 30 jours : on ne peut pas se
  réinscrire avec avant la purge.
- Réactiver un compte pendant ces 30 jours est une opération manuelle du support
  (runbook) ; les adhésions aux groupes partagés sont perdues.
- Les invitations envoyées par le compte dans des groupes partagés restent (elles
  appartiennent au groupe) avec un identifiant d'auteur orphelin.
- Les sauvegardes contiennent encore les données jusqu'à leur rotation (durée fixée en #7,
  à indiquer dans la politique de confidentialité).
- Si l'API s'arrête entre la purge en base et l'effacement des fichiers, des fichiers
  restent orphelins ; `scripts/purge-orphan-files.ps1` les retrouve (il connaît aussi les
  archives d'export).
- Les tests d'intégration coupent le service d'arrière-plan et lancent les tâches
  eux-mêmes, avec une horloge qu'ils avancent.