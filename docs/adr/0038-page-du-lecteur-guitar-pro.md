# ADR 0038 — Page du lecteur Guitar Pro : un seul bloc en plein écran, piloté au clavier

- Statut : accepté
- Date : 2026-10-22

## Contexte
Le lecteur Guitar Pro (#28, ADR 0034 ; rendu de l'ADR 0037) s'ouvrait dans la liste des
fichiers de la page d'une version, sous les paroles, dans la colonne de 900 px de l'application.
Ses éléments étaient dispersés sur la hauteur de la page : les commandes en haut, la partition
au milieu, le panneau des pistes en bas et le lecteur audio de l'application encore en dessous.
Pendant la lecture, le défilement automatique d'alphaTab faisait défiler toute la page : les
boutons lecture et stop sortaient de l'écran, et il fallait remonter pour arrêter. Le tempo ne
proposait que quatre valeurs, et aucun raccourci clavier n'existait. Niko trouve le lecteur
« pas pratique » et souhaite que tout ce qui le concerne tienne dans un seul bloc, sur une page
ou une fenêtre à part.

## Décision
- **Une page à part**, `/b/:bandId/songs/:songId/versions/:versionId/tablature/:fileId`
  (`TablaturePage.vue`, chargée à la demande par le routeur, et alphaTab avec elle). « Ouvrir la
  tablature », dans la liste des fichiers, est un lien vers elle : un clic molette l'ouvre dans
  un nouvel onglet, et l'adresse peut se garder en favori. Pas de fenêtre surgissante
  (`window.open`) : les navigateurs la bloquent souvent, elle se comporte mal sur téléphone et ne
  partage pas l'état de l'application (lecteur audio du bas, session).
- **La page couvre l'application** (`position: fixed`), en-tête et pied de page compris, sauf le
  lecteur audio du bas, qui reste visible au-dessus duquel elle s'arrête
  (`--audio-player-height`, ADR 0037). Elle a un lien « Retour à la version » et, si le
  navigateur le permet, un bouton « Plein écran » (Fullscreen API). La page derrière ne défile
  plus tant qu'elle est ouverte.
- **Un seul bloc** : avec `fill`, `GuitarProViewer` occupe toute la hauteur de son parent, en
  colonne : les commandes en haut, la partition au milieu dans son propre cadre qui défile, et le
  panneau des pistes en bas. alphaTab retient à sa création l'élément qui défile : on lui donne ce
  cadre (`player.scrollElement`), si bien que son défilement automatique ne bouge plus la page.
  « Placer la partition sur une mesure » fait défiler ce cadre aussi.
- **Raccourcis clavier**, sur cette page seulement : Espace lecture ou pause, ← et → mesure
  précédente ou suivante (sans sélectionner), L boucle, Échap retour au début. Ils sont écoutés en
  phase de capture et arrêtés là : le lecteur audio du bas, qui écoute aussi Espace, ne réagit
  donc pas en même temps. Ils ne jouent pas quand un champ, un bouton ou un lien a le focus
  (`isGlobalShortcut`, comme le lecteur du bas), ni pendant le calage sur une maquette (#29), qui
  se tape à la barre d'espace sur son propre bouton.
- **Tempo de 25 à 150 %** par pas de 5 % (curseur), au lieu de quatre valeurs, avec un bouton
  « 100 % » pour revenir au tempo du fichier.
- La comparaison de deux fichiers (#30) garde le lecteur dans sa page, sans `fill` ni raccourcis.
- Pas de nouveau paquet npm ; aucun changement côté API.

## Conséquences
- Commandes et pistes restent à l'écran pendant toute la lecture, à toute taille d'écran.
- La page de la version ne télécharge plus jamais alphaTab ; ouvrir une tablature change de page
  (le retour ramène à la version, rechargée).
- Les réglages de la séance (zoom, portées, tempo, mix) sont toujours perdus à la fermeture :
  leur mémorisation est prévue dans le lot suivant.
- Les tests Playwright couvrent le défilement dans le cadre et le clavier (`?fill=1` du banc
  d'essai) ; vitest couvre la page, le lien et les raccourcis.
