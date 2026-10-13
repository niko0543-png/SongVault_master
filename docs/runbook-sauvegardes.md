# Sauvegardes et reprise sur Azure

Ce que SongVault sauvegarde en production, comment restaurer, et l'exercice trimestriel qui le prouve ([ADR 0013](adr/0013-sauvegardes-et-reprise.md)). Pour Docker sur un PC, voir le [runbook Docker](runbook-docker.md#sauvegarder--restaurer).

Toutes les commandes se lancent dans PowerShell, à la racine du dépôt, après `az login`.

## Objectifs

| | Objectif | Ce que permet l'infrastructure |
|---|---|---|
| **RPO** : perte de données acceptable | 1 heure | Base : 10 minutes environ (sauvegarde du journal toutes les 5 à 10 minutes). Fichiers : aucune perte pour un fichier supprimé depuis moins de 14 jours. |
| **RTO** : durée pour revenir en service | 4 heures | Mesurée à chaque exercice, voir le tableau en fin de page. |

Ces objectifs couvrent les erreurs (suppression ou modification de données, bogue, mauvaise manipulation) et la suppression d'une ressource par erreur. Une panne complète de la région France Central n'est pas couverte : les sauvegardes restent dans la région (ADR 0013).

## Ce qui est sauvegardé

| Quoi | Comment | Rétention |
|---|---|---|
| Base `SongVault` | Sauvegardes automatiques d'Azure SQL, restauration à n'importe quel instant | 7 jours |
| Fichiers (`songvault-files`) | Suppression réversible des fichiers et du conteneur | 14 jours |
| Base et compte de stockage | Verrou `songvault-ne-pas-supprimer` : leur suppression échoue tant qu'il est là | permanent |
| Clé Key Vault | Suppression réversible du coffre | 7 jours |
| Configuration | `infra/main.bicep` et les images sur GitHub : tout se redéploie | dans le dépôt |

Une donnée supprimée par un utilisateur reste donc au plus 7 jours dans les sauvegardes de la base et 14 jours pour ses fichiers ; c'est ce qu'indiquent le registre RGPD et la politique de confidentialité.

## Restaurer la base à un instant donné

Pour revenir sur une erreur dans les données (suppression massive, migration ratée). La base actuelle est conservée à part pour l'analyse.

1. Choisir l'instant, **en UTC**, juste avant l'incident. Le plus ancien possible :

```powershell
$server = az sql server list -g songvault-prod --query "[0].name" -o tsv
az sql db show -g songvault-prod -s $server -n SongVault --query earliestRestoreDate -o tsv
$t = '2026-10-20T14:00:00'
```

2. Restaurer sous un autre nom. Compter de quelques minutes à une heure :

```powershell
az sql db restore -g songvault-prod -s $server -n SongVault --dest-name SongVault-restauree --time $t --service-objective Basic --backup-storage-redundancy Local
```

3. Couper l'API, pour que plus rien n'écrive dans l'ancienne base :

```powershell
$revision = az containerapp revision list -g songvault-prod -n songvault-api --query "[?properties.active].name | [0]" -o tsv
az containerapp revision deactivate -g songvault-prod -n songvault-api --revision $revision
```

4. Retirer le verrou de la base, puis échanger les deux bases par leur nom :

```powershell
az lock delete --ids (az lock list -g songvault-prod --query "[?contains(id, '/databases/SongVault/')].id" -o tsv)
az sql db rename -g songvault-prod -s $server -n SongVault --new-name "SongVault-incident-$(Get-Date -Format yyyyMMdd)"
az sql db rename -g songvault-prod -s $server -n SongVault-restauree --new-name SongVault
```

5. Récupérer les fichiers supprimés depuis cet instant, pour que la base restaurée les retrouve tous :

```powershell
.\scripts\undelete-files-azure.ps1 -Since "$($t)Z"
```

6. Réactiver l'API, puis redéployer la version en cours : le déploiement remet le verrou sur la nouvelle base et redémarre l'API.

```powershell
az containerapp revision activate -g songvault-prod -n songvault-api --revision $revision
$env:SONGVAULT_SQL_PASSWORD = Read-Host 'Mot de passe SQL'
.\scripts\deploy-azure.ps1 -ImageTag v1.1.0
```

