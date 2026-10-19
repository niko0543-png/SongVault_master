# ADR 0029 — Crédits et répartition des droits : règle des 100 % dans le domaine, ISWC sur le morceau, ISRC sur la version, CSV écrit sans bibliothèque

- Statut : accepté
- Date : 2026-10-19

## Contexte
Le backlog (#22) demande les crédits d'un morceau : chaque ayant droit avec un nom, un rôle
(auteur, compositeur, arrangeur, éditeur) et une part des droits en `decimal(5,2)`. Le morceau
(l'œuvre) porte un ISWC ; l'enregistrement porte un ISRC, sur la version ou la piste de sortie.
Les formats sont vérifiés. La règle des 100 % est vérifiée dans le domaine quand on enregistre
l'ensemble des crédits. Un export CSV doit s'ouvrir dans Excel en français (point-virgule,
UTF-8 avec BOM). Le front montre le total en direct et une erreur s'il ne fait pas 100 %.
Tests demandés : un total à 99 % refusé, un export lisible. Le kit de sortie (#24) reprendra
l'export des crédits d'une sortie.

## Décision
- **Les crédits sont dans l'agrégat `Song`** (table `Credits` : `SongId`, `Position`, `Name`,
  `Role` en texte, `SharePercent` en `decimal(5,2)`, contrainte `> 0 AND <= 100`, suppression en
  cascade avec le morceau). Ils ne se modifient qu'ensemble, par `Song.SetRights(iswc, crédits)` :
  la liste reçue remplace l'ancienne, dans son ordre.
- **Règle des 100 %** dans `Song.SetRights`, tout vérifié avant de toucher au morceau. Une liste
  vide est permise (droits pas encore renseignés) ; sinon les parts font exactement 100,00. Chaque
  part va de 0,01 à 100 avec deux décimales au plus ; chaque nom est requis (200 caractères) ; le
  même nom deux fois avec le même rôle est refusé (il faut regrouper ses parts) ; au plus 30
  crédits. Le même nom avec deux rôles est permis (auteur et compositeur). Un refus répond 422
  avec le total atteint (« il fait 99 % »).
- **ISWC sur le morceau** (`Songs.Iswc`, `nvarchar(15)`) : `T`, neuf chiffres et une clé de
  contrôle, gardé sous la forme écrite `T-034.524.680-1`. La clé est vérifiée (1 + somme des
  chiffres pondérés par leur rang, complément à 10). La saisie accepte espaces, points, tirets et
  minuscules.
- **ISRC sur la version** (`SongVersions.Isrc`, `nvarchar(12)`) : pays (2 lettres), déclarant
  (3 lettres ou chiffres), année (2 chiffres), numéro (5 chiffres). Gardé en 12 caractères,
  affiché `FR-Z03-26-00001`. **Une piste de sortie reprend l'ISRC de la version choisie** : une
  sortie (ADR 0016) désigne déjà une version précise, et le même enregistrement sur un single
  puis un album garde le même ISRC. Il n'y a donc pas de second champ sur la piste. L'unicité d'un
  ISRC entre versions n'est pas vérifiée : c'est le déclarant qui les attribue.
- **Endpoints** (`[BandScoped]` : lecture et export pour tous les membres, Guest compris ;
  écriture pour Member et Owner, ADR 0008) :
  `GET` et `PUT /api/bands/{id}/songs/{songId}/rights` (ISWC, crédits, total, ISRC des versions),
  `PUT …/songs/{songId}/versions/{versionId}/isrc`, `GET …/songs/{songId}/rights.csv` et
  `GET /api/bands/{id}/releases/{releaseId}/credits.csv`. Le détail d'un morceau et d'une version
  ne change pas.
- **Journal d'audit** (ADR 0026) : chaque enregistrement des crédits écrit `CreditsChanged`, avec
  le titre du morceau et un résumé (« 3 crédits, ISWC T-034.524.680-1 ») : la répartition des
  droits est une donnée sensible pour un groupe. L'ISRC n'y est pas écrit.
- **Export CSV écrit à la main, sans CsvHelper** (`CreditsCsv`) : point-virgule, UTF-8 avec BOM,
  fins de ligne CRLF, parts à virgule sans zéros inutiles (`33,33`, `12,5`, `100`), rôles en
  français. Une cellule avec `;`, `"` ou un retour à la ligne passe entre guillemets, les
  guillemets doublés. Une cellule qui commence par `=`, `+`, `-` ou `@` reçoit une apostrophe
  devant : Excel ne l'exécute pas comme une formule. Un morceau sans crédit garde une ligne pour
  son titre et son ISWC. Ces règles tiennent en une quarantaine de lignes testées ; un paquet
  serait une dépendance de plus pour deux fichiers en écriture seule.
- **Front** : section « Crédits et codes » sur la page d'un morceau. Champ ISWC, éditeur des
  ayants droit (nom, rôle, part saisie avec virgule ou point), total en direct calculé en
  centièmes (33,33 + 33,33 + 33,34 fait bien 100), message d'erreur et bouton « Enregistrer »
  désactivé tant que la règle n'est pas respectée ; « Ajouter un ayant droit » propose ce qui
  manque pour 100 %, « Répartir à parts égales » donne le reste à la dernière ligne. ISRC de
  chaque version, enregistré ligne par ligne. Lien « Exporter en CSV » sur le morceau, et
  « Exporter les crédits (CSV) » sur une sortie. Un Guest voit un tableau, sans champ.
- **Données personnelles** : les crédits nomment des personnes qui ne sont pas forcément
  inscrites. Le registre des traitements (section 2) le dit. Les crédits ne sont pas dans
  l'export du compte (ADR 0011) : ce sont des données du groupe, que chaque membre exporte en CSV.

## Conséquences
- Changer une part oblige à revoir l'ensemble : on ne peut pas enregistrer un état
  intermédiaire à 90 %. Une liste vide reste possible pour commencer.
- Les crédits partent avec le morceau à la corbeille et en reviennent avec lui ; la purge les
  efface par la cascade de la base.
- Le CSV d'une sortie a une ligne par ayant droit de chaque piste ; le kit de sortie (#24) le
  reprendra tel quel.
- Une piste qui devrait avoir un ISRC distinct de sa version (remaster) demande une nouvelle
  version du morceau.