# ADR 0009 — Invitations par lien à usage unique et envoi d'e-mails

- Statut : accepté
- Date : 2026-10-09

## Contexte
Depuis l'ADR 0008, un groupe a des rôles, mais personne ne peut y entrer sans
intervention en base. Il faut inviter une personne par son adresse, qu'elle ait déjà un
compte ou non. C'est aussi le premier e-mail envoyé par SongVault ; la confirmation
d'adresse (#4) et les notifications (#12) suivront.

## Décision
- **Une invitation est un agrégat à part** (`Invitation`) : groupe, adresse normalisée,
  rôle (Member ou Guest), empreinte du jeton, auteur, dates de création, d'expiration
  (7 jours), d'acceptation et d'annulation.
- **Jeton** : 32 octets aléatoires (`RandomNumberGenerator`), envoyés en Base64Url dans le
  lien `/invite/{jeton}`. Seule l'empreinte SHA-256 est stockée (`binary(32)`, index
  unique) : une fuite de la base ne donne aucun lien utilisable.
- **Le compte qui accepte doit avoir l'adresse invitée** (comparaison sans casse). Sinon 403.
- **Usage unique** : `Invitation.Accept` et `Band.AddMember` sont enregistrés par un seul
  `SaveChanges` (une transaction) ; un `RowVersion` sur l'invitation fait échouer une
  seconde acceptation simultanée (409). Utilisée, annulée ou expirée : 410.
- **Aperçu sans connexion** : `GET /api/invitations/{jeton}` montre le nom du groupe,
  l'adresse et le rôle, pour que la personne sache quoi faire avant de créer un compte.
  Limité en débit comme la connexion.
- **E-mails derrière `IEmailSender`** (Application). Deux implémentations choisies par
  `Email:Provider` : `Log` (défaut, écrit l'e-mail dans les journaux) et `Brevo` (API HTTP
  v3, société française, palier gratuit). L'envoi se fait pendant la requête.
- **L'Owner reçoit le lien une fois**, dans la réponse de création, pour le partager
  lui-même si l'e-mail n'arrive pas.

## Alternatives écartées
- **Jeton stocké en clair** : plus simple à déboguer, mais la base devient un trousseau
  de liens valides.
- **Tout compte qui détient le lien peut l'utiliser** : pratique pour une adresse
  différente, mais un lien transféré fait entrer n'importe qui avec le rôle prévu.
- **UPDATE conditionnel (`ExecuteUpdate` sur `AcceptedAt IS NULL`)** : même garantie, mais
  il s'exécute hors de `SaveChanges` et impose une transaction explicite ; le `RowVersion`
  suit le mécanisme déjà utilisé par `Song` (ADR 0003).
- **SMTP direct** : un port de plus à ouvrir et une délivrabilité à gérer soi-même.
- **Envoi en arrière-plan dès maintenant** : utile pour #12, prématuré pour un e-mail
  déclenché à la main par un Owner.

## Conséquences
- Avec `Email:Provider=Log`, les liens d'invitation apparaissent dans les journaux : à ne
  garder qu'en développement. En production, configurer `Brevo` et sa clé.
- Une invitation pour une adresse sans compte ne crée pas le compte : la personne
  s'inscrit elle-même, puis accepte (la page `/invite` enchaîne les deux).
- Tant que #4 n'est pas là, l'adresse d'un compte n'est pas confirmée ; le contrôle
  d'adresse repose donc sur le fait que seul le destinataire a reçu le lien.
- Un nouvel e-mail transactionnel réutilise `IEmailSender` ; #12 ajoutera une file
  d'envoi derrière la même interface.