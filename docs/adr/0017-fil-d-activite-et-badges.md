# ADR 0017 — Fil d'activité et badges : un événement par modification, écrit dans la même transaction

- Statut : accepté
- Date : 2026-10-14

## Contexte
Dans un groupe, chacun veut savoir ce qui a bougé depuis sa dernière visite : un morceau
ajouté, une nouvelle version, un fichier déposé, un commentaire. Le backlog (#11) demande
une pastille « nouveau » ou « modifié » dans la liste des morceaux, qui disparaît quand on
ouvre le morceau, et un fil d'activité du groupe. Les notifications par e-mail (#12) liront
les mêmes événements.

## Décision
- **Table `ActivityEvents`** : groupe, auteur, type, morceau, version et fichier ou commentaire
  concernés, date, et une colonne JSON `Details` (titre du morceau, numéro et titre de la
  version, nom du fichier) pour écrire la phrase même après la suppression du morceau.
  Identifiant `bigint` attribué par la base.
- **Écriture dans les handlers**, par `ActivityLog`, avant leur `SaveChangesAsync` : l'événement
  et la modification partent dans la même transaction. Une modification refusée ne laisse
  aucun événement ; un événement ne décrit jamais une modification qui n'a pas eu lieu.
- **Neuf types** : morceau créé, modifié, supprimé ; version ajoutée, modifiée ; fichier
  ajouté, retiré ; commentaire, réponse. Pas d'événement pour la modification, la
  suppression ou la résolution d'un commentaire, ni pour les membres.
- **Table `SongViews`** (utilisateur, morceau, date) : dernière ouverture, notée par
  `POST …/songs/{id}/seen` à l'ouverture de la page du morceau, Guest compris.
- **Badge calculé à la lecture de la liste** : « Modifié » s'il existe un événement d'un autre
  membre après ma dernière ouverture ; « Nouveau » si je ne l'ai jamais ouvert et qu'un autre
  membre l'a modifié depuis mon arrivée dans le groupe. Mes propres modifications ne comptent
  pas. Un nouveau membre ne voit donc pas tout l'historique en « Nouveau ».
- **Fil paginé par curseur** `(CreatedAt, Id)`, du plus récent au plus ancien, avec l'index
  `(BandId, CreatedAt, Id)`. Un événement arrivé pendant la lecture ne décale pas les pages.
- **Compte purgé** : ses événements dans les groupes partagés restent, sans auteur
  (« Compte supprimé »), comme ses commentaires (ADR 0014) ; ses ouvertures sont effacées.
  Les événements d'un groupe effacé partent avec lui.
- **Export RGPD** : le fil n'y figure pas. Il ne fait que dater des modifications dont le
  contenu (morceaux, versions, commentaires) est déjà dans `data.json`.

## Alternatives écartées
- **Intercepteur `SaveChanges`** qui déduit les événements des entités modifiées : rien à
  écrire dans les handlers, mais il faut deviner l'intention (une version ajoutée modifie
  aussi le morceau). Il convient mieux au journal d'audit (#19), qui doit tout tracer.
- **Pagination par numéro de page** : une page glisse dès qu'un événement arrive en tête.
- **Badge stocké par membre** (une ligne « non lu » par membre et par morceau) : une écriture
  par membre à chaque modification, pour une information que la lecture calcule.

## Conséquences
- Chaque nouveau handler qui modifie un morceau doit appeler `ActivityLog`, sinon il
  n'apparaît ni dans le fil ni dans les badges.
- La liste des morceaux fait deux sous-requêtes de plus par ligne ; les index
  `(SongId, CreatedAt)` et la clé `(UserId, SongId)` les gardent courtes.
- #12 lira `ActivityEvents` après le dernier `Id` traité. Deux transactions peuvent valider
  leurs `Id` dans le désordre : le dispatcher devra relire un peu en arrière ou attendre
  quelques secondes avant de traiter un événement.