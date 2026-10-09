<#
.SYNOPSIS  Supprime du volume songvault-files les fichiers qu'aucune ligne de SongFiles ne référence.
.EXAMPLE   .\scripts\purge-orphan-files.ps1
#>
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"
$password = Get-SaPassword
$sqlcmd   = '/opt/mssql-tools18/bin/sqlcmd'

Push-Location $RepoRoot
try {
    $referenced = docker compose exec -T db $sqlcmd -S localhost -U sa -P $password -C -b -h -1 -W `
        -d SongVault -Q "SET NOCOUNT ON; SELECT StorageKey FROM SongFiles"
    Assert-LastExit 'lecture des clés'
    $onDisk = docker compose exec -T api ls -1 /var/songvault/files
    Assert-LastExit 'liste du volume'

    $orphans = @($onDisk | Where-Object { $_ -and ($referenced -notcontains $_) })
    Write-Host "$($orphans.Count) fichier(s) orphelin(s)."
    if ($orphans.Count -eq 0 -or -not (Confirm-Danger "Supprimer ces fichiers du volume ?")) { return }

    foreach ($key in $orphans) {
        docker compose exec -T api rm -- "/var/songvault/files/$key"; Assert-LastExit "suppression de $key"
    }
}
finally { Pop-Location }