# ADR 0015 — Différences entre versions : calculées dans le navigateur, sans dépendance

- Statut : accepté
- Date : 2026-10-14

## Contexte
L'écran de comparaison A/B affiche deux versions côte à côte : pour voir ce qui a changé dans
les paroles, il faut lire les deux textes. Le backlog (#9) demande les lignes ajoutées et
supprimées des paroles et des notes, les champs modifiés (BPM, tonalité, statut) et les
fichiers ajoutés ou retirés, lisibles sans la couleur, au clavier et au lecteur d'écran. Il
proposait la bibliothèque jsdiff.

## Décision
- **Calcul dans le front**, à partir des deux versions que la page charge déjà
  (`GET …/versions/{id}`) : aucun endpoint ni DTO nouveau.
- **Différence ligne à ligne maison** (`features/versions/diff.ts`, une centaine de lignes) :
  plus longue sous-suite commune après avoir retiré le début et la fin communs. Paroles
  (20 000 caractères au plus) et notes (4 000) restent petites ; au-delà de 4 millions de
  cases, le texte est montré entièrement remplacé plutôt que de bloquer la page.
- **Sens** : de A (avant) vers B (après), quel que soit l'ordre des numéros choisis.
- **Fichiers** : même nom, sans tenir compte de la casse, veut dire même fichier. L'API ne
  renvoie pas d'empreinte.
- **Accessibilité** : `<ins>` et `<del>` avec un signe « + » ou « − » visible et un texte caché
  « Ajouté : » ou « Supprimé : » pour les lecteurs d'écran ; champs dans un tableau avec une
  colonne « Identique » ou « Modifié » ; boutons « Changement précédent / suivant » qui
  déplacent le focus et annoncent « Changement 2 sur 5 ».

## Alternatives écartées
- **jsdiff (paquet `diff`)** : fiable, mais une dépendance de plus à suivre pour un besoin
  limité aux lignes. À reprendre si l'on veut un jour les mots modifiés dans une ligne.
- **Calcul côté API** : un endpoint et un contrat de plus, pour des données que le navigateur
  a déjà.
- **axe-core dans les tests** : une dépendance de plus ; les tests du composant vérifient
  directement les balises, les textes cachés et le focus.

## Conséquences
- Les notes, les paroles et la liste des fichiers ne s'affichent plus en double dans les
  colonnes A et B : seulement leurs différences, sous les colonnes.
- Pas de différence mot à mot : une ligne retouchée apparaît supprimée puis ajoutée.