param(
    [string]$UnityEditorData = 'C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$runtime = Get-ChildItem "$UnityEditorData/NetCoreRuntime/shared/Microsoft.NETCore.App" -Directory |
    Sort-Object Name -Descending | Select-Object -First 1
if ($null -eq $runtime) { throw 'Unity .NET runtime not found.' }
$checkDir = Join-Path ([System.IO.Path]::GetTempPath()) ('ClassFPS-GunslingerChecks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $checkDir | Out-Null
$arguments = @('-nologo', '-target:exe', '-nostdlib+', ('-out:"' + $checkDir + '/GunslingerChecks.dll"'))
$arguments += @('System.Private.CoreLib.dll', 'System.Runtime.dll', 'System.Console.dll',
    'System.Runtime.Extensions.dll') | ForEach-Object { '-r:"' + (Join-Path $runtime.FullName $_) + '"' }
$arguments += '"' + (Join-Path $projectRoot 'Assets/Scripts/GunslingerRules.cs') + '"'
$arguments += '"' + (Join-Path $PSScriptRoot 'GunslingerChecks.cs') + '"'
$arguments += @('IDamageable', 'PlayerHealth', 'TargetHealth') | ForEach-Object { '"' + (Join-Path $projectRoot ('Assets/Scripts/' + $_ + '.cs')) + '"' }
$arguments | Set-Content "$checkDir/checks.rsp" -Encoding UTF8
& "$UnityEditorData/NetCoreRuntime/dotnet.exe" "$UnityEditorData/DotNetSdkRoslyn/csc.dll" "@$checkDir/checks.rsp"
if ($LASTEXITCODE -ne 0) { throw 'Gunslinger checks failed to compile.' }
@{ runtimeOptions = @{ tfm = 'net6.0'; framework = @{ name = 'Microsoft.NETCore.App'; version = $runtime.Name } } } |
    ConvertTo-Json -Depth 5 | Set-Content "$checkDir/GunslingerChecks.runtimeconfig.json" -Encoding UTF8
& "$UnityEditorData/NetCoreRuntime/dotnet.exe" "$checkDir/GunslingerChecks.dll"
if ($LASTEXITCODE -ne 0) { throw 'Gunslinger state checks failed.' }
