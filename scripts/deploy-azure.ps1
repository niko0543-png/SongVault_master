<#
.SYNOPSIS  Déploie SongVault sur Azure (ADR 0012) : infrastructure, migrations, puis redémarrage de l'API.
.EXAMPLE   $env:SONGVAULT_SQL_PASSWORD = '…' ; .\scripts\deploy-azure.ps1 -ImageTag v1.1.0
.EXAMPLE   .\scripts\deploy-azure.ps1 -ImageTag v1.1.0 -ResourceGroup songvault-drill -NoDeleteLocks
.EXAMPLE   $env:BREVO_API_KEY = '…' ; .\scripts\deploy-azure.ps1 -ImageTag v1.1.0 -EmailProvider Brevo -EmailFrom no-reply@songvault.eu
.EXAMPLE   $env:STRIPE_SECRET_KEY = '…' ; $env:STRIPE_WEBHOOK_SECRET = '…'
           .\scripts\deploy-azure.ps1 -ImageTag v1.9.0 -BillingProvider Stripe -StripePriceBand price_… -StripePriceStudio price_…
.EXAMPLE   .\scripts\deploy-azure.ps1 -ImageTag v1.10.0 -AntivirusProvider ClamAv
.EXAMPLE   .\scripts\deploy-azure.ps1 -ImageTag v1.11.0 -AlertEmail contact@songvault.eu
.NOTES     Prérequis : Azure CLI connecté (az login) et images publiées par le workflow « images ».
           Secrets lus dans l'environnement, jamais en paramètre :
             SONGVAULT_SQL_PASSWORD  mot de passe de l'administrateur SQL (obligatoire, toujours le même)
             BREVO_API_KEY           clé d'API Brevo : à définir la première fois, ou pour la changer
             STRIPE_SECRET_KEY       clé secrète Stripe (sk_live_…) : avec -BillingProvider Stripe, la première fois (#16)
             STRIPE_WEBHOOK_SECRET   secret de signature du webhook (whsec_…) : idem
           Sans -EmailProvider ni -EmailFrom, les réglages e-mail de l'API en place sont gardés, clé Brevo
           comprise (#27). Un nouveau groupe de ressources part sur Log : aucun e-mail ne part.
           De même sans -BillingProvider ni prix : les réglages de paiement en place sont gardés ;
           un nouveau groupe part sur None (formules et quotas, sans paiement en ligne).
           De même sans -AntivirusProvider : le réglage en place est gardé ; un nouveau groupe, ou une API
           d'avant #17, part sur None (fichiers non analysés, pas de conteneur ClamAV).
           De même sans -AlertEmail : l'adresse des alertes en place est gardée (#18) ; sans aucune adresse,
           pas d'alerte (environnement d'exercice).
#>
param(
    [Parameter(Mandatory)][string]$ImageTag,
    [string]$ResourceGroup = 'songvault-prod',
    [string]$Location = 'francecentral',
    # Sans valeur : celle de l'API déjà déployée (Log et no-reply@songvault.local pour un nouveau groupe)
    [ValidateSet('Log', 'Brevo')][string]$EmailProvider,
    [string]$EmailFrom,
    # Sans valeur : celle de l'API déjà déployée (None pour un nouveau groupe)
    [ValidateSet('None', 'Stripe')][string]$BillingProvider,
    [string]$StripePriceBand,
    [string]$StripePriceStudio,
    # Sans valeur : celle de l'API déjà déployée (None pour un nouveau groupe) ; ClamAv ajoute le conteneur (#17)
    [ValidateSet('None', 'ClamAv')][string]$AntivirusProvider,
    # Sans valeur : celle des alertes déjà déployées (aucune alerte pour un nouveau groupe) (#18)
    [string]$AlertEmail,
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

# Réglages de paiement de l'API en place (#16), même principe que pour les e-mails
function Get-DeployedBilling([string]$Group, [string]$App) {
    $settings = @{ Provider = 'None'; PriceBand = ''; PriceStudio = ''; SecretKey = ''; WebhookSecret = '' }
    if ((az group exists --name $Group) -ne 'true') { return $settings }
    $json = az containerapp list --resource-group $Group `
        --query "[?name=='$App'] | [0].properties.template.containers[0].env" --output json
    Assert-LastExit "lecture des réglages de $App"
    $vars = $json | Out-String | ConvertFrom-Json
    if (-not $vars) { return $settings }
    $provider = ($vars | Where-Object name -eq 'Billing__Provider').value
    if ($provider) { $settings.Provider = $provider }
    $settings.PriceBand = "$(($vars | Where-Object name -eq 'Billing__Stripe__Prices__band').value)"
    $settings.PriceStudio = "$(($vars | Where-Object name -eq 'Billing__Stripe__Prices__studio').value)"
    $json = az containerapp secret list --resource-group $Group --name $App --show-values --output json
    Assert-LastExit 'lecture des secrets Stripe'
    $secretList = $json | Out-String | ConvertFrom-Json
    $key = ($secretList | Where-Object name -eq 'stripe-secret-key').value
    $hook = ($secretList | Where-Object name -eq 'stripe-webhook-secret').value
    if ($key -and $key -ne 'aucune') { $settings.SecretKey = $key }
    if ($hook -and $hook -ne 'aucune') { $settings.WebhookSecret = $hook }
    return $settings
}

# Antivirus de l'API en place (#17), même principe : un redéploiement ne le coupe pas sans le dire
function Get-DeployedAntivirus([string]$Group, [string]$App) {
    if ((az group exists --name $Group) -ne 'true') { return 'None' }
    $json = az containerapp list --resource-group $Group `
        --query "[?name=='$App'] | [0].properties.template.containers[0].env" --output json
    Assert-LastExit "lecture des réglages de $App"
    $vars = $json | Out-String | ConvertFrom-Json
    $provider = ($vars | Where-Object name -eq 'Antivirus__Provider').value
    if ($provider) { return $provider } else { return 'None' }
}

# Adresse des alertes en place (#18), même principe : un redéploiement ne coupe pas les alertes sans le dire
function Get-DeployedAlertEmail([string]$Group) {
    if ((az group exists --name $Group) -ne 'true') { return '' }
    $email = az monitor action-group list --resource-group $Group `
        --query "[?name=='songvault-alertes'] | [0].emailReceivers[0].emailAddress" --output tsv
    Assert-LastExit 'lecture du groupe d''actions des alertes'
    return "$email"
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

$billing = Get-DeployedBilling $ResourceGroup 'songvault-api'
if (-not $BillingProvider) { $BillingProvider = $billing.Provider }
if (-not $StripePriceBand) { $StripePriceBand = $billing.PriceBand }
if (-not $StripePriceStudio) { $StripePriceStudio = $billing.PriceStudio }
$stripeSecretKey = if ($env:STRIPE_SECRET_KEY) { $env:STRIPE_SECRET_KEY } else { $billing.SecretKey }
$stripeWebhookSecret = if ($env:STRIPE_WEBHOOK_SECRET) { $env:STRIPE_WEBHOOK_SECRET } else { $billing.WebhookSecret }
if ($BillingProvider -eq 'Stripe') {
    if (-not $stripeSecretKey -or -not $stripeWebhookSecret) {
        throw 'Définir $env:STRIPE_SECRET_KEY et $env:STRIPE_WEBHOOK_SECRET pour encaisser avec Stripe.'
    }
    if (-not $StripePriceBand -or -not $StripePriceStudio) {
        throw 'Avec Stripe, passer -StripePriceBand et -StripePriceStudio (identifiants price_… du tableau de bord).'
    }
    if ($ResourceGroup -eq 'songvault-prod' -and $stripeSecretKey -like 'sk_test_*') {
        Write-Warning 'Clé Stripe de test en production : aucun vrai paiement ne sera encaissé.'
    }
}

if (-not $AntivirusProvider) { $AntivirusProvider = Get-DeployedAntivirus $ResourceGroup 'songvault-api' }

if (-not $AlertEmail) { $AlertEmail = Get-DeployedAlertEmail $ResourceGroup }

# Les secrets passent par un fichier de paramètres temporaire : jamais dans la ligne de commande
$secrets = Join-Path ([IO.Path]::GetTempPath()) "songvault-$([guid]::NewGuid()).parameters.json"
$parameters = @{
    '$schema'      = 'https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#'
    contentVersion = '1.0.0.0'
    parameters     = @{
        sqlAdminPassword = @{ value = $env:SONGVAULT_SQL_PASSWORD }
        brevoApiKey      = @{ value = "$brevoApiKey" }
        stripeSecretKey     = @{ value = "$stripeSecretKey" }
        stripeWebhookSecret = @{ value = "$stripeWebhookSecret" }
        # Pas des secrets, mais peut-être vides : un fichier de paramètres les passe tels quels
        stripePriceBand     = @{ value = "$StripePriceBand" }
        stripePriceStudio   = @{ value = "$StripePriceStudio" }
        alertEmail          = @{ value = "$AlertEmail" }
    }
}
[IO.File]::WriteAllText($secrets, ($parameters | ConvertTo-Json -Depth 5))     # UTF-8 sans BOM

Push-Location $RepoRoot
try {
    Write-Host "1/4 Groupe de ressources $ResourceGroup ($Location)"
    az group create --name $ResourceGroup --location $Location --output none
    Assert-LastExit 'création du groupe de ressources'

    Write-Host "2/4 Infrastructure (infra/main.bicep), images $ImageTag, e-mails $EmailProvider ($EmailFrom), paiement $BillingProvider, antivirus $AntivirusProvider, alertes $(if ($AlertEmail) { $AlertEmail } else { 'aucune' })"
	$locks = if ($NoDeleteLocks) { 'false' } else { 'true' }
    $json = az deployment group create --resource-group $ResourceGroup --name "songvault-$ImageTag" `
        --template-file infra/main.bicep `
        --parameters "@$secrets" imageTag=$ImageTag emailProvider=$EmailProvider emailFrom=$EmailFrom `
            enableDeleteLocks=$locks billingProvider=$BillingProvider antivirusProvider=$AntivirusProvider `
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
    if ($BillingProvider -eq 'None') {
        Write-Host 'Paiement : None. Formules et quotas appliqués, paiement en ligne fermé.'
    }
    if ($AntivirusProvider -eq 'None') {
        Write-Host 'Antivirus : None. Fichiers lisibles sans analyse, à activer avant la commercialisation.'
    }
    Write-Host "Supervision : Application Insights $($outputs.appInsightsName.value), page $($outputs.publicUrl.value)/statut"
    if (-not $AlertEmail) {
        Write-Warning 'Alertes : aucune. Passer -AlertEmail avec l''adresse qui doit les recevoir.'
    }
}
finally {
    Remove-Item $secrets -ErrorAction SilentlyContinue
    Pop-Location
}