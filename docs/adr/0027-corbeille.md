# ADR 0027 — Corbeille : suppression en deux temps, restauration 30 jours, purge quotidienne

- Statut : accepté
- Date : 2026-10-19

## Contexte
Le backlog (#20) demande une suppression réversible pendant 30 jours des morceaux, des versions et
des fichiers, puis une purge automatique, avec trois critères : un morceau supprimé se restaure en
entier, ses fichiers sont effacés du stockage après 30 jours, et ce qui est purgé ne compte plus
dans le quota du groupe (ADR 0023).
Jusqu'ici, supprimer un morceau ou un fichier effaçait les lignes, puis les fichiers du stockage, et
recalculait la jauge tout de suite. Une version ne se supprimait pas. Les sauvegardes (ADR 0013)
permettent de revenir en arrière, mais pour toute la base et par un administrateur.

## Décision
- **Trois colonnes par table** : `DeletedAt` et `DeletedById` sur `Songs`, `SongVersions` et
  `SongFiles` (interface `ISoftDeletable` du domaine). Mettre à la corbeille renseigne `DeletedAt` ;
  rien ne quitte le stockage.
- **Ce qui part ensemble porte la même date.** Supprimer un morceau marque ses versions et leurs
  fichiers encore en place avec la même date ; supprimer une version, ses fichiers. Restaurer
  remet exactement cet ensemble : une version ou un fichier supprimé avant, à une autre date,
  reste à la corbeille.
- **Filtre nommé `SoftDelete`** sur les trois entités, à côté du filtre `Band` (EF Core 10, filtres
  nommés). Toutes les lectures existantes ignorent la corbeille sans changer : la liste, le détail,
  l'écoute, les commentaires, les liens de partage, les sorties. Ce qui doit la voir l'ôte
  explicitement : la corbeille elle-même, la jauge (qui compte la corbeille jusqu'à la purge),
  l'export des données et la purge d'un compte (ADR 0011), l'antivirus (ADR 0024) et l'aperçu
  audio (ADR 0021), qui continuent leur travail sur un fichier à la corbeille.
- **Index uniques inchangés.** `(SongId, Number)` reste unique sur toutes les lignes : les numéros
  viennent de `LastVersionNumber` et ne resservent jamais, donc une version restaurée ne peut pas
  entrer en conflit. Un index filtré `WHERE DeletedAt IS NOT NULL` sur chaque table sert la purge.
- **Ce qui est sur une sortie ne va pas à la corbeille** (409, comme avant pour un morceau, ADR 0016),
  morceau ou version : une piste ne disparaît pas en silence.
- **Restaurer d'abord le contenant** : une version d'un morceau à la corbeille, ou un fichier d'une
  version à la corbeille, ne revient pas seul (409, avec la marche à suivre). La liste de la
  corbeille ne montre d'ailleurs que le contenant.
- **Routes** sous `/api/bands/{id}/trash` : `GET` (Member et Owner), `POST {kind}/{id}/restore`
  (Member et Owner), `DELETE {kind}/{id}` (Owner seulement, suppression définitive immédiate) ;
  `kind` vaut `song`, `version` ou `file`. Nouvelle route `DELETE …/versions/{id}`. Page
  « Corbeille » du front, lien dans le menu des Member et Owner.
- **Purge** : `TrashPurgeWorker` appelle chaque jour `PurgeExpiredTrashHandler`, qui efface tout ce
  qui est à la corbeille depuis 30 jours ou plus, tous groupes confondus, morceaux d'abord (leurs
  versions partent avec eux). Pour chaque élément, `TrashPurger` efface dans cet ordre : les
  fichiers du stockage (originaux et MP3 d'aperçu), puis les lignes (versions, fichiers,
  commentaires, aperçus, liens de partage en cascade), puis recalcule la jauge. Interrompue, elle
  recommence le lendemain : effacer un fichier déjà absent ne fait rien.
- **Journal d'audit** (ADR 0026) : `SongDeleted` et `FileDeleted` veulent dire « mis à la
  corbeille » ; s'ajoutent `VersionDeleted`, `SongRestored`, `VersionRestored`, `FileRestored` et
  `SongPurged`, `VersionPurged`, `FilePurged` (sans auteur quand c'est la purge à J+30). Le fil
  d'activité ne change pas.

## Conséquences
- La place se libère à la purge, pas à la suppression : un groupe plein vide sa corbeille (Owner)
  pour envoyer de nouveaux fichiers. La page Corbeille affiche ce que chaque élément occupe.
- Un lien de partage vers une version à la corbeille répond 404, et refonctionne si elle revient.
- Toute nouvelle lecture de morceaux, versions ou fichiers ignore la corbeille par défaut ; une
  requête qui doit la voir passe `IgnoreQueryFilters([SoftDeleteFilter])`, et une tâche hors
  requête HTTP ajoute `BandFilter`.
- Une suppression définitive (Owner ou purge) reste récupérable 14 jours dans les sauvegardes du
  stockage et 7 jours dans celles de la base (ADR 0013), par un administrateur.