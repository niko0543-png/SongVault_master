# ADR 0041 — Barre du lecteur façon Guitar Pro et panneau des pistes redimensionnable

- Statut : accepté
- Date : 2026-10-23

## Contexte
Après les trois lots de confort (ADR 0038 à 0040), le haut du lecteur restait une suite de
boutons et de champs. Le musicien qui connaît Guitar Pro 8 cherche sa barre : transport, petit
écran noir qui dit où on en est, boucle, vitesse et transposition. Le panneau des pistes du bas
avait une hauteur fixe : trop haut pour un fichier de deux pistes, trop bas pour un orchestre.

## Décision
- **Une seule barre sombre** (`tab-gp-bar`) remplace la barre principale et la ligne
  tempo / transposition :
  - **Transport** : début (et arrêt), mesure précédente, lecture ou pause dans un rond, mesure
    suivante, dernière mesure.
  - **Écran** : la couleur et le nom de la première piste affichée (« 1. Guitare »), les
    boutons décompte et métronome (`aria-pressed`), ⋮ pour les réglages ; dessous, la mesure
    (« 12/229 »), le chiffrage de la mesure jouée, le temps (« 00:42 / 05:23 ») et le tempo du
    fichier à cet endroit (« ♩ = 170 », dernier changement de tempo jusqu'à la mesure).
  - **Boucle** en bouton à bascule, **vitesse** « ♩ 90 % » avec flèches de 5 % (un clic sur la
    valeur revient à 100 %), **transposition** avec diapason, − et +.
  - Le choix du son (#29) et le chargement des sons restent dans la barre.
- **Réglages derrière ⋮ à toutes les largeurs** sur la page du lecteur ; le choix (ouvert ou
  non) est gardé sur l'appareil comme les autres réglages (ADR 0039). Métronome et décompte
  quittent les réglages : ils sont sur l'écran.
- **Panneau des pistes redimensionnable** : une poignée en haut du panneau (`role="separator"`).
  On la tire vers le haut pour agrandir ; au clavier ↑ ↓ par 24 px, Origine pour le maximum,
  Fin pour le minimum ; double-clic pour revenir à la hauteur par défaut. Hauteur entre 48 px
  et 75 % de la fenêtre, gardée sur l'appareil (`panelHeight`, `null` = par défaut).
- Icônes ajoutées à `TransportIcon.vue` (SVG), sans paquet d'icônes.

## Conséquences
- Front seulement : pas de changement d'API, pas de migration, pas de nouveau paquet npm.
- `api.score` d'alphaTab n'est pas réactif : le lecteur garde la partition chargée dans un
  `shallowRef` pour que l'écran suive (chiffrage, tempo).
- Les tests repèrent la mesure par l'`aria-label` de `tab-bar` (« Mesure 2 sur 3 ») ; le texte
  visible est « 2/3 ».
- Symboles ♩ = ▴ ▾ ajoutés à la liste autorisée de `vue/no-bare-strings-in-template`.
