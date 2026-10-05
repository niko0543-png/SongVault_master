<#
.SYNOPSIS  Sauvegarde cohérente : base PUIS fichiers, en une commande.
.EXAMPLE   .\scripts\backup-all.ps1
#>
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot\backup-db.ps1"
& "$PSScriptRoot\backup-files.ps1"