# ADR 0021 — Forme d'onde et aperçu audio : ffmpeg dans une tâche de fond, dessin SVG sans bibliothèque

- Statut : accepté
- Date : 2026-10-15

## Contexte
Le backlog (#15) demande d'afficher la forme d'onde des fichiers audio et de convertir les
WAV/FLAC en MP3 légers, par un service en arrière-plan, sans file de messages. Critères :
aperçu MP3 généré après l'upload avec l'original conservé, forme d'onde précalculée, échec de
conversion visible et relançable. Un WAV de 10 minutes pèse environ 170 Mo : l'écouter sur un
téléphone ou par un lien de partage coûte cher en données et met du temps à démarrer.

## Décision
- **ffmpeg** (paquet Debian dans l'image de l'API) fait tout en **un seul passage** par
  fichier : l'original lui arrive par l'entrée standard, lu en flux depuis le stockage, et il
  produit en même temps :
  - le **MP3** (LAME, 128 kbit/s, stéréo, 44,1 kHz) dans un fichier temporaire, envoyé ensuite
    au stockage sous `preview-{id}.mp3` ; seulement pour WAV et FLAC, un MP3 est déjà léger ;
  - un **PCM mono 8 kHz** sur la sortie standard, dont l'API tire la durée et **1000 crêtes**
    de 0 à 255 (la plus forte vaut 255), gardées en base (`varbinary(1000)`).
  Mesuré : WAV de 10 minutes traité en 15 s, 53 Mo de mémoire au plus, MP3 de 9,6 Mo.
- **Une tâche de fond `audio.preview`** (ADR 0011) par fichier audio, créée dans la même
  transaction que le fichier, à l'upload comme à l'envoi direct. Trois essais (1 min puis
  5 min d'écart), 20 minutes au plus par essai. Pas de file de messages ni de service à part.
- **Table `AudioPreviews`** (clé = `SongFileId`, suppression en cascade) : `Pending`, `Ready`
  ou `Failed` avec un message en français. Le fichier lui-même ne change pas : l'original est
  toujours ce qui se télécharge.
- **Écoute par `GET …/files/{id}/listen`** : l'aperçu quand il est prêt, l'original sinon.
  Le lecteur et les liens de partage l'utilisent ; le téléchargement reste sur `/content`.
- **Échec visible et relançable** : après le troisième essai l'aperçu passe à `Failed`, la
  liste des fichiers l'indique et un membre peut le relancer (`POST …/preview`, 202 ; 409 s'il
  est déjà prêt ou en préparation). L'écoute de l'original reste possible.
- **Forme d'onde dessinée en SVG à la main** plutôt qu'avec wavesurfer.js : un seul chemin de
  200 barres, une copie colorée découpée selon la position de lecture ; clic et flèches du
  clavier pour se déplacer. Le lecteur reste l'élément `<audio>` existant (pas de second
  moteur de lecture) et aucun paquet npm n'est ajouté.
- **Fichiers existants** : la migration `AddAudioPreviews` crée un aperçu `Pending` et une
  tâche pour chaque fichier audio déjà envoyé.

## Conséquences
- ffmpeg devient une dépendance : image Docker, CI (`apt-get install ffmpeg`) et poste de
  développement (`winget install Gyan.FFmpeg`), sans quoi les tests d'intégration échouent.
  Son chemin se règle par `AudioPreview:FfmpegPath`.
- Les tâches de fond s'exécutent une à une : pendant la conversion d'un long fichier, un
  e-mail de notification ou un export attend (au pire quelques dizaines de secondes par
  fichier ; la migration en crée une par fichier existant au premier démarrage).
- Le stockage contient un fichier de plus par WAV/FLAC (environ 1 Mo par minute) ; il est
  supprimé avec le fichier, la purge du compte et compté par `purge-orphan-files.ps1`.
- La forme d'onde ne change jamais pour un fichier donné : le navigateur la garde un jour.
- Si l'aperçu est plus tard utile hors connexion (ADR 0020), c'est le MP3 qu'on mettrait en
  cache.