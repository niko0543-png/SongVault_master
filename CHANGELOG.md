# Changelog

Format : [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/) — versions : [SemVer](https://semver.org/lang/fr/).

## [Non publié]
### Ajouté
- Groupes : un morceau appartient à un groupe, un utilisateur peut être membre de plusieurs groupes (ADR 0007).
- Sélecteur de groupe dans l'en-tête ; groupe personnel créé automatiquement pour chaque compte.
- Script `scripts/purge-orphan-files.ps1` : retire du volume les fichiers qui ne sont plus référencés.
- Rôles dans le groupe : Owner gère les membres, Member crée et modifie, Guest lit et écoute ; vérifiés par l'API (ADR 0008).
- Page Membres : liste, changement de rôle, retrait d'un membre, départ du groupe.
- Invitations par e-mail : lien à usage unique valable 7 jours, page /invite qui enchaîne inscription ou connexion et adhésion, invitations en attente et annulation dans la page Membres (ADR 0009).
- Envoi d'e-mails derrière `IEmailSender` : journaux en développement, Brevo en production.
- Compte complet : confirmation de l'adresse à l'inscription, mot de passe oublié et réinitialisation (lien valable 2 h), changement de mot de passe depuis « Mon compte » (ADR 0010).
- File d'envoi des e-mails en arrière-plan ; clés Data Protection conservées en base.
- Pages Mentions légales et Politique de confidentialité, liées depuis un pied de page et l'inscription ; registre des traitements dans `docs/rgpd/registre.md`.
- Tâches de fond en base (`BackgroundJobs`) : trois essais, reprise après un arrêt de l'API, plusieurs instances possibles (ADR 0011).
- Export des données du compte depuis « Mon compte » : archive ZIP préparée en arrière-plan, e-mail quand elle est prête, disponible 7 jours.
- Suppression du compte : désactivation immédiate, départ des groupes partagés, purge à 30 jours avec les groupes dont il était le seul membre.
- Stockage Azure Blob derrière `IFileStorageService`, choisi par `FileStorage:Provider` ; Azurite dans Docker Compose avec le profil `azurite` (ADR 0012).
- Envoi direct des fichiers au stockage par URL signée, jusqu'à 2 Go, avec la progression ; lecture et téléchargement par URL signée.
- Hébergement Azure France Central : `infra/main.bicep`, workflow `images` (GitHub Container Registry) et `scripts/deploy-azure.ps1` ; runbook `docs/runbook-azure.md`.
- Clés Data Protection chiffrées par Key Vault quand `DataProtection:KeyVaultKeyUri` est défini.
- Sauvegardes sur Azure : restauration de la base à un instant donné sur 7 jours, fichiers supprimés récupérables 14 jours, verrous contre la suppression de la base et du stockage (ADR 0013).
- `scripts/undelete-files-azure.ps1` et `scripts/restore-drill-azure.ps1` (exercice de restauration chronométré) ; rappel trimestriel par le workflow `restore-drill` ; runbook `docs/runbook-sauvegardes.md`.
- Commentaires horodatés sur les fichiers audio : réponses, résolution, lecture à la position d'un clic ; ouverts aux invités (ADR 0014).
- Différences entre deux versions dans l'écran de comparaison : paroles et notes ligne à ligne, champs et fichiers modifiés, navigation au clavier (ADR 0015).
- Sorties (single, EP, album) avec pochette et pistes ordonnées, chaque piste dans la version choisie ; un morceau sur plusieurs sorties (ADR 0016).
- Badges « Nouveau » et « Modifié » dans la liste des morceaux, retirés à l'ouverture, et page « Activité » du groupe avec « Voir plus » (ADR 0017).
- Notifications par e-mail (nouvelle version, commentaire, mention) préparées en arrière-plan, tout de suite ou dans un résumé à 7 h, réglables dans « Mon compte » ; lien et en-tête de désinscription dans chaque e-mail (ADR 0018).
- Liens de partage externes : écoute seule d'une version ou d'une sortie sans compte, page `/s/{jeton}`, expiration de 1 à 90 jours, mot de passe facultatif, révocation, journal des ouvertures et des écoutes visible des membres (ADR 0019).
- Mobile : menu replié sous 720 px, pages lisibles dès 360 px de large, cibles tactiles de 44 px, lecteur au pouce avec ±10 s et commandes de l'écran verrouillé (Media Session).
- Application installable sur Android et iOS : manifeste, icônes, service worker et page « Pas de connexion » (ADR 0020).
- Forme d'onde des fichiers audio, colorée pendant l'écoute, clic ou flèches pour se déplacer ; aperçu MP3 de 128 kbit/s des WAV et FLAC préparé en arrière-plan par ffmpeg, utilisé par le lecteur et les liens de partage, l'original restant le fichier téléchargé ; échec visible et bouton « Réessayer » (ADR 0021).
- Script `scripts/test-brevo.ps1` : vérifie la clé Brevo et l'expéditeur depuis le PC, avec un e-mail de test (ADR 0022).
- Formules et quotas : Gratuit (2 Go, un groupe), Groupe (50 Go) et Studio (250 Go) dans la table `Plans` ; stockage de chaque groupe recalculé après chaque envoi et chaque nuit ; envoi refusé (402) au-delà du quota, jamais d'effacement ; second groupe gratuit refusé (ADR 0023).
- Abonnement d'un groupe par Stripe : page Facturation (propriétaires), Checkout, portail client, webhook signé et idempotent, e-mail aux propriétaires quand un paiement est refusé ; activé par `Billing:Provider=Stripe`.
- Pages publiques Formules (`/formules`) et Conditions générales de vente ; jauge de stockage sur la liste des morceaux.
- Analyse antivirus de chaque fichier par ClamAV, en arrière-plan : fichier illisible jusqu'au verdict, fichier infecté bloqué et signalé à son auteur par e-mail, analyse en échec relançable (`POST …/files/{id}/scan`) ; activée par `Antivirus:Provider=ClamAv` : conteneur `clamav` dans Docker Compose et, une fois activé, à côté de l'API dans Azure ; avec `None` (production avant la commercialisation), fichiers lisibles et marqués non analysés, remis en analyse à l'activation (ADR 0024).
- Supervision de la production : traces, erreurs et métriques OpenTelemetry vers Application Insights, sans donnée personnelle (e-mails, jetons des liens et valeurs des URL retirés avant l'envoi) ; alertes par e-mail sur le taux d'erreurs 5xx et sur les erreurs enregistrées (`-AlertEmail`) ; référence de l'erreur affichée à l'utilisateur sur une erreur 500 ; page publique `/statut` (`GET /api/status`) et vérification de disponibilité toutes les 15 minutes par le workflow `uptime` (ADR 0025).
- Double authentification par application (TOTP) dans « Mon compte » : clé à recopier ou lien `otpauth://`, 10 codes de secours, désactivation et nouveaux codes contre le mot de passe ; code demandé à la connexion, navigateur reconnu 14 jours (ADR 0026).
- Historique de connexion dans « Mon compte » (90 jours) : connexions, échecs, verrouillages et changements de sécurité, avec le réseau masqué et le navigateur.
- Journal d'audit de chaque groupe, page « Journal » des Owner : suppressions, liens de partage, invitations, rôles, départs, renommage et fichiers bloqués, gardé un an ; impossible à modifier depuis l'application (déclencheur SQL).
- Corbeille du groupe (page « Corbeille », `/api/bands/{id}/trash`) : morceaux, versions et fichiers supprimés se restaurent pendant 30 jours, avec exactement ce qui est parti avec eux ; suppression définitive par un Owner ; purge quotidienne ensuite, fichiers du stockage compris (ADR 0027).
- Suppression d'une version (`DELETE …/versions/{id}`), refusée si une sortie l'utilise.
- Setlists (page « Setlists », `/api/bands/{id}/setlists`) : nom, date, lieu et morceaux dans l'ordre de jeu, réordonnés à la souris ou au clavier ; version facultative (sinon la dernière), tonalité et BPM de la version, notes de scène ; durée du fichier audio ou saisie, durée totale calculée par l'API ; feuille imprimable d'une page en gros caractères (ADR 0028).
- Crédits d'un morceau (section « Crédits et codes ») : ayants droit avec rôle (auteur, compositeur, arrangeur, éditeur) et part des droits, total en direct et enregistrement refusé s'il ne fait pas 100 % ; ISWC du morceau et ISRC de chaque version, formats vérifiés ; export CSV pour Excel (point-virgule, UTF-8 avec BOM) d'un morceau (`…/rights.csv`) et d'une sortie (`…/credits.csv`) ; chaque enregistrement des crédits écrit au journal d'audit (ADR 0029).
- Pistes séparées d'une version (section « Pistes séparées », `…/versions/{id}/stems`) : jusqu'à 8 fichiers audio nommés (voix, guitare, batterie…) et ordonnés ; mélangeur lu par Web Audio, toutes les pistes calées sur l'horloge audio, muet, solo et volume par piste ; ouverture refusée au-delà de la mémoire de l'appareil (768 Mo de son décodé sur ordinateur, 256 Mo sur mobile) ; dérive mesurée sur 3 minutes dans Chromium par la CI (ADR 0030).
- Archive d'une sortie pour le distributeur (section « Archive pour le distributeur », `…/releases/{id}/exports`) : ZIP préparé en arrière-plan avec les fichiers audio des versions choisies, les pistes séparées, les paroles, `credits.csv` et la pochette, écrit en flux dans le stockage ; refusé (409) pour une sortie sans piste, une piste sans audio ou un fichier pas encore analysé ; e-mail au demandeur quand il est prêt ; téléchargeable 24 heures par une adresse courte du stockage, puis 410 et effacé (ADR 0031).
- Paroles avec accords au format ChordPro (`[Am]Je marche…`) : accords affichés au-dessus de leur syllabe, gardés avec elle quand la ligne passe à la ligne sur mobile ; transposition par demi-tons à l'affichage, ouverte aux invités, suffixes et basses conservés, dièses ou bémols selon la tonalité d'arrivée ; « Enregistrer dans cette tonalité » réécrit les accords et la tonalité après confirmation ; test de mise en page à 360 px dans Chromium par la CI (ADR 0032).
- Interface en français et en anglais : sélecteur de langue en bas de chaque page, langue gardée dans le compte (`PUT /api/me/locale`) ou sur l'appareil sans compte, sinon celle du navigateur ; nombres, dates et pluriels selon la langue ; messages d'erreur de l'API traduits selon `Accept-Language`, avec un `code` stable dans chaque réponse d'erreur (ADR 0033).
- Lecteur Guitar Pro dans la liste des fichiers (« Afficher la tablature ») : partition et tablature des fichiers `.gp3` à `.gp5`, `.gpx` et `.gp`, lecture avec curseur, tempo ralenti, boucle sur une sélection, transposition, choix des pistes, muet et solo, sans jamais modifier le fichier ; alphaTab chargé seulement à l'ouverture ; le lecteur du bas se met en pause quand la tablature joue, et inversement ; `.gp3` et `.gp4` acceptés à l'envoi, avec leur signature vérifiée ; test Playwright dans Chromium par la CI (ADR 0034).
- Comparaison des fichiers Guitar Pro de deux versions dans l'écran A/B (section « Différences de la tablature ») : partition de B avec les notes ajoutées en vert et modifiées en orange, liste des différences mesure par mesure (ajoutée, supprimée, notes modifiées) où un clic place la partition sur la mesure ; une mesure insérée ne décale rien d'autre ; accordage, capodastre, tempo et pistes ajoutées ou supprimées annoncés en une ligne ; pistes associées par nom et instrument ; calcul dans un Web Worker ; test Playwright dans Chromium par la CI (ADR 0035).
- Tablature calée sur la maquette : « Caler sur une maquette » dans le lecteur Guitar Pro, en tapant le début de chaque mesure pendant que la maquette joue (reprises comprises) ; le calage est enregistré par tablature (`GET`, `PUT` et `DELETE …/files/{id}/sync`, Member ou Owner pour modifier) ; une tablature calée suit sa maquette dès l'ouverture, y compris après un déplacement, et « Son » revient au son de la tablature ; la mesure jouée s'affiche ; test Playwright dans Chromium par la CI (ADR 0036).
- Lecteur Guitar Pro façon Guitar Pro : mise en page du fichier sur grand écran, page blanche, zoom de 75 à 150 %, choix des portées (partition, tablature ou les deux), métronome et décompte (ADR 0037).
- Une tablature calée sur sa maquette ne se met plus en pause dès le départ quand les sons du lecteur arrivent tard.
### Modifié
- Section « Paroles » d'une version : lecture par défaut, saisie par « Modifier le texte ». Dans l'archive d'une sortie, `paroles.txt` est sans accords et `accords.txt` garde la source ChordPro.
- Les journaux de l'API ne nomment plus le destinataire d'un e-mail non parti, et une erreur 500 y est écrite avec un chemin sans jeton.
- Le mot de passe se change par `POST /api/me/password`, sans reconnexion ; `/api/auth/manage/2fa` et `POST /api/auth/manage/info` d'Identity sont fermés (404).
- Supprimer un morceau ou un fichier le met à la corbeille : il reste compté dans le stockage du groupe jusqu'à sa suppression définitive.
- Renommer un groupe est réservé à ses Owner.
- La connexion exige une adresse confirmée ; les comptes existants sont marqués confirmés par la migration `ConfirmExistingEmails`.
- L'inscription ne connecte plus directement : elle mène à « Vérifiez votre boîte mail ».
- Le lien de réinitialisation du mot de passe s'ouvre aussi depuis un navigateur déjà connecté.
- `scripts/purge-orphan-files.ps1` tient compte des archives d'export, puis des pochettes des sorties, puis des aperçus MP3, puis des archives des sorties.
- L'image de l'API contient ffmpeg ; la migration `AddAudioPreviews` prépare l'aperçu des fichiers audio déjà envoyés.
- Un morceau présent sur une sortie ne se supprime plus : l'API répond 409 avec le nom des sorties.
- Un morceau présent dans une setlist, ou une version qui y est choisie, ne va pas à la corbeille : l'API répond 409 avec le nom des setlists.
- Répondre à un commentaire vérifie aussi le morceau et la version de la route : 404 s'ils ne correspondent pas au fichier.
- Taille maximale d'un fichier envoyé depuis l'application : 2 Go au lieu de 50 Mo.
- nginx est configuré par variables (`API_UPSTREAM`, `STORAGE_ORIGIN`) et transmet `X-Forwarded-Host`.
- `scripts/deploy-azure.ps1` garde les réglages e-mail de l'API en place quand `-EmailProvider` et `-EmailFrom` sont omis, clé Brevo comprise ; il refuse Brevo avec une adresse `@songvault.local` et avertit quand aucun e-mail ne part (ADR 0022).
- La suppression du compte est refusée (409) tant qu'un groupe dont on est seul membre a un abonnement en cours ; la purge résilie chez Stripe ce qui resterait.
- `scripts/deploy-azure.ps1` : options `-BillingProvider`, `-StripePriceBand`, `-StripePriceStudio`, secrets `STRIPE_SECRET_KEY` et `STRIPE_WEBHOOK_SECRET` ; réglages de paiement gardés d'un déploiement à l'autre.
### Supprimé
- `Song.OwnerId`, remplacé par `Song.BandId`. Les morceaux sans propriétaire valide sont supprimés par la migration `BackfillBands`.
### Corrigé
- Un délai dépassé chez le fournisseur d'e-mails n'arrête plus l'API.
- La page Facturation avait perdu sa route : le lien « Facturation » des Owner y mène de nouveau.
- Sur Azure, les pochettes des sorties s'affichent : la CSP du front autorise les images du stockage objet.
- Le bouton « Supprimer » d'un fichier, sur la page d'une version, supprime vraiment le fichier.

## [0.6.0] - 2026-10-23
### Ajouté
- BPM et tonalité sur les versions (objet valeur `MusicalKey`, contrainte CHECK SQL).
- Recherche par titre/artiste et filtre par statut, synchronisés avec l'URL.
- CI GitHub Actions : backend (dont tests d'intégration SQL Server), frontend, build Docker et test de fumée.
- Dependabot, secret scanning avec push protection, CodeQL.
- Journal d'utilisation critique de l'IA (`docs/ai-usage.md`).

## [0.5.0]
### Ajouté
- Docker Compose (db, migrator, api, web), migrations par bundle EF Core, health checks, sauvegardes.

## [0.4.0]
### Ajouté
- Versions, upload, lecteur audio global, comparaison A/B.

<!-- Complétez 0.1.0 à 0.3.0 à partir de vos tags : git log --oneline v0.2.0..v0.3.0 -->