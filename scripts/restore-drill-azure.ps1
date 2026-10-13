<#
.SYNOPSIS  Exercice de restauration (ADR 0013) : recrée SongVault dans un groupe de ressources vierge, à partir de
           la base de production restaurée à un instant donné et d'une copie de ses fichiers. Chronométré.
.EXAMPLE   $env:SONGVAULT_SQL_PASSWORD = '…' ; .\scripts\restore-drill-azure.ps1
.NOTES     Ne modifie pas la production : la base y est restaurée sous un autre nom, copiée, puis effacée.
           L'environnement d'exercice envoie ses e-mails dans les journaux (Email:Provider=Log).
           À la fin : vérifier à la main (docs/runbook-sauvegardes.md), puis supprimer le groupe d'exercice.
#>
param(
    [datetime]$RestorePoint = (Get-Date).AddMinutes(-15),
    [string]$ImageTag,
    [string]$SourceResourceGroup = 'songvault-prod',
    [string]$DrillResourceGroup = 'songvault-drill',
    [string]$Container = 'songvault-files'
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

if ($DrillResourceGroup -eq $SourceResourceGroup) { throw "Le groupe d'exercice doit être différent de $SourceResourceGroup." }
if (-not $env:SONGVAULT_SQL_PASSWORD) { throw 'Définir $env:SONGVAULT_SQL_PASSWORD (mot de passe administrateur SQL).' }

$clock = [Diagnostics.Stopwatch]::StartNew()
function Step([string]$Text) { Write-Host ("[{0:hh\:mm\:ss}] {1}" -f $clock.Elapsed, $Text) }

$restoreUtc = $RestorePoint.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss')
$sourceServer = az sql server list --resource-group $SourceResourceGroup --query "[0].name" --output tsv
Assert-LastExit 'lecture du serveur SQL de production'
$sourceStorage = az storage account list --resource-group $SourceResourceGroup --query "[0].name" --output tsv
Assert-LastExit 'lecture du stockage de production'
if (-not $ImageTag) {
    # Même version que la production : la base restaurée a son schéma
    $image = az containerapp show --resource-group $SourceResourceGroup --name songvault-api `
        --query "properties.template.containers[0].image" --output tsv
    Assert-LastExit "lecture de l'image de production"
    $ImageTag = $image.Split(':')[-1]
}
$temporary = "SongVault-exercice-$(Get-Date -Format 'yyyyMMddHHmm')"

try {
    Step "1/5 Environnement vierge $DrillResourceGroup, images $ImageTag"
    & "$PSScriptRoot\deploy-azure.ps1" -ImageTag $ImageTag -ResourceGroup $DrillResourceGroup -NoDeleteLocks
    $drillServer = az sql server list --resource-group $DrillResourceGroup --query "[0].name" --output tsv
    Assert-LastExit "lecture du serveur SQL d'exercice"
    $drillStorage = az storage account list --resource-group $DrillResourceGroup --query "[0].name" --output tsv
    Assert-LastExit "lecture du stockage d'exercice"

    Step "2/5 Base de production restaurée à $restoreUtc (UTC), sous le nom $temporary"
    az sql db restore --resource-group $SourceResourceGroup --server $sourceServer --name SongVault `
        --dest-name $temporary --time $restoreUtc --service-objective Basic --backup-storage-redundancy Local --output none
    Assert-LastExit 'restauration à un instant donné'

    Step "3/5 Copie de la base restaurée vers $drillServer, à la place de la base vide"
    az sql db delete --resource-group $DrillResourceGroup --server $drillServer --name SongVault --yes
    Assert-LastExit "suppression de la base vide d'exercice"
    az sql db copy --resource-group $SourceResourceGroup --server $sourceServer --name $temporary `
        --dest-resource-group $DrillResourceGroup --dest-server $drillServer --dest-name SongVault `
        --service-objective Basic --backup-storage-redundancy Local --output none
    Assert-LastExit 'copie de la base'

    Step "4/5 Copie des fichiers $sourceStorage → $drillStorage"
    $sourceKey = az storage account keys list --resource-group $SourceResourceGroup --account-name $sourceStorage --query "[0].value" --output tsv
    Assert-LastExit 'lecture de la clé du stockage de production'
    $drillKey = az storage account keys list --resource-group $DrillResourceGroup --account-name $drillStorage --query "[0].value" --output tsv
    Assert-LastExit "lecture de la clé du stockage d'exercice"
    # Copie de serveur à serveur : les fichiers ne passent pas par le PC
    az storage blob copy start-batch --account-name $drillStorage --account-key $drillKey --destination-container $Container `
        --source-account-name $sourceStorage --source-account-key $sourceKey --source-container $Container --output none
    Assert-LastExit 'lancement de la copie des fichiers'
    do {
        Start-Sleep -Seconds 10
        $pending = az storage blob list --account-name $drillStorage --account-key $drillKey --container-name $Container `
            --num-results '*' --query "length([?properties.copy.status=='pending'])" --output tsv
        Assert-LastExit 'suivi de la copie des fichiers'
        Write-Host "    $pending en cours"
    } while ($pending -ne '0')
    $sourceCount = az storage blob list --account-name $sourceStorage --account-key $sourceKey --container-name $Container `
        --num-results '*' --query 'length(@)' --output tsv
    $drillCount = az storage blob list --account-name $drillStorage --account-key $drillKey --container-name $Container `
        --num-results '*' --query "length([?properties.copy.status=='success'])" --output tsv
    Write-Host "    $drillCount fichier(s) copié(s) sur $sourceCount"
    if ($drillCount -ne $sourceCount) { throw "Copie des fichiers incomplète : $drillCount sur $sourceCount." }

    Step "5/5 Redémarrage de l'API d'exercice sur la base restaurée"
    $revision = az containerapp revision list --resource-group $DrillResourceGroup --name songvault-api `
        --query "[?properties.active].name | [0]" --output tsv
    Assert-LastExit 'lecture de la révision active'
    az containerapp revision restart --resource-group $DrillResourceGroup --name songvault-api --revision $revision --output none
    Assert-LastExit "redémarrage de l'API d'exercice"

    $fqdn = az containerapp show --resource-group $DrillResourceGroup --name songvault-web `
        --query properties.configuration.ingress.fqdn --output tsv
    Step "Restauration terminée. Site d'exercice : https://$fqdn"
    Write-Host 'Vérifier à la main (docs/runbook-sauvegardes.md, « Exercice de restauration »), noter la durée totale,'
    Write-Host "puis supprimer l'environnement : az group delete --name $DrillResourceGroup --yes"
}
finally {
    # La copie temporaire coûte tant qu'elle existe : toujours l'effacer, même après un échec
    $leftover = az sql db list --resource-group $SourceResourceGroup --server $sourceServer `
        --query "[?name=='$temporary'].name" --output tsv
    if ($leftover) {
        az sql db delete --resource-group $SourceResourceGroup --server $sourceServer --name $temporary --yes
        Write-Host "Base temporaire $temporary effacée de la production."
    }
}