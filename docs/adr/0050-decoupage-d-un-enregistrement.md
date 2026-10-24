# ADR 0050 — Découpage d'un enregistrement en morceaux

- Statut : accepté
- Date : 2026-10-24

## Contexte
Un groupe enregistre souvent une répétition entière d'un bloc (enregistreur posé dans la salle, ou
l'enregistrement depuis le navigateur, ADR 0049). Le fichier contient dix morceaux, des accordages
et des bavardages : il faut pouvoir en tirer un fichier par morceau, rangé dans le bon morceau.

## Décision
- **Point de départ : n'importe quel fichier audio d'une version**, une fois son aperçu prêt
  (ADR 0021). La forme d'onde sert à placer les coupures, et la durée de l'aperçu borne les
  passages. Pas de nouvelle sorte de fichier « enregistrement ».
- **Page « Découper »** (`/b/:bandId/songs/:songId/versions/:versionId/files/:fileId/split`),
  ouverte par le lien « ✂ Découper » du fichier (Member et plus). On écoute (aperçu MP3) et on
  coupe à la position d'écoute (bouton ou touche C), au dixième de seconde ; une coupure se décale
  d'une seconde ou se retire. Chaque passage se garde ou non (accordage, bavardages) et va dans
  **un nouveau morceau** (son titre) **ou une nouvelle version d'un morceau existant**. Aucune
  bibliothèque : le dessin reprend `waveform.ts`.
- **API** : `POST …/files/{fileId}/splits` (202) et `GET …/splits/latest` (200 ou 204). Vérifié tout
  de suite : fichier audio lisible (#17), aperçu prêt, pas d'autre découpage en cours (409),
  passages dans le fichier, d'au moins une seconde, sans chevauchement, 30 au plus, morceaux
  choisis du groupe et hors corbeille (422), place estimée dans le stockage (402, #16).
- **Tâche de fond `audio.split`** (ADR 0011) : ffmpeg lit l'original **une seule fois** sur son
  entrée standard et écrit une sortie par passage (`-ss` / `-t` en option de sortie). Format :
  WAV et MP3 recopiés sans réencodage (coupe au paquet près, quelques centièmes de seconde),
  FLAC réencodé sans perte (une recopie garderait la durée de l'original dans l'en-tête), tout le
  reste (WebM, Ogg, M4A du navigateur) en MP3 192 kbit/s.
- **Chaque passage devient une version au statut Répétition**, titrée « Extrait de <fichier> »,
  avec en notes l'endroit d'où il vient, et un fichier « <titre> (<fichier>).ext ». Les fichiers
  suivent le parcours d'un envoi : analyse antivirus, aperçu, quota revérifié avec les tailles
  réelles, jauge rafraîchie, fil d'activité au nom du membre qui a demandé (nouveau morceau,
  nouvelle version). Tout part en une seule sauvegarde ; en cas d'erreur, les passages déjà
  stockés sont effacés.
- **Échec** : un refus qui ne passera pas mieux plus tard (morceau mis à la corbeille entre-temps,
  stockage plein) fait échouer tout de suite ; une erreur de ffmpeg est réessayée (trois essais),
  puis la raison s'affiche sur la page, d'où l'on peut recommencer.
- **Table `AudioSplits`** : passages et résultat en une colonne JSON (comme les calages, ADR 0036),
  clé étrangère vers le fichier d'origine en cascade, sans clé vers les groupes (un seul chemin de
  cascade). Le groupe est passé en paramètre au dépôt : la tâche de fond n'a pas de groupe actif.
- **L'original n'est jamais modifié** ni déplacé.

## Conséquences
- Une répétition se range en quelques clics, sans logiciel de montage.
- Les passages occupent de la place en plus de l'original : à supprimer à la main si on n'en veut
  plus.
- ffmpeg lit tout le fichier même pour un passage court (l'entrée standard ne se parcourt pas) :
  une heure de WAV prend le temps de sa lecture dans le stockage.
