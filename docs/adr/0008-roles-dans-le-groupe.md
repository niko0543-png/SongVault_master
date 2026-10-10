# ADR 0008 — Rôles dans le groupe, vérifiés par le filtre [BandScoped]

- Statut : accepté
- Date : 2026-10-09

## Contexte
L'ADR 0007 a introduit les groupes : chaque adhésion porte un rôle (`Owner`, `Member`,
`Guest`), mais tous les membres ont aujourd'hui les mêmes droits. La version commerciale
a besoin de trois niveaux :
- Owner : gère les membres (et plus tard la facturation) ;
- Member : crée et modifie morceaux, versions et fichiers ;
- Guest : écoute et commente, aucune écriture.
Le contrôle doit être fait par l'API ; le front ne fait que masquer les boutons.

## Décision
- **Le rôle est vérifié dans `BandScopedFilter`**, juste après l'adhésion, avec le rôle
  que le filtre vient de lire. Non-membre : 404 (inchangé). Membre au rôle insuffisant : 403.
- **Rôle minimal par défaut selon la méthode HTTP** : `GET` et `HEAD` demandent `Guest`,
  toute autre méthode demande `Member`. Un nouvel endpoint d'écriture est donc protégé
  sans qu'on y pense.
- **Exceptions explicites avec `[MinimumBandRole(BandRole.X)]`** sur l'action ou le
  contrôleur ; l'attribut de l'action l'emporte. Utilisé pour la gestion des membres et le
  renommage du groupe (`Owner`), pour « quitter le groupe » (`Guest`), et plus tard pour
  les commentaires (`Guest`).
- **Hiérarchie Owner ⊃ Member ⊃ Guest**, portée par `BandRole.Allows` dans le domaine. On ne
  se fie pas à l'ordre numérique de l'enum (Owner vaut 0).
- **Toujours au moins un Owner** : `Band.ChangeRole` et `Band.RemoveMember` refusent de
  rétrograder ou de retirer le dernier Owner (`LastOwnerException`, HTTP 409).
- **Endpoints des membres** : `GET /api/bands/{bandId}/members` (tout membre),
  `PUT …/members/{userId}/role` et `DELETE …/members/{userId}` (Owner),
  `DELETE …/members/me` (quitter, tout membre). L'ajout de membres arrive avec les
  invitations (#3).

## Alternatives écartées
- **Politiques ASP.NET (`[Authorize(Policy = "BandWrite")]` et un `IAuthorizationHandler`)** :
  les filtres d'autorisation passent avant les filtres de ressource, donc avant que
  `[BandScoped]` ait renseigné `IBandContext`. Le handler devrait relire `bandId` dans la
  route et refaire la requête d'adhésion, et l'ordre « 404 pour un non-membre, puis 403 pour
  un rôle insuffisant » serait plus difficile à garantir.
- **Vérification dans chaque handler** : facile à oublier sur un nouvel endpoint, et le
  corps de la requête serait déjà lu et validé (un Guest recevrait 400 au lieu de 403).
- **Rôle minimal toujours explicite, sans défaut par méthode** : plus lisible endpoint par
  endpoint, mais un oubli ouvre l'écriture aux Guest.

## Conséquences
- Les écritures existantes (morceaux, versions, fichiers) exigent `Member` sans que leurs
  contrôleurs changent ; renommer le groupe devient réservé aux Owner.
- Un Guest reçoit 403 avant la liaison du modèle, même avec un corps invalide.
- Limite connue : deux Owner qui se rétrogradent l'un l'autre au même instant peuvent
  laisser le groupe sans Owner (aucun verrou). Accepté tant qu'il n'y a pas de facturation ;
  à revoir avec elle (concurrence optimiste sur `Band`).
- Le `POST` des commentaires devra porter `[MinimumBandRole(BandRole.Guest)]`.