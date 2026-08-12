[CmdletBinding()]
param(
    [string] $RiderHome = $env:RIDER_HOME,

    [string] $Token = $env:PUBLISH_TOKEN
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Token)) {
    throw "Pass -Token or set PUBLISH_TOKEN to a JetBrains Marketplace permanent token."
}

$previousToken = $env:PUBLISH_TOKEN
try {
    $env:PUBLISH_TOKEN = $Token
    & (Join-Path $PSScriptRoot "Build-Rider.ps1") -RiderHome $RiderHome -Publish
    if ($LASTEXITCODE -ne 0) {
        throw "Rider publication failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:PUBLISH_TOKEN = $previousToken
}
