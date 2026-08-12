[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$expected = [IO.File]::ReadAllText((Join-Path $PSScriptRoot "version.txt")).Trim()
$unityPackagePath = Join-Path $repositoryRoot "Unity\Packages\com.greenbox.i18n\package.json"
$unityPackage = Get-Content -Raw -LiteralPath $unityPackagePath | ConvertFrom-Json

if ($unityPackage.version -ne $expected) {
    throw "Unity package version '$($unityPackage.version)' does not match product version '$expected'. Run eng/Set-Version.ps1."
}

if ($expected -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "Product version '$expected' is not valid SemVer."
}

Write-Host "GreenBox I18n component versions match $expected."
