# ADR 0046 — Setlists : morceaux existants à cocher et commentaires entre les morceaux

- Statut : accepté
- Date : 2026-10-23

## Contexte
Pour remplir une setlist (ADR 0028), il fallait taper une recherche, cliquer « Chercher », puis
ajouter les morceaux un par un parmi dix résultats. Rien ne permettait non plus de noter ce qui se
passe entre deux morceaux sur scène (présenter le groupe, changer de guitare, laisser un silence) :
les notes de scène d'une ligne parlent du morceau lui-même (« capo 2 », « intro rallongée »).

## Décision
- **Morceaux à cocher** : sous la setlist, la liste des morceaux du groupe s'affiche d'emblée
  (50 par page, le maximum de l'API, avec « Plus de morceaux »), filtrée pendant la frappe par
  titre ou artiste. On coche, puis « Ajouter les N morceaux » les place en fin de setlist, dans
  l'ordre des cases cochées, joués dans leur dernière version. Les morceaux déjà dans la setlist
  sont cochés et grisés ; on ne coche pas au-delà des places libres (50 lignes au plus).
- La liste montre **tous** les morceaux, y compris ceux de l'onglet Sorties (ADR 0044), marqués
  « sorti » : on joue ses morceaux sortis en concert. Le filtre `released=false` reste propre à la
  liste des morceaux.
- **Commentaire avant un morceau** : un champ `CommentBefore` (500 caractères) sur chaque ligne,
  affiché entre la ligne précédente et celle-ci, en italique ; celui de la première ligne sert
  d'ouverture. Il suit le morceau quand on le déplace, comme ses notes. « + Commentaire » ouvre le
  champ ; le vider le retire. Il part dans le même `PUT …/setlists/{id}/items` que le reste.
- **Feuille imprimée** : chaque commentaire y prend une ligne plus petite avant son morceau ; deux
  commentaires comptent comme un morceau pour choisir la taille du texte.

## Alternatives écartées
- **Lignes « intermède » libres dans la liste** : la clé (SetlistId, SongId) des lignes devrait
  changer, avec la vérification des morceaux et le refus de mise à la corbeille (409) qui en
  dépendent. Un commentaire rattaché au morceau suivant couvre le besoin sans toucher à la clé.
- **Commentaire après un morceau** : il faudrait un champ à part pour ouvrir le concert ; un
  commentaire « avant » couvre l'ouverture et tous les intervalles.

## Conséquences
- Migration `AddSetlistComments` : une colonne `CommentBefore nvarchar(500) NULL` sur `SetlistItems`.
- `commentBefore` facultatif dans la requête : un client qui ne l'envoie pas efface le commentaire,
  comme pour les notes (la liste envoyée remplace tout).
- Pas de commentaire après la dernière ligne : pour un rappel, on le note avant le morceau du rappel.
