[CmdletBinding()]
param(
    [string] $RiderHome = $env:RIDER_HOME,

    [switch] $Publish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$version = [IO.File]::ReadAllText((Join-Path $PSScriptRoot "version.txt")).Trim()

if ($Publish -and [string]::IsNullOrWhiteSpace($env:PUBLISH_TOKEN)) {
    throw "PUBLISH_TOKEN is required to publish the Rider plugin."
}

if ([string]::IsNullOrWhiteSpace($RiderHome)) {
    $jetBrainsRoot = Join-Path $env:ProgramFiles "JetBrains"
    if (Test-Path -LiteralPath $jetBrainsRoot -PathType Container) {
        $RiderHome = Get-ChildItem -LiteralPath $jetBrainsRoot -Directory |
            Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName "product-info.json") } |
            ForEach-Object {
                $product = Get-Content -Raw -LiteralPath (Join-Path $_.FullName "product-info.json") |
                    ConvertFrom-Json
                if ($product.productCode -eq "RD") {
                    [pscustomobject]@{
                        Path = $_.FullName
                        Build = [string]$product.buildNumber
                    }
                }
            } |
            Sort-Object Build -Descending |
            Select-Object -First 1 -ExpandProperty Path
    }
}

if ([string]::IsNullOrWhiteSpace($RiderHome) -or
    !(Test-Path -LiteralPath (Join-Path $RiderHome "product-info.json") -PathType Leaf)) {
    throw "Rider was not found. Pass -RiderHome or set RIDER_HOME."
}

$riderProject = Join-Path $repositoryRoot "Ide\Rider"
$gradle = Join-Path $riderProject "gradlew.bat"
$javaHome = Join-Path $RiderHome "jbr"
if (!(Test-Path -LiteralPath $javaHome -PathType Container)) {
    throw "Rider's bundled Java runtime was not found at '$javaHome'."
}

$previousRiderHome = $env:RIDER_HOME
$previousJavaHome = $env:JAVA_HOME
try {
    $env:RIDER_HOME = $RiderHome
    $env:JAVA_HOME = $javaHome
    Push-Location $riderProject
    try {
        $gradleTask = if ($Publish) { "publishPlugin" } else { "buildPlugin" }
        & $gradle clean $gradleTask --no-daemon
        if ($LASTEXITCODE -ne 0) {
            throw "Rider plugin $gradleTask failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}
finally {
    $env:RIDER_HOME = $previousRiderHome
    $env:JAVA_HOME = $previousJavaHome
}

$distributions = Join-Path $riderProject "build\distributions"
$packages = @(Get-ChildItem -LiteralPath $distributions -Filter "*.zip" -File)
if ($packages.Count -ne 1) {
    throw "Expected one Rider plugin ZIP, found $($packages.Count)."
}

$output = Join-Path $repositoryRoot "artifacts\rider\$version"
New-Item -ItemType Directory -Path $output -Force | Out-Null
Copy-Item -LiteralPath $packages[0].FullName -Destination $output -Force

Write-Host "Rider plugin: $(Join-Path $output $packages[0].Name)"
if ($Publish) {
    Write-Host "Rider plugin $version was uploaded to JetBrains Marketplace."
}
