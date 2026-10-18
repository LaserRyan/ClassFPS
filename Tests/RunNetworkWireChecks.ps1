param([string]$Player = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (!$Player) { $Player = Join-Path $projectRoot 'Builds/NetworkWireBuild/ClassFPS.exe' }
$executable = (Resolve-Path -LiteralPath $Player).Path
$logs = Join-Path $projectRoot 'Builds'
$hostLog = Join-Path $logs 'NetworkWireHost.log'
$clientLog = Join-Path $logs 'NetworkWireClient.log'
$hostProcess = $null
$clientProcess = $null
try {
    $hostProcess = Start-Process -FilePath $executable -ArgumentList @('-batchmode', '-nographics', '--classfps-wire-host', '-logFile', ('"' + $hostLog + '"')) -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 2
    $clientProcess = Start-Process -FilePath $executable -ArgumentList @('-batchmode', '-nographics', '--classfps-wire-client', '-logFile', ('"' + $clientLog + '"')) -WindowStyle Hidden -PassThru
    if (!$clientProcess.WaitForExit(45000)) { throw 'Wire client timed out.' }
    if (!$hostProcess.WaitForExit(10000)) { throw 'Wire host timed out.' }
    if ($clientProcess.ExitCode -ne 0 -or $hostProcess.ExitCode -ne 0) { throw "Wire process failed. Inspect $clientLog and $hostLog." }
    $hostResult = Select-String -LiteralPath $hostLog -Pattern 'NETWORK_WIRE_HOST_PASSED'
    $clientResult = Select-String -LiteralPath $clientLog -Pattern 'NETWORK_WIRE_CLIENT_PASSED'
    if (!$hostResult -or !$clientResult) { throw 'Missing wire assertion success markers.' }
    $hostResult.Line
    $clientResult.Line
}
finally {
    foreach ($checkProcess in @($hostProcess, $clientProcess)) {
        if ($null -ne $checkProcess -and !$checkProcess.HasExited) { Stop-Process -Id $checkProcess.Id -Force }
    }
}
