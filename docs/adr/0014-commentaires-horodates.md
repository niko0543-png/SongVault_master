# ADR 0014 — Commentaires horodatés : agrégat à part, ouverts aux Guests, gardés sans auteur

- Statut : accepté
- Date : 2026-10-14

## Contexte
Un groupe veut annoter ses maquettes : « à 1:42, la basse décale », avec des réponses, puis
marquer le point comme traité. Les invités (producteur, ingénieur du son) doivent pouvoir
commenter, alors que l'ADR 0008 réserve toute écriture au rôle Member. Un commentaire est aussi
une donnée personnelle de son auteur, qui doit figurer dans son export et survivre ou non à la
suppression de son compte (ADR 0011).

## Décision
- **Agrégat `Comment`** à part, pas une collection de `SongVersion` : un fichier peut recevoir
  des centaines de commentaires écrits par des personnes qui ne modifient pas le morceau.
  Champs : fichier, groupe, auteur, position en millisecondes (facultative), texte de 2 000
  caractères au plus, parent, résolution, dates.
- **Un seul niveau de réponse** : une réponse vise toujours le commentaire d'origine, n'a pas
  de position et ne se résout pas. Le fil se résout et se rouvre en entier.
- **Seuls les fichiers audio** se commentent (422 sinon).
- **Droits** : tout membre, Guest compris, lit, commente et répond (`[MinimumBandRole(Guest)]`
  sur `CommentsController`, exception explicite à l'ADR 0008). Modifier : l'auteur. Supprimer :
  l'auteur ou un Owner ; le commentaire d'origine part avec ses réponses. Résoudre et rouvrir :
  l'auteur du fil, ou un Member ou Owner. Ces règles sont vérifiées dans les handlers.
- **Isolation** : `BandId` copié sur chaque commentaire, et toutes les lectures filtrées sur le
  groupe actif (`CommentRepository`), comme les morceaux.
- **Suppression en cascade par le fichier** : supprimer un fichier, sa version ou son morceau
  efface ses commentaires. Ni `ParentId` ni `BandId` n'ont de cascade : SQL Server refuse
  plusieurs chemins de cascade vers une même table.
- **Compte supprimé** : ses commentaires dans les groupes partagés restent, sans auteur
  (`AuthorId` et `ResolvedById` mis à null par la purge) ; l'interface affiche « Compte
  supprimé ». Ceux des groupes effacés avec le compte disparaissent avec leurs fichiers.
- **Export** : `data.json` contient les commentaires écrits par le compte, avec le groupe, le
  morceau, la version et le fichier, même dans un groupe qu'il a quitté.
- **Affichage** : les commentaires généraux d'abord, puis les autres dans l'ordre de leur
  position. Un clic sur l'horodatage lance la lecture à cette position dans le lecteur global.

## Alternatives écartées
- **Commentaires dans l'agrégat `Song`** : chaque commentaire chargerait et verrouillerait le
  morceau entier (numérotation des versions, ADR 0003) ; un Guest écrirait dans un agrégat
  qu'il ne peut pas modifier.
- **Fils à plusieurs niveaux** : plus difficile à lire sur un téléphone, et inutile pour des
  remarques sur un enregistrement.
- **Effacer les commentaires d'un compte supprimé** : casserait les fils des autres membres,
  dont les réponses n'auraient plus de sens. Un auteur peut supprimer ses commentaires avant
  de supprimer son compte.
- **Mentions `@nom` et fil d'activité dès maintenant** : il n'existe ni nom d'affichage ni
  table d'activité. Ils viendront avec #11 (fil d'activité) et #12 (notifications), qui
  liront les commentaires existants.
- **Marqueurs sur la forme d'onde** : viendront avec #15.

## Conséquences
- Nouvelle table `Comments` (migration `AddComments`), et un nouveau traitement de données
  personnelles dans le registre RGPD.
- Les Guests écrivent désormais quelque chose : `CommentsEndpointsTests` vérifie ce qui leur est permis et refusé.
- La liste n'est pas paginée : quelques dizaines de commentaires par fichier. À revoir si un
  fichier en reçoit des centaines.
- #11 et #12 s'appuieront sur `Comments` (date, auteur, fichier) sans changer ce modèle.