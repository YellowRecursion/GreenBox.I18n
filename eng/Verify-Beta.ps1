[CmdletBinding()]
param(
    [ValidateSet("win-x64")]
    [string] $Runtime = "win-x64"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$version = [IO.File]::ReadAllText((Join-Path $PSScriptRoot "version.txt")).Trim()
$bundleRoot = Join-Path $repositoryRoot "artifacts\desktop\$Runtime\$version\bundle"
$requiredFiles = @(
    "GreenBox.I18n.exe",
    "i18n.cmd",
    "wwwroot\index.html",
    "release-manifest.json"
)

foreach ($requiredFile in $requiredFiles) {
    $path = Join-Path $bundleRoot $requiredFile
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Desktop bundle is incomplete. Missing: $requiredFile"
    }
}

$hostExecutable = Join-Path $bundleRoot "GreenBox.I18n.exe"
$port = Get-Random -Minimum 20000 -Maximum 40000
$hostUrl = "http://127.0.0.1:$port"
$hostProcess = Start-Process `
    -FilePath $hostExecutable `
    -ArgumentList "host", "--urls", $hostUrl `
    -WorkingDirectory $bundleRoot `
    -WindowStyle Hidden `
    -PassThru

try {
    $health = $null
    for ($attempt = 0; $attempt -lt 100; $attempt++) {
        try {
            $health = Invoke-RestMethod -Uri "$hostUrl/api/health" -TimeoutSec 1
            break
        }
        catch {
            Start-Sleep -Milliseconds 100
        }
    }

    if ($null -eq $health -or
        $health.product -ne "GreenBox.I18n" -or
        $health.status -ne "ready" -or
        $health.version -ne $version) {
        throw "Published Host did not become ready."
    }

    $editorResponse = Invoke-WebRequest -Uri $hostUrl -TimeoutSec 2 -UseBasicParsing
    if ($editorResponse.StatusCode -ne 200 -or $editorResponse.Content -notmatch '<div id="root"></div>') {
        throw "Published Host did not serve the Web editor."
    }
}
finally {
    if (!$hostProcess.HasExited) {
        Stop-Process -Id $hostProcess.Id -Force
        $hostProcess.WaitForExit()
    }
    $hostProcess.Dispose()
}

$generatedId = & (Join-Path $bundleRoot "i18n.cmd") generate-id
if ($LASTEXITCODE -ne 0 -or $generatedId -notmatch '^\d{19}$') {
    throw "Published CLI did not generate an entry ID."
}

$mcpPort = Get-Random -Minimum 41000 -Maximum 49000
$mcpStartInfo = [Diagnostics.ProcessStartInfo]::new()
$mcpStartInfo.FileName = $hostExecutable
$mcpStartInfo.Arguments = "mcp"
$mcpStartInfo.WorkingDirectory = $bundleRoot
$mcpStartInfo.UseShellExecute = $false
$mcpStartInfo.CreateNoWindow = $true
$mcpStartInfo.RedirectStandardInput = $true
$mcpStartInfo.RedirectStandardOutput = $true
$mcpStartInfo.RedirectStandardError = $true
$mcpStartInfo.EnvironmentVariables["GREENBOX_I18N_HOST_URL"] = "http://127.0.0.1:$mcpPort"
$mcpProcess = [Diagnostics.Process]::Start($mcpStartInfo)

try {
    $mcpProcess.StandardInput.WriteLine(
        '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"release-smoke-test","version":"1.0"}}}')
    $mcpProcess.StandardInput.Flush()
    $initializeRead = $mcpProcess.StandardOutput.ReadLineAsync()
    if (!$initializeRead.Wait(15000)) {
        throw "Published MCP initialize request timed out."
    }

    $initializeResponse = $initializeRead.Result
    if ($initializeResponse -notmatch '"id":1' -or
        $initializeResponse -notmatch '"serverInfo"') {
        throw "Published MCP returned an unexpected initialize response: $initializeResponse"
    }

    $mcpProcess.StandardInput.WriteLine(
        '{"jsonrpc":"2.0","method":"notifications/initialized"}')
    $mcpProcess.StandardInput.WriteLine(
        '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}')
    $mcpProcess.StandardInput.Flush()
    $toolsRead = $mcpProcess.StandardOutput.ReadLineAsync()
    if (!$toolsRead.Wait(15000)) {
        throw "Published MCP tools/list request timed out."
    }

    $toolsResponse = $toolsRead.Result
    if ($toolsResponse -notmatch '"id":2' -or
        $toolsResponse -notmatch 'get_workspace') {
        throw "Published MCP returned an unexpected tools response: $toolsResponse"
    }
}
finally {
    if (!$mcpProcess.HasExited) {
        $mcpProcess.Kill()
        $mcpProcess.WaitForExit()
    }
    $mcpProcess.Dispose()
}

Write-Host "GreenBox I18n $version desktop bundle, CLI, Host, Web, and MCP passed production smoke tests."
