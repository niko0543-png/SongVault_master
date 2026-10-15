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

Pour envoyer de vrais e-mails, ajouter `-EmailProvider Brevo -EmailFrom no-reply@ton-domaine.fr`, avec `$env:BREVO_API_KEY` défini.

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

## Changer la clé du compte de stockage

C'est le seul moyen d'invalider d'un coup toutes les URL signées en cours, par exemple après une fuite. Les envois en cours échouent, il suffit de les recommencer.

```powershell
$account = az storage account list -g songvault-prod --query "[0].name" -o tsv
az storage account keys renew -g songvault-prod -n $account --key primary --output none
.\scripts\deploy-azure.ps1 -ImageTag v1.1.0
```

Le déploiement relit la clé et met à jour le secret de l'API.

## Coût et arrêt complet

Pour un usage faible, compter de l'ordre de 10 à 20 € par mois. L'essentiel vient de l'API toujours active et de la base Basic ; le stockage et Key Vault coûtent quelques centimes. Vérifier avec la [calculatrice de prix Azure](https://azure.microsoft.com/fr-fr/pricing/calculator/) et poser une alerte de budget dans « Gestion des coûts ».

Tout supprimer, données comprises (sans retour possible). Les verrous de la base et du stockage bloquent la suppression : les retirer d'abord.

```powershell
az lock list -g songvault-prod -o table
az lock delete --ids (az lock list -g songvault-prod --query "[].id" -o tsv)
az group delete --name songvault-prod
```

Key Vault reste 7 jours en suppression douce. Un nouveau déploiement dans un groupe du même nom pendant ces 7 jours échoue : `az keyvault purge --name <nom>` libère le nom.