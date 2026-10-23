# ADR 0035 — Comparaison de deux fichiers Guitar Pro : empreintes de mesures, diff dans un Web Worker

- Statut : accepté
- Date : 2026-10-21

## Contexte
L'écran de comparaison A/B montre les différences des paroles, des notes, des champs et des
fichiers (#9, ADR 0015), mais pas celles des tablatures : pour voir ce qui a changé entre deux
fichiers Guitar Pro, il faut ouvrir les deux et les lire. Le backlog (#30) demande les
différences mises en relief dans la partition, une liste cliquable, et des changements
d'ensemble (accordage, tempo, pistes) annoncés à part. Une mesure insérée au début ne doit pas
marquer tout le morceau comme modifié.

## Décision
- **Tout se calcule dans le navigateur**, comme #9 : les deux fichiers sont lus par l'adresse de
  lecture habituelle et par `ScoreLoader` d'alphaTab (#28, ADR 0034). Aucun endpoint nouveau, et
  rien n'est jamais renvoyé à l'API.
- **Sens** : de A (avant) vers B (après), comme #9. La partition affichée est celle de B ; une
  mesure de A disparue n'y existe plus et n'apparaît que dans la liste (« supprimée, avant la
  mesure 3 »).
- **Données simples** (`compare/plain.ts`) : la partition est réduite à des pistes, des mesures
  et des temps sans classe ni référence vers alphaTab. Chaque mesure a une empreinte :
  chiffrage, puis pour chaque voix et portée les temps (durée, points, n-olet, silence) et leurs
  notes (corde, case, effets de jeu : liaison, glissé, bend, vibrato, harmonique, étouffé…).
- **Différence de suites** (`compare/sequence.ts`) : début et fin communs retirés, puis plus
  longue sous-suite commune sur les empreintes de mesures, comme les lignes de #9. Une mesure
  insérée décale les suivantes sans les marquer. Dans un bloc de changements, les mesures de A
  et de B sont associées dans l'ordre (« modifiée ») ; le surplus est ajouté ou supprimé. Dans une
  mesure modifiée, la même méthode s'applique aux temps, puis note par note. Au-delà de 4 millions
  de cases (2 000 mesures de chaque côté), le milieu est montré entièrement remplacé plutôt que de
  bloquer la page.
- **Pistes** associées par nom et instrument, puis par nom seul (piste dont le son a changé), puis
  par instrument seul (piste renommée). Une piste sans partenaire est annoncée ajoutée ou
  supprimée.
- **Changements d'ensemble en une ligne** : tempo de départ, pistes ajoutées ou supprimées,
  accordage, capodastre. Les notes sont comparées par corde et case, ce que l'on joue : un passage
  en drop D est annoncé une fois, sans colorer une seule note.
- **Web Worker** (`compare.worker.ts`, module ES comme les workers d'alphaTab) : le calcul ne fige
  pas la page sur un long morceau. La lecture des fichiers reste dans la page, car la partition
  lue est aussi celle que l'on dessine. Sans `Worker` (tests), le calcul se fait sur place.
- **Couleurs** par `NoteStyle` et `BarStyle` d'alphaTab : vert `#1a7f37` pour ajouté, orange
  `#b35900` pour modifié, chacun d'un contraste d'au moins 4,5:1 sur le blanc de la partition.
- **La liste est la référence** (accessibilité) : chaque différence y est écrite en mots, dans
  l'ordre des mesures. Un clic place la partition sur la mesure (défilement, sélection, départ de
  la lecture) ; « Boucler la sélection » la rejoue.
- **Chargé à la demande** : `GuitarProCompare.vue` n'est importé que si les deux versions ont un
  fichier Guitar Pro lisible. Avec plusieurs fichiers, on choisit lesquels comparer ; par défaut,
  le même nom des deux côtés.

## Alternatives écartées
- **Myers (O(ND))** : moins de mémoire que la table de la sous-suite commune, mais plus de code
  pour des morceaux de quelques centaines de mesures ; la méthode de #9 suffit et est déjà testée.
- **Comparer par hauteur de note** : un changement d'accordage colorerait tout le morceau.
- **Diff côté API** : il faudrait lire les formats Guitar Pro en C#, ce qu'alphaTab fait déjà dans
  le navigateur.
- **Lire les fichiers dans le Worker** : alphaTab y serait chargé une seconde fois (1,1 Mo) et la
  partition de B devrait revenir à la page pour être dessinée.

## Conséquences
- Une mesure retouchée et une mesure insérée juste à côté, dans le même bloc, peuvent être
  associées dans le mauvais sens : la liste dit alors « modifiée » et « ajoutée » pour deux
  mesures voisines. Le total des différences reste juste.
- Seule la première voix de la première portée est détaillée note par note ; une différence
  dans une autre voix ou portée marque la mesure « modifiée dans une autre voix ».
- Les changements de tempo en cours de morceau, de nuances et de paroles ne sont pas comparés.