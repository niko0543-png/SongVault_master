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
- **Icône 🎸 dans les listes** : la liste des morceaux et celle des versions d'un morceau
  ouvrent directement la tablature. L'API ajoute pour cela `tablatureVersionId` et
  `tablatureFileId` à chaque morceau de la liste (la dernière version qui a une tablature
  lisible, et sa première tablature), et `tablatureFileId` à chaque version du résumé. Une
  tablature n'y figure qu'une fois déclarée saine par l'antivirus (`Clean` ou `NotScanned`,
  ADR 0024), comme pour sa lecture. Deux sous-requêtes dans les lectures existantes, sans
  migration.

## Conséquences
- Deux champs en lecture seule ajoutés aux réponses de l'API, sans migration ; pas de nouveau
  paquet npm.
- Le double-clic dépend du comportement d'alphaTab au clic (curseur placé sur la note) ; le test
  Playwright le vérifie sur un vrai fichier.
