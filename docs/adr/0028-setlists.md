# ADR 0028 — Setlists : liste ordonnée des sorties, durée totale calculée par l'API, feuille imprimée sans bibliothèque

- Statut : accepté
- Date : 2026-10-19

## Contexte
Le backlog (#21) demande des setlists de concert : un nom, une date, un lieu, des morceaux dans
l'ordre de jeu, chacun avec une version facultative et des notes. Chaque ligne montre la tonalité
et le BPM de la version, et sa durée : celle du fichier audio (#15, ADR 0021), sinon une durée
saisie. L'API calcule la durée totale. La liste se réordonne à la souris et au clavier, avec la
nouvelle position annoncée aux lecteurs d'écran, en reprenant le composant des sorties (#10,
ADR 0016). Une vue imprimable tient sur une page, en gros caractères. Tests demandés :
réordonnancement et total exact.

## Décision
- **Agrégat `Setlist`** (`BandId`, `Name`, `Date` facultative, `Venue` facultatif) et ses lignes
  `SetlistItem` (`SongId`, `VersionId` facultatif, `Position`, `Notes`, `DurationMs` saisie).
  Même mécanique que les pistes d'une sortie : clé `(SetlistId, SongId)`, un morceau une fois
  par setlist ; index unique `(SetlistId, Position)` ; la liste complète est envoyée à chaque
  changement (`PUT …/setlists/{id}/items`) et enregistrée en deux passes dans une transaction
  (positions mises en négatif, puis remises dans le nouvel ordre). Au plus 50 lignes.
- **Version facultative** : `VersionId` null veut dire « la dernière version du morceau », relue à
  chaque affichage. La réponse donne la version choisie (`VersionId`) et la version jouée
  (`PlayedVersionId`), d'où viennent la tonalité, le BPM et la durée audio.
- **Durée d'une ligne** : la durée saisie (1 s à 60 min) prime ; sinon celle du dernier fichier
  audio envoyé sur la version jouée dont l'aperçu est prêt (ADR 0021) ; sinon inconnue. La saisie
  prime parce qu'un morceau se joue souvent plus long sur scène que sur la maquette.
- **Total calculé par l'API** dans `SetlistDto` : somme exacte, en millisecondes, des durées
  connues (`TotalDurationMs`), et nombre de lignes sans durée (`MissingDurationCount`) que
  l'écran signale au lieu de les compter pour zéro en silence. Rien n'est stocké : le total suit
  un nouvel aperçu audio sans migration.
- **Ce qui est dans une setlist ne va pas à la corbeille** (409 avec le nom des setlists), comme
  pour une sortie (ADR 0016, ADR 0027) : un morceau présent dans une setlist, et une version
  choisie explicitement. Une ligne « dernière version » ne bloque pas la mise à la corbeille
  d'une version : la précédente devient la dernière.
- **Droits** : `[BandScoped]` comme les sorties, lecture pour tous les membres (Guest compris,
  qui peut aussi imprimer), écriture pour Member et Owner (ADR 0008).
- **Front** : pages « Setlists » (`/b/{id}/setlists`) et détail, lien dans le menu. Le
  réordonnancement réutilise `ReorderableList` de #10 : glisser-déposer HTML5 natif et boutons
  Monter / Descendre, annonce `aria-live`. **SortableJS n'est pas ajouté** : le composant existant
  couvre le besoin sans nouvelle dépendance.
- **Impression** : `window.print()` et une feuille `@media print` (composant `SetlistPrintSheet`,
  règles globales dans `main.css`). Une page qui contient `.print-sheet` n'imprime qu'elle : ni
  en-tête, ni menu, ni lecteur. Une ligne par morceau (texte trop long coupé), et une taille de
  28 pt jusqu'à 12 morceaux, puis 22, 16, 12 et 10 pt jusqu'à 50 : une page A4 dans tous les cas
  (vérifié en PDF avec Chromium, notes sur chaque ligne).
- **Purge d'un compte** (ADR 0011) : les setlists des groupes effacés partent avant leurs
  morceaux. Elles ne contiennent pas de donnée personnelle et ne sont pas dans l'export des
  données, comme les sorties.

## Conséquences
- Une setlist se prépare avant que les morceaux aient un fichier : la durée se saisit, puis
  peut s'effacer pour reprendre celle du fichier.
- Le total ne compte pas les lignes sans durée ; l'écran et la feuille imprimée disent combien
  il en manque.
- Au-delà de 50 morceaux, il faut deux setlists ; la limite tient à la page imprimée.
- Retirer un morceau d'une setlist passée est nécessaire avant de le mettre à la corbeille.