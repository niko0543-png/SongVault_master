<#
.SYNOPSIS  Vérifie la clé Brevo et l'expéditeur avant de déployer : affiche le compte, puis envoie un e-mail de test (#27).
.EXAMPLE   $env:BREVO_API_KEY = Read-Host 'Clé Brevo' ; .\scripts\test-brevo.ps1 -From no-reply@songvault.eu -To toi@gmail.com
.NOTES     L'appel part de ce PC : si Brevo bloque les adresses IP inconnues, autoriser la sienne
           (Paramètres > Sécurité > IP autorisées), ou répondre au mail d'alerte de Brevo.
#>
param(
    [Parameter(Mandatory)][string]$From,
    [Parameter(Mandatory)][string]$To
)
$ErrorActionPreference = 'Stop'
if (-not $env:BREVO_API_KEY) { throw 'Définir $env:BREVO_API_KEY (clé d''API Brevo).' }

# Windows PowerShell 5.1 ne propose pas TLS 1.2 par défaut
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
$headers = @{ 'api-key' = $env:BREVO_API_KEY; accept = 'application/json' }

$account = Invoke-RestMethod -Uri 'https://api.brevo.com/v3/account' -Headers $headers
Write-Host "Compte Brevo : $($account.companyName) ($($account.email))"
foreach ($plan in $account.plan) { Write-Host "    offre $($plan.type) : $($plan.credits) crédits ($($plan.creditsType))" }

$text = "Cet e-mail confirme que SongVault peut envoyer par Brevo depuis $From."
$body = @{
    sender      = @{ name = 'SongVault'; email = $From }
    to          = @(@{ email = $To })
    subject     = 'SongVault : e-mail de test'
    textContent = $text
    htmlContent = "<p>$text</p>"
} | ConvertTo-Json -Depth 4
$sent = Invoke-RestMethod -Method Post -Uri 'https://api.brevo.com/v3/smtp/email' -Headers $headers `
    -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body))
Write-Host "Envoyé à $To (messageId $($sent.messageId)). Vérifier la réception et l'en-tête Authentication-Results."