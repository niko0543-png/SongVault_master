# ADR 0019 — Liens de partage externes : jeton haché, lecture seule, journal pseudonymisé

- Statut : accepté
- Date : 2026-10-14

## Contexte
Le backlog (#13) demande un lien d'écoute seule vers une version ou une sortie, pour une
personne sans compte (ingénieur du son, label, salle), avec une expiration et un mot de passe
facultatif. Le lien doit être révocable, ne donner aucun accès en écriture ni aux autres
morceaux, et chaque accès doit être journalisé. Toute l'API exige aujourd'hui un compte
(`RequireAuthorization`), et chaque lecture passe par le groupe actif (filtre `Band`, ADR 0007).

## Décision
- **Un agrégat `ShareLink`** (table `ShareLinks`) : groupe, cible (`TargetType` `Version` ou
  `Release`, avec `VersionId` ou `ReleaseId`, une contrainte CHECK exige exactement l'une des
  deux), libellé, empreinte du jeton, mot de passe haché facultatif, expiration (1 à 90 jours,
  30 par défaut), révocation, auteur. Version, sortie ou groupe supprimés : leurs liens partent
  en cascade, journal compris. Compte purgé : le lien reste au groupe, sans auteur.
- **Jeton comme les invitations** (ADR 0009) : 32 octets aléatoires en Base64Url dans l'URL
  `/s/{jeton}`, seule l'empreinte SHA-256 est en base. Le lien complet n'est montré qu'à la
  création.
- **Routes publiques à part** : `api/share/{jeton}` (`[AllowAnonymous]`), en lecture seule :
  le contenu, les fichiers audio de la cible, la pochette d'une sortie, et le déverrouillage.
  Aucune n'écrit dans les morceaux. Lien inconnu, expiré ou révoqué : 404, sans dire lequel ;
  jamais 401, que le front prendrait pour une session expirée.
- **Lecture limitée à la cible** : le dépôt ôte le filtre de groupe mais part toujours du lien
  (sa cible et son `BandId`). Un fichier n'est servi que s'il est audio et appartient à la
  version partagée, ou à la version choisie d'une piste de la sortie. Le contenu est lu à chaque
  ouverture : un fichier ajouté depuis apparaît, un fichier supprimé disparaît.
- **Mot de passe** : haché par le `PasswordHasher` d'Identity. `POST …/unlock` le vérifie (limité
  comme la connexion, 10 essais par minute et par IP) et pose un cookie `songvault.share`
  `HttpOnly`, `SameSite=Strict`, limité au chemin `/api/share/{jeton}`, valable 12 h au plus
  et jamais au-delà du lien. Son contenu (identifiant du lien et fin) est signé par Data
  Protection. Un cookie plutôt qu'un en-tête : la balise `<audio>` ne sait pas en envoyer.
- **Fichiers** : stockage objet, redirection vers une URL signée de lecture (30 min) ; stockage
  local, contenu servi par l'API avec `Range`. Jamais en pièce jointe : le lien sert à écouter.
- **Journal `ShareLinkAccesses`** : date, type (`Opened`, `Played`, `WrongPassword`), fichier
  écouté, empreinte SHA-256 de l'IP salée par lien (sel aléatoire en base, jamais renvoyé), et
  navigateur (256 caractères au plus). Une écoute compte quand le fichier est demandé depuis
  son début : les reprises au milieu (`Range`) ne comptent pas. Les membres voient les 200
  dernières lignes, avec les 8 premiers caractères de l'empreinte pour reconnaître un même
  visiteur.
- **Gestion par les Member et Owner** (`[MinimumBandRole(Member)]`, GET compris : le journal
  dit qui a écouté) : créer, lister par cible, révoquer (le lien reste visible avec son
  journal), supprimer (lien et journal effacés).
- **Limitation de débit** : nouvelle politique `share`, 120 requêtes par minute et par IP,
  sur les routes publiques ; réponses `Cache-Control: no-store` et `X-Robots-Tag: noindex`.

## Alternatives écartées
- **Compte invité automatique** : un Guest voit tout le groupe, pas une seule version, et il
  faudrait une adresse e-mail.
- **URL signée du stockage envoyée telle quelle** : impossible à révoquer, pas de journal, et
  elle expire trop vite pour un lien de plusieurs jours.
- **Jeton gardé en clair** pour pouvoir réafficher le lien : une fuite de la base donnerait
  tous les liens actifs.
- **Mot de passe dans l'URL ou en paramètre de chaque requête** : il finirait dans
  l'historique et les journaux.
- **IP en clair dans le journal** : donnée personnelle d'une personne qui n'a pas de compte,
  inutile pour savoir si le lien a été écouté.

## Conséquences
- Révoquer coupe tout de suite l'ouverture de la page et les nouvelles écoutes. Une écoute déjà
  lancée depuis le stockage objet peut finir : son URL signée vaut 30 minutes.
- Un navigateur qui sonde le début du fichier avant de le lire (Safari) peut compter deux
  écoutes ; le journal reste une indication, pas une mesure d'audience.
- Le journal vit avec le lien. Il n'a pas de purge automatique : il part quand un membre
  supprime le lien, ou avec la version, la sortie ou le groupe.
- La CSP du front autorise les images du stockage objet (`img-src`), comme déjà l'audio : la
  pochette arrive par une URL signée.