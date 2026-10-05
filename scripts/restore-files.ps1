<#
.SYNOPSIS  Remplace le contenu du volume des fichiers par une archive de backups/.
.EXAMPLE   .\scripts\restore-files.ps1 -Archive files-20261018-143005.tgz
#>
param(
    [Parameter(Mandatory)] [string] $Archive      # nom du fichier dans backups/
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

if (-not (Test-Path (Join-Path $BackupDir $Archive))) { throw "Archive introuvable : backups/$Archive" }
if (-not (Confirm-Danger "⚠️ Le contenu actuel du volume $FilesVolume va être REMPLACÉ par $Archive.")) {
    Write-Host 'Annulé.'; return
}

Push-Location $RepoRoot                           # docker compose doit trouver compose.yaml
try {
    docker compose stop api; Assert-LastExit "arrêt de l'API"

    docker run --rm `
      -v "${FilesVolume}:/data" `
      -v "${BackupDir}:/backup" `
      alpine sh -c "rm -rf /data/* && tar xzf /backup/$Archive -C /data"
    Assert-LastExit 'restauration du volume'

    Write-Host "✅ Fichiers restaurés depuis backups/$Archive"
}
finally {
    docker compose start api                      # redémarre l'API même en cas d'erreur
    Pop-Location
}