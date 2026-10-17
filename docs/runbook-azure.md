# Exploitation sur Azure

SongVault tourne sur Azure, région France Central ([ADR 0012](adr/0012-stockage-objet-et-hebergement.md)) :

| Ressource | Nom | Rôle |
|---|---|---|
| Container App | `songvault-web` | nginx et le front ; seule adresse publique |
| Container App | `songvault-api` | l'API, au moins une instance (tâches de fond) |
| Job Container Apps | `songvault-migrator` | migrations, lancées par le script de déploiement |
| Azure SQL Database | `SongVault` (Basic) | la base ; restauration à un instant donné sur 7 jours, verrouillée |
| Compte de stockage | `songvault…` | les fichiers, conteneur privé `songvault-files` ; suppression réversible 14 jours, verrouillé |
| Key Vault | `songvault-kv-…` | la clé qui chiffre les clés Data Protection |
| Log Analytics | `songvault-logs` | journaux, 30 jours |

Toutes les commandes se lancent dans PowerShell, à la racine du dépôt, après `az login`. Le groupe de ressources est `songvault-prod`.

## Déployer une version

1. Pousser un tag : le workflow `images` publie les trois images sous ce tag.

```powershell
git switch main
git pull
git tag v1.1.0
git push origin v1.1.0
gh run watch (gh run list --workflow images --limit 1 --json databaseId --jq '.[0].databaseId')
```

2. Déployer ce tag. Le mot de passe SQL est toujours le même : le garder dans un gestionnaire de mots de passe.

```powershell
$env:SONGVAULT_SQL_PASSWORD = Read-Host 'Mot de passe SQL'
.\scripts\deploy-azure.ps1 -ImageTag v1.1.0
```

Le script met à jour l'infrastructure et les images, lance les migrations, puis redémarre l'API. Il affiche l'adresse du site à la fin.

Les réglages e-mail de l'API en place sont gardés d'un déploiement à l'autre, clé Brevo comprise ([ADR 0022](adr/0022-e-mails-en-production-avec-brevo.md)) : voir [E-mails](#e-mails-brevo). Le script le rappelle à l'étape 2/4 et avertit à la fin si aucun e-mail ne part.

Depuis v1.8.0 (ADR 0021), l'API prépare après chaque envoi un aperçu MP3 et une forme d'onde. Au premier démarrage de cette version, la migration crée une tâche par fichier audio déjà présent : elles passent une à une dans les minutes qui suivent (`Préparation de l'aperçu…` dans la page de la version en attendant). Un aperçu en échec se relance depuis cette page, bouton « Réessayer » ; le message d'erreur complet est dans les journaux (`audio.preview`).

## Revenir à la version précédente

