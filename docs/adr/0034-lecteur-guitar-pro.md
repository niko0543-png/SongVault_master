# ADR 0034 — Lecteur Guitar Pro : alphaTab chargé à la demande, en lecture seule

- Statut : accepté
- Date : 2026-10-21

## Contexte
Le backlog (#28) demande d'afficher et d'écouter les fichiers Guitar Pro d'une version sans
quitter SongVault : partition et tablature, lecture avec un curseur, choix des pistes, muet et
solo, tempo ralenti, boucle et transposition pour travailler un passage. Aujourd'hui, un fichier
`.gp` ou `.gp5` ne peut que se télécharger, et `.gp3` et `.gp4` sont refusés à l'envoi. Écrire
un lecteur de ces formats (cinq formats binaires, un synthétiseur, une mise en page musicale)
n'est pas raisonnable : il faut une bibliothèque.

## Décision
- **alphaTab** (`@coderline/alphatab` 1.8.4, et `@coderline/alphatab-vite` 1.8.4 pour la
  construction), versions exactes. Il lit GP3 à GP5, GPX (GP6) et GP (GP7 et 8), dessine en SVG
  et joue avec son propre synthétiseur dans un worker et un AudioWorklet. Licence MPL-2.0 :
  l'usage commercial est permis ; seules des modifications des fichiers d'alphaTab eux-mêmes
  devraient être publiées, et nous n'en faisons pas. La police Bravura est sous licence OFL et la
  banque de sons Sonivox sous licence Apache 2.0 ; leurs licences sont copiées avec eux.
- **Chargé à la demande** : `GuitarProViewer.vue` est un composant asynchrone
  (`defineAsyncComponent`) ouvert par « Afficher la tablature » dans la liste des fichiers.
  alphaTab (1,1 Mo, 276 Ko compressé) est dans ce morceau seulement ; le worker et l'AudioWorklet
  (modules ES, `worker: { format: 'es' }`) ne sont demandés qu'à l'ouverture. Une seule
  tablature est ouverte à la fois.
- **Ressources statiques** : le greffon Vite copie la police dans `/font/` et la banque de sons
  (`sonivox.sf2`, 1,3 Mo) dans `/soundfont/` au démarrage de chaque construction ; ces copies
  ne sont pas versionnées (`.gitignore`). nginx les garde une semaine en cache, faute
  d'empreinte dans leur nom.
- **La CSP ne change pas** : le worker, l'AudioWorklet, la police et les sons viennent de
  `'self'`, le fichier lui-même de l'adresse signée du stockage déjà permise par `connect-src`.
  Le test Playwright s'exécute sous une CSP équivalente et échoue sur la moindre violation.
- **Lecture seule** : le fichier est lu par `fetch` puis donné à alphaTab ; rien n'est jamais
  renvoyé à l'API. Tempo (50, 75, 90 ou 100 %), boucle sur la sélection faite à la souris,
  transposition (±12 demi-tons, son et notation ; les cases de la tablature restent celles du
  fichier), pistes affichées, muet et solo sont des réglages de la séance, perdus à la
  fermeture. Pas d'édition.
- **Un seul son à la fois** : la tablature met le lecteur audio du bas en pause quand elle
  joue, et le lecteur du bas met la tablature en pause.
- **Signatures** : `.gp` doit commencer par `PK` (ZIP), `.gpx` par `BCFS`, `.gp3` à `.gp5` par
  l'octet de longueur suivi de `FICHIER GUITAR PRO`. `.gp3` et `.gp4` rejoignent la liste
  blanche (10 extensions).

## Conséquences
- Ouvrir une tablature télécharge environ 7 Mo la première fois (code, worker, worklet, police,
  sons), puis le cache du navigateur s'en charge. Le paquet de la première page ne grossit pas.
- La partition suit la largeur de l'écran (mise en page « Page »), lisible dès 360 px.
- Le fichier n'est pas analysé par le serveur : un fichier Guitar Pro corrompu s'annonce
  « Impossible d'ouvrir ce fichier Guitar Pro. » dans le lecteur, le téléchargement restant
  possible.
- La synchronisation avec la maquette audio (#29) et la comparaison de deux fichiers (#30)
  s'appuieront sur ce composant et sur `ScoreLoader` d'alphaTab.
- Monter de version d'alphaTab se fait par une PR qui relance le test Playwright
  `e2e/guitar-pro.spec.ts`.