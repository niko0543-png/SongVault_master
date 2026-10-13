<#
.SYNOPSIS  Déploie SongVault sur Azure (ADR 0012) : infrastructure, migrations, puis redémarrage de l'API.
.EXAMPLE   $env:SONGVAULT_SQL_PASSWORD = '…' ; .\scripts\deploy-azure.ps1 -ImageTag v1.1.0
.NOTES     Prérequis : Azure CLI connecté (az login) et images publiées par le workflow « images ».
           Secrets lus dans l'environnement, jamais en paramètre :
             SONGVAULT_SQL_PASSWORD  mot de passe de l'administrateur SQL (obligatoire, toujours le même)
             BREVO_API_KEY           clé d'API Brevo (avec -EmailProvider Brevo)
#>
param(
    [Parameter(Mandatory)][string]$ImageTag,
    [string]$ResourceGroup = 'songvault-prod',
    [string]$Location = 'francecentral',
    [ValidateSet('Log', 'Brevo')][string]$EmailProvider = 'Log',
    [string]$EmailFrom = 'no-reply@songvault.local'
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

if (-not $env:SONGVAULT_SQL_PASSWORD) { throw 'Définir $env:SONGVAULT_SQL_PASSWORD (mot de passe administrateur SQL).' }
if ($EmailProvider -eq 'Brevo' -and -not $env:BREVO_API_KEY) { throw 'Définir $env:BREVO_API_KEY pour envoyer avec Brevo.' }

# Les secrets passent par un fichier de paramètres temporaire : jamais dans la ligne de commande
$secrets = Join-Path ([IO.Path]::GetTempPath()) "songvault-$([guid]::NewGuid()).parameters.json"
$parameters = @{
    '$schema'      = 'https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#'
    contentVersion = '1.0.0.0'
    parameters     = @{
        sqlAdminPassword = @{ value = $env:SONGVAULT_SQL_PASSWORD }
        brevoApiKey      = @{ value = "$env:BREVO_API_KEY" }
    }
}
[IO.File]::WriteAllText($secrets, ($parameters | ConvertTo-Json -Depth 5))     # UTF-8 sans BOM

Push-Location $RepoRoot
try {
    Write-Host "1/4 Groupe de ressources $ResourceGroup ($Location)"
    az group create --name $ResourceGroup --location $Location --output none
    Assert-LastExit 'création du groupe de ressources'

    Write-Host "2/4 Infrastructure (infra/main.bicep), images $ImageTag"
    $json = az deployment group create --resource-group $ResourceGroup --name "songvault-$ImageTag" `
        --template-file infra/main.bicep `
        --parameters "@$secrets" imageTag=$ImageTag emailProvider=$EmailProvider emailFrom=$EmailFrom `
        --query properties.outputs --output json
    Assert-LastExit 'déploiement Bicep'
    $outputs = $json | Out-String | ConvertFrom-Json
    $api = $outputs.apiName.value
    $migrator = $outputs.migratorName.value

    Write-Host "3/4 Migrations (job $migrator)"
    $execution = az containerapp job start --resource-group $ResourceGroup --name $migrator --query name --output tsv
    Assert-LastExit 'lancement des migrations'
    do {
        Start-Sleep -Seconds 10
        $status = az containerapp job execution show --resource-group $ResourceGroup --name $migrator `
            --job-execution-name $execution --query properties.status --output tsv
        Assert-LastExit 'suivi des migrations'
        Write-Host "    $status"
    } while ($status -in 'Running', 'Processing')
    if ($status -ne 'Succeeded') {
        throw "Migrations : $status. Journaux : portail Azure, job $migrator, Historique d'exécution, $execution."
    }

    # Le rôle Key Vault de l'API met quelques minutes à s'appliquer au premier déploiement :
    # un redémarrage après les migrations repart avec le schéma à jour et l'accès à la clé.
    Write-Host "4/4 Redémarrage de l'API"
    $revision = az containerapp revision list --resource-group $ResourceGroup --name $api `
        --query "[?properties.active].name | [0]" --output tsv
    Assert-LastExit 'lecture de la révision active'
    az containerapp revision restart --resource-group $ResourceGroup --name $api --revision $revision --output none
    Assert-LastExit "redémarrage de l'API"

    Write-Host "SongVault : $($outputs.publicUrl.value)"
}
finally {
    Remove-Item $secrets -ErrorAction SilentlyContinue
    Pop-Location
}