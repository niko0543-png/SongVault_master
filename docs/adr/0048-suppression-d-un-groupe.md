# ADR 0048 — Suppression d'un groupe par son Owner

- Statut : accepté
- Date : 2026-10-24

## Contexte
Aucune route ne supprimait un groupe. Un groupe créé par erreur (sur le mauvais compte, avec le
mauvais nom) restait là : le dernier Owner ne peut pas le quitter (`LastOwnerException`, 409), et
seule la suppression du compte (ADR 0011) effaçait, 30 jours plus tard, les groupes dont il était
le seul membre. Depuis la page Bienvenue (PR #101), un compte sans groupe a pourtant un parcours
propre : il suffisait de pouvoir revenir à zéro.

## Décision
- **`DELETE /api/bands/{id}`, réservé aux Owner** (`[MinimumBandRole(Owner)]`, ADR 0008).
  Un Member ou un Guest reçoit 403, un non-membre 404.
- **Confirmation par le nom** : le corps porte `confirmName`, le nom du groupe recopié (espaces
  autour ignorés, casse comprise). Sinon 422. Le front n'active le bouton « Supprimer
  définitivement » qu'une fois le nom exact saisi : une simple boîte « OK / Annuler » se valide
  trop vite pour une action sans retour.
- **Effacement immédiat, sans corbeille** : la corbeille (ADR 0027) rattrape un morceau supprimé
  par erreur dans un groupe qui continue. Mettre tout un groupe en attente 30 jours aurait demandé
  un filtre de plus sur chaque requête, une jauge, un quota et une facturation pour un groupe
  invisible. La confirmation par le nom tient lieu de garde-fou ; les sauvegardes (ADR 0013)
  restent le dernier recours.
- **Même purge que la suppression du compte** : `BandContentPurge` (Infrastructure) efface, en une
  transaction, les archives des sorties, les sorties, les setlists, les morceaux (corbeille
  comprise) puis le groupe ; le reste part en cascade (adhésions, invitations, liens de partage,
  abonnement, fil d'activité, notifications, journal d'audit). `AccountRepository.PurgeAsync`
  l'appelle pour les groupes solo du compte : une seule liste à tenir à jour quand une table
  s'ajoute au groupe.
- **Journal d'audit** : il part avec le groupe. Son déclencheur (ADR 0026) laisse déjà passer les
  lignes d'un groupe qui n'existe plus ; il n'y a plus personne pour le lire.
- **Fichiers** : effacés du stockage après la transaction (fichiers, MP3 d'aperçu, pochettes,
  archives). Un fichier qui résiste ne fait pas échouer la réponse : le groupe n'existe déjà plus,
  et `scripts/purge-orphan-files.ps1` retrouve l'orphelin.
- **Abonnement** (ADR 0023) : 409 tant qu'un abonnement Stripe court sans être résilié, avec le
  même message que la suppression du compte (« résiliez-le depuis la page Facturation »). Un
  abonnement déjà résilié pour la fin de la période est arrêté tout de suite chez Stripe, avant la
  base : une erreur de Stripe laisse le groupe intact.
- **Les autres membres** ne bloquent pas la suppression : l'Owner gère déjà qui entre et qui sort.
  La zone de suppression annonce combien d'entre eux perdront l'accès. Aucun e-mail ne les
  prévient : le groupe disparaît de leur sélecteur au prochain chargement.
- **Front** : zone « Supprimer le groupe » en bas de la page Membres, aux seuls Owner. Après la
  suppression, le groupe sort du store et on revient à « / » : le groupe suivant s'ouvre, ou la
  page Bienvenue s'il n'en reste aucun.

## Conséquences
- Pas de migration ni de nouveau paquet.
- Une suppression ne s'annule pas depuis l'application : seule une restauration de la base à un
  instant donné (7 jours, ADR 0013) la rattraperait, pour tous les groupes à la fois.
- Une table ajoutée plus tard avec une clé étrangère `Restrict` vers `Bands` doit être effacée dans
  `BandContentPurge`, sinon la suppression d'un groupe et la purge d'un compte échouent toutes les
  deux (les tests `BandDeletionTests` et `AccountDeletionTests` le montrent).
