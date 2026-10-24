# ADR 0049 — Enregistrer une répétition depuis le navigateur

- Statut : accepté
- Date : 2026-10-24

## Contexte
Pour garder une trace d'une répétition, il fallait l'enregistrer avec une autre application (dictaphone
du téléphone, enregistreur), récupérer le fichier sur un ordinateur, le convertir parfois (un mémo vocal
d'iPhone est un `.m4a`, refusé jusqu'ici), puis l'envoyer dans un morceau. Le navigateur sait enregistrer
le micro (`MediaRecorder`), sur ordinateur comme sur téléphone, et SongVault est déjà une PWA (ADR 0020).
Un autre chantier découpera ensuite un long enregistrement en morceaux (ADR 0050).

## Décision
- **Page « Enregistrer »** (`/b/{groupe}/record`, Member et Owner) dans la navigation, et lien
  « 🎙 Enregistrer au micro dans cette version » sous l'envoi de fichiers d'une version, qui choisit
  d'avance cette version comme destination (`?songId=&versionId=`).
- **Pas de nouvelle entité** : l'enregistrement devient un fichier audio ordinaire (`SongFile`) d'une
  version, envoyé par le parcours direct existant (ADR 0012 : annonce, PUT, confirmation). Quota (ADR 0023),
  antivirus (ADR 0024), aperçu MP3 et forme d'onde (ADR 0021), commentaires, partage, corbeille : tout
  suit sans une ligne de plus.
- **Destination** choisie après l'arrêt, une fois l'enregistrement écouté : par défaut un **nouveau
  morceau** « Répétition du JJ/MM/AAAA » avec sa version 1 ; sinon une **nouvelle version** d'un morceau
  existant, ou une **version existante**. Une version créée ici a le statut « Répétition ». En cas
  d'échec de l'envoi, le morceau et la version déjà créés sont repris au nouvel essai, pas recréés.
- **Formats** : Opus dans WebM (Chrome, Edge, Firefox), sinon MP4/AAC (Safari), sinon Opus dans Ogg ;
  l'extension suit le type réellement produit. L'API accepte désormais `.webm`, `.m4a` et `.ogg` comme
  fichiers audio, avec leur signature (en-tête EBML, boîte `ftyp`, page `OggS`) : les mémos vocaux des
  téléphones s'envoient aussi tels quels. 160 kbit/s : un groupe sans perte audible, environ 70 Mo
  par heure.
- **Micro réglé pour la musique** : annulation d'écho, réduction de bruit et gain automatique coupés
  (faits pour la voix d'un appel, ils écrasent un groupe) ; stéréo quand l'entrée le permet ; choix de
  l'entrée (interface USB, enregistreur branché) quand il y en a plusieurs.
- **Pendant l'enregistrement** : chrono (heures comprises), indicateur de niveau avec alerte de
  saturation, pause et reprise. L'écran est gardé allumé (Wake Lock, repris au retour sur la page) :
  un téléphone qui se verrouille coupe le micro de la page. Quitter la page, ou la fermer, demande
  confirmation tant que l'enregistrement n'est pas envoyé. Un micro débranché arrête l'enregistrement
  en gardant ce qui a été pris.
- **Aperçu d'un `.m4a`** : son index (`moov`) est souvent rangé à la fin du fichier, ce qu'ffmpeg ne
  peut pas lire depuis son entrée standard. `AudioPreviewJob` copie donc un `.m4a` dans un fichier
  temporaire, et `FfmpegAudioProcessor` lit par son chemin toute source déjà sur le disque (`FileStream`,
  dont le stockage local). Les autres formats restent lus au fil du stockage, sans copie.

## Conséquences
- Pas de migration ni de nouveau paquet npm ou NuGet.
- Le son reste en mémoire dans le navigateur jusqu'à l'envoi (environ 70 Mo par heure) : une page
  fermée ou un téléphone qui tue l'onglet perd l'enregistrement en cours. Un enregistrement de secours
  au fil de l'eau (IndexedDB) pourra s'ajouter si le besoin se confirme.
- Sur iPhone, Safari coupe le micro quand on change d'application ou qu'on verrouille l'écran : la page
  le dit pendant l'enregistrement.
- Un WebM produit par `MediaRecorder` n'annonce pas sa durée ; l'écoute passe par l'aperçu MP3
  (`/listen`) dès qu'il est prêt, et la durée vient de la forme d'onde.
- Un `.m4a` occupe un temps son volume sur le disque de l'API pendant la conversion.
