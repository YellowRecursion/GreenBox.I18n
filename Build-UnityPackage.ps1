[CmdletBinding()]
param(
    [switch] $Verify
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = $PSScriptRoot
$coreProject = Join-Path $repositoryRoot "GreenBox.I18n.Core\GreenBox.I18n.Core.csproj"
$coreOutput = Join-Path $repositoryRoot "GreenBox.I18n.Core\bin\Release\netstandard2.1"
$packageOutput = Join-Path $repositoryRoot "UnityPackage\Runtime\Plugins"
$artifactNames = @(
    "GreenBox.I18n.Core.dll",
    "GreenBox.I18n.Core.xml"
)

dotnet build $coreProject --configuration Release --nologo
if ($LASTEXITCODE -ne 0) {
    throw "The GreenBox.I18n.Core Release build failed with exit code $LASTEXITCODE."
}

if ($Verify) {
    foreach ($artifactName in $artifactNames) {
        $builtArtifact = Join-Path $coreOutput $artifactName
        $packagedArtifact = Join-Path $packageOutput $artifactName

        if (!(Test-Path -LiteralPath $packagedArtifact -PathType Leaf)) {
            throw "Packaged artifact is missing: $packagedArtifact"
        }

        $builtHash = (Get-FileHash -LiteralPath $builtArtifact -Algorithm SHA256).Hash
        $packagedHash = (Get-FileHash -LiteralPath $packagedArtifact -Algorithm SHA256).Hash
        if ($builtHash -ne $packagedHash) {
            throw "Packaged artifact is outdated: $packagedArtifact. Run Build-UnityPackage.ps1 without -Verify."
        }
    }

    Write-Host "Unity package artifacts are up to date."
    exit 0
}

New-Item -ItemType Directory -Path $packageOutput -Force | Out-Null

foreach ($artifactName in $artifactNames) {
    $builtArtifact = Join-Path $coreOutput $artifactName
    if (!(Test-Path -LiteralPath $builtArtifact -PathType Leaf)) {
        throw "Expected build artifact was not produced: $builtArtifact"
    }

    Copy-Item -LiteralPath $builtArtifact -Destination $packageOutput -Force
}

Write-Host "Unity package artifacts updated in $packageOutput."
