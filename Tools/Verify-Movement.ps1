param([switch]$SimulatedDelay, [string]$GamePath = '', [string]$RunTag = ('Motion-' + (Get-Date -Format 'yyyyMMdd-HHmmssfff')))
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$gameExe = if ($GamePath) { (Resolve-Path -LiteralPath $GamePath).Path } else { Join-Path $projectRoot 'Builds/Development/DEAD-CROWD.exe' }
if (!(Test-Path -LiteralPath $gameExe)) { throw 'Build the Windows Development player before running session tests.' }
$logRoot = Join-Path $projectRoot 'Logs'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
function Start-Game([string[]]$gameArgs, [string]$logName) {
    $logPath = Join-Path $logRoot $logName
    return Start-Process -FilePath $gameExe -ArgumentList (@('-batchmode', '-nographics', '-logFile', $logPath) + $gameArgs) -WindowStyle Hidden -PassThru
}
function Wait-Game($gameProcess, [string]$logName) {
    if (!$gameProcess.WaitForExit(115000)) { Stop-Process -Id $gameProcess.Id; throw "Timed out: $logName" }
    $gameProcess.Refresh()
    $logPath = Join-Path $logRoot $logName
    Select-String -LiteralPath $logPath -Pattern '\[(MotionSmoke)\] (PASS|FAIL)' | ForEach-Object { $_.Line }
    if ($gameProcess.ExitCode -ne 0) { throw "$logName exit $($gameProcess.ExitCode)" }
    if (!(Select-String -LiteralPath $logPath -Pattern '\[(MotionSmoke)\] PASS')) { throw "$logName missing PASS" }
}
$hostLog = "$RunTag-motion-Host.log"
$clientLog = "$RunTag-motion-Client.log"
$hostArgs = @('--motion-smoke', 'host')
if ($SimulatedDelay) { $hostArgs += '--motion-delay' }
$hostProcess = Start-Game $hostArgs $hostLog
$deadline = [DateTime]::UtcNow.AddSeconds(22)
$inviteCode = $null
do {
    Start-Sleep -Milliseconds 250
    if (Test-Path -LiteralPath (Join-Path $logRoot $hostLog)) {
        $match = Select-String -LiteralPath (Join-Path $logRoot $hostLog) -Pattern '\[Crowd\] Connected mode=Host, room=([A-Z2-9]{6})' | Select-Object -Last 1
        if ($match) { $inviteCode = $match.Matches[0].Groups[1].Value }
    }
} while (!$inviteCode -and [DateTime]::UtcNow -lt $deadline -and !$hostProcess.HasExited)
if (!$inviteCode) { if (!$hostProcess.HasExited) { Stop-Process -Id $hostProcess.Id }; throw "No host invite code" }
$clientArgs = @('--motion-smoke', 'client', '--motion-code', $inviteCode)
if ($SimulatedDelay) { $clientArgs += '--motion-delay' }
$clientProcess = Start-Game $clientArgs $clientLog
try { Wait-Game $hostProcess $hostLog; Wait-Game $clientProcess $clientLog }
finally { foreach ($gameProcess in @($hostProcess, $clientProcess)) { if (!$gameProcess.HasExited) { Stop-Process -Id $gameProcess.Id } } }

