<#
.SYNOPSIS  Sauvegarde la base SongVault (BACKUP DATABASE) dans backups/SongVault-<date>.bak
.EXAMPLE   .\scripts\backup-db.ps1
#>
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

$password  = Get-SaPassword
$name      = "SongVault-$(Get-Date -Format 'yyyyMMdd-HHmmss').bak"
$inside    = "/var/opt/mssql/backup/$name"        # chemin DANS le conteneur
$sqlcmd    = '/opt/mssql-tools18/bin/sqlcmd'

Push-Location $RepoRoot
try {
    docker compose exec -T db mkdir -p /var/opt/mssql/backup
    Assert-LastExit 'création du dossier de sauvegarde'

    docker compose exec -T db $sqlcmd -S localhost -U sa -P $password -C -b `
      -Q "BACKUP DATABASE SongVault TO DISK = N'$inside' WITH INIT"
    Assert-LastExit 'BACKUP DATABASE'

    docker compose cp "db:$inside" (Join-Path $BackupDir $name)
    Assert-LastExit 'copie du .bak vers le PC'

    docker compose exec -T db rm -f $inside        # pas de copie oubliée dans le conteneur

    Write-Host "✅ Base sauvegardée : backups/$name"
}
finally { Pop-Location }