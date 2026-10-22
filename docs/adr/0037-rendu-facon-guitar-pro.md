# ADR 0037 — Rendu de la tablature façon Guitar Pro : réglages d'alphaTab, sans nouvelle bibliothèque

- Statut : accepté
- Date : 2026-10-22

## Contexte
Le lecteur Guitar Pro (#28, ADR 0034) dessine la partition avec les réglages par défaut
d'alphaTab : mise en page « Page », qui range les mesures selon leur contenu (largeurs inégales,
nombre de mesures par ligne variable), lignes de portée gris clair, partition posée directement
dans la carte. Les musiciens du groupe connaissent l'écran de Guitar Pro et demandent un
affichage qui s'en rapproche : mêmes lignes de mesures que dans leur fichier, page blanche,
zoom, choix entre partition et tablature, métronome et décompte.

## Décision
- **alphaTab dessine déjà « à la Guitar Pro »** (`NotationMode.GuitarPro` par défaut : bends,
  vibratos, let ring, P.M. comme dans Guitar Pro). On ne change donc que des réglages d'alphaTab
  1.8.4, dans `src/features/guitarpro/display.ts`. Pas de nouveau paquet npm, pas de police
  ajoutée : Bravura reste la police musicale (celle de Guitar Pro est propriétaire).
- **Mise en page du fichier** sur un écran d'au moins 720 px : `LayoutMode.Parchment` respecte
  les mesures par ligne et leurs proportions enregistrées par Guitar Pro 6 à 8 (`systemsLayout`,
  `displayScale`) ; un fichier `.gp3` à `.gp5`, qui n'en a pas, prend 3 mesures par ligne. Sous
  720 px (le seuil du menu de #14), `LayoutMode.Page` range les mesures lui-même : quatre mesures
  à 360 px seraient illisibles. Le passage d'un côté à l'autre du seuil redessine la partition.
- **Couleurs et polices** proches de Guitar Pro : lignes de portée `#6b6b6b` (au lieu de gris
  clair), barres de mesure `#1a1a1a`, numéros de mesure rouges, chiffres de tablature en Arial
  13 px.
- **Page blanche sur bureau gris** (CSS), largeur 1 100 px au plus ; à moins de 720 px la page
  prend toute la largeur, sans ombre, et rien ne défile à l'horizontale.
- **Réglages de la séance** (perdus à la fermeture, comme ceux de l'ADR 0034) : zoom de 75 à
  150 %, portées « Comme dans le fichier », « Partition et tablature », « Tablature » ou
  « Partition » (appliquées aux pistes à cordes ; batterie et piano gardent leur portée),
  métronome et décompte (le son de la tablature seulement : désactivés quand elle suit une
  maquette, #29). Le fichier n'est jamais modifié.
- **La mention « rendered by alphaTab »** en bas de la partition reste : alphaTab l'écrit sans
  réglage pour la retirer, et nous ne modifions pas ses fichiers (MPL-2.0, ADR 0034).
- **Correctif #29** : une tablature calée ne passe sur sa maquette qu'une fois les sons d'alphaTab
  chargés (`playerReady`). Avant, la banque de sons pouvait arriver après le changement de
  lecteur et mettre la maquette en pause dès son départ ; le test Playwright de #29 échouait une
  fois sur deux avec la mise en page du fichier.

## Conséquences
- Un fichier Guitar Pro 7 ou 8 s'affiche avec les mêmes lignes que dans Guitar Pro sur un
  ordinateur ; sur téléphone, l'affichage reste celui de #14.
- Changer de zoom ou de portées redessine la partition (environ une demi-seconde sur un long
  morceau) ; la lecture continue.
- La vue « défilement horizontal » de Guitar Pro n'est pas reprise : alphaTab fixe l'élément qui
  défile à sa création, et passer de la page à la ligne demanderait de recréer le lecteur. À
  reprendre si le besoin se confirme.