Redéployer le tag précédent : `.\scripts\deploy-azure.ps1 -ImageTag v1.0.0`. Les migrations ne sont pas annulées. Une version qui en ajoute une ne se défait donc pas ainsi : restaurer la base ([runbook des sauvegardes](runbook-sauvegardes.md#restaurer-la-base-à-un-instant-donné)).

## Vérifier l'état

```powershell
az containerapp show -g songvault-prod -n songvault-api --query "properties.runningStatus"
az containerapp revision list -g songvault-prod -n songvault-api --query "[].{revision:name, active:properties.active, etat:properties.healthState}" -o table
```

## Lire les journaux

En direct :

```powershell
az containerapp logs show -g songvault-prod -n songvault-api --follow --tail 50
```

Sur les 30 derniers jours, dans le portail Azure : `songvault-logs`, Journaux. Par exemple, les erreurs de l'API depuis une heure :

```kusto
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "songvault-api" and TimeGenerated > ago(1h)
| where Log_s has "\"LogLevel\":\"Error\""
| project TimeGenerated, Log_s
```

## Vérifier que les clés Data Protection sont chiffrées

Après le premier déploiement, chaque clé enregistrée doit contenir un élément `encryptedKey` qui pointe vers Key Vault. Dans le portail Azure, ouvrir la base `SongVault`, puis l'Éditeur de requêtes avec le compte `songvaultadmin` :

```sql
SELECT TOP 1 FriendlyName, CHARINDEX('encryptedKey', Xml) AS Chiffree FROM DataProtectionKeys ORDER BY Id DESC;
```

`Chiffree` doit être supérieur à 0. Si l'API ne démarre pas avec une erreur Key Vault (403), le rôle de son identité n'est pas encore appliqué : relancer le script quelques minutes plus tard.

## E-mails (Brevo)

L'API envoie ses e-mails par Brevo, depuis `no-reply@songvault.eu` ([ADR 0022](adr/0022-e-mails-en-production-avec-brevo.md)). Le domaine est chez OVHcloud, zone DNS comprise, et authentifié chez Brevo. `no-reply@` est un alias de la boîte `contact@songvault.eu` (Zimbra, webmail sur https://webmail.mail.ovh.net/), qui est aussi l'adresse du compte Brevo : ses alertes y arrivent.

Voir le réglage en place :

```powershell
az containerapp show -g songvault-prod -n songvault-api --query "properties.template.containers[0].env[?starts_with(name, 'Email__')].{nom:name, valeur:value}" -o table

```
`Email__Provider` doit valoir `Brevo`. La clé n'apparaît pas : elle est dans le secret `brevo-api-key`.

### Enregistrements DNS

Chez OVHcloud : Web Cloud, « Noms de domaine », `songvault.eu`, onglet « Zone DNS ». Les valeurs exactes sont dans Brevo, « Expéditeurs, domaines et IP dédiées », « Domaines », bouton d'authentification du domaine.

| Rôle | Type | Nom |
|---|---|---|
| Code Brevo (preuve de propriété) | TXT | `@` (sous-domaine vide chez OVHcloud), `brevo-code:…` |
| DKIM 1 (signature) | CNAME | `brevo1._domainkey`, vers `b1.songvault-eu.dkim.brevo.com.` |
| DKIM 2 (signature) | CNAME | `brevo2._domainkey`, vers `b2.songvault-eu.dkim.brevo.com.` |
| DMARC | TXT | `_dmarc`, `v=DMARC1; p=none; rua=mailto:rua@dmarc.brevo.com` |

Les autres entrées de la zone (MX, SPF, DKIM `ovhmo-selector`) servent à la boîte Zimbra : ne pas y toucher.

Vérifier depuis le PC, en interrogeant directement le DNS d'OVHcloud :

```powershell
Resolve-DnsName -Type TXT songvault.eu -Server dns103.ovh.net
Resolve-DnsName -Type TXT _dmarc.songvault.eu -Server dns103.ovh.net
Resolve-DnsName -Type CNAME brevo1._domainkey.songvault.eu -Server dns103.ovh.net
Resolve-DnsName -Type CNAME brevo2._domainkey.songvault.eu -Server dns103.ovh.net

```
Ne pas supprimer ces enregistrements, ni laisser expirer le domaine (renouvellement automatique chez OVHcloud, prochain en octobre 2027) : les e-mails partiraient en indésirables, ou plus du tout.

### Vérifier la clé et l'expéditeur sans déployer

```powershell
$env:BREVO_API_KEY = Read-Host 'Clé Brevo'
.\scripts\test-brevo.ps1 -From no-reply@songvault.eu -To <ton adresse>

```
Le script affiche le compte et les crédits restants, puis envoie un e-mail de test.

### Changer la clé Brevo

Après une fuite, ou une fois par an.

1. Dans Brevo, « SMTP et API », « Clés API » : générer une nouvelle clé, la ranger dans le gestionnaire de mots de passe.
2. Redéployer la version en place avec cette clé :

```powershell
$tag = (az containerapp show -g songvault-prod -n songvault-api --query "properties.template.containers[0].image" -o tsv).Split(':')[-1]
$env:SONGVAULT_SQL_PASSWORD = Read-Host 'Mot de passe SQL'
$env:BREVO_API_KEY = Read-Host 'Nouvelle clé Brevo'
.\scripts\deploy-azure.ps1 -ImageTag $tag

```
3. Se faire envoyer un e-mail (« Mot de passe oublié » sur le site), puis supprimer l'ancienne clé dans Brevo.

### Un e-mail n'arrive pas

1. Dans Brevo, les journaux des e-mails transactionnels : l'e-mail y est-il, et dans quel état (livré, refusé, bloqué) ?
2. Absent chez Brevo : l'API n'a pas pu le confier. Chercher dans `songvault-logs` :

```kql
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "songvault-api" and TimeGenerated > ago(1d)
| where Log_s has "Brevo"
| project TimeGenerated, Log_s

```
- `Brevo a refusé l'e-mail (401)` avec un message sur l'adresse IP : Brevo bloque une nouvelle adresse sortante de l'API. Il envoie aussi un e-mail d'alerte. Autoriser les adresses sortantes actuelles dans Brevo, « Paramètres », « Sécurité », « IP autorisées » :

```powershell
az containerapp show -g songvault-prod -n songvault-api --query "properties.outboundIpAddresses" -o tsv

```
- `Brevo a refusé l'e-mail (401)` sans mention d'IP : la clé est fausse ou supprimée, la changer comme plus haut.
- `(402)` ou un message sur les crédits : la limite de l'offre Brevo est atteinte. Les notifications en échec se voient dans les tâches de fond ; une confirmation d'adresse ou un mot de passe oublié se redemande depuis le site.
- `(400)` sur l'expéditeur : l'adresse `Email__FromAddress` n'est pas un expéditeur validé du domaine authentifié.

Revenir temporairement aux journaux, sans e-mail réel : `.\scripts\deploy-azure.ps1 -ImageTag $tag -EmailProvider Log` (`$tag` comme dans « Changer la clé Brevo »). La clé reste dans le secret ; repasser avec `-EmailProvider Brevo`.

## Paiements (Stripe)

Chaque groupe a une formule ([ADR 0023](adr/0023-abonnements-et-quotas.md)) : Gratuit (2 Go), Groupe (50 Go) ou Studio (250 Go). Les quotas s'appliquent toujours ; le paiement en ligne n'est ouvert qu'avec `Billing__Provider` à `Stripe`. Tant qu'il vaut `None`, la page Facturation dit que le paiement n'est pas ouvert.

Voir le réglage en place :

```powershell
az containerapp show -g songvault-prod -n songvault-api --query "properties.template.containers[0].env[?starts_with(name, 'Billing__')].{nom:name, valeur:value}" -o table

```
Comme pour Brevo, un redéploiement simple garde ces réglages, secrets compris.

### Offrir une formule à un groupe

Sans paiement : avant l'ouverture, pour un groupe de test ou un geste commercial. Dans le portail Azure, base `SongVault`, Éditeur de requêtes (compte `songvaultadmin`, autoriser l'adresse IP du PC quand le portail le propose, retirer la règle après) :

```sql
DECLARE @band uniqueidentifier = (SELECT Id FROM Bands WHERE Name = N'Nom du groupe');
INSERT INTO Subscriptions (BandId, PlanId, Status, CancelAtPeriodEnd, UpdatedAt)
VALUES (@band, 'studio', 'Active', 0, SYSDATETIMEOFFSET());

```
Une ligne existe déjà (ancien abonnement) : `UPDATE Subscriptions SET PlanId = 'studio', Status = 'Active', StripeSubscriptionId = NULL, UpdatedAt = SYSDATETIMEOFFSET() WHERE BandId = @band;` (le client Stripe est gardé). Reprendre la formule : `DELETE FROM Subscriptions WHERE BandId = @band AND StripeSubscriptionId IS NULL;`. Un paiement remplace une formule offerte.

Le stockage d'un groupe se lit dans `SELECT Name, StorageUsedBytes / 1048576 AS Mo FROM Bands ORDER BY StorageUsedBytes DESC;` ; il est recalculé après chaque envoi et chaque nuit à 3 h.

### Ouvrir le paiement

Une fois l'entreprise immatriculée, les CGV et le médiateur complétés dans `web/songvault-web/src/features/legal/publisher.ts`, et le DPA de Stripe archivé ([registre](rgpd/registre.md)).

1. **Compte Stripe activé** (dashboard.stripe.com, mode production) : entreprise, SIRET, IBAN pour les virements, et la double authentification.
2. **Produits** : « Catalogue de produits », créer « SongVault Groupe » avec un prix récurrent mensuel de 6,00 € TTC, et « SongVault Studio » à 15,00 €. Noter les deux identifiants de prix (`price_…`).
3. **Conditions** : « Paramètres », « Informations publiques », adresse des conditions d'utilisation : `https://<adresse du site>/conditions-generales-de-vente`. Sans elle, Checkout refuse la case « J'accepte les CGV ».
4. **Portail client** : « Paramètres », « Facturation », « Portail client » : activer la mise à jour du moyen de paiement, l'historique des factures, l'annulation (à la fin de la période) et le changement de formule entre les deux produits.
5. **Webhook** : « Développeurs », « Webhooks », « Ajouter une destination » : adresse `https://<adresse du site>/api/billing/stripe/webhook`, version d'API `2025-03-31.basil`, événements `checkout.session.completed`, `customer.subscription.created`, `customer.subscription.updated`, `customer.subscription.deleted`, `invoice.payment_failed`. Noter le secret de signature (`whsec_…`).
6. **Clé secrète** : « Développeurs », « Clés API », la clé secrète `sk_live_…`. La ranger dans le gestionnaire de mots de passe avec le secret du webhook ; ne jamais l'envoyer par e-mail ni en capture.
7. Déployer :

```powershell
$tag = (az containerapp show -g songvault-prod -n songvault-api --query "properties.template.containers[0].image" -o tsv).Split(':')[-1]
$env:SONGVAULT_SQL_PASSWORD = Read-Host 'Mot de passe SQL'
$env:STRIPE_SECRET_KEY = Read-Host 'Clé secrète Stripe'
$env:STRIPE_WEBHOOK_SECRET = Read-Host 'Secret du webhook Stripe'
.\scripts\deploy-azure.ps1 -ImageTag $tag -BillingProvider Stripe -StripePriceBand price_… -StripePriceStudio price_…
Remove-Item Env:STRIPE_SECRET_KEY, Env:STRIPE_WEBHOOK_SECRET

```
8. Payer une formule pour un groupe à soi, avec sa propre carte. Vérifier la page Facturation, puis rembourser et résilier depuis le tableau de bord Stripe.

Revenir à `None` : `.\scripts\deploy-azure.ps1 -ImageTag $tag -BillingProvider None`. Les abonnements en cours continuent chez Stripe, mais le webhook n'est plus lu : à ne faire que s'il n'y en a aucun.

### Un groupe a payé mais reste en Gratuit

La formule change à la réception du webhook, pas au retour sur le site.

1. Stripe, « Webhooks », la destination SongVault : les envois en échec sont en rouge, avec la réponse de l'API. `400` : le secret `stripe-webhook-secret` ne correspond pas à celui de la destination ; redéployer avec le bon. `5xx` : voir les journaux de l'API.
2. Une fois corrigé, « Renvoyer » l'événement : chaque événement n'est appliqué qu'une fois, le renvoyer est sans risque.
3. Dans les journaux de l'API : `Événement Stripe evt_… (type) : Applied`, `Duplicate` ou `Ignored`.

### Changer la clé Stripe

Après une fuite, ou une fois par an : « Clés API », « Faire tourner la clé », puis le déploiement de l'étape 7 avec la nouvelle clé seulement (`$env:STRIPE_SECRET_KEY`). Le secret du webhook se change de la même façon, depuis la destination (« Faire tourner le secret »).

## Antivirus (ClamAV)

L'antivirus se règle avec `Antivirus:Provider` ([ADR 0024](adr/0024-analyse-antivirus.md)) :

- `None` (par défaut, avant la commercialisation) : aucun fichier n'est analysé, ils sont lisibles dès leur envoi et marqués « non analysés ». Pas de conteneur `clamav`, rien de plus à payer.
- `ClamAv` : chaque fichier envoyé est analysé par le conteneur `clamav`, à côté de l'API dans la même Container App (environ 15 € par mois). Tant que l'analyse n'a pas dit « sain », le fichier ne s'écoute, ne se télécharge et ne se partage pas.

Le réglage en place est gardé à chaque déploiement ; le script l'affiche à l'étape 2/4.

### Activer l'antivirus (avant la commercialisation)

1. Vérifier que l'image `songvault-clamav` de la version déployée est publique, comme les deux autres (elle est publiée à chaque version depuis la v1.10.0) : sur GitHub, profil, onglet « Packages », `songvault-clamav`, « Package settings », « Change visibility », « Public ». Sans cela, la Container App ne peut pas la télécharger.
2. Redéployer la version en place avec l'antivirus :

```powershell
$tag = (az containerapp show -g songvault-prod -n songvault-api --query "properties.template.containers[0].image" -o tsv).Split(':')[-1]
$env:SONGVAULT_SQL_PASSWORD = Read-Host 'Mot de passe SQL'
.\scripts\deploy-azure.ps1 -ImageTag $tag -AntivirusProvider ClamAv

```
3. Au démarrage, l'API remet en analyse tous les fichiers envoyés sans antivirus (journal « Antivirus activé : N fichiers non analysés remis en analyse »). Ils sont illisibles le temps de leur analyse ; compter une à deux minutes de chargement des signatures, puis quelques secondes par fichier (une à deux minutes pour un WAV de 2 Go). Suivre l'avancement avec la première requête de « Des fichiers restent en cours d'analyse ».

Revenir à `None` (`-AntivirusProvider None`) retire le conteneur. Les fichiers déjà analysés gardent leur verdict ; un fichier bloqué le reste.

### Journaux de ClamAV

Ses journaux (chargement des signatures, mises à jour de freshclam, menaces trouvées) :

```powershell
az containerapp logs show -g songvault-prod -n songvault-api --container clamav --tail 50

```
### Des fichiers restent « en cours d'analyse »

Dans l'Éditeur de requêtes de la base (voir « Offrir une formule à un groupe ») :

```sql
SELECT s.Status, COUNT(*) AS Fichiers, MIN(s.RequestedAt) AS PlusAncien FROM FileScans s GROUP BY s.Status;
SELECT TOP 20 Type, Status, Attempts, RunAfter, Error FROM BackgroundJobs WHERE Type = N'file.scan' ORDER BY CreatedAt DESC;

```
`Error` à `MalwareScannerException : ClamAV injoignable` : le conteneur `clamav` ne répond pas. Lire ses journaux ; s'il redémarre en boucle faute de mémoire (`OOMKilled` dans `az containerapp revision show`), passer sa mémoire à `2Gi` et son processeur à `1.0` dans `infra/main.bicep`, puis redéployer. Une fois réparé, les analyses en `Failed` se relancent depuis la version (« Relancer l'analyse »), ou toutes d'un coup :

