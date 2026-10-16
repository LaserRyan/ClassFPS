param([string]$UnityEditorData = 'C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$checkDir = Join-Path ([IO.Path]::GetTempPath()) ('ClassFPS-Compile-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $checkDir | Out-Null
$references = @(Get-ChildItem "$UnityEditorData/NetStandard/ref/2.1.0" -Filter '*.dll')
$references += @(Get-ChildItem "$UnityEditorData/Managed/UnityEngine" -Filter '*.dll')
$referenceArgs = @($references | ForEach-Object { '-r:"' + $_.FullName + '"' })
$referenceArgs += '-r:"' + (Join-Path $projectRoot 'Library/ScriptAssemblies/Unity.InputSystem.dll') + '"'
$sources = @(Get-ChildItem "$projectRoot/Assets/Scripts" -Filter '*.cs' | ForEach-Object { '"' + $_.FullName + '"' })
foreach ($configuration in @('Editor', 'Development', 'Release')) {
    $output = Join-Path $checkDir "Runtime-$configuration.dll"
    $arguments = @('-nologo', '-target:library', '-nostdlib+', ('-out:"' + $output + '"')) + $referenceArgs
    if ($configuration -eq 'Editor') { $arguments += '-define:UNITY_EDITOR' }
    if ($configuration -eq 'Development') { $arguments += '-define:DEVELOPMENT_BUILD' }
    $arguments += $sources
    $arguments | Set-Content "$checkDir/runtime.rsp" -Encoding UTF8
    & "$UnityEditorData/NetCoreRuntime/dotnet.exe" "$UnityEditorData/DotNetSdkRoslyn/csc.dll" "@$checkDir/runtime.rsp"
    if ($LASTEXITCODE -ne 0) { throw "$configuration runtime compilation failed." }
}
$editorRefs = @(Get-ChildItem "$UnityEditorData/Managed" -Filter 'UnityEditor*.dll' | Where-Object { $_.Name -ne 'UnityEditor.dll' })
$arguments = @('-nologo', '-target:library', '-nostdlib+', '-define:UNITY_EDITOR', ('-out:"' + $checkDir + '/Editor.dll"')) + $referenceArgs
$arguments += @($editorRefs | ForEach-Object { '-r:"' + $_.FullName + '"' })
$arguments += '-r:"' + $checkDir + '/Runtime-Editor.dll"'
$arguments += @(Get-ChildItem "$projectRoot/Assets/Scripts/Editor" -Filter '*.cs' | ForEach-Object { '"' + $_.FullName + '"' })
$arguments | Set-Content "$checkDir/editor.rsp" -Encoding UTF8
& "$UnityEditorData/NetCoreRuntime/dotnet.exe" "$UnityEditorData/DotNetSdkRoslyn/csc.dll" "@$checkDir/editor.rsp"
if ($LASTEXITCODE -ne 0) { throw 'Editor compilation failed.' }
Write-Output 'Runtime Editor/development/release and separate Editor compilation passed.'
