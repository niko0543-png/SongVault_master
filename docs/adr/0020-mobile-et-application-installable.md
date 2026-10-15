# ADR 0020 — Mobile et application installable : CSS adaptatif et service worker écrit à la main

- Statut : accepté
- Date : 2026-10-15

## Contexte
Le backlog (#14) demande une mise en page adaptée aux petits écrans, une application
installable sur Android et iOS et un lecteur utilisable au pouce. Critères : Lighthouse mobile
≥ 90 en accessibilité, installable sur les deux systèmes, aucun défilement horizontal à 360 px.
Mesuré avant ce travail, à 360 px de large : 13 pages sur 19 défilaient à l'horizontale
(en-tête, liste des versions, fichiers, membres, tableau de la politique de confidentialité,
lecteur audio).

## Décision
- **Pas d'application native ni de framework d'interface** : le front Vue reste le même, rendu
  adaptatif par des règles CSS `@media (max-width: 720px)` et `(max-width: 600px)` dans les
  composants concernés. Aucun nouveau paquet npm.
- **En-tête replié** sous 720 px : un bouton « Menu » (`aria-expanded`, `aria-controls`) ouvre la
  navigation, une entrée par ligne ; elle se referme à chaque changement de page.
- **Cibles tactiles** de 44 px de haut au moins sur un écran tactile (`pointer: coarse`) pour les
  boutons, listes et champs.
- **Lecteur au pouce** : sur téléphone, titre et temps en haut, barre de position pleine largeur,
  puis « −10 », lecture et « +10 » centrés en bas. Marges `env(safe-area-inset-bottom)` pour la
  barre d'accueil de l'iPhone. Titre et commandes sur l'écran verrouillé par l'API Media Session
  (lecture, pause, ±10 s, position).
- **Manifeste** `public/manifest.webmanifest` (`display: standalone`, icônes 192 et 512 px, une
  icône `maskable`) et balises iOS dans `index.html` (`apple-touch-icon`, titre). Les PNG sont
  générés depuis `public/icons/icon.svg` par `scripts/generate-icons.mjs`, avec le Chromium de
  Playwright déjà utilisé par les tests e2e, et versionnés.
- **Service worker écrit à la main** (`public/sw.js`, une soixantaine de lignes) plutôt que
  `vite-plugin-pwa` / Workbox : il ne fait que trois choses, et rien de plus ne doit être mis en
  cache.
  - `/api/` n'est **jamais** intercepté : morceaux, fichiers audio, session et liens de partage
    passent toujours par le réseau. Rien de privé n'est gardé sur l'appareil, une révocation ou
    une déconnexion vaut tout de suite.
  - Les pages passent par le réseau ; sans réseau, une page « Pas de connexion » autonome.
  - Les fichiers `/assets/` de Vite (nom avec empreinte) sont gardés en cache, 60 au plus.
  - `VERSION` dans le fichier change à chaque modification ; nginx sert `sw.js` en `no-cache`.
- **Pas de mode hors connexion** pour l'écoute ni la consultation : ce serait garder des fichiers
  privés sur un téléphone, avec la question de leur effacement à la déconnexion ou au retrait du
  groupe. À reprendre si le besoin vient (#15 produira des MP3 légers qui s'y prêteraient).
- **Pas de bouton « Installer » dans l'application** : Android le propose de lui-même ; iOS
  passe toujours par « Partager → Sur l'écran d'accueil ».
- **Pas de notifications push** : les notifications restent par e-mail (ADR 0018).

## Conséquences
- Le front marche installé, sans barre d'adresse ; les liens externes (stockage Azure) s'ouvrent
  comme avant.
- L'installation exige HTTPS : vérifiable sur Azure, ou en local par `localhost` (le service
  worker n'est enregistré qu'en production, donc dans l'image Docker).
- Un test e2e (`e2e/mobile.spec.ts`) vérifie à 360 px qu'aucune page ne défile à
  l'horizontale, la taille des boutons du lecteur, le menu, le manifeste et le service worker.
- Le score Lighthouse se mesure à la main dans Chrome (pas de Lighthouse en CI : il faudrait
  une dépendance et une pile lancée dans le workflow).
- Toute nouvelle page doit passer le test à 360 px : l'ajouter à `mobile.spec.ts`.