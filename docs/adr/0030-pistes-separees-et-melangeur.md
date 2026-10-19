# ADR 0030 — Pistes séparées : la piste portée par le fichier, mélangeur Web Audio calé sur l'horloge audio

- Statut : accepté
- Date : 2026-10-19

## Contexte
Le backlog (#23) demande plusieurs pistes par version (voix, guitare, batterie) et un mélangeur
simple : muet, solo et volume par piste, lecture synchronisée. Il dépend de l'envoi direct (#6,
ADR 0012) et de l'aperçu MP3 (#15, ADR 0021). La vue d'ensemble propose une entité
`Stem (VersionId, FileId, Name, Order)`, une lecture par Web Audio (chaque aperçu décodé en
`AudioBuffer`, toutes les sources démarrées au même instant du contexte audio), un `GainNode`
par piste, une limite pour la mémoire, surtout sur mobile, et deux tests : la dérive mesurée
après 3 minutes de lecture, la logique muet et solo en unitaire.

## Décision
- **Une piste est un fichier audio de la version, nommé et placé** : deux colonnes nulles sur
  `SongFiles`, `StemName` (`nvarchar(60)`) et `StemPosition` (`int`), nulles ensemble
  (contrainte `CK_SongFiles_Stem`). Pas de table `Stems` : une piste n'a rien d'autre que son nom
  et sa place ; une table vers `SongVersions` et `SongFiles` aurait ouvert deux chemins de
  suppression en cascade que SQL Server refuse ; et la corbeille (ADR 0027) suit sans code de
  plus : une version jetée puis restaurée revient avec ses pistes.
- **`SongVersion.SetStems(pistes)` remplace toutes les pistes**, dans l'ordre de la liste, tout
  vérifié avant de rien changer : au plus 8 pistes, chacune un fichier audio en place de cette
  version, pas deux fois le même, un nom de 1 à 60 caractères. Liste vide = plus de mélangeur.
  Un fichier mis seul à la corbeille quitte le mélangeur (`TrashFile` efface son nom de piste) :
  restauré, il redevient un simple fichier, et la limite de 8 tient toujours.
- **Endpoints** (`[BandScoped]` : lecture pour tous les membres, Guest compris ; écriture pour
  Member et Owner, ADR 0008) : `GET` et `PUT /api/bands/{id}/songs/{songId}/versions/{versionId}/stems`.
  Chaque piste rend `fileId`, `name`, `position`, `originalFileName`, `durationMs` (de l'aperçu
  prêt) et `playable` : fichier déclaré sain (#17) et aperçu MP3 prêt (#15). Le son se lit par la
  route existante `…/files/{fileId}/listen`, qui redirige vers le stockage en production.
- **Mélangeur dans le navigateur, sans bibliothèque** (`mixerEngine.ts`) : chaque aperçu MP3
  est téléchargé (`fetch`, qui suit la redirection, CORS du stockage déjà ouvert en GET) puis
  décodé par `decodeAudioData`. Toutes les sources partent au même instant de l'horloge audio
  (`currentTime + 0,1 s`) avec le même décalage : elles ne peuvent pas dériver l'une de l'autre,
  puisqu'elles lisent au même rythme d'échantillons. Un saut dans le morceau recrée toutes les
  sources de la même façon. Chaque piste passe par son `GainNode`, puis par un gain général ;
  un changement de gain se fait en 15 ms (`setTargetAtTime`), sans clic.
- **Muet et solo** (`effectiveGains`) : une piste muette se tait, même en solo ; dès qu'une piste
  est en solo, celles qui ne le sont pas se taisent ; sinon, le volume de la piste (0 à 1).
  Lancer le mélangeur met le lecteur du bas en pause ; relancer le lecteur arrête le mélangeur.
- **Mémoire** : une piste décodée prend 384 000 octets par seconde (48 kHz, stéréo, 4 octets),
  soit 66 Mo pour 3 minutes. Le navigateur estime le total avant de rien télécharger, d'après
  `durationMs` : 768 Mo au plus sur ordinateur (8 pistes de 4 min 10 s), 256 Mo sur un écran
  tactile ou un appareil de 4 Go ou moins (`navigator.deviceMemory`), soit 4 pistes de 2 min 50 s.
  Au-delà, le bouton « Ouvrir le mélangeur » reste grisé avec la durée totale et la limite. Le
  contexte audio est fermé en quittant la page.
- **Le mélangeur s'ouvre sur un clic** : le navigateur n'accepte un `AudioContext` qui joue
  qu'après un geste de l'utilisateur, et rien n'est téléchargé avant.
- **Tests** : la logique muet et solo, le budget mémoire et la programmation du moteur (mêmes
  instant et décalage pour toutes les sources) en unitaire avec Vitest ; la dérive dans le vrai
  moteur de Chromium par Playwright (`e2e/stems-drift.spec.ts`) : deux pistes de tops décalées
  d'une demi-seconde, 3 minutes rendues par un `OfflineAudioContext`, écart mesuré sur les
  10 dernières secondes et après un saut, à un échantillon près. Ce test n'a pas besoin de la
  pile Docker : la CI le lance dans le job frontend.

## Conséquences
- Pas de piste plus longue que son aperçu : un fichier dont l'aperçu a échoué n'entre pas dans
  le mélangeur tant qu'on ne l'a pas relancé (« Réessayer » dans Fichiers).
- Les pistes se lisent en MP3 de 128 kbit/s, comme le lecteur : c'est un outil de répétition et
  d'écoute, pas de mixage. Les WAV d'origine restent téléchargeables.
- Toutes les pistes sont en mémoire avant la lecture : l'ouverture prend le temps de télécharger
  les aperçus (environ 3 Mo par piste de 3 minutes).
- Les liens de partage (ADR 0019) ne montrent pas le mélangeur : un invité externe écoute les
  fichiers un par un.