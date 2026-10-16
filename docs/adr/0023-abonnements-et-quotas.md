# ADR 0023 — Abonnements et quotas : formules en base, abonnement porté par le groupe, Stripe sans SDK

- Statut : accepté
- Date : 2026-10-16

## Contexte
La version commerciale fait payer la place. Aujourd'hui rien ne limite les fichiers d'un
groupe (2 Go par fichier depuis l'ADR 0012, sans total), ni le nombre de groupes d'un compte.
Il faut des formules, une limite vérifiée par l'API, et un paiement mensuel.

Le paiement en ligne demande un statut d'entreprise (SIRET), des CGV et un médiateur de la
consommation : il ne sera pas ouvert tout de suite. Les formules et les quotas, eux, doivent
s'appliquer dès cette version, pour que l'usage gratuit reste borné.

## Décision
- **Trois formules dans une table `Plans`**, créées par la migration : Gratuit (2 Go, un seul
  groupe dont on est propriétaire sans abonnement), Groupe (50 Go, 6 € par mois) et Studio
  (250 Go, 15 € par mois). Les limites vivent en base : les changer, c'est une migration ou un
  `UPDATE`, pas une nouvelle version. Le prix affiché doit rester celui du prix Stripe.
- **L'abonnement est porté par le groupe**, pas par le compte : c'est le groupe qui remplit le
  stockage, et un propriétaire peut payer pour un groupe et pas pour un autre. Une ligne
  `Subscriptions` par groupe au plus ; pas de ligne : formule gratuite.
- **Formule effective** : celle de l'abonnement s'il est `Active`, `Trialing` ou `PastDue`
  (Stripe réessaie la carte), sinon la gratuite. Un abonnement résilié reste en base pour
  garder le client Stripe (mêmes factures, même carte au prochain paiement).
- **Formule offerte** : une ligne sans identifiants Stripe, ajoutée en SQL (runbook). Elle sert
  avant l'ouverture du paiement, et aux tests. Un paiement la remplace.
- **Stockage compté sur les originaux** : `Bands.StorageUsedBytes`, recalculé par un seul
  `UPDATE … SUM` après chaque envoi ou suppression, et pour tous les groupes chaque nuit à
  3 h (heure de Paris), tâche `billing.storage-recount`. Aperçus MP3, pochettes et archives
  d'export ne comptent pas : ils se recréent, ou disparaissent seuls.
- **Quota vérifié à trois endroits** : à l'annonce d'un envoi direct, à sa confirmation (deux
  envois annoncés en même temps : le second est refusé et son fichier effacé du stockage), et
  à l'envoi classique. Refus en **402** avec les tailles en clair.
- **Rien n'est jamais effacé** quand un groupe dépasse son quota (formule résiliée ou
  descendue) : les fichiers restent lisibles, les nouveaux envois sont refusés.
- **Un second groupe gratuit est refusé (402)** : un compte peut être propriétaire d'autant de
  groupes payants qu'il veut, et d'un seul groupe gratuit. Être membre n'a pas de limite.
- **Stripe, par `HttpClient`**, sans le SDK Stripe.net : quatre appels (Checkout, portail
  client, lecture et résiliation d'un abonnement) et la vérification de signature du webhook
  (HMAC-SHA256, 5 minutes de tolérance), en encodage formulaire. Version d'API fixée
  (`Stripe-Version: 2025-03-31.basil`).
- **Stripe Checkout et portail client** : SongVault n'affiche jamais de formulaire de carte.
  Le portail gère carte, factures, changement de formule et résiliation. La case « J'accepte
  les CGV » est demandée par Checkout (`consent_collection`).
- **Le webhook fait foi**, pas le retour sur le site. À chaque événement, l'abonnement est
  relu chez Stripe : l'ordre d'arrivée ne compte pas. Chaque événement est noté
  (`ProcessedStripeEvents`) et ne passe qu'une fois. Événements écoutés :
  `checkout.session.completed`, `customer.subscription.created`, `.updated`, `.deleted` et
  `invoice.payment_failed`, qui prévient aussi les propriétaires par e-mail.
- **`Billing:Provider`** : `None` (défaut) ou `Stripe`. Avec `None`, formules et quotas
  s'appliquent, la page Facturation dit que le paiement n'est pas ouvert, et Checkout répond
  503. La production reste sur `None` jusqu'au SIRET.
- **Suppression de compte** : refusée (409) tant qu'un groupe dont on est seul membre a un
  abonnement en cours non résilié ; la purge à 30 jours résilie chez Stripe ce qui resterait.
- **Secrets** : clé secrète et secret du webhook en user-secrets sur le PC, en secrets du
  Container App en production, comme la clé Brevo (ADR 0022). Les identifiants de prix ne
  sont pas secrets.

## Alternatives écartées
- **Abonnement par compte** : plus simple à facturer, mais un compte membre de cinq groupes
  ne sait pas lequel il paie, et un groupe change de propriétaire.
- **Limites dans le code** (constantes, `appsettings`) : un changement de prix serait une
  nouvelle version ; et la page publique devrait recopier les chiffres.
- **Somme calculée à chaque envoi** : exacte, mais une requête de plus en plus lourde pour les
  gros groupes, et inutilisable pour afficher la jauge sur la liste des morceaux.
- **Compteur incrémenté (+ taille, − taille)** : rapide, mais dérive au premier envoi
  interrompu ; le recalcul par `SUM` est aussi rapide avec l'index sur les fichiers.
- **SDK Stripe.net** : typé et à jour, mais une dépendance de plus pour quatre appels, et des
  mises à jour majeures fréquentes.
- **Stripe Elements (formulaire de carte dans la page)** : plus intégré, mais SongVault
  entrerait dans le périmètre PCI-DSS (SAQ A-EP) et devrait gérer 3-D Secure.
- **Effacer les fichiers au-delà du quota** : rendrait la place, mais une carte expirée ferait
  perdre des enregistrements. Inacceptable.
- **Paddle ou Lemon Squeezy (vendeur de référence, TVA gérée)** : plus de frais par paiement,
  et inutile tant que les ventes restent en France sous la franchise de TVA.

## Conséquences
- Les envois et les suppressions font une requête SQL de plus (le recalcul).
- Les deux formules payantes doivent exister dans Stripe, avec leurs prix, et leurs
  identifiants dans `Billing:Stripe:Prices` ; le runbook décrit la mise en place.
- Stripe Tax (calcul de la TVA) est désactivé (`AutomaticTax=false`) : à activer, payant, si
  l'entreprise sort de la franchise en base ou vend hors de France.
- Les CGV et le médiateur sont à compléter (`publisher.ts`) et à faire relire avant
  d'ouvrir le paiement.
- Résilier se fait par le portail Stripe, depuis la page Facturation (« Gérer
  l'abonnement », puis « Annuler ») : la fonction de résiliation exigée depuis 2023 pour les
  abonnements en ligne est donc accessible en quelques clics, à vérifier lors de la relecture
  des CGV.