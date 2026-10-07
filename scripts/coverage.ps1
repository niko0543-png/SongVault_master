<#
.SYNOPSIS  Mesure la couverture des tests .NET et génère le rapport HTML dans artifacts/coverage.
.EXAMPLE   .\scripts\coverage.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $root
try {
    # 1. Repartir d'une mesure propre (sinon les anciennes exécutions sont fusionnées)
    Remove-Item -Recurse -Force artifacts/coverage-raw, artifacts/coverage -ErrorAction SilentlyContinue

    # 2. Exécuter les tests en mesurant la couverture
    dotnet test --collect:"XPlat Code Coverage" --results-directory artifacts/coverage-raw
    if ($LASTEXITCODE -ne 0) { throw "Des tests ont échoué : rapport non généré." }

    # 3. Générer le rapport, sans le code généré, les migrations ni Program
    dotnet reportgenerator `
      -reports:"artifacts/coverage-raw/**/coverage.cobertura.xml" `
      -targetdir:artifacts/coverage `
      -reporttypes:"Html;TextSummary" `
      -filefilters:"-*.g.cs;-*.generated.cs" `
      -classfilters:"-*.Migrations.*;-Program"
    if ($LASTEXITCODE -ne 0) { throw "Échec de la génération du rapport." }

    Get-Content artifacts/coverage/Summary.txt -Head 15
    Write-Host "`nRapport détaillé : artifacts/coverage/index.html"
}
finally { Pop-Location }