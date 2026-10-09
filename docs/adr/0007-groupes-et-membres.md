# ADR 0007 — Les morceaux appartiennent à un groupe, plus à un utilisateur

- Statut : accepté
- Date : 2026-10-09

## Contexte
Jusqu'à la v1.0.0, chaque morceau appartient à un utilisateur (`Song.OwnerId`) et
`SongRepository` ne lit que les morceaux de l'utilisateur connecté. La version commerciale
vise des groupes de musique : plusieurs personnes doivent travailler sur les mêmes morceaux,
et une même personne peut jouer dans plusieurs groupes.

## Décision
- **Le groupe (`Band`) devient la frontière d'isolation des données.** Un morceau porte un
  `BandId` ; ses versions et fichiers en héritent. `OwnerId` disparaît.
- **Adhésion par la table `BandMemberships`** (BandId, UserId, Role, JoinedAt), clé
  composite. Un utilisateur peut être membre de plusieurs groupes. La colonne `Role` existe
  dès maintenant (tout le monde est `Owner`) ; les droits par rôle arrivent avec #2.
- **Le groupe actif est dans l'URL** : `/api/bands/{bandId}/songs/…` côté API,
  `/b/{bandId}/songs/…` côté front.
- **Deux niveaux de contrôle** :
  1. le filtre `[BandScoped]` vérifie l'adhésion avant la liaison du modèle et renseigne
     `IBandContext` ;
  2. `SongRepository` lit `BandSongs` et un filtre EF Core nommé `Band` sur `Song` sert de
     filet de sécurité si une future requête oublie de partir de `BandSongs`.
- **Un non-membre reçoit 404**, comme un utilisateur sur les données d'autrui aujourd'hui :
  l'API ne révèle pas qu'un groupe ou un morceau existe. Un membre sans le droit requis
  recevra 403 (#2).
- **Groupe personnel** : chaque utilisateur existant reçoit « Groupe de <nom> » par
  migration ; un nouvel inscrit le reçoit au premier appel de `GET /api/bands`, l'inscription
  étant fournie par `MapIdentityApi`.
- **Migration en trois temps** : `AddBands` (ajout), `BackfillBands` (données),
  `RequireSongBand` (verrouillage). Les morceaux sans propriétaire valide (`OwnerId` vide ou
  compte disparu) sont supprimés par `BackfillBands`, puis leurs fichiers retirés du volume
  par `scripts/purge-orphan-files.ps1`.

## Alternatives écartées
- **Groupe actif dans un en-tête `X-Band-Id`** : moins de routes à changer, mais les liens
  ne sont plus partageables entre membres et chaque test doit penser à l'en-tête.
- **Groupe actif mémorisé dans le cookie ou la session** : deux onglets sur deux groupes se
  mélangeraient, et l'API deviendrait dépendante d'un état caché.
- **Garder `OwnerId` et ajouter un partage morceau par morceau** : ne répond pas au besoin
  d'un catalogue commun, et multiplie les règles d'accès.
- **Filtre EF seul, sans `[BandScoped]`** : un non-membre obtiendrait une liste vide (200)
  au lieu d'un 404, et l'adhésion ne serait jamais vérifiée explicitement.

## Conséquences
- Toutes les routes des morceaux, versions et fichiers changent ; le front et les tests
  d'API suivent. Les anciens liens `/songs/…` du front redirigent vers l'accueil.
- Les morceaux sans propriétaire valide sont perdus ; seule la sauvegarde prise avant la
  migration permet de les retrouver.
- Les traitements hors requête HTTP (tâches de fond futures) doivent désactiver le filtre
  explicitement : `IgnoreQueryFilters([SongVaultDbContext.BandFilter])`.
- La corbeille (#20) ajoutera un second filtre nommé sans toucher à celui-ci.