```sql
UPDATE FileScans SET Status = N'Pending', Error = NULL, CompletedAt = NULL, RequestedAt = SYSDATETIMEOFFSET() WHERE Status = N'Failed';
INSERT INTO BackgroundJobs (Id, Type, Payload, Status, Attempts, CreatedAt, RunAfter)
SELECT NEWID(), N'file.scan', LOWER(CONVERT(nvarchar(36), SongFileId)), N'Pending', 0, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()
FROM FileScans WHERE Status = N'Pending' AND CompletedAt IS NULL
  AND NOT EXISTS (SELECT 1 FROM BackgroundJobs j WHERE j.Type = N'file.scan' AND j.Status IN (N'Pending', N'Running')
                  AND j.Payload = LOWER(CONVERT(nvarchar(36), FileScans.SongFileId)));

```
### Un fichier est bloqué à tort

ClamAV se trompe rarement, mais cela arrive (faux positif). Le fichier reste à sa place dans le stockage : le débloquer, c'est changer son statut. Vérifier d'abord le nom de la menace et le fichier avec la personne qui l'a envoyé.

```sql
SELECT f.Id, f.OriginalFileName, s.Threat, s.CompletedAt
FROM FileScans s JOIN SongFiles f ON f.Id = s.SongFileId WHERE s.Status = N'Infected' ORDER BY s.CompletedAt DESC;

DECLARE @file uniqueidentifier = '<Id du fichier>';
UPDATE FileScans SET Status = N'Clean', Threat = NULL WHERE SongFileId = @file AND Status = N'Infected';
-- Fichier audio : son aperçu, passé en échec au blocage, se relance depuis la version (« Réessayer »)

```
## Changer la clé du compte de stockage

