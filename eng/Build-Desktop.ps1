[CmdletBinding()]
param(
    [ValidateSet("win-x64")]
    [string] $Runtime = "win-x64",

    [string] $UpdateSource = $env:GREENBOX_I18N_UPDATE_URL,

    [switch] $SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$version = [IO.File]::ReadAllText((Join-Path $PSScriptRoot "version.txt")).Trim()
$desktopArtifacts = Join-Path $repositoryRoot "artifacts\desktop"
$buildRoot = Join-Path $desktopArtifacts "$Runtime\$version"
$stagingRoot = Join-Path $buildRoot "staging"
$bundleRoot = Join-Path $buildRoot "bundle"

function Assert-ArtifactPath([string] $path) {
    $resolvedArtifacts = [IO.Path]::GetFullPath($desktopArtifacts) + [IO.Path]::DirectorySeparatorChar
    $resolvedPath = [IO.Path]::GetFullPath($path)
    if (!$resolvedPath.StartsWith($resolvedArtifacts, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside desktop artifacts: $resolvedPath"
    }
}

function Invoke-Checked([string] $description, [scriptblock] $command) {
    Write-Host "==> $description"
    & $command
    if ($LASTEXITCODE -ne 0) {
        throw "$description failed with exit code $LASTEXITCODE."
    }
}

function Copy-PublishOutput([string] $source, [string] $destination) {
    $sourcePrefix = [IO.Path]::GetFullPath($source).TrimEnd('\') + '\'
    foreach ($sourceFile in Get-ChildItem -LiteralPath $source -File -Recurse) {
        $relativePath = $sourceFile.FullName.Substring($sourcePrefix.Length)
        $destinationFile = Join-Path $destination $relativePath
        $destinationDirectory = Split-Path -Parent $destinationFile
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null

        if (Test-Path -LiteralPath $destinationFile -PathType Leaf) {
            throw "Publish output unexpectedly contains a duplicate file at '$relativePath'."
        }

        Copy-Item -LiteralPath $sourceFile.FullName -Destination $destinationFile
    }
}

Assert-ArtifactPath $buildRoot
if (Test-Path -LiteralPath $buildRoot) {
    Remove-Item -LiteralPath $buildRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null
New-Item -ItemType Directory -Path $bundleRoot -Force | Out-Null

& (Join-Path $PSScriptRoot "Test-Version.ps1")

if (!$SkipTests) {
    Invoke-Checked "Run .NET tests" {
        dotnet test (Join-Path $repositoryRoot "GreenBox.I18n.sln") -c Release --verbosity minimal
    }
}

$projects = @(
    @{
        Name = "desktop"
        Path = "GreenBox.I18n.Desktop\GreenBox.I18n.Desktop.csproj"
        Properties = @("-p:GreenBoxUpdateFeedUrl=$UpdateSource")
    }
)

foreach ($project in $projects) {
    $projectOutput = Join-Path $stagingRoot $project.Name
    $projectPath = Join-Path $repositoryRoot $project.Path
    $publishArguments = @(
        "publish",
        $projectPath,
        "-c", "Release",
        "-r", $Runtime,
        "--self-contained", "true",
        "-p:PublishSingleFile=false",
        "-p:DebugType=None",
        "-p:DebugSymbols=false",
        "-o", $projectOutput
    ) + $project.Properties

    Invoke-Checked "Publish $($project.Name)" {
        dotnet @publishArguments
    }
    Copy-PublishOutput $projectOutput $bundleRoot
}

$webOutput = Join-Path $repositoryRoot "GreenBox.I18n.Editor.Host\wwwroot"
if (!(Test-Path -LiteralPath (Join-Path $webOutput "index.html") -PathType Leaf)) {
    throw "Web editor output was not produced."
}
Copy-Item -LiteralPath $webOutput -Destination $bundleRoot -Recurse -Force

$cliShim = @'
@echo off
"%~dp0i18n.exe" %*
'@
[IO.File]::WriteAllText(
    (Join-Path $bundleRoot "i18n.cmd"),
    $cliShim.Replace("`n", "`r`n") + "`r`n",
    [Text.ASCIIEncoding]::new())

$manifestFiles = Get-ChildItem -LiteralPath $bundleRoot -File -Recurse |
    Sort-Object FullName |
    ForEach-Object {
        $bundlePrefix = [IO.Path]::GetFullPath($bundleRoot).TrimEnd('\') + '\'
        [ordered]@{
            path = $_.FullName.Substring($bundlePrefix.Length).Replace('\', '/')
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            size = $_.Length
        }
    }

$manifest = [ordered]@{
    product = "GreenBox I18n"
    version = $version
    runtime = $Runtime
    builtAtUtc = [DateTime]::UtcNow.ToString("O")
    files = @($manifestFiles)
}
$manifestPath = Join-Path $bundleRoot "release-manifest.json"
[IO.File]::WriteAllText(
    $manifestPath,
    ($manifest | ConvertTo-Json -Depth 5) + "`n",
    [Text.UTF8Encoding]::new($false))

$portableArchive = Join-Path $buildRoot "GreenBox.I18n-$version-$Runtime-portable.zip"
Compress-Archive -Path (Join-Path $bundleRoot "*") -DestinationPath $portableArchive -CompressionLevel Optimal

Write-Host ""
Write-Host "Desktop bundle: $bundleRoot"
Write-Host "Portable archive: $portableArchive"