7. **Demandes de suppression de compte.** Celles faites avant l'instant choisi se relancent seules : leur tâche `account.purge` est dans la base restaurée. Celles faites après sont perdues. Les retrouver dans l'ancienne base (`SongVault-incident-…`), dans l'Éditeur de requêtes du portail, puis les traiter comme une demande au support :

```sql
SELECT Payload AS UserId, CreatedAt FROM BackgroundJobs WHERE Type = 'account.purge' AND CreatedAt > '2026-10-20T14:00:00+00:00';
```

8. Supprimer l'ancienne base une fois l'incident analysé (elle est facturée) :

```powershell
az sql db delete -g songvault-prod -s $server -n SongVault-incident-20261020
```

L'Éditeur de requêtes demande d'autoriser l'adresse IP du PC dans le pare-feu du serveur : le portail le propose. Retirer cette règle après usage.

## Récupérer des fichiers supprimés

Un fichier supprimé, par l'application ou à la main, reste récupérable 14 jours. Le script liste ceux supprimés depuis la date donnée, demande confirmation, puis les récupère :

```powershell
.\scripts\undelete-files-azure.ps1 -Since '2026-10-20T14:00:00Z'
```

Un fichier récupéré sans sa ligne en base n'apparaît pas dans l'application : c'est le cas quand la suppression venait de l'application elle-même. Il faut alors aussi restaurer la base (section précédente).

## Une ressource a été supprimée par erreur

- **Base ou compte de stockage** : le verrou bloque la suppression. Pour une suppression voulue, le retirer d'abord (`az lock delete`, voir « Tout supprimer » dans le [runbook Azure](runbook-azure.md#coût-et-arrêt-complet)).
- **Compte de stockage supprimé malgré tout** : il se récupère pendant 14 jours depuis le portail (Comptes de stockage, « Restaurer un compte supprimé »), tant qu'aucun compte du même nom n'a été recréé.
- **Key Vault supprimé** : `az keyvault recover --name <nom>` pendant 7 jours. Sans lui, l'API ne relit plus ses clés Data Protection : les sessions et les liens en cours deviennent invalides, les comptes et les données ne sont pas touchés.
- **Container Apps, environnement, journaux** : `.\scripts\deploy-azure.ps1` les recrée.

## Exercice de restauration

Chaque trimestre, le workflow `restore-drill` ouvre une issue de rappel. L'exercice recrée SongVault dans un groupe de ressources vierge, `songvault-drill`, à partir de la base de production restaurée 15 minutes en arrière et d'une copie de ses fichiers. La production n'est pas modifiée.

1. Lancer l'exercice. Le script chronomètre chaque étape :

```powershell
$env:SONGVAULT_SQL_PASSWORD = Read-Host 'Mot de passe SQL'
.\scripts\restore-drill-azure.ps1
```

2. Sur l'adresse affichée à la fin, vérifier à la main :
   - se connecter avec un compte de production : les mots de passe sont ceux de la base restaurée ;
   - ouvrir un morceau et une version, lire un fichier audio, télécharger une partition ;
   - comparer le nombre de morceaux avec la production.

   Les e-mails de l'environnement d'exercice partent dans ses journaux, jamais vers les utilisateurs. Ses clés Data Protection sont nouvelles : il faut se reconnecter, et les journaux de l'API signalent que les anciennes clés, chiffrées par le Key Vault de production, ne peuvent pas être lues. C'est attendu.

3. Noter la durée totale (script et vérifications) dans le tableau ci-dessous, puis supprimer l'environnement le jour même, car il contient une copie des données :

```powershell
az group delete --name songvault-drill --yes
```

Son Key Vault reste 7 jours en suppression douce. Pour relancer un exercice avant, libérer le nom : `az keyvault purge --name (az keyvault list-deleted --query "[?contains(name, 'songvault-kv')].name | [0]" -o tsv)`.

### Exercices réalisés

| Date | Point de restauration (UTC) | Durée du script | Durée totale | Remarques |
|---|---|---|---|---|