#requires -Version 5.1
<#
.SYNOPSIS
    Bitcraft launcher for Windows + Docker Desktop (mirrors run.sh).
.DESCRIPTION
    Wraps `docker compose` with the same modes (core, core+AI, core+AI+GPU,
    core+AI+Jetson) plus status/logs/rebuild/reset operations.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$Project    = 'bitcraft'
$BaseFile   = 'docker-compose.yml'
$DevFile    = 'docker-compose.dev.yml'
$ProdFile   = 'docker-compose.prod.yml'
$AiFile     = 'docker-compose.ai.yml'
$GpuFile    = 'docker-compose.ai.gpu.yml'
$JetsonFile = 'docker-compose.ai.jetson.yml'

function Write-Info ($Message)    { Write-Host "[INFO] $Message" -ForegroundColor Blue }
function Write-Ok   ($Message)    { Write-Host "[OK]   $Message" -ForegroundColor Green }
function Write-Warn2 ($Message)   { Write-Host "[WARN] $Message" -ForegroundColor Yellow }
function Write-Err  ($Message)    { Write-Host "[ERR]  $Message" -ForegroundColor Red }

function Test-DockerReady {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        Write-Err 'Docker CLI not found in PATH.'
        exit 1
    }
    try { docker info 1>$null 2>$null } catch {
        Write-Err 'Docker Desktop / daemon is not running.'
        exit 1
    }
    & docker compose version 1>$null 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Err "'docker compose' plugin is not available."
        exit 1
    }
}

function Invoke-Compose {
    param(
        [Parameter(Mandatory)] [string]   $Mode,
        [Parameter(Mandatory)] [string[]] $Files,
        [Parameter(Mandatory)] [string[]] $Action
    )
    $args = @()
    foreach ($f in $Files) { $args += '-f'; $args += $f }
    $args += '-p'; $args += $Project
    $args += $Action
    Write-Info "Running: docker compose $($args -join ' ')"
    & docker compose @args
    if ($LASTEXITCODE -ne 0) { throw "docker compose exited $LASTEXITCODE" }
    Write-Ok "$Mode complete."
}

function Start-Core         { Invoke-Compose -Mode 'Core'          -Files @($BaseFile)                     -Action @('up','--build','-d') }
function Start-CoreDev      { Invoke-Compose -Mode 'Core+Dev'      -Files @($BaseFile,$DevFile)            -Action @('up','--build','-d') }
function Start-CoreAi       { Invoke-Compose -Mode 'Core+AI'       -Files @($BaseFile,$AiFile)             -Action @('up','--build','-d') }
function Start-CoreAiGpu    { Invoke-Compose -Mode 'Core+AI+GPU'   -Files @($BaseFile,$AiFile,$GpuFile)    -Action @('up','--build','-d') }
function Start-CoreAiJetson { Invoke-Compose -Mode 'Core+AI+Jetson' -Files @($BaseFile,$AiFile,$JetsonFile) -Action @('up','--build','-d') }
function Start-CoreProd     { Invoke-Compose -Mode 'Core+Prod'     -Files @($BaseFile,$ProdFile)           -Action @('up','--build','-d') }

function Show-Status        { Invoke-Compose -Mode 'Status' -Files @($BaseFile,$AiFile,$GpuFile,$JetsonFile) -Action @('ps') }
function Show-Logs          { Invoke-Compose -Mode 'Logs'   -Files @($BaseFile,$AiFile)                    -Action @('logs','-f') }
function Stop-All           { Invoke-Compose -Mode 'Down'   -Files @($BaseFile,$AiFile,$GpuFile,$JetsonFile) -Action @('down') }

function Reset-All {
    Write-Warn2 'This will stop containers AND delete volumes.'
    $confirm = Read-Host 'Type YES to continue'
    if ($confirm -ne 'YES') { Write-Info 'Reset cancelled.'; return }
    Invoke-Compose -Mode 'Reset' -Files @($BaseFile,$AiFile,$GpuFile,$JetsonFile) -Action @('down','-v')
}

function Show-Menu {
    Clear-Host
    Write-Host '===' -ForegroundColor Cyan
    Write-Host '          BITCRAFT LAUNCHER           ' -ForegroundColor Cyan
    Write-Host '===' -ForegroundColor Cyan
    Write-Host ''
    Write-Host '1) Run Core Stack'
    Write-Host '2) Run Core + Dev (HMR)'
    Write-Host '3) Run Core + AI'
    Write-Host '4) Run Core + AI + GPU'
    Write-Host '5) Run Core + AI + Jetson'
    Write-Host '6) Run Core + Prod (nginx)'
    Write-Host '7) Show Status'
    Write-Host '8) Show Logs (Ctrl+C to detach)'
    Write-Host '9) Stop Stack'
    Write-Host 'R) Reset Stack (delete volumes)'
    Write-Host '0) Exit'
    Write-Host ''
}

Test-DockerReady

while ($true) {
    Show-Menu
    $choice = Read-Host 'Select option'
    Write-Host ''
    switch ($choice) {
        '1' { Start-Core }
        '2' { Start-CoreDev }
        '3' { Start-CoreAi }
        '4' { Start-CoreAiGpu }
        '5' { Start-CoreAiJetson }
        '6' { Start-CoreProd }
        '7' { Show-Status }
        '8' { Show-Logs }
        '9' { Stop-All }
        'R' { Reset-All }
        'r' { Reset-All }
        '0' { exit 0 }
        default { Write-Warn2 'Invalid option.' }
    }
    Read-Host 'Press Enter to continue' | Out-Null
}
