# ADR 0016 — Sorties et pistes : agrégat Release, liste complète en deux passes

- Statut : accepté
- Date : 2026-10-14

## Contexte
Un groupe publie ses morceaux sur des sorties : un single, un EP, un album. Un même morceau
passe souvent d'un single à l'album, et pas forcément dans la même version (mix radio sur le
single, master final sur l'album). Il faut garder l'ordre des pistes, pouvoir le changer, et
que la base refuse deux pistes au même numéro (#10). Les setlists (#21) réutiliseront la même
liste ordonnée.

## Décision
- **Agrégat `Release`** (titre, type `Single`/`EP`/`Album`, date facultative, pochette) qui
  possède ses `ReleaseTrack` (SongId, VersionId, TrackNumber). Un dépôt, `IReleaseRepository`
  (ADR 0006). Les sorties appartiennent au groupe comme les morceaux : filtre `BandFilter`,
  lecture Guest, écriture Member (ADR 0008).
- **Clé `(ReleaseId, SongId)`** : un morceau figure au plus une fois sur une sortie, mais sur
  autant de sorties qu'on veut. **Index unique `(ReleaseId, TrackNumber)`**.
- **La version est choisie piste par piste** et doit appartenir au morceau, dans le groupe de
  la sortie. Vérifié par le handler, qui lit les versions à travers le groupe actif.
- **Une seule écriture des pistes : `PUT …/releases/{id}/tracks` avec la liste complète,
  dans l'ordre.** Ajouter, retirer, réordonner et changer de version passent tous par là.
  L'enregistrement se fait **en deux passes dans une transaction** : les numéros existants
  passent d'abord en négatif, puis prennent leur place finale. SQL Server vérifie l'index
  unique ligne par ligne : échanger les pistes 1 et 2 en une seule passe le violerait.
- **Suppression** : supprimer une sortie garde les morceaux. Supprimer un morceau présent sur
  une sortie est refusé (409, avec le titre des sorties) : une piste ne disparaît pas en
  silence. Les clés étrangères des pistes vers `Songs` et `SongVersions` sont `NO ACTION`.
- **Pochette** : JPEG, PNG ou WebP de 5 Mo au plus, envoyée à travers l'API et vérifiée par sa
  signature comme les fichiers des versions ; rangée dans le même stockage (ADR 0002, 0012).
  Clé neuve à chaque envoi, l'ancienne est effacée après l'enregistrement.

## Alternatives écartées
- **Endpoints « déplacer la piste n vers m »** : moins de données envoyées, mais deux
  personnes qui réordonnent en même temps obtiennent un mélange des deux. Avec la liste
  complète, une écriture passe en entier et le `RowVersion` de la sortie renvoie un 409 à
  l'autre.
- **Numéros espacés (10, 20, 30) ou fractionnaires** : évitent la seconde passe, mais le
  numéro stocké n'est plus le numéro affiché et il faut renuméroter de temps en temps.
- **Pas d'index unique, ordre garanti par le code seul** : le critère de la tâche demande que
  la base le garantisse.
- **Clé `(ReleaseId, TrackNumber)` et un morceau plusieurs fois sur la même sortie** (version
  acoustique en bonus) : rare, et le choix de version par piste couvre la plupart des cas. À
  reprendre si un groupe le demande.
- **Envoi direct de la pochette vers le stockage (ADR 0012)** : inutile pour 5 Mo.

## Conséquences
- `IUnitOfWork` gagne `ExecuteInTransactionAsync`.
- Pour supprimer un morceau, on le retire d'abord de ses sorties.
- La purge d'un compte efface les sorties de ses groupes personnels, et leurs pochettes, avant
  les morceaux. `scripts/purge-orphan-files.ps1` compte les pochettes parmi les fichiers
  référencés.
- Les sorties ne figurent pas dans l'export des données d'un compte : ce sont des données du
  groupe, sans donnée personnelle.
- `ReorderableList.vue`, côté front, sert aussi aux setlists (#21).