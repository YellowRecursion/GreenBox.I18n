[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string] $Version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$versionFile = Join-Path $PSScriptRoot "version.txt"
$unityPackage = Join-Path $repositoryRoot "Unity\Packages\com.greenbox.i18n\package.json"

[IO.File]::WriteAllText($versionFile, "$Version`n", [Text.UTF8Encoding]::new($false))

$packageJson = [IO.File]::ReadAllText($unityPackage)
$versionPattern = '(?m)^(\s*"version"\s*:\s*")[^"]+("\s*,\s*)$'
if (![Text.RegularExpressions.Regex]::IsMatch($packageJson, $versionPattern)) {
    throw "Unity package version was not found in $unityPackage."
}

$updatedPackageJson = [Text.RegularExpressions.Regex]::Replace(
    $packageJson,
    $versionPattern,
    "`${1}$Version`${2}",
    1)

[IO.File]::WriteAllText(
    $unityPackage,
    $updatedPackageJson.Replace("`r`n", "`n"),
    [Text.UTF8Encoding]::new($false))

Write-Host "GreenBox I18n version set to $Version."
