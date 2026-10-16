<#
.SYNOPSIS  Déploie SongVault sur Azure (ADR 0012) : infrastructure, migrations, puis redémarrage de l'API.
.EXAMPLE   $env:SONGVAULT_SQL_PASSWORD = '…' ; .\scripts\deploy-azure.ps1 -ImageTag v1.1.0
.EXAMPLE   .\scripts\deploy-azure.ps1 -ImageTag v1.1.0 -ResourceGroup songvault-drill -NoDeleteLocks
.EXAMPLE   $env:BREVO_API_KEY = '…' ; .\scripts\deploy-azure.ps1 -ImageTag v1.1.0 -EmailProvider Brevo -EmailFrom no-reply@songvault.eu
.NOTES     Prérequis : Azure CLI connecté (az login) et images publiées par le workflow « images ».
           Secrets lus dans l'environnement, jamais en paramètre :
             SONGVAULT_SQL_PASSWORD  mot de passe de l'administrateur SQL (obligatoire, toujours le même)
             BREVO_API_KEY           clé d'API Brevo : à définir la première fois, ou pour la changer
           Sans -EmailProvider ni -EmailFrom, les réglages e-mail de l'API en place sont gardés, clé Brevo
           comprise (#27). Un nouveau groupe de ressources part sur Log : aucun e-mail ne part.
#>
param(
    [Parameter(Mandatory)][string]$ImageTag,
    [string]$ResourceGroup = 'songvault-prod',
    [string]$Location = 'francecentral',
    # Sans valeur : celle de l'API déjà déployée (Log et no-reply@songvault.local pour un nouveau groupe)
    [ValidateSet('Log', 'Brevo')][string]$EmailProvider,
    [string]$EmailFrom,
    # Sans verrous sur la base et le stockage : environnement d'exercice, supprimé ensuite (ADR 0013)
    [switch]$NoDeleteLocks
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

# Réglages e-mail de l'API en place, pour qu'un redéploiement ne repasse pas aux journaux sans le dire
function Get-DeployedEmail([string]$Group, [string]$App) {
    $settings = @{ Provider = 'Log'; From = 'no-reply@songvault.local'; ApiKey = '' }
    if ((az group exists --name $Group) -ne 'true') { return $settings }
    $json = az containerapp list --resource-group $Group `
        --query "[?name=='$App'] | [0].properties.template.containers[0].env" --output json
    Assert-LastExit "lecture des réglages de $App"
    $vars = $json | Out-String | ConvertFrom-Json
    if (-not $vars) { return $settings }                                  # pas encore d'API dans ce groupe
    $provider = ($vars | Where-Object name -eq 'Email__Provider').value
    $from = ($vars | Where-Object name -eq 'Email__FromAddress').value
    if ($provider) { $settings.Provider = $provider }
    if ($from) { $settings.From = $from }
    $key = az containerapp secret list --resource-group $Group --name $App --show-values `
        --query "[?name=='brevo-api-key'] | [0].value" --output tsv
    Assert-LastExit 'lecture du secret brevo-api-key'
    if ($key -and $key -ne 'aucune') { $settings.ApiKey = $key }          # 'aucune' : valeur mise par main.bicep
    return $settings
}

if (-not $env:SONGVAULT_SQL_PASSWORD) { throw 'Définir $env:SONGVAULT_SQL_PASSWORD (mot de passe administrateur SQL).' }

$deployed = Get-DeployedEmail $ResourceGroup 'songvault-api'
if (-not $EmailProvider) { $EmailProvider = $deployed.Provider }
if (-not $EmailFrom) { $EmailFrom = $deployed.From }
$brevoApiKey = if ($env:BREVO_API_KEY) { $env:BREVO_API_KEY } else { $deployed.ApiKey }
if ($EmailProvider -eq 'Brevo' -and -not $brevoApiKey) { throw 'Définir $env:BREVO_API_KEY pour envoyer avec Brevo.' }
if ($EmailProvider -eq 'Brevo' -and $EmailFrom -like '*@songvault.local') {
    throw 'Avec Brevo, passer -EmailFrom avec une adresse du domaine authentifié chez Brevo.'
}

# Les secrets passent par un fichier de paramètres temporaire : jamais dans la ligne de commande
$secrets = Join-Path ([IO.Path]::GetTempPath()) "songvault-$([guid]::NewGuid()).parameters.json"
$parameters = @{
    '$schema'      = 'https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#'
    contentVersion = '1.0.0.0'
    parameters     = @{
        sqlAdminPassword = @{ value = $env:SONGVAULT_SQL_PASSWORD }
        brevoApiKey      = @{ value = "$brevoApiKey" }
    }
}
[IO.File]::WriteAllText($secrets, ($parameters | ConvertTo-Json -Depth 5))     # UTF-8 sans BOM

Push-Location $RepoRoot
try {
    Write-Host "1/4 Groupe de ressources $ResourceGroup ($Location)"
    az group create --name $ResourceGroup --location $Location --output none
    Assert-LastExit 'création du groupe de ressources'

    Write-Host "2/4 Infrastructure (infra/main.bicep), images $ImageTag, e-mails $EmailProvider ($EmailFrom)"
	$locks = if ($NoDeleteLocks) { 'false' } else { 'true' }
    $json = az deployment group create --resource-group $ResourceGroup --name "songvault-$ImageTag" `
        --template-file infra/main.bicep `
        --parameters "@$secrets" imageTag=$ImageTag emailProvider=$EmailProvider emailFrom=$EmailFrom `
            enableDeleteLocks=$locks `
        --query properties.outputs --output json
    Assert-LastExit 'déploiement Bicep'
    $outputs = $json | Out-String | ConvertFrom-Json
    $api = $outputs.apiName.value
    $migrator = $outputs.migratorName.value

    Write-Host "3/4 Migrations (job $migrator)"
    $execution = az containerapp job start --resource-group $ResourceGroup --name $migrator --query name --output tsv
    Assert-LastExit 'lancement des migrations'
    $deadline = (Get-Date).AddMinutes(15)
    do {
        Start-Sleep -Seconds 10
        $status = az containerapp job execution show --resource-group $ResourceGroup --name $migrator `
            --job-execution-name $execution --query properties.status --output tsv
        Assert-LastExit 'suivi des migrations'
        Write-Host "    $status"
        if ((Get-Date) -gt $deadline) { throw "Migrations : encore $status après 15 minutes." }
    } while ($status -notin 'Succeeded', 'Failed', 'Stopped', 'Degraded')
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
    if ($EmailProvider -eq 'Log') {
        Write-Warning 'E-mails : Log. Aucun e-mail ne part, ils sont écrits dans les journaux de l''API.'
    }
}
finally {
    Remove-Item $secrets -ErrorAction SilentlyContinue
    Pop-Location
}