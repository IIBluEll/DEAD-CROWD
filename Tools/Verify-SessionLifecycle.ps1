param([string[]]$Cases = @('lifecycle', 'ui', 'delay', 'timeout', 'load-failure', 'host-close', 'host-timeout', 'loading-leave', 'countdown-leave', 'result-close'), [string]$RunTag = ('Session-' + (Get-Date -Format 'yyyyMMdd-HHmmssfff')), [string]$GamePath = '')
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
    Select-String -LiteralPath $logPath -Pattern '\[(SessionSmoke|UiSmoke)\] (PASS|FAIL)' | ForEach-Object { $_.Line }
    if ($gameProcess.ExitCode -ne 0) { throw "$logName exit $($gameProcess.ExitCode)" }
    if (!(Select-String -LiteralPath $logPath -Pattern '\[(SessionSmoke|UiSmoke)\] PASS')) { throw "$logName missing PASS" }
}
foreach ($case in $Cases) {
    Write-Output "Running $case"
    if ($case -eq 'lifecycle') {
        $singleLog = "$RunTag-Lifecycle.log"
        $single = Start-Game @('--session-smoke', 'lifecycle') $singleLog
        Wait-Game $single $singleLog
        continue
    }
    $hostLog = "$RunTag-$case-Host.log"
    $clientLog = "$RunTag-$case-Client.log"
    if ($case -eq 'ui') { $hostArgs = @('--ui-smoke', 'host') } else { $hostArgs = @('--session-smoke', 'host', '--session-case', $case) }
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
    if (!$inviteCode) { if (!$hostProcess.HasExited) { Stop-Process -Id $hostProcess.Id }; throw "No host code: $case" }
    if ($case -eq 'ui') { $clientArgs = @('--ui-smoke', 'client', '--ui-code', $inviteCode) } else { $clientArgs = @('--session-smoke', 'client', '--session-case', $case, '--session-code', $inviteCode) }
    $clientProcess = Start-Game $clientArgs $clientLog
    try { Wait-Game $hostProcess $hostLog; Wait-Game $clientProcess $clientLog }
    finally { foreach ($gameProcess in @($hostProcess, $clientProcess)) { if (!$gameProcess.HasExited) { Stop-Process -Id $gameProcess.Id } } }
}
