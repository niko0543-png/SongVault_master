<#
.SYNOPSIS  Sauvegarde le volume des fichiers uploadés dans backups/files-<date>.tgz
.EXAMPLE   .\scripts\backup-files.ps1
#>
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

$name = "files-$(Get-Date -Format 'yyyyMMdd-HHmmss').tgz"

docker run --rm `
  -v "${FilesVolume}:/data:ro" `
  -v "${BackupDir}:/backup" `
  alpine tar czf "/backup/$name" -C /data .
Assert-LastExit 'archivage du volume'

Write-Host "✅ Fichiers sauvegardés : backups/$name"