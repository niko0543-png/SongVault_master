# ADR 0032 — Paroles avec accords : ChordPro dans le champ paroles, transposition à l'affichage

- Statut : accepté
- Date : 2026-10-20

## Contexte
Le backlog (#25) demande les accords au-dessus des paroles et la transposition, avec deux
critères : une transposition sans perte et un affichage lisible sur mobile. Les paroles d'une
version sont un texte libre de 20 000 caractères (`SongVersion.Lyrics`) ; des versions en ont
déjà, sans accords. La vue d'ensemble propose le format ChordPro dans ce champ, une
transposition faite à l'affichage et un texte source jamais réécrit.

## Décision
- **Format** : ChordPro dans le champ `Lyrics`, sans nouvelle colonne ni migration. Un accord
  s'écrit entre crochets juste avant la syllabe où il tombe : `[Am]Je mar[G]che seul`. Des
  paroles sans crochets restent des paroles. Directives reconnues : `{c: …}` (et `comment`, `ci`,
  `cb`…) pour une indication affichée à part ; les autres (`{title: …}`) sont ignorées à
  l'affichage.
- **Grammaire d'un accord** : une note (A à G, `#` ou `b`), un suffixe fait des seuls éléments
  d'accord (`m`, `maj`, `min`, `dim`, `aug`, `sus`, `add`, chiffres, `#`, `b`, `+`, `-`, `°`, `ø`,
  `Δ`, parenthèses) et une basse facultative (`/F#`). `[Refrain]`, `[Bridge]` ou `[x2]` ne sont
  donc pas des accords : seuls sur leur ligne, ce sont des titres de partie ; ailleurs, du texte.
  La même expression régulière existe en TypeScript (`chords.ts`) et en C# (`ChordPro.cs`).
- **Transposition à l'affichage** : décalage en demi-tons choisi par boutons (−½ ton, +½ ton),
  remis à zéro à chaque version, ouvert aux invités. Le suffixe ne change jamais, la basse est
  transposée avec la note. On transpose toujours depuis le texte enregistré : un décalage de 0
  ou de ±12 rend la source à l'identique, et l'affichage ne réécrit rien.
- **Dièses ou bémols** : ceux de la tonalité d'arrivée (Eb, Bb, F, Cm, Gm… en bémols ; E, A, D,
  F#m… en dièses). En do majeur et la mineur, sans altération à la clé, on suit l'écriture de la
  source. La tonalité de départ est celle de la version, sinon celle du premier accord.
- **Enregistrer une transposition** reste une action explicite (« Enregistrer dans cette
  tonalité », Member et Owner, après confirmation) : les accords du texte sont réécrits et la
  tonalité de la version suit, si elle en a une. Refusé tant que le formulaire a d'autres
  modifications non enregistrées.
- **Affichage** (`ChordSheet.vue`) : chaque mot est un bloc et chaque morceau de mot une colonne,
  accord au-dessus, syllabe en dessous. Une ligne trop longue passe à la ligne entre deux mots,
  jamais au milieu de `mar[G]che` ; un mot plus large que l'écran se coupe quand même. Texte à
  16 px au moins. Pas de bibliothèque ChordPro : l'analyseur tient en une centaine de lignes et
  ne sert que ce format réduit.
- **Section Paroles** : lecture par défaut, « Modifier le texte » pour la saisie (le mode texte
  s'ouvre seul sur des paroles vides). La section sort du `fieldset` désactivé des invités pour
  que les boutons de transposition restent actifs ; la zone de texte se désactive elle-même.
- **Archive d'une sortie** (ADR 0031) : `paroles.txt` reste un texte brut, sans les accords ni
  les directives (une indication `{c: Refrain}` devient la ligne `Refrain`) ; quand la version a
  des accords, `accords.txt` contient la source ChordPro.

## Conséquences
- Une transposition enregistrée est une réécriture comme une autre : revenir en arrière demande
  la transposition inverse, et l'écriture des altérations peut changer (G# devenu Ab). La
  différence entre versions (ADR 0015) la montre ligne à ligne.
- Le critère « affichage lisible sur mobile » est mesuré dans Chromium à 360 px par
  `e2e/chords-mobile.spec.ts`, sans la pile Docker : Vite sert la grille seule avec la feuille de
  style de l'application, et le test vérifie qu'il n'y a pas de défilement horizontal, que chaque
  accord est calé au-dessus de sa syllabe et qu'aucun mot ne se coupe entre deux accords. La CI le
  lance à côté du test de dérive des pistes.
- Les diagrammes d'accords, la notation latine (Do, Ré…) et le capodastre ne sont pas couverts.