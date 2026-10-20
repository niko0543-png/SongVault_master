# ADR 0031 — Archive complète d'une sortie : ZIP écrit en flux dans le stockage, téléchargeable 24 h après connexion

- Statut : accepté
- Date : 2026-10-19

## Contexte
Le backlog (#24) demande une archive ZIP d'une sortie, prête pour le distributeur : fichiers
finaux, paroles, crédits et pochette, générée en arrière-plan, avec un lien de téléchargement
temporaire. Il s'appuie sur les sorties (#10, ADR 0016) et les crédits (#22, ADR 0029). La vue
d'ensemble propose une tâche de fond qui écrit le ZIP en flux dans le stockage (`ZipArchive`),
sans tout charger en mémoire, un lien SAS valable 24 h envoyé par notification, et deux tests :
le contenu du ZIP et le refus d'un lien expiré. Une sortie peut peser plusieurs gigaoctets (WAV
et pistes séparées) : l'export du compte (ADR 0011), qui passe par un fichier temporaire sur le
disque du conteneur, ne convient pas.

## Décision
- **Une ligne par archive** : table `ReleaseExports` (`BandId`, `ReleaseId`, `RequestedById`,
  `Status` Pending, Ready ou Failed, dates, taille), supprimée en cascade avec sa sortie. La clé
  du fichier se déduit de l'identifiant (`release-export-{id}.zip`) : la tâche d'expiration
  efface le fichier même quand la ligne est déjà partie avec sa sortie. Une seule archive en
  préparation par sortie (409).
- **Contenu** (`ReleaseArchive.Plan`, une fonction sans lecture ni écriture) :
  `Titre de la sortie/NN - Titre du morceau/` avec les fichiers audio de la version choisie sur
  la piste, ses pistes séparées (#23) dans `Pistes séparées/` sous la forme `1 - Voix.wav`, et
  `paroles.txt` si la version a des paroles (UTF-8 avec BOM, fins de ligne CRLF) ; à la racine,
  `credits.csv` (le même fichier que `GET …/credits.csv`, ADR 0029) et `pochette.jpg|png|webp`.
  Seuls les fichiers audio y entrent, pas les partitions ni les documents. Les noms sont lisibles
  sous Windows, macOS et Linux (caractères interdits remplacés par `_`, 80 caractères au plus),
  et deux fichiers du même nom dans un dossier deviennent `nom.wav` et `nom (2).wav`.
- **Refus avant la tâche** (409, avec le nom des pistes ou des fichiers en cause) : sortie sans
  piste, piste dont la version n'a aucun fichier audio, fichier pas encore déclaré sain par
  l'antivirus ou bloqué (ADR 0024). La tâche vérifie de nouveau : un fichier ajouté entre-temps
  et pas encore analysé la fait réessayer, puis échouer au troisième essai.
- **Écriture en flux** : `IFileStorageService.OpenWriteAsync` ouvre un flux d'écriture vers un
  fichier neuf (jamais d'écrasement) : `FileMode.CreateNew` en local, `BlockBlobClient.OpenWriteAsync`
  sur Azure, qui envoie des blocs de 4 Mo au fil de l'écriture et valide la liste des blocs à la
  fermeture avec `If-None-Match: *`. Le ZIP (`ZipArchive.CreateAsync` de .NET 10) y est écrit
  entrée par entrée, chaque fichier lu du stockage et recopié tel quel : un seul fichier ouvert à
  la fois, la mémoire de la tâche ne dépend pas de la taille de la sortie. L'audio et la pochette
  sont stockés sans compression (déjà compressés, ou sans gain utile), le texte est compressé. Un
  essai raté efface l'archive partielle ; le dernier marque l'archive en échec.
- **Lien temporaire, mais derrière la connexion** : l'archive reste disponible 24 heures. L'e-mail
  envoyé au demandeur quand elle est prête pointe vers la page de la sortie, pas vers le fichier,
  comme l'export du compte (ADR 0011) : un e-mail transféré ou lu par quelqu'un d'autre ne donne
  pas les masters. `GET …/releases/{id}/exports/{exportId}/archive` vérifie le groupe, puis
  redirige vers une adresse SAS de lecture courte (30 minutes, `FileStorage:AzureBlob:DownloadUrlLifetime`)
  qui impose le type et le nom du fichier ; en local, l'API sert le fichier. C'est l'écart avec
  la vue d'ensemble, qui envoyait un SAS de 24 h par e-mail.
- **Expiration** : passé 24 heures, le lien répond **410** (« Ce lien a expiré : préparez une
  nouvelle archive »), puis la tâche `release.export.expire` efface la ligne et le fichier ; le
  lien répond alors 404. Une archive en échec part de la même façon 24 heures plus tard.
- **Droits** (`[BandScoped]`, ADR 0008) : Member et Owner demandent l'archive ; tous les
  membres, Guest compris, voient la dernière et la téléchargent, comme chaque fichier.
- **Endpoints** : `POST …/releases/{id}/exports` (202), `GET …/exports/latest` (200 ou 204),
  `GET …/exports/{exportId}/archive` (302, 200, 404 ou 410).

## Conséquences
- Pendant 24 heures, l'archive occupe le stockage en plus des fichiers d'origine. Elle n'est pas
  comptée dans le quota du groupe (ADR 0023) : elle est courte et une seule se prépare à la fois.
- Une sortie supprimée emporte ses lignes d'archive ; un fichier déjà écrit reste dans le stockage
  jusqu'à son expiration, sans lien qui y mène.
- La purge d'un compte efface les archives des groupes effacés avec lui et celles qu'il a
  demandées ; `scripts/purge-orphan-files.ps1` garde les clés des archives encore en base.
- Au-delà de 4 Go, `ZipArchive` passe l'entrée en Zip64, même écrite en flux : vérifié avec un
  fichier de 4,3 Go recopié dans le ZIP sans retour en arrière, relu en entier par un autre
  lecteur (le module `zipfile` de Python, contrôle CRC compris).
- Pas de notification dans l'application : l'e-mail et la section « Archive pour le distributeur »
  de la page de la sortie, qui se rafraîchit toutes les 5 secondes pendant la préparation.