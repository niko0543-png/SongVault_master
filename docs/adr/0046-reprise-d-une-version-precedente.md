# ADR 0046 — Reprendre des éléments d'une version précédente

- Statut : accepté
- Date : 2026-10-23

## Contexte
Une nouvelle version part souvent de la précédente : mêmes paroles et accords, même tempo, même
tonalité, la même tablature ou les mêmes pistes, avec quelques changements. Jusqu'ici, le
formulaire « Nouvelle version » créait une version vide, et il fallait tout ressaisir et
renvoyer chaque fichier.

## Décision
- **Dans le formulaire « Nouvelle version »**, un bloc « Reprendre de la version précédente »
  propose une version du morceau (la dernière par défaut, les autres au choix) et liste ce qu'elle
  contient : paroles et accords, BPM, tonalité, notes, et chacun de ses fichiers. Par défaut, tout
  est coché sauf les notes, qui décrivent en général ce qui a changé dans *cette* version. Cocher
  les notes les copie dans le champ Notes, où on peut les retoucher.
- **Les textes et réglages voyagent dans les champs** de `POST …/versions` (`lyrics`, `bpm`,
  `key`, `notes`), comme si on les avait saisis : pas de règle à part côté API.
- **Les fichiers sont copiés**, pas partagés : `copyFromVersionId` et `copyFileIds` dans le même
  `POST`. Chaque copie a sa propre clé de stockage, ses commentaires (aucun au départ), sa
  corbeille et sa purge : supprimer la copie ne touche pas l'original, et inversement. Une clé
  partagée aurait obligé la purge (ADR 0027) à compter les références avant d'effacer.
- La copie se fait **avant** l'enregistrement, une seule fois même si le numéro de version doit
  être retenté ; si l'enregistrement échoue, les copies sont effacées du stockage.
- **Quota** (ADR 0023) : les copies comptent dans le stockage du groupe et sont refusées d'un bloc
  si elles ne tiennent pas, avant toute copie. Le formulaire affiche la taille à copier.
- **Antivirus** (ADR 0024) : seuls les fichiers lisibles (sains, ou non analysés quand
  l'antivirus est désactivé) peuvent être repris ; un fichier bloqué ou en cours d'analyse est
  grisé dans le formulaire et refusé par l'API (409). La copie repasse par l'analyse et l'aperçu
  audio (ADR 0021), comme un envoi : quelques secondes d'attente avant de l'écouter.
- **Mélangeur et calage** : les pistes du mélangeur (ADR 0030) suivent, dans le même ordre et
  resserrées, pour les fichiers repris ; le calage d'une tablature sur sa maquette (ADR 0036)
  suit si la tablature et la maquette sont reprises toutes les deux.
- **Pas repris** : le titre et le statut (on les choisit), l'ISRC (un code par enregistrement),
  les commentaires (ils parlent de l'ancienne version). Les crédits et l'ISWC appartiennent au
  morceau (ADR 0029) : toutes les versions les partagent déjà.

## Conséquences
- Pas de migration ni de nouveau paquet. L'API lit et réécrit chaque fichier (jusqu'à 2 Go) en
  flux, sans le garder en mémoire ; une copie côté serveur du stockage objet serait plus rapide
  mais demanderait une méthode de plus à `IFileStorageService`.
- Le fil d'activité garde un seul événement « nouvelle version » : les copies n'y ajoutent pas
  un « fichier ajouté » chacune.
