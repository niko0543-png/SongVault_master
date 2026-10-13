<#
.SYNOPSIS  Récupère les fichiers supprimés depuis une date dans le stockage Azure de SongVault (ADR 0013).
.EXAMPLE   .\scripts\undelete-files-azure.ps1 -Since '2026-10-20T14:00:00Z'
.NOTES     Les fichiers supprimés restent récupérables 14 jours (suppression réversible du compte de stockage).
           À lancer après une restauration de la base à un instant donné, avec ce même instant :
           la base restaurée retrouve ainsi tous ses fichiers. Prérequis : Azure CLI connecté (az login).
#>
param(
    [Parameter(Mandatory)][datetime]$Since,
    [string]$ResourceGroup = 'songvault-prod',
    [string]$Container = 'songvault-files'
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

$account = az storage account list --resource-group $ResourceGroup --query "[0].name" --output tsv
Assert-LastExit 'lecture du compte de stockage'
$key = az storage account keys list --resource-group $ResourceGroup --account-name $account --query "[0].value" --output tsv
Assert-LastExit 'lecture de la clé du compte de stockage'

# Comparaison de dates au format ISO 8601 en UTC : l'ordre alphabétique est l'ordre chronologique
$sinceUtc = $Since.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss')
$names = az storage blob list --account-name $account --account-key $key --container-name $Container `
    --include d --num-results '*' `
    --query "[?deleted && properties.deletedTime >= '$sinceUtc'].name" --output tsv
Assert-LastExit 'liste des fichiers supprimés'
$names = @($names | Where-Object { $_ })

Write-Host "$($names.Count) fichier(s) supprimé(s) depuis $sinceUtc (UTC) dans $account/$Container"
if ($names.Count -eq 0) { return }
if (-not (Confirm-Danger "Les récupérer ?")) { Write-Host 'Abandon.'; return }

foreach ($name in $names) {
    az storage blob undelete --account-name $account --account-key $key --container-name $Container --name $name --output none
    Assert-LastExit "récupération de $name"
    Write-Host "    $name"
}
Write-Host 'Terminé.'