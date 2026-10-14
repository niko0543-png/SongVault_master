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
| `GET` | `/api/files/policy` | Extensions acceptées et taille maximale |

Sous /api/bands/{bandId}, les lectures sont ouvertes à tout membre du groupe et les écritures demandent au moins le rôle Member, 
sauf mention contraire ([ADR 0008](docs/adr/0008-roles-dans-le-groupe.md)).
Les erreurs suivent la RFC 9457 (application/problem+json) : 400 validation, 401 non
connecté, 403 rôle insuffisant dans le groupe (ou invitation destinée à une autre adresse),
404 introuvable (ou groupe dont on n'est pas membre), 409 conflit de concurrence ou dernier
propriétaire du groupe, 410 invitation déjà utilisée, annulée ou expirée, 413 fichier trop
volumineux, 422 règle métier, 429 trop de requêtes.
---

## Qualité et tests

| Niveau | Outils | Nombre | Ce qui est vérifié |
|---|---|---|---|
| Domaine | xUnit | … | règles métier, sans base ni conteneur |
| Application | xUnit, faux repositories | … | cas d'usage |
| Intégration API | xUnit, WebApplicationFactory, **Testcontainers SQL Server** | … | HTTP de bout en bout contre un vrai SQL Server : codes d'erreur, concurrence, isolation par utilisateur, sécurité des fichiers |
| Front | Vitest, Vue Test Utils | … | stores Pinia, composables, garde de navigation |
| Bout en bout | Playwright | 2 | inscription → morceau → version → fichier → lecture → déconnexion |

<!-- Remplacez les … par les chiffres de `dotnet test` et `npm run test:unit -- --run`. -->

- Couverture du code : voir [docs/test-coverage.md](docs/test-coverage.md)
  (`scripts/coverage.ps1` génère le rapport).
- Accessibilité Lighthouse : **…/100** sur la liste, le détail d'un morceau et le détail
  d'une version.
- Requêtes SQL maîtrisées : 2 pour la liste, 3 pour le détail d'un morceau, 4 pour le
  détail d'une version, quel que soit le volume de données.

**Lancer les tests :**

```bash
dotnet test                                   # backend (Docker requis pour Testcontainers)
cd web/songvault-web
npm ci
npm run test:unit -- --run                    # front
npx playwright test                           # E2E (application lancée sur :8080)
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
- **Clés Data Protection** chiffrées par une clé Key Vault sur Azure.
- **Limitation de débit** sur l'authentification et l'envoi de fichiers (429).
- **En-têtes de sécurité** : `Content-Security-Policy`, `X-Content-Type-Options`,
  `Referrer-Policy: no-referrer`, `X-Frame-Options: DENY`.
- **Secrets** : aucun dans le dépôt. `.env` est ignoré par Git, `.env.example` documente
  les variables ; en développement, `dotnet user-secrets`.
- **Dépendances** : mises à jour proposées automatiquement par Dependabot, avec la CI comme
  garde-fou.

---

## Développer sans Docker

**Prérequis :** SDK .NET 10, Node.js (version indiquée dans `web/songvault-web/.nvmrc`), et un SQL Server (LocalDB, ou le
conteneur `db` de Compose publié sur `localhost,1433`).

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
  ([ADR 0009](docs/adr/0009-invitations-et-e-mails.md)).
- **Santé** : `GET /health/live` (processus vivant) et `GET /health/ready` (base joignable)
  sur l'API, utilisés par les healthchecks Compose.
- **Journaux** : JSON structurés hors développement (`docker compose logs api`).
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
- Pas de traitement audio côté serveur (forme d'onde, transcodage).

Pistes suivies dans les [issues](https://github.com/<owner>/<repo>/issues).

---

## Licence

Code sous licence [MIT](LICENSE).
Les fichiers de `demo-assets/` ont leurs propres conditions :
voir [demo-assets/LICENSE.md](demo-assets/LICENSE.md).

---

**Auteur :** <Votre nom> — développeur .NET ·
[LinkedIn](https://www.linkedin.com/in/<profil>) · [GitHub](https://github.com/<owner>)
