# ADR 0036 — Tablature calée sur la maquette : points de synchronisation d'alphaTab, enregistrés par fichier

- Statut : accepté
- Date : 2026-10-22

## Contexte
Le lecteur Guitar Pro (#28, ADR 0034) joue la tablature avec son propre son, au tempo du
fichier. Le backlog (#29) demande de la faire défiler en même temps que la maquette MP3 de la
même version, avec des points de synchronisation enregistrés par version. Une maquette jouée
par un groupe ne suit pas un tempo fixe : il faut dire à quel instant de l'enregistrement tombe
chaque endroit de la partition. Sans points, le lecteur doit fonctionner comme avant.

## Décision
- **alphaTab fait la synchronisation** (version 1.6 et suivantes, déjà là avec 1.8.4) : en mode
  `PlayerMode.EnabledExternalMedia`, il commande un élément `<audio>` de la page (lecture, pause,
  déplacement, vitesse) et reçoit son heure à chaque image. Les points sont ses `FlatSyncPoint` :
  mesure, passage (pour une reprise), place dans la mesure (0 à 1), instant en millisecondes.
  Entre deux points, alphaTab interpole ; après le dernier, il étire la fin du morceau jusqu'à la
  fin de la maquette.
- **Un calage par tablature** : deux colonnes de `SongFiles`, `SyncAudioFileId` (la maquette, un
  fichier audio de la même version) et `SyncPoints` (JSON, lu et écrit d'un bloc). Une contrainte
  `CHECK` les veut nulles ensemble. Un fichier appartient à une version : le calage est bien
  « par version », et deux tablatures d'une même version ont chacune le leur.
- **Pas de clé étrangère vers la maquette** : mise seule à la corbeille (#20), elle suspend le
  calage (la lecture rend 204, la tablature reprend son propre son) ; restaurée, il revient. Purgée,
  elle laisse un calage ignoré, que le prochain enregistrement remplace.
- **Règles dans `SongVersion.SetTablatureSync`** : la tablature et la maquette sont des fichiers
  en place de la version, de 1 à 2 000 points, dans l'ordre de la lecture (instants strictement
  croissants), sans endroit calé deux fois. Lire : tout membre ; caler et supprimer : Member ou
  Owner (ADR 0008).
- **Calage au tapé** (`TabSyncRecorder.vue`) : la maquette joue dans son propre élément `<audio>` ;
  on appuie sur un grand bouton (ou Espace) au début de chaque mesure. La tape n° i marque la
  i-ième mesure jouée, reprises comprises, dans l'ordre de la table de temps d'alphaTab
  (`tickCache.masterBars`). La mesure à taper est sélectionnée dans la partition. On peut
  s'arrêter avant la fin, annuler la dernière tape ou recommencer.
- **Suivre la maquette dès l'ouverture** quand la tablature est calée ; le choix « Son » revient au
  son de la tablature. La lecture n'est possible qu'une fois la durée de la maquette connue
  (`loadedmetadata`) : avant, alphaTab croirait le morceau fini.
- **Déplacements regroupés** : alphaTab demande plusieurs déplacements d'affilée au branchement et
  au chargement des points ; seul le dernier est appliqué, à la fin de la tâche. Des déplacements
  qui se chevauchent laissaient parfois l'élément `<audio>` de Chromium bloqué en attente de
  données quand la machine était chargée (vu dans le test Playwright).
- **Avec la maquette**, tempo et boucle agissent (vitesse de l'élément `<audio>`) ; transposer,
  couper ou isoler une piste n'agit que sur le son de la tablature : ces boutons sont désactivés.

## Alternatives écartées
- **Le lecteur du bas comme source** : son élément `<audio>` est partagé par toute l'application et
  ne donne son heure qu'environ quatre fois par seconde ; le curseur sauterait.
- **Une table `TablatureSyncs`** : deux clés étrangères vers `SongFiles` forment deux chemins de
  suppression en cascade, refusés par SQL Server, et la purge devrait les gérer à part ; pour une
  donnée lue et écrite d'un bloc avec son fichier, deux colonnes suffisent (comme les pistes, ADR
  0030).
- **Placer les points à la souris sur une forme d'onde** : plus précis, mais un éditeur complet ;
  le tapé suffit pour suivre une maquette, et se refait en un passage.
- **Les points intégrés aux fichiers Guitar Pro 8** : seuls les fichiers GP8 avec piste audio en
  ont, et SongVault ne modifie jamais un fichier (ADR 0034).

## Conséquences
- La précision dépend de la régularité des tapes (quelques dizaines de millisecondes). Un point par
  mesure, au début de la mesure ; un changement de tempo au milieu d'une mesure n'est pas suivi.
- Un nouveau fichier Guitar Pro dans une nouvelle version n'hérite pas du calage : on le refait.
- Le calage n'est ni dans l'archive d'une sortie (#24) ni dans l'export des données du compte (#5).