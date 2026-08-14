[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $repositoryRoot "GreenBox.I18n.Editor.Web"
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) "GreenBox.I18n"
$stagingRoot = Join-Path $temporaryRoot ("web-build-" + [Guid]::NewGuid().ToString("N"))
$resolvedOutput = [IO.Path]::GetFullPath($OutputPath)

New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

try {
    $rootFiles = @(
        "index.html",
        "package.json",
        "package-lock.json",
        "tsconfig.json",
        "tsconfig.app.json",
        "tsconfig.node.json",
        "vite.config.ts"
    )

    foreach ($rootFile in $rootFiles) {
        Copy-Item -LiteralPath (Join-Path $sourceRoot $rootFile) -Destination $stagingRoot
    }

    Copy-Item `
        -LiteralPath (Join-Path $sourceRoot "src") `
        -Destination (Join-Path $stagingRoot "src") `
        -Recurse
    Copy-Item `
        -LiteralPath (Join-Path $sourceRoot "public") `
        -Destination (Join-Path $stagingRoot "public") `
        -Recurse
    if (!(Test-Path -LiteralPath (Join-Path $stagingRoot "src\main.tsx") -PathType Leaf)) {
        throw "Web source staging is incomplete."
    }

    npm ci --prefix $stagingRoot
    if ($LASTEXITCODE -ne 0) {
        throw "npm ci failed with exit code $LASTEXITCODE."
    }

    Push-Location $stagingRoot
    try {
        npm run lint
        if ($LASTEXITCODE -ne 0) {
            throw "Web lint failed with exit code $LASTEXITCODE."
        }

        npm run build -- --outDir $resolvedOutput --emptyOutDir
        if ($LASTEXITCODE -ne 0) {
            throw "Web build failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}
finally {
    $resolvedStaging = [IO.Path]::GetFullPath($stagingRoot)
    $resolvedTemporaryRoot = [IO.Path]::GetFullPath($temporaryRoot) + [IO.Path]::DirectorySeparatorChar
    if ($resolvedStaging.StartsWith($resolvedTemporaryRoot, [StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolvedStaging)) {
        Remove-Item -LiteralPath $resolvedStaging -Recurse -Force
    }
}
