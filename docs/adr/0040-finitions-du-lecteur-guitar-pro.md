# ADR 0040 — Finitions du lecteur Guitar Pro : temps, icônes, double-clic, accès direct

- Statut : accepté
- Date : 2026-10-22

## Contexte
Lot 3 du compte rendu « Confort du lecteur Guitar Pro », après la page du lecteur (ADR 0038) et
le confort de travail (ADR 0039) : des finitions qui rendent le lecteur plus proche de Guitar
Pro et plus rapide à atteindre.

## Décision
- **Panneau des pistes dès une piste** : volume et vue d'ensemble des mesures servent aussi à un
  fichier d'une seule piste. Cette piste n'a ni case « afficher » (elle reste toujours affichée)
  ni muet ou solo (sans objet).
- **Temps écoulé** : « 0:42 / 2:31 » à côté du numéro de mesure, dans la barre et dans le
  panneau des pistes, d'après `playerPositionChanged` d'alphaTab. La durée suit le tempo choisi.
- **Icônes** lecture, pause et retour au début, dessinées en SVG dans `TransportIcon.vue`, sans
  paquet d'icônes. Le libellé reste sur le bouton (`aria-label`, et `title` pour la souris).
- **« Son » dans la barre principale** : le choix entre le son de la tablature et la maquette
  suivie (#29) se fait sans ouvrir les réglages ; le calage (enregistrer, refaire, supprimer)
  reste dans les réglages.
- **Double-clic sur une note** : un clic place déjà le curseur sur la note (alphaTab) ; un
  double-clic lance la lecture depuis elle. Rien ne se passe pendant la lecture ou un calage.
- **Accès direct** : l'en-tête de la page d'une version porte un lien « 🎸 Tablature » vers la
  page du lecteur, un par fichier lisible s'il y en a plusieurs. On n'a plus à descendre
  jusqu'aux fichiers.
- **Pas d'icône dans la liste des morceaux pour l'instant** : ni la liste des morceaux ni celle
  des versions ne disent aujourd'hui si une version a une tablature. Il faudrait un champ dans
  les réponses de l'API (par exemple l'identifiant de la tablature de la dernière version) ;
  ce sera une petite tâche séparée, avec ses tests côté API.

## Conséquences
- Aucun changement côté API ni nouveau paquet npm.
- Le double-clic dépend du comportement d'alphaTab au clic (curseur placé sur la note) ; le test
  Playwright le vérifie sur un vrai fichier.
