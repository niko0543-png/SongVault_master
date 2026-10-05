# Éléments partagés par les scripts SongVault.
# Chargé au début de chaque script avec :  . "$PSScriptRoot\_common.ps1"

$RepoRoot    = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$BackupDir   = Join-Path $RepoRoot 'backups'
$FilesVolume = 'songvault_songvault-files'      # nom réel : préfixe "songvault_" du projet Compose

New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null

function Get-SaPassword {
    $envFile = Join-Path $RepoRoot '.env'
    if (-not (Test-Path $envFile)) { throw "Fichier .env introuvable : $envFile" }
    $line = Get-Content $envFile | Where-Object { $_ -match '^MSSQL_SA_PASSWORD=' } | Select-Object -First 1
    if (-not $line) { throw 'MSSQL_SA_PASSWORD absent du fichier .env' }
    return $line.Split('=', 2)[1]
}

# Les commandes externes (docker) ne déclenchent pas d'exception PowerShell en cas d'échec :
# on vérifie leur code de sortie après chaque appel.
function Assert-LastExit([string]$Step) {
    if ($LASTEXITCODE -ne 0) { throw "Échec : $Step (code $LASTEXITCODE)" }
}

function Confirm-Danger([string]$Message) {
    $answer = Read-Host "$Message`nTaper OUI pour continuer"
    return $answer -ceq 'OUI'
}