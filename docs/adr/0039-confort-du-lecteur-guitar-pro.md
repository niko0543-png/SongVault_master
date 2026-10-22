# ADR 0039 — Confort du lecteur Guitar Pro : boucle par mesures, réglages gardés sur l'appareil

- Statut : accepté
- Date : 2026-10-22

## Contexte
Après la page du lecteur (ADR 0038), quatre gênes restaient au quotidien (compte rendu
« Confort du lecteur Guitar Pro », lot 2) :
- la boucle demandait de glisser sur la partition puis de cocher une case, et une longue aide
  restait affichée en permanence ;
- zoom, portées, tempo et mix revenaient aux valeurs par défaut à chaque ouverture ;
- le premier téléchargement des sons (quelques mégaoctets) n'affichait aucune progression ;
- sur téléphone, les barres d'outils prenaient environ six lignes au-dessus de la partition.

## Décision
- **Boucle par numéros de mesures** : « De la mesure … à … », « Début ici » et « Fin ici » (sur
  la mesure jouée) et « Effacer la boucle ». La boucle passe par les fonctions d'alphaTab faites
  pour cela (`highlightPlaybackRange` puis `applyPlaybackRangeFromHighlight`) : c'est la même
  sélection que celle glissée sur la partition. Dans l'autre sens, `playbackRangeChanged` remplit
  les deux numéros quand on glisse. La case « Boucler la sélection » reste dans la barre
  principale (raccourci L).
- **Aide repliée** dans un `<details>` « Aide ».
- **Réglages gardés sur cet appareil** (`localStorage`, `src/features/guitarpro/prefs.ts`) :
  zoom, portées et panneau des pistes replié valent pour toutes les tablatures
  (`songvault.tab.view`) ; tempo et mix (muet, solo, volume) valent pour un fichier
  (`songvault.tab.file.<id>`). Rien ne part au serveur et le fichier n'est jamais modifié. Un mix
  n'est repris que si le fichier a toujours le même nombre de pistes. Une valeur abîmée, ou un
  stockage bloqué (navigation privée), donne les réglages par défaut, sans erreur. Les portées
  gardées sont posées sur la partition avant son premier rendu, pour ne pas dessiner deux fois.
  Le mix est rendu à alphaTab à chaque `playerReady`, donc aussi après un passage du son de la
  tablature à la maquette (#29). La comparaison (#30) n'a pas de fichier à elle : elle ne garde
  que les réglages communs.
- **Progression des sons** : l'évènement `soundFontLoad` d'alphaTab affiche « Chargement des
  sons… 45 % » et une barre de progression.
- **Réglages repliés sur téléphone** : sur la page du lecteur, la barre principale ne garde que
  lecture, retour au début, boucle, mesure jouée et un bouton « Réglages ». Tempo, transposition,
  boucle par mesures, affichage, calage et aide se déplient derrière ce bouton sous 720 px ; ils
  restent toujours affichés au-dessus. Pendant un calage (#29), ils restent dépliés.
- Pas de nouveau paquet npm ; aucun changement côté API.

## Conséquences
- Un musicien retrouve son tempo et son mix de travail en rouvrant un morceau, sur le même
  navigateur. Sur un autre appareil, il repart des réglages par défaut : les garder dans le
  compte demanderait une table et une API, pour un gain faible aujourd'hui.
- Le stockage du navigateur garde une petite entrée par tablature ouverte (quelques dizaines
  d'octets), jamais purgée ; c'est négligeable.
