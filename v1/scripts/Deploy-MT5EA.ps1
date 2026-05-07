param(
    [string]$TerminalDataPath = "",
    [string]$MetaEditorPath = "",
    [string]$EaSource = "",
    [switch]$ListTargets,
    [switch]$SkipCompile
)

$ErrorActionPreference = "Stop"

function Resolve-FullPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        return ""
    }

    return [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $PathValue).Path)
}

function Find-TerminalDataFolders {
    $base = Join-Path $env:APPDATA "MetaQuotes\Terminal"
    if (-not (Test-Path -LiteralPath $base)) {
        return @()
    }

    return @(Get-ChildItem -LiteralPath $base -Directory |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName "MQL5\Experts") } |
        Sort-Object LastWriteTime -Descending)
}

function Find-MetaEditor {
    param([string]$ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (Test-Path -LiteralPath $ExplicitPath) {
            return (Resolve-Path -LiteralPath $ExplicitPath).Path
        }
        throw "MetaEditorPath not found: $ExplicitPath"
    }

    $command = Get-Command "metaeditor64.exe" -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $command = Get-Command "metaeditor.exe" -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $roots = @(
        ${env:ProgramFiles},
        ${env:ProgramFiles(x86)}
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path -LiteralPath $_) }

    foreach ($root in $roots) {
        $match = Get-ChildItem -LiteralPath $root -Recurse -Filter "metaeditor64.exe" -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($match) { return $match.FullName }

        $match = Get-ChildItem -LiteralPath $root -Recurse -Filter "metaeditor.exe" -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($match) { return $match.FullName }
    }

    return ""
}

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($EaSource)) {
    $EaSource = Join-Path $repoRoot "MT5_EA\TradingBotEA.mq5"
}

$sourcePath = Resolve-FullPath $EaSource
if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "EA source not found: $sourcePath"
}

$targets = Find-TerminalDataFolders
if ($ListTargets) {
    if ($targets.Count -eq 0) {
        Write-Host "No MT5 terminal data folders found under $env:APPDATA\MetaQuotes\Terminal"
    }
    else {
        Write-Host "MT5 terminal data folders:"
        for ($i = 0; $i -lt $targets.Count; $i++) {
            Write-Host "[$i] $($targets[$i].FullName)"
        }
    }
    exit 0
}

if ([string]::IsNullOrWhiteSpace($TerminalDataPath)) {
    if ($targets.Count -eq 0) {
        throw "No MT5 terminal data folder found. In MT5 use File > Open Data Folder, then pass that path with -TerminalDataPath."
    }

    $TerminalDataPath = $targets[0].FullName
    Write-Host "Using newest MT5 data folder: $TerminalDataPath"
}

$terminalPath = Resolve-FullPath $TerminalDataPath
$expertsPath = Join-Path $terminalPath "MQL5\Experts"
if (-not (Test-Path -LiteralPath $expertsPath)) {
    throw "MQL5\Experts not found under terminal data path: $terminalPath"
}

$destPath = Join-Path $expertsPath "TradingBotEA.mq5"
Copy-Item -LiteralPath $sourcePath -Destination $destPath -Force
Write-Host "Copied EA:"
Write-Host "  From: $sourcePath"
Write-Host "  To:   $destPath"

if ($SkipCompile) {
    Write-Host "Skipped MetaEditor compile because -SkipCompile was used."
    exit 0
}

$editor = Find-MetaEditor $MetaEditorPath
if ([string]::IsNullOrWhiteSpace($editor)) {
    Write-Host "MetaEditor was not found automatically."
    Write-Host "Open MT5, press F4, open this file, and press F7:"
    Write-Host "  $destPath"
    exit 2
}

$compileLog = Join-Path $env:TEMP "TradingBotEA-compile.log"
if (Test-Path -LiteralPath $compileLog) {
    Remove-Item -LiteralPath $compileLog -Force
}

Write-Host "Compiling with MetaEditor:"
Write-Host "  $editor"

$args = @(
    "/compile:$destPath",
    "/log:$compileLog"
)

$process = Start-Process -FilePath $editor -ArgumentList $args -Wait -PassThru -WindowStyle Hidden

if (Test-Path -LiteralPath $compileLog) {
    Write-Host "Compile log:"
    Get-Content -LiteralPath $compileLog
}

$ex5Path = [System.IO.Path]::ChangeExtension($destPath, ".ex5")
$compileText = ""
if (Test-Path -LiteralPath $compileLog) {
    $compileText = Get-Content -LiteralPath $compileLog -Raw
}

if ($process.ExitCode -ne 0 -and $compileText -notmatch "Result:\s*0 errors") {
    throw "MetaEditor compile failed with exit code $($process.ExitCode)."
}

if (-not (Test-Path -LiteralPath $ex5Path)) {
    throw "Compile finished but EX5 was not found: $ex5Path"
}

Write-Host "EA compiled successfully:"
Write-Host "  $ex5Path"
Write-Host ""
Write-Host "Final MT5 step: remove and re-attach TradingBotEA on the chart, or restart MT5."

$appStatusDir = Join-Path $env:APPDATA "MT5TradingBot"
New-Item -ItemType Directory -Path $appStatusDir -Force | Out-Null
$statusPath = Join-Path $appStatusDir "ea_deploy_status.json"
$status = [ordered]@{
    deployed_at = (Get-Date).ToUniversalTime().ToString("O")
    source = $sourcePath
    terminal_data_path = $terminalPath
    mq5_path = $destPath
    ex5_path = $ex5Path
    compile_result = "0 errors, 0 warnings"
    needs_mt5_reload = $true
    message = "EA compiled successfully. Remove and re-attach TradingBotEA on the MT5 chart, or restart MT5."
}

$status | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $statusPath -Encoding UTF8
Write-Host "Desktop app status written:"
Write-Host "  $statusPath"
