# ADR 0024 — Analyse antivirus : ClamAV partout, dans une tâche de fond, fichier illisible avant le verdict, activée en production à la commercialisation

- Statut : accepté
- Date : 2026-10-17

## Contexte
Le backlog (#17) demande d'analyser chaque fichier envoyé avant qu'il soit lu, téléchargé,
partagé ou converti, de mettre en quarantaine un fichier infecté et de prévenir son auteur.
Les fichiers arrivent par l'API (50 Mo au plus) ou directement dans le stockage par URL signée
(2 Go au plus, ADR 0012) : l'analyse ne peut donc pas se faire pendant l'envoi. Le contrôle de
signature (premiers octets) reste en place mais ne dit rien du reste du fichier : un PDF ou un
fichier Guitar Pro (une archive ZIP) peut porter un contenu malveillant.

## Décision
- **ClamAV partout** : le démon `clamd`, dans un conteneur, en développement (Docker Compose),
  en CI (Testcontainers) et en production. L'API lui envoie le fichier par la commande
  `INSTREAM` en morceaux de 256 Kio, lu en flux depuis le stockage, sans passer par son disque.
  Une seule implémentation, testée avec le fichier EICAR. Microsoft Defender for Storage a été
  écarté : il n'existe qu'en production (rien à tester sur le PC ni en CI) et se paie au
  volume analysé (environ 0,15 $ par Go, plus un forfait par compte de stockage).
- **Activation par réglage, `Antivirus:Provider`** (`None` par défaut, ou `ClamAv`), comme
  `Billing:Provider` (ADR 0023). Avec `None`, la tâche `file.scan` ne lit pas le fichier : elle
  le marque `NotScanned`, lisible comme un fichier sain, et lance son aperçu. La production
  reste sur `None` tant que SongVault n'est pas commercialisé et que seuls des proches
  envoient des fichiers ; le développement (Compose, `dotnet run`) et la CI analysent avec
  ClamAV. Passer à `ClamAv` ajoute le conteneur ci-dessous et, au démarrage de l'API, remet
  chaque fichier `NotScanned` en analyse (`AntivirusActivationWorker`) : ces fichiers sont
  illisibles le temps de leur analyse, puis `Clean` ou `Infected` comme les autres.
- **Production, une fois activé : un conteneur `clamav` à côté de l'API** dans la même Container App
  (0,75 vCPU, 1,5 Go), joignable sur `localhost:3310` seulement. Son image
  `songvault-clamav` part de `clamav/clamav:1.4` et règle clamd pour nos fichiers
  (`infra/clamav/Dockerfile`) : flux jusqu'à 2,1 Go, rechargement des signatures sans seconde
  copie en mémoire. freshclam met les signatures à jour au démarrage puis toutes les 2 heures.
- **Une tâche de fond `file.scan`** (ADR 0011) par fichier, créée dans la même transaction que
  le fichier, à l'envoi par l'API comme à l'envoi direct. Trois essais (1 min puis 5 min
  d'écart), 10 minutes au plus par essai.
- **Table `FileScans`** (clé = `SongFileId`, suppression en cascade) : `Pending`, `Clean`,
  `Infected` (avec le nom de la menace), `Failed` ou `NotScanned` (antivirus désactivé). Pas de statut « en cours d'analyse » :
  la réservation de la tâche (UPDLOCK, READPAST) suffit à éviter deux analyses en même temps.
- **Fichier illisible avant le verdict** : le contenu (`/content`), l'écoute (`/listen`) et
  la relance d'aperçu répondent 409 tant que le fichier n'est ni `Clean` ni `NotScanned` ;
  les liens de partage et l'export RGPD ne montrent que ces fichiers-là. Sans ligne d'analyse, un
  fichier est traité comme « en cours ».
- **Aperçu audio (#15) après le verdict** : l'aperçu est créé « en préparation » avec le
  fichier, mais sa tâche n'est programmée qu'une fois le fichier déclaré sain. Un fichier
  infecté n'est jamais donné à ffmpeg ; son aperçu passe en échec.
- **Quarantaine sur place** : le fichier infecté reste à sa place dans le stockage, sous la
  même clé, mais aucune URL ni aucun contenu n'en sort plus, puisque l'API est la seule porte
  vers le stockage (conteneur privé, URL signées de 30 minutes émises par elle). Le déplacer
  dans un conteneur à part ne retirerait aucun accès. La liste des fichiers le montre
  « Bloqué » avec le nom de la menace ; un membre le supprime. Il compte dans le stockage du
  groupe tant qu'il n'est pas supprimé.
- **Auteur prévenu par e-mail** (tâche `file.infected-notice`) : le compte de l'événement
  « fichier ajouté » du fil d'activité (#11), sans nouvelle colonne. La ligne de `FileScans`
  (menace, date) sert de trace jusqu'au journal d'audit du groupe (#19).
- **Échec visible et relançable** : après le troisième essai (antivirus injoignable),
  l'analyse passe à `Failed` ; un membre la relance (`POST …/files/{id}/scan`, 202 ; 409 si
  elle est en cours, si le fichier est sain, infecté ou non analysé). Un fichier infecté n'est jamais
  analysé de nouveau.
- **Fichiers existants** : la migration `AddFileScans` crée une analyse `Pending` et une
  tâche pour chaque fichier déjà envoyé. Ils restent illisibles le temps de leur tâche :
  quelques secondes avec `None` (rien n'est lu), le temps de l'analyse avec `ClamAv`.
- **Pochettes des sorties** (#10) : hors champ. Ce sont des images de 5 Mo au plus,
  contrôlées par leur signature et servies avec leur type d'image.

## Conséquences
- Rien de plus à payer tant que la production reste sur `None`. Une fois `ClamAv` activé,
  environ 15 € de plus par mois (estimation pour un conteneur toujours actif, à vérifier avec
  la calculatrice de prix Azure).
- Avec `None`, aucun fichier n'est analysé en production : seuls restent la liste blanche
  d'extensions, le contrôle de signature, la limite de taille, le type de contenu fixé par le
  serveur et `X-Content-Type-Options: nosniff`. C'est accepté avant la commercialisation,
  pas après : passer à `ClamAv` fait partie de la mise en vente.
- ClamAV devient une dépendance en développement : `docker compose up -d clamav` sur le PC
  pour lancer l'API avec `dotnet run` (`appsettings.Development.json` active `ClamAv`), et une
  image de plus publiée par le workflow `images`, prête pour l'activation en production. Sans antivirus
  joignable, les envois réussissent mais les fichiers restent « en cours d'analyse », puis
  passent en échec et se relancent.
- Au démarrage, clamd met une à deux minutes à charger ses signatures : les analyses de ce
  moment échouent une première fois et réussissent au deuxième essai.
- Les tâches de fond s'exécutent une à une : l'analyse d'un WAV de 2 Go retarde de une à deux
  minutes un e-mail ou une conversion.
- Un fichier de plus de 2000 Mo n'est pas analysé en entier : clamd le déclare sain sans le
  lire (`MaxFileSize`). Seuls des WAV atteignent cette taille.
- Les tests d'intégration utilisent un antivirus factice (`TestMalwareScanner`) qui
  reconnaît une chaîne propre à SongVault. La chaîne EICAR n'est assemblée qu'en mémoire,
  dans `ClamAvScannerTests` : écrite sur le disque, elle serait effacée par l'antivirus du PC.