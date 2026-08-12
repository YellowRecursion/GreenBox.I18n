[CmdletBinding()]
param(
    [string] $Version,

    [string] $UpdateSource = $env:GREENBOX_I18N_UPDATE_URL,

    [string] $ReleaseNotes,

    [string] $GitHubToken = $env:GITHUB_TOKEN,

    [string] $RiderHome = $env:RIDER_HOME,

    [switch] $DownloadPrevious,

    [switch] $SkipRider,

    [switch] $SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot

if (![string]::IsNullOrWhiteSpace($Version)) {
    & (Join-Path $PSScriptRoot "Set-Version.ps1") -Version $Version
}

& (Join-Path $PSScriptRoot "Test-Version.ps1")
& (Join-Path $repositoryRoot "Build-UnityPackage.ps1")
if (!$SkipRider) {
    if ([string]::IsNullOrWhiteSpace($RiderHome)) {
        & (Join-Path $PSScriptRoot "Build-Rider.ps1")
    }
    else {
        & (Join-Path $PSScriptRoot "Build-Rider.ps1") -RiderHome $RiderHome
    }
}

$currentVersion = [IO.File]::ReadAllText((Join-Path $PSScriptRoot "version.txt")).Trim()
$nugetOutput = Join-Path $repositoryRoot "artifacts\nuget"
New-Item -ItemType Directory -Path $nugetOutput -Force | Out-Null

dotnet pack (Join-Path $repositoryRoot "GreenBox.I18n.Cli\GreenBox.I18n.Cli.csproj") `
    -c Release `
    -o $nugetOutput
if ($LASTEXITCODE -ne 0) {
    throw "CLI package creation failed with exit code $LASTEXITCODE."
}

& (Join-Path $PSScriptRoot "Pack-Beta.ps1") `
    -UpdateSource $UpdateSource `
    -ReleaseNotes $ReleaseNotes `
    -GitHubToken $GitHubToken `
    -DownloadPrevious:$DownloadPrevious `
    -SkipTests:$SkipTests

& (Join-Path $PSScriptRoot "Verify-Beta.ps1")

Write-Host ""
Write-Host "GreenBox I18n $currentVersion release artifacts are ready."
Write-Host "Desktop: $(Join-Path $repositoryRoot 'artifacts\releases\beta')"
Write-Host "CLI: $nugetOutput"
if (!$SkipRider) {
    Write-Host "Rider: $(Join-Path $repositoryRoot "artifacts\rider\$currentVersion")"
}
