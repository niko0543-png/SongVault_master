# 🎸 SongVault

[![CI](https://github.com/<owner>/<repo>/actions/workflows/ci.yml/badge.svg)](https://github.com/<owner>/<repo>/actions/workflows/ci.yml)
![.NET 10](https://img.shields.io/badge/.NET-10%20LTS-512BD4)
![Vue 3](https://img.shields.io/badge/Vue-3-42b883)
![Licence MIT](https://img.shields.io/badge/licence-MIT-blue)

**SongVault** est une application web qui permet à un musicien ou à un groupe de gérer ses
morceaux et leurs **versions successives**, de l'idée acoustique à l'arrangement studio.
Chaque version regroupe maquettes audio, tablatures, paroles et notes ; on l'écoute dans le
navigateur et on la compare à une autre version.

> Projet portfolio réalisé en 8 semaines pour démontrer une stack .NET et web actuelle :
> .NET 10, ASP.NET Core, EF Core, Vue 3 + TypeScript, Docker et GitHub Actions.

![Comparaison de deux versions](docs/images/comparaison-ab.png)

---

## Sommaire

- [Démarrer en 5 minutes](#démarrer-en-5-minutes)
- [Fonctionnalités](#fonctionnalités)
- [Captures d'écran](#captures-décran)
- [Stack technique](#stack-technique)
- [Architecture](#architecture)
- [Organisation du dépôt](#organisation-du-dépôt)
- [API](#api)
- [Qualité et tests](#qualité-et-tests)
- [Sécurité](#sécurité)
- [Développer sans Docker](#développer-sans-docker)
- [Intégration continue](#intégration-continue)
- [Exploitation](#exploitation)
- [Documentation](#documentation)
- [Limites connues et évolutions](#limites-connues-et-évolutions)
- [Licence](#licence)

---

## Démarrer en 5 minutes

**Prérequis :** [Docker Desktop](https://docs.docker.com/desktop/) (ou Docker Engine avec
Compose v2). Rien d'autre : ni SDK .NET, ni Node.js, ni SQL Server.

```bash
git clone https://github.com/<owner>/<repo>.git
cd <repo>
cp .env.example .env            # PowerShell : Copy-Item .env.example .env
# Éditez .env : MSSQL_SA_PASSWORD et DEMO_PASSWORD
docker compose --profile demo up -d --build
```

Le premier démarrage prend quelques minutes (téléchargement des images, compilation).
Ensuite :

1. Ouvrez **http://localhost:8080**.
2. Connectez-vous avec **`demo@songvault.local`** et le mot de passe `DEMO_PASSWORD` de
   votre fichier `.env`.
3. Ouvrez un morceau, écoutez la v2, puis comparez la v2 et la v3.

Le profil `demo` ajoute un compte et des morceaux d'exemple. Le chargement est idempotent :
le relancer ne duplique rien. Sans `--profile demo`, l'application démarre vide.

Un compte créé depuis l'écran d'inscription doit confirmer son adresse avant de se
connecter. Avec la configuration par défaut (`EMAIL_PROVIDER=Log`), aucun e-mail ne part :
le lien de confirmation est dans les journaux (`docker compose logs api`).

**Arrêter :**

```bash
docker compose down          # conserve les données
docker compose down -v       # supprime aussi la base et les fichiers
```

---

## Fonctionnalités

- **Morceaux et versions** : chaque version reçoit un numéro (v1, v2…) attribué par le
  serveur, sans doublon même en cas de créations simultanées.
- **Fiche de version** : statut (Idée → Maquette → Arrangement → Répétition → Studio →
  Finale), BPM, tonalité, notes et
  paroles.
- **Fichiers** : maquettes audio (MP3, WAV, FLAC), tablatures (PDF, Guitar Pro), images.
  Jusqu'à 2 Go, **envoyés directement au stockage** (Azure Blob) par une URL signée, avec la
  progression ; extension, taille et **signature binaire** vérifiées à la confirmation.
- **Lecteur audio global** qui reste actif d'une page à l'autre, avec déplacement dans le
  morceau (requêtes HTTP `Range`) et raccourci clavier lecture/pause.
- **Comparaison A/B** de deux versions d'un même morceau, avec une URL partageable, et leurs
  différences : lignes ajoutées et supprimées des paroles et des notes, champs et fichiers modifiés.
- **Commentaires horodatés** sur les fichiers audio (« à 1:42, la basse décale ») : un clic
  sur l'horodatage lance la lecture à cet instant ; réponses, résolution des fils. Les invités
  peuvent commenter.
- **Sorties** : singles, EP et albums avec leur pochette et leurs pistes dans l'ordre, chaque
  piste dans la version choisie. Un morceau peut figurer sur plusieurs sorties ; les pistes se
  réordonnent à la souris ou au clavier.
- **Badges et fil d'activité** : une pastille « Nouveau » ou « Modifié » signale les morceaux
  changés par un autre membre depuis votre dernière visite ; la page « Activité » raconte qui a
  fait quoi (« ana a ajouté la v4 de Nocturne »).
- **Notifications par e-mail** : nouvelle version, commentaire, mention (« @ » suivi de
  l'adresse d'un membre), chacune tout de suite, dans le résumé du matin ou jamais, au choix
  dans « Mon compte ». Chaque e-mail a son lien de désinscription.
- **Liens de partage externes** : un lien d'écoute seule vers une version ou une sortie, pour
  une personne sans compte (ingénieur du son, label, salle), valable 1 à 90 jours, avec un mot
  de passe facultatif. Révocable à tout moment ; chaque ouverture et chaque écoute sont au journal.
- **Recherche et filtres** synchronisés avec l'URL.
- **Comptes utilisateurs et groupes** : chaque morceau appartient à un groupe, un utilisateur peut être membre de plusieurs groupes et ne voit que les leurs. 
  Trois rôles par groupe : propriétaire (gère les membres), membre (crée et modifie), invité (lit et écoute).
- **Invitations par e-mail** : le propriétaire invite une adresse comme membre ou invité ; le lien, valable 7 jours et utilisable une seule fois, 
  mène à l'inscription ou à la connexion puis au groupe.
- **Compte complet** : adresse e-mail confirmée avant la première connexion, mot de passe oublié (lien valable 2 heures), changement de mot de passe depuis « Mon compte ».
- **Vos données** : export de tout ce que le compte peut consulter (archive ZIP avec `data.json` et les fichiers, préparée en arrière-plan, disponible 7 jours) ; 
  suppression du compte, effacé définitivement 30 jours plus tard ; mentions légales et politique de confidentialité.
- **Accessibilité** : navigation complète au clavier, lien d'évitement, focus géré à
  chaque changement de page, boîtes de confirmation accessibles.
- **Mobile et application installable** : mise en page prévue dès 360 px de large, menu
  replié, lecteur au pouce avec ±10 s et commandes sur l'écran verrouillé ; s'installe sur
  l'écran d'accueil d'Android et d'iOS, avec une page claire hors connexion.
- **Forme d'onde et aperçu audio** : chaque fichier audio affiche sa forme d'onde, qui se
  colore pendant l'écoute et permet d'aller à un endroit d'un clic ; les WAV et FLAC sont
  écoutés à travers un MP3 léger préparé en arrière-plan par ffmpeg, l'original reste celui
  qu'on télécharge. Un échec de conversion s'affiche et se relance.
- **Formules et quotas** : chaque groupe a sa formule (Gratuit 2 Go, Groupe 50 Go, Studio
  250 Go), une jauge de stockage sur la liste des morceaux, et un refus clair au-delà. Le
  propriétaire paie par Stripe (Checkout et portail client : carte, factures, résiliation),
  activé par `Billing:Provider` ; sans lui, formules et quotas s'appliquent quand même.
- **Analyse antivirus** : chaque fichier envoyé passe par ClamAV en arrière-plan avant de
  pouvoir être écouté, téléchargé ou partagé. Un fichier infecté est bloqué, son auteur
  prévenu par e-mail ; une analyse en échec se relance. Activée par `Antivirus:Provider`
  (en production, à la commercialisation).
- **Supervision** : en production, traces, erreurs et métriques dans Application Insights
  (OpenTelemetry), alertes par e-mail sur les erreurs, page publique `/statut` et vérification
  de disponibilité par GitHub Actions ([ADR 0025](docs/adr/0025-supervision-en-production.md)).
- **Double authentification et journal d'audit** : code d'une application (TOTP) en plus du mot
  de passe, codes de secours, historique de connexion dans « Mon compte » ; chaque groupe a un
  journal d'audit (qui a supprimé quoi, qui a invité ou retiré qui) que ses Owner consultent
  et que personne ne peut modifier ([ADR 0026](docs/adr/0026-double-authentification-et-journal-d-audit.md)).
- **Corbeille** : un morceau, une version ou un fichier supprimé se restaure pendant 30 jours,
  avec ce qui est parti avec lui ; ensuite, la purge quotidienne l'efface, fichiers compris, et
  libère la place. Un Owner peut vider la corbeille avant ([ADR 0027](docs/adr/0027-corbeille.md)).
- **Setlists** : les morceaux d'un concert dans l'ordre de jeu, réordonnés à la souris ou au
  clavier, avec tonalité, BPM, notes de scène et durée (celle du fichier audio, ou saisie) ;
  l'API calcule la durée totale, et la feuille imprimée tient sur une page
  ([ADR 0028](docs/adr/0028-setlists.md)).
- **Crédits et droits** : les ayants droit d'un morceau (auteur, compositeur, arrangeur,
  éditeur) et leur part, qui doit faire 100 % ; l'ISWC du morceau et l'ISRC de chaque version ;
  export CSV d'un morceau ou d'une sortie, qui s'ouvre tel quel dans Excel
  ([ADR 0029](docs/adr/0029-credits-et-repartition-des-droits.md)).
- **Pistes séparées** : jusqu'à 8 fichiers audio d'une version (voix, guitare, batterie…) lus
  ensemble, calés à l'échantillon près, avec muet, solo et volume pour chacun
  ([ADR 0030](docs/adr/0030-pistes-separees-et-melangeur.md)).  
- **Archive pour le distributeur** : un ZIP de la sortie (fichiers audio des versions choisies,
  pistes séparées, paroles, crédits et pochette) préparé en arrière-plan, annoncé par e-mail et
  téléchargeable 24 heures ([ADR 0031](docs/adr/0031-archive-complete-d-une-sortie.md)).
- **Paroles avec accords** : les accords au-dessus des paroles (`[Am]Je marche…`), lisibles sur
  téléphone, et la transposition par demi-tons sans jamais toucher au texte enregistré
  ([ADR 0032](docs/adr/0032-paroles-avec-accords.md)).

---

## Captures d'écran

| Liste des morceaux | Détail d'une version |
|---|---|
| ![Liste](docs/images/liste.png) | ![Version](docs/images/version.png) |

| Pull request avec CI verte | Audit Lighthouse |
|---|---|
| ![CI](docs/images/ci.png) | ![Lighthouse](docs/images/lighthouse.png) |

Une vidéo de démonstration de 3 minutes est disponible : <!-- lien vers la vidéo -->

---

## Stack technique

| Couche | Technologies |
|---|---|
| Backend | .NET 10 (LTS), ASP.NET Core Web API, EF Core 10, SQL Server 2022, ASP.NET Core Identity |
| Frontend | Vue 3 (Composition API), TypeScript, Vite, Pinia, Vue Router |
| Tests | xUnit, Testcontainers, WebApplicationFactory, Vitest, Vue Test Utils, Playwright |
| Conteneurs | Dockerfile multi-stage, Docker Compose, nginx (non privilégié) |
| CI/CD | GitHub Actions, Dependabot, protection de branche par ruleset |
| Documentation | OpenAPI (généré depuis le code), Scalar, Mermaid, ADR |

---

## Architecture

Monolithe modulaire en **architecture propre légère** : le domaine ne dépend d'aucun
paquet technique, et le compilateur garantit le sens des dépendances.

```mermaid
flowchart LR
    Browser[Navigateur] -->|":8080"| Nginx[nginx<br/>front Vue + relais /api]
    Nginx -->|"/api"| Api[API ASP.NET Core]
    Api --> Db[(SQL Server)]
    Api --> Files[(Volume fichiers)]
    Migrator[migrator<br/>bundle EF Core] -.->|"une fois, avant l'API"| Db
```

```mermaid
flowchart LR
    Api[SongVault.Api] --> Application[SongVault.Application]
    Api --> Infrastructure[SongVault.Infrastructure]
    Infrastructure --> Application
    Application --> Domain[SongVault.Domain]
```

**Points clés :**

- **Une seule origine** : nginx sert le front et relaie `/api` vers l'API. Pas de CORS, et
  un cookie d'authentification `SameSite=Strict` suffit.
- **Fichiers hors base** : SQL Server garde les métadonnées, le contenu est sur un volume
  ou dans Azure Blob Storage, derrière `IFileStorageService` (`FileStorage:Provider`). Le
  navigateur envoie et lit les fichiers directement, par des URL signées de courte durée
  ([ADR 0012](docs/adr/0012-stockage-objet-et-hebergement.md)).
- **Concurrence optimiste** : `rowversion`, index unique et nouvelles tentatives pour la
  numérotation des versions.
- **Migrations** appliquées par un conteneur ponctuel (`migrator`) avant le démarrage de
  l'API, jamais par l'API elle-même.

Détail et diagrammes : [docs/architecture.md](docs/architecture.md).
Décisions argumentées : [docs/adr](docs/adr/README.md).

| ADR | Décision |
|---|---|
| [0001](docs/adr/0001-monolithe-modulaire.md) | Monolithe modulaire plutôt que microservices |
| [0002](docs/adr/0002-fichiers-hors-base.md) | Fichiers stockés hors de la base |
| [0003](docs/adr/0003-numerotation-des-versions.md) | Numérotation des versions et concurrence optimiste |
| [0004](docs/adr/0004-cookies-plutot-que-jwt.md) | Cookie plutôt que JWT |
| [0005](docs/adr/0005-bundle-de-migrations.md) | Bundle de migrations dans un conteneur ponctuel |
| [0006](docs/adr/0006-pas-de-mediatr.md) | Ni MediatR ni repository générique |

---

## Organisation du dépôt

```text
.
├── src/
│   ├── SongVault.Domain/           # entités, règles métier, aucune dépendance technique
│   ├── SongVault.Application/      # cas d'usage (handlers), abstractions
│   ├── SongVault.Infrastructure/   # EF Core, SQL Server, stockage des fichiers
│   └── SongVault.Api/              # contrôleurs, authentification, Program.cs, Dockerfile
├── tests/
│   ├── SongVault.Domain.Tests/
│   ├── SongVault.Application.Tests/
│   └── SongVault.Api.Tests/        # tests d'intégration (Testcontainers + SQL Server)
├── web/songvault-web/              # front Vue 3 + TypeScript, Dockerfile nginx
│   ├── src/features/               # songs, versions, files, player, auth
│   └── e2e/                        # tests Playwright
├── demo-assets/                    # fichiers du jeu de démonstration
├── docs/                           # architecture, ADR, exploitation, images
├── scripts/                        # sauvegarde, couverture de tests
├── compose.yaml                    # db, migrator, api, web (+ seed avec --profile demo)
├── compose.override.yaml           # ports supplémentaires en développement
└── .github/                        # workflow CI, Dependabot
```

---

## API

Le contrat OpenAPI est généré depuis le code et ses commentaires XML. En développement :
**http://localhost:5080/scalar** (interface) ou **/openapi/v1.json** (document).

| Méthode | Route | Description |
|---|---|---|
| `POST` | `/api/auth/register` | Créer un compte |
| `POST` | `/api/auth/login?useCookies=true` | Se connecter (cookie) |
| `POST` | `/api/auth/logout` | Se déconnecter |
| `GET` | `/api/auth/me` | Utilisateur connecté |
| `GET` | `/api/auth/confirmEmail?userId=…&code=…` | Confirmer l'adresse (lien de l'e-mail) |
| `POST` | `/api/auth/resendConfirmationEmail` | Renvoyer l'e-mail de confirmation |
| `POST` | `/api/auth/forgotPassword` | Recevoir un lien de réinitialisation (même réponse, compte connu ou non) |
| `POST` | `/api/auth/resetPassword` | Choisir un nouveau mot de passe avec le code reçu |
| `POST` | `/api/auth/manage/info` | Changer de mot de passe (l'ancien est demandé) |
| `POST` | `/api/me/export` | Demander l'archive de ses données (202 ; 409 si une archive est déjà en préparation) |
| `GET` | `/api/me/export` | État de la dernière archive (204 s'il n'y en a pas) |
| `GET` | `/api/me/export/download` | Télécharger l'archive prête (ZIP) |
| `POST` | `/api/me/delete` | Supprimer son compte (mot de passe demandé ; effacé à J+30) |
| `GET` `PUT` | `/api/me/notifications` | Lire, modifier ses préférences de notification (un mode par type) |
| `POST` | `/api/notifications/unsubscribe?token=` | Lien de désinscription d'un e-mail, sans connexion (400 si le jeton est invalide) |
| `GET` `POST` | `/api/bands` | Mes groupes (avec mon rôle), créer un groupe |
| `GET` `PUT` | `/api/bands/{bandId}` | Lire, renommer un groupe (renommer : Owner) |
| `GET` | `/api/bands/{bandId}/members` | Membres du groupe et leurs rôles |
| `PUT` | `/api/bands/{bandId}/members/{userId}/role` | Changer le rôle d'un membre (Owner) |
| `DELETE` | `/api/bands/{bandId}/members/{userId}` | Retirer un membre (Owner) |
| `DELETE` | `/api/bands/{bandId}/members/me` | Quitter le groupe |
| `GET POST` | /api/bands/{bandId}/invitations | Invitations en attente, inviter une adresse (Owner) |
| `DELETE` | /api/bands/{bandId}/invitations/{id} | Annuler une invitation (Owner) |
| `GET` | /api/invitations/{token} | Aperçu d'une invitation, sans connexion |
| `POST` | /api/invitations/{token}/accept | Rejoindre le groupe avec le compte connecté |
| `GET` | `/api/bands/{bandId}/songs` | Liste paginée, recherche |
| `POST` | `/api/bands/{bandId}/songs` | Créer un morceau |
| `GET` `PUT` `DELETE` | `/api/bands/{bandId}/songs/{id}` | Lire, modifier, supprimer un morceau |
| `POST` | `/api/bands/{bandId}/songs/{id}/seen` | Noter l'ouverture du morceau : son badge disparaît (Guest compris) |
| `GET` | `/api/bands/{bandId}/activity` | Fil d'activité, du plus récent au plus ancien (`cursor`, `pageSize`) |
| `GET` `POST` | `/api/bands/{bandId}/songs/{songId}/versions` | Lister, créer des versions |
| `GET` `PUT` | `…/versions/{versionId}` | Lire, modifier une version |
| `POST` | `…/versions/{versionId}/files/uploads` | Annoncer un envoi direct (jusqu'à 2 Go) : renvoie l'adresse du `PUT` |
| `PUT` | `…/files/uploads/{uploadId}/content` | Stockage local seulement : recevoir le fichier annoncé |
| `POST` | `…/files/uploads/{uploadId}/complete` | Confirmer l'envoi : taille et signature vérifiées, fichier rattaché |
| `POST` | `…/versions/{versionId}/files` | Envoyer un fichier de 50 Mo au plus à travers l'API (scripts) |
| `GET` | `…/files/{fileId}/content` | Lire ou télécharger un fichier (`Range` accepté ; redirection vers une URL signée avec Azure Blob) |
| `DELETE` | `…/files/{fileId}` | Supprimer un fichier |
| `GET` `POST` | `…/files/{fileId}/comments` | Fils de commentaires d'un fichier audio, commenter (Guest compris) |
| `POST` | `…/comments/{commentId}/replies` | Répondre à un fil (Guest compris) |
| `PUT` `DELETE` | `…/comments/{commentId}` | Modifier (auteur), supprimer (auteur ou Owner) un commentaire |
| `POST` `DELETE` | `…/comments/{commentId}/resolve` | Résoudre, rouvrir un fil (auteur du fil, Member ou Owner) |
| `GET` `POST` | `/api/bands/{bandId}/releases` | Sorties du groupe, créer une sortie |
| `GET` `PUT` `DELETE` | `/api/bands/{bandId}/releases/{id}` | Lire (avec les pistes), modifier, supprimer une sortie |
| `PUT` | `…/releases/{id}/tracks` | Remplacer toutes les pistes, dans l'ordre envoyé |
| `GET` `PUT` `DELETE` | `…/releases/{id}/cover` | Lire, envoyer (JPEG, PNG, WebP, 5 Mo), retirer la pochette |
| `GET` `POST` | `/api/bands/{bandId}/share-links` | Liens de partage d'une version (`?versionId=`) ou d'une sortie (`?releaseId=`), créer un lien (Member ; le lien complet n'est donné qu'à la création) |
| `POST` | `…/share-links/{id}/revoke` | Révoquer un lien : il ne marche plus, il reste dans la liste |
| `DELETE` | `…/share-links/{id}` | Supprimer un lien et son journal |
| `GET` | `…/share-links/{id}/accesses` | Journal du lien : ouvertures, écoutes, mauvais mots de passe |
| `GET` | `/api/share/{token}` | Page d'un lien de partage, sans connexion (404 inconnu, expiré ou révoqué ; 403 mot de passe attendu) |
| `POST` | `/api/share/{token}/unlock` | Donner le mot de passe d'un lien ; pose le cookie `songvault.share` (422 s'il est faux) |
| `GET` | `/api/share/{token}/files/{fileId}/content` | Écouter un fichier audio de la cible du lien |
| `GET` | `/api/share/{token}/cover` | Pochette de la sortie partagée |
| `GET` | `/api/files/policy` | Extensions acceptées et taille maximale |

Sous /api/bands/{bandId}, les lectures sont ouvertes à tout membre du groupe et les écritures demandent au moins le rôle Member, 
sauf mention contraire ([ADR 0008](docs/adr/0008-roles-dans-le-groupe.md)).
Les erreurs suivent la RFC 9457 (application/problem+json) : 400 validation, 401 non
connecté, 403 rôle insuffisant dans le groupe (ou invitation destinée à une autre adresse),
404 introuvable (ou groupe dont on n'est pas membre), 409 conflit de concurrence ou dernier
propriétaire du groupe ou morceau présent sur une sortie, 410 invitation déjà utilisée, annulée ou expirée, 413 fichier trop
volumineux, 422 règle métier, 429 trop de requêtes.
---

## Qualité et tests

| Niveau | Outils | Nombre | Ce qui est vérifié |
|---|---|---|---|
| Domaine | xUnit | … | règles métier, sans base ni conteneur |
| Application | xUnit, faux repositories | … | cas d'usage |
| Intégration API | xUnit, WebApplicationFactory, **Testcontainers SQL Server** | … | HTTP de bout en bout contre un vrai SQL Server : codes d'erreur, concurrence, isolation par utilisateur, sécurité des fichiers |
| Front | Vitest, Vue Test Utils | … | stores Pinia, composables, garde de navigation |
| Bout en bout | Playwright | 7 | inscription → morceau → version → fichier → lecture → déconnexion ; à 360 px : aucun défilement horizontal, lecteur et menu ; application installable ; dérive du mélangeur après 3 minutes et après un saut |

<!-- Remplacez les … par les chiffres de `dotnet test` et `npm run test:unit -- --run`. -->

- Couverture du code : voir [docs/test-coverage.md](docs/test-coverage.md)
  (`scripts/coverage.ps1` génère le rapport).
- Accessibilité Lighthouse, en mode mobile : **…/100** sur la connexion, la liste, le détail
  d'une version et la page d'un lien de partage.
  Le … se remplace à l'étape 2 de la PR, avec le score le plus bas des quatre pages.
- Requêtes SQL maîtrisées : 2 pour la liste, 3 pour le détail d'un morceau, 4 pour le
  détail d'une version, quel que soit le volume de données.

**Lancer les tests :**

```bash
dotnet test                                   # backend (Docker requis pour Testcontainers)
cd web/songvault-web
npm ci
npm run test:unit -- --run                    # front
npx playwright test                           # E2E (application lancée sur :8080)
npx playwright test e2e/stems-drift.spec.ts   # dérive du mélangeur, sans l'application
npx playwright test e2e/chords-mobile.spec.ts # accords à 360 px, sans l'application
```

---

## Sécurité

- **Authentification** par ASP.NET Core Identity et cookie `HttpOnly`, `SameSite=Strict` :
  aucun jeton accessible à JavaScript.
- **Compte** : connexion refusée tant que l'adresse n'est pas confirmée ; « mot de passe
  oublié » répond pareil, et aussi vite, que le compte existe ou non (e-mails envoyés en
  arrière-plan) ; liens valables 24 h (confirmation) et 2 h (réinitialisation) ; un mot de
  passe changé coupe les autres sessions en une minute ([ADR 0010](docs/adr/0010-compte-complet.md)).
  **Données personnelles** : l'archive ne se télécharge que connecté (l'e-mail ne contient aucun lien secret) ; le mot de passe est redemandé avant la suppression du compte ; 
  registre des traitements dans [docs/rgpd/registre.md](docs/rgpd/registre.md) ([ADR 0011](docs/adr/0011-rgpd-taches-de-fond.md)).
- **Isolation des données** : chaque requête est limitée au groupe de l'URL ; un non-membre
  reçoit 404 (OWASP API1 — *Broken Object Level Authorization*), un membre dont le rôle ne
  suffit pas reçoit 403 (OWASP API5 — *Broken Function Level Authorization*).
- **Fichiers** : liste blanche d'extensions, taille maximale, contrôle de la **signature
  binaire** (un `.exe` renommé en `.mp3` est refusé), nom de stockage généré par le serveur.
  Conteneur privé ; URL signées limitées à un fichier, pour créer (1 h) ou lire (30 min).
  Analyse **antivirus ClamAV** de chaque fichier avant toute lecture, une fois
  `Antivirus:Provider=ClamAv` ; un fichier infecté reste bloqué ([ADR 0024](docs/adr/0024-analyse-antivirus.md)).
- **Double authentification** (TOTP d'Identity) : mot de passe redemandé pour la désactiver ou
  renouveler les codes de secours, un code faux compte comme un mot de passe faux (verrouillage) ;
  historique de connexion par compte, avec le réseau masqué ; journal d'audit des groupes protégé
  par un déclencheur SQL ([ADR 0026](docs/adr/0026-double-authentification-et-journal-d-audit.md)).
- **Télémétrie sans donnée personnelle** : adresses e-mail, jetons des liens et valeurs des URL
  retirés avant l'envoi à Application Insights ; ni adresse IP ni navigateur ([ADR 0025](docs/adr/0025-supervision-en-production.md)).
- **Clés Data Protection** chiffrées par une clé Key Vault sur Azure.
- **Limitation de débit** sur l'authentification et l'envoi de fichiers (429).
- **Liens de partage** : jeton aléatoire dont seule l'empreinte est en base, routes publiques
  en lecture seule limitées aux fichiers audio de la cible, mot de passe haché et essais
  limités, IP du journal remplacée par une empreinte salée ([ADR 0019](docs/adr/0019-liens-de-partage-externes.md)).
- **En-têtes de sécurité** : `Content-Security-Policy`, `X-Content-Type-Options`,
  `Referrer-Policy: no-referrer`, `X-Frame-Options: DENY`.
- **Secrets** : aucun dans le dépôt. `.env` est ignoré par Git, `.env.example` documente
  les variables ; en développement, `dotnet user-secrets`.
- **Dépendances** : mises à jour proposées automatiquement par Dependabot, avec la CI comme
  garde-fou.

---

## Développer sans Docker

**Prérequis :** SDK .NET 10, Node.js (version indiquée dans `web/songvault-web/.nvmrc`), un SQL Server (LocalDB, ou le
conteneur `db` de Compose publié sur `localhost,1433`), [ffmpeg](https://ffmpeg.org/) dans le `PATH`
(`winget install Gyan.FFmpeg`), qui prépare les aperçus audio et sert aux tests d'intégration, et
l'antivirus ClamAV de Compose publié sur `localhost:3310` (`docker compose up -d clamav`) : sans lui,
les fichiers envoyés restent « en cours d'analyse ».

```powershell
# 1. Configuration locale de l'API (secrets hors du dépôt)
dotnet user-secrets set "ConnectionStrings:SongVault" "Server=localhost,1433;Database=SongVault;User Id=sa;Password=<mot de passe>;TrustServerCertificate=True" --project src/SongVault.Api
dotnet user-secrets set "FileStorage:RootPath" "C:\SongVault\files" --project src/SongVault.Api

# 2. Base de données
dotnet tool restore
dotnet ef database update --project src/SongVault.Infrastructure --startup-project src/SongVault.Api

# 3. API (http://localhost:5080, Scalar sur /scalar)
dotnet run --project src/SongVault.Api --launch-profile http

# 4. Front, dans un second terminal (http://localhost:5173, /api relayé vers l'API)
cd web/songvault-web
npm ci
npm run dev
```

Paiement en mode test de Stripe (facultatif, #16) : créer les deux prix dans le tableau de bord en mode test, puis

```powershell
dotnet user-secrets set "Billing:Provider" "Stripe" --project src/SongVault.Api
dotnet user-secrets set "Billing:Stripe:SecretKey" (Read-Host 'Clé secrète de test sk_test_') --project src/SongVault.Api
dotnet user-secrets set "Billing:Stripe:Prices:band" "price_…" --project src/SongVault.Api
dotnet user-secrets set "Billing:Stripe:Prices:studio" "price_…" --project src/SongVault.Api
# Stripe CLI : relaie les événements vers l'API et affiche le secret du webhook (whsec_…) à enregistrer
stripe listen --forward-to http://localhost:5080/api/billing/stripe/webhook
dotnet user-secrets set "Billing:Stripe:WebhookSecret" "whsec_…" --project src/SongVault.Api

```
Carte de test : `4242 4242 4242 4242`, date future, n'importe quel code. Carte refusée : `4000 0000 0000 0002`.

---

## Intégration continue

Chaque pull request vers `main` déclenche [`.github/workflows/ci.yml`](.github/workflows/ci.yml) :

| Job | Étapes |
|---|---|
| `backend` | restauration, build en Release (avertissements = erreurs), tests unitaires et d'intégration |
| `frontend` | `npm ci`, vérification des types, lint, tests Vitest, build |
| `docker` | construction des images, `docker compose up`, test de fumée : inscription → connexion → appel authentifié |

La branche `main` est protégée : fusion uniquement par pull request, avec les trois
vérifications au vert. Dependabot propose chaque semaine les mises à jour NuGet, npm,
images Docker et actions GitHub, regroupées pour limiter le bruit.

---

## Exploitation

- **Configuration** : variables d'environnement définies dans `.env` (voir `.env.example`).
- **Stockage des fichiers** : `Local` (volume `songvault-files`, défaut) ou `AzureBlob`.
  `docker compose --profile azurite up` lance l'émulateur Azurite pour essayer le second
  sur le PC (lignes à décommenter dans `.env.example`).
- **Hébergement** : Azure France Central, déployé par `scripts/deploy-azure.ps1` à partir
  d'un tag ; voir le [runbook Azure](docs/runbook-azure.md).
- **E-mails** : `EMAIL_PROVIDER=Log` (défaut) écrit les e-mails dans les journaux de l'API ;
  `EMAIL_PROVIDER=Brevo` les envoie, avec `BREVO_API_KEY` et `EMAIL_FROM` (adresse validée
  chez Brevo). `PUBLIC_URL` est l'adresse du site utilisée dans les liens
  ([ADR 0009](docs/adr/0009-invitations-et-e-mails.md)). En production, Brevo envoie depuis
  un domaine authentifié, et le réglage est gardé d'un déploiement à l'autre
  ([ADR 0022](docs/adr/0022-e-mails-en-production-avec-brevo.md),
  [runbook](docs/runbook-azure.md#e-mails-brevo)).
- **Santé** : `GET /health/live` (processus vivant) et `GET /health/ready` (base joignable)
  sur l'API, utilisés par les healthchecks Compose. `GET /api/status`, ouvert à tous, alimente
  la page `/statut` (application, base de données, tâches de fond).
- **Journaux** : JSON structurés hors développement (`docker compose logs api`).
- **Supervision** : en production, Application Insights `songvault-insights`, alertes par
  e-mail et workflow `uptime` ([runbook](docs/runbook-azure.md#supervision)) ; rien n'est
  envoyé sans `APPLICATIONINSIGHTS_CONNECTION_STRING`.
- **Tâches de fond** : table `BackgroundJobs`, consultée toutes les 5 secondes par l'API (export, expiration des archives, purge des comptes) ; 
trois essais, puis `Failed` avec le message d'erreur. Tâches en échec et réactivation d'un compte supprimé : [runbook](docs/runbook-docker.md#comptes-supprimés-et-tâches-de-fond).
- **Sauvegarde** : sur Azure, restauration de la base à un instant donné (7 jours) et
  fichiers supprimés récupérables 14 jours ; RPO 1 h, RTO 4 h, exercice de restauration
  chaque trimestre ([runbook des sauvegardes](docs/runbook-sauvegardes.md)). Avec Docker :
  `scripts/` et [docs/runbook-docker.md](docs/runbook-docker.md).

---

## Documentation

- [Architecture](docs/architecture.md) — composants, flux, modèle de données
- [Décisions d'architecture (ADR)](docs/adr/README.md)
- [Registre des traitements (RGPD)](docs/rgpd/registre.md) — données, durées de conservation, sous-traitants
- [Exploitation Docker](docs/runbook-docker.md) — démarrage, sauvegarde, restauration, dépannage
- [Exploitation sur Azure](docs/runbook-azure.md) — déploiement, retour arrière, journaux, coût
- [Sauvegardes et reprise sur Azure](docs/runbook-sauvegardes.md) — objectifs, restauration, exercice trimestriel
- [Couverture des tests](docs/test-coverage.md)
- [Usage de l'IA](docs/ai-usage.md) — comment l'IA a été utilisée et ce qui a été vérifié
- [CHANGELOG](CHANGELOG.md)

---

## Limites connues et évolutions

Choix assumés pour la v1 :

- Envoi d'un fichier en une seule requête : une coupure oblige à recommencer l'envoi
  (l'envoi par blocs, avec reprise, est écarté dans l'ADR 0012).
- URL signées avec la clé du compte de stockage, pas encore par délégation d'identité
  managée (ADR 0012).
- E-mails du compte dans une file en mémoire : un e-mail pas encore parti est perdu si l'API
  s'arrête (il se redemande depuis l'écran). Les passer par les tâches de fond ralentirait
  « mot de passe oublié » pour les seuls comptes connus (ADR 0011). Les invitations partent
  encore pendant la requête.
- Les aperçus audio passent par les tâches de fond, exécutées une à une : pendant la
  conversion d'un long WAV, un e-mail ou un export attend son tour (ADR 0021).
- Pas d'écoute hors connexion : l'application installée a besoin du réseau, son service
  worker ne garde aucune donnée de l'API (ADR 0020).
- TVA non calculée par Stripe (`AutomaticTax` à faux) : prix TTC fixes, valables tant que
  les ventes restent en France sous la franchise en base (ADR 0023).

Pistes suivies dans les [issues](https://github.com/<owner>/<repo>/issues).

---

## Licence

Code sous licence [MIT](LICENSE).
Les fichiers de `demo-assets/` ont leurs propres conditions :
voir [demo-assets/LICENSE.md](demo-assets/LICENSE.md).

---

**Auteur :** <Votre nom> — développeur .NET ·
[LinkedIn](https://www.linkedin.com/in/<profil>) · [GitHub](https://github.com/<owner>)
