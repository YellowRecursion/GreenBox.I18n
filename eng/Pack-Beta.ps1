[CmdletBinding()]
param(
    [ValidateSet("win-x64")]
    [string] $Runtime = "win-x64",

    [string] $UpdateSource = $env:GREENBOX_I18N_UPDATE_URL,

    [string] $ReleaseNotes,

    [string] $GitHubToken = $env:GITHUB_TOKEN,

    [switch] $DownloadPrevious,

    [switch] $SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$version = [IO.File]::ReadAllText((Join-Path $PSScriptRoot "version.txt")).Trim()
$bundleRoot = Join-Path $repositoryRoot "artifacts\desktop\$Runtime\$version\bundle"
$releasesRoot = Join-Path $repositoryRoot "artifacts\releases\beta"
$artifactsRoot = Join-Path $repositoryRoot "artifacts"

$resolvedReleases = [IO.Path]::GetFullPath($releasesRoot)
$resolvedArtifacts = [IO.Path]::GetFullPath($artifactsRoot) + [IO.Path]::DirectorySeparatorChar
if (!$resolvedReleases.StartsWith($resolvedArtifacts, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to reset a release directory outside artifacts: $resolvedReleases"
}
if (Test-Path -LiteralPath $resolvedReleases) {
    Remove-Item -LiteralPath $resolvedReleases -Recurse -Force
}

dotnet tool restore
if ($LASTEXITCODE -ne 0) {
    throw "dotnet tool restore failed with exit code $LASTEXITCODE."
}

New-Item -ItemType Directory -Path $releasesRoot -Force | Out-Null

if ($DownloadPrevious) {
    if ([string]::IsNullOrWhiteSpace($UpdateSource) -or
        !$UpdateSource.StartsWith("https://github.com/", [StringComparison]::OrdinalIgnoreCase)) {
        throw "-DownloadPrevious currently requires a GitHub repository URL in -UpdateSource."
    }

    $downloadArguments = @(
        "tool", "run", "vpk", "download", "github",
        "--repoUrl", $UpdateSource,
        "--channel", "beta",
        "--outputDir", $releasesRoot
    )
    if (![string]::IsNullOrWhiteSpace($GitHubToken)) {
        $downloadArguments += @("--token", $GitHubToken)
    }

    dotnet @downloadArguments
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "No previous beta release was downloaded. This is expected for the first beta."
    }
}

& (Join-Path $PSScriptRoot "Build-Desktop.ps1") `
    -Runtime $Runtime `
    -UpdateSource $UpdateSource `
    -SkipTests:$SkipTests

$packArguments = @(
    "tool", "run", "vpk", "pack",
    "--packId", "GreenBox.I18n",
    "--packTitle", "GreenBox I18n Beta",
    "--packVersion", $version,
    "--packDir", $bundleRoot,
    "--mainExe", "GreenBox.I18n.exe",
    "--icon", (Join-Path $repositoryRoot "GreenBox.I18n.Desktop\Assets\greenbox-i18n.ico"),
    "--runtime", $Runtime,
    "--channel", "beta",
    "--outputDir", $releasesRoot
)
if (!([string]::IsNullOrWhiteSpace($ReleaseNotes))) {
    $packArguments += @("--releaseNotes", [IO.Path]::GetFullPath($ReleaseNotes))
}

dotnet @packArguments
if ($LASTEXITCODE -ne 0) {
    throw "Velopack packaging failed with exit code $LASTEXITCODE."
}

Write-Host ""
Write-Host "Beta installer and update packages: $releasesRoot"
