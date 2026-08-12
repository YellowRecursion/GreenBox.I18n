[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $RepositoryUrl,

    [string] $Token = $env:GITHUB_TOKEN,

    [string] $Tag,

    [switch] $Draft
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Token)) {
    throw "A GitHub token is required through -Token or GITHUB_TOKEN."
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$version = [IO.File]::ReadAllText((Join-Path $PSScriptRoot "version.txt")).Trim()
if ([string]::IsNullOrWhiteSpace($Tag)) {
    $Tag = "v$version"
}
if ($Tag -ne "v$version") {
    throw "Release tag '$Tag' does not match product version '$version'."
}

$releasesRoot = Join-Path $repositoryRoot "artifacts\releases\beta"
if (!(Test-Path -LiteralPath (Join-Path $releasesRoot "GreenBox.I18n-beta-Setup.exe"))) {
    throw "Desktop beta artifacts are missing. Run eng/Build-Release.ps1 first."
}

dotnet tool restore
if ($LASTEXITCODE -ne 0) {
    throw "dotnet tool restore failed with exit code $LASTEXITCODE."
}

$uploadArguments = @(
    "tool", "run", "vpk", "upload", "github",
    "--outputDir", $releasesRoot,
    "--channel", "beta",
    "--repoUrl", $RepositoryUrl,
    "--token", $Token,
    "--pre", "true",
    "--tag", $Tag,
    "--releaseName", "GreenBox I18n $version Beta"
)
if (!$Draft) {
    $uploadArguments += @("--publish", "true")
}

dotnet @uploadArguments
if ($LASTEXITCODE -ne 0) {
    throw "GitHub beta publication failed with exit code $LASTEXITCODE."
}

Write-Host "GreenBox I18n $version was uploaded to GitHub tag $Tag."
