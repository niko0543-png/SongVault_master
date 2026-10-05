<#
.SYNOPSIS  Remplace la base SongVault par une sauvegarde de backups/.
.EXAMPLE   .\scripts\restore-db.ps1 -Backup SongVault-20261018-143004.bak
#>
param(
    [Parameter(Mandatory)] [string] $Backup       # nom du fichier dans backups/
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

$local = Join-Path $BackupDir $Backup
if (-not (Test-Path $local)) { throw "Sauvegarde introuvable : backups/$Backup" }
if (-not (Confirm-Danger "⚠️ La base SongVault actuelle va être REMPLACÉE par $Backup.")) {
    Write-Host 'Annulé.'; return
}

$password = Get-SaPassword
$inside   = "/var/opt/mssql/backup/$Backup"
$sqlcmd   = '/opt/mssql-tools18/bin/sqlcmd'

Push-Location $RepoRoot
try {
    docker compose stop api; Assert-LastExit "arrêt de l'API"     # libère les connexions

    docker compose exec -T db mkdir -p /var/opt/mssql/backup
    docker compose cp $local "db:$inside";                    Assert-LastExit 'copie du .bak'
    docker compose exec -T -u 0 db chown mssql $inside;       Assert-LastExit 'droits sur le .bak'

    docker compose exec -T db $sqlcmd -S localhost -U sa -P $password -C -b `
      -Q "RESTORE DATABASE SongVault FROM DISK = N'$inside' WITH REPLACE"
    Assert-LastExit 'RESTORE DATABASE'

    docker compose exec -T db rm -f $inside
    Write-Host "✅ Base restaurée depuis backups/$Backup"
}
finally {
    docker compose start api
    Pop-Location
}