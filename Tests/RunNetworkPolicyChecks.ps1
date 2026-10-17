param([string]$UnityEditorData = 'C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$runtime = Get-ChildItem "$UnityEditorData/NetCoreRuntime/shared/Microsoft.NETCore.App" -Directory |
    Sort-Object Name -Descending | Select-Object -First 1
$checkDir = Join-Path ([IO.Path]::GetTempPath()) ('ClassFPS-NetworkChecks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $checkDir | Out-Null
foreach ($configuration in @('Editor', 'Development', 'Release')) {
    $arguments = @('-nologo', '-target:exe', '-nostdlib+', '-main:NetworkPolicyChecks', ('-out:"' + $checkDir + '/Checks.dll"'))
    $arguments += @('System.Private.CoreLib.dll', 'System.Runtime.dll', 'System.Console.dll', 'System.Runtime.Extensions.dll') |
        ForEach-Object { '-r:"' + (Join-Path $runtime.FullName $_) + '"' }
    if ($configuration -eq 'Editor') { $arguments += '-define:UNITY_EDITOR' }
    if ($configuration -eq 'Development') { $arguments += '-define:DEVELOPMENT_BUILD' }
    $arguments += @('IDamageable','PlayerHealth','TargetHealth','GunslingerRules','NetworkTestRules','NetworkRevolverState') |
        ForEach-Object { '"' + (Join-Path $projectRoot ('Assets/Scripts/' + $_ + '.cs')) + '"' }
    $arguments += '"' + (Join-Path $PSScriptRoot 'GunslingerChecks.cs') + '"'
    $arguments += '"' + (Join-Path $PSScriptRoot 'NetworkPolicyChecks.cs') + '"'
    $arguments | Set-Content "$checkDir/checks.rsp" -Encoding UTF8
    & "$UnityEditorData/NetCoreRuntime/dotnet.exe" "$UnityEditorData/DotNetSdkRoslyn/csc.dll" "@$checkDir/checks.rsp"
    if ($LASTEXITCODE -ne 0) { throw "$configuration network checks failed to compile." }
    @{ runtimeOptions = @{ tfm = 'net6.0'; framework = @{ name = 'Microsoft.NETCore.App'; version = $runtime.Name } } } |
        ConvertTo-Json -Depth 5 | Set-Content "$checkDir/Checks.runtimeconfig.json" -Encoding UTF8
    & "$UnityEditorData/NetCoreRuntime/dotnet.exe" "$checkDir/Checks.dll"
    if ($LASTEXITCODE -ne 0) { throw "$configuration network checks failed." }
}