C'est le seul moyen d'invalider d'un coup toutes les URL signées en cours, par exemple après une fuite. Les envois en cours échouent, il suffit de les recommencer.

```powershell
$account = az storage account list -g songvault-prod --query "[0].name" -o tsv
az storage account keys renew -g songvault-prod -n $account --key primary --output none
.\scripts\deploy-azure.ps1 -ImageTag v1.1.0
```

Le déploiement relit la clé et met à jour le secret de l'API.

## Coût et arrêt complet

Pour un usage faible, compter de l'ordre de 10 à 20 € par mois, et environ 15 € de plus une fois l'antivirus activé (#17). L'essentiel vient de l'API toujours active, de la base Basic et, s'il est activé, du conteneur antivirus ; le stockage et Key Vault coûtent quelques centimes. Vérifier avec la [calculatrice de prix Azure](https://azure.microsoft.com/fr-fr/pricing/calculator/) et poser une alerte de budget dans « Gestion des coûts ».

Tout supprimer, données comprises (sans retour possible). Les verrous de la base et du stockage bloquent la suppression : les retirer d'abord.

```powershell
az lock list -g songvault-prod -o table
az lock delete --ids (az lock list -g songvault-prod --query "[].id" -o tsv)
az group delete --name songvault-prod
```

Key Vault reste 7 jours en suppression douce. Un nouveau déploiement dans un groupe du même nom pendant ces 7 jours échoue : `az keyvault purge --name <nom>` libère le nom.