param([string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/6000.3.8f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
Set-Location -LiteralPath $projectRoot
$scratch = Join-Path $PSScriptRoot '.offline'
New-Item -ItemType Directory -Path $scratch -Force | Out-Null
$generated = [System.Collections.Generic.List[string]]::new()
$script:compiledCount = 0
function Compile-Assembly([string]$name, [string]$folder, [string[]]$references, [string]$defines = 'UNITY_EDITOR;UNITY_6000_3_OR_NEWER;UNITY_INCLUDE_TESTS') {
    # Match Unity's declared assembly graph; a broad compiler reference set can hide missing asmdef references.
    $definition = Get-ChildItem -LiteralPath $folder -Filter '*.asmdef' | Select-Object -First 1
    if ($definition) {
        $asm = Get-Content -LiteralPath $definition.FullName -Raw | ConvertFrom-Json
        $references = @($baseReferences)
        if (!$asm.noEngineReferences) { $references += $engineReferences }
        foreach ($dependency in $asm.references) {
            $compiled = Join-Path $scratch ($dependency + '.dll')
            $references += $(if (Test-Path -LiteralPath $compiled) { $compiled } else { 'Library/ScriptAssemblies/' + $dependency + '.dll' })
        }
        if ($asm.optionalUnityReferences -contains 'TestAssemblies') {
            $references += 'Library/ScriptAssemblies/UnityEngine.TestRunner.dll', 'Library/ScriptAssemblies/UnityEditor.TestRunner.dll'
        }
        if ($asm.precompiledReferences -contains 'nunit.framework.dll') {
            $references += (Get-ChildItem 'Library/PackageCache/com.unity.ext.nunit*/net40/unity-custom/nunit.framework.dll').FullName
        }
    }
    $output = Join-Path $scratch ($name + '.dll')
    $response = Join-Path $scratch ($name + '.rsp')
    $generated.Add($output)
    $generated.Add($response)
    $argsForCompiler = @('-nologo', '-target:library', '-langversion:9', '-nostdlib+', ('-define:' + $defines), ('-out:"' + $output + '"'))
    foreach ($reference in $references) { $argsForCompiler += '-r:"' + $reference + '"' }
    foreach ($source in (Get-ChildItem -LiteralPath $folder -Recurse -Filter '*.cs' | Where-Object { $folder -notmatch 'Runtime/(VR|MR|Start)$' -or $_.FullName -notmatch '[\\/]Editor[\\/]' })) { $argsForCompiler += '"' + $source.FullName + '"' }
    $argsForCompiler | Set-Content -LiteralPath $response -Encoding UTF8
    & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll') ('@' + $response)
    if ($LASTEXITCODE -ne 0) { throw "Offline compilation failed: $name" }
    $script:compiledCount++
    Write-Output "$name`: 0 errors"
}
try {
    $baseReferences = @((Join-Path $UnityData 'NetStandard/ref/2.1.0/netstandard.dll'))
    $baseReferences += (Get-ChildItem (Join-Path $UnityData 'NetStandard/compat/2.1.0/shims/netfx') -Filter '*.dll').FullName
    $jsonDll = (Get-ChildItem 'Library/PackageCache/com.unity.nuget.newtonsoft-json*/Runtime/Newtonsoft.Json.dll').FullName
    $baseReferences += $jsonDll
    $sdkAssemblies = @(Get-ChildItem 'Library/ScriptAssemblies/Oculus.Interaction*.dll', 'Library/ScriptAssemblies/Meta.XR.BuildingBlocks*.dll')
    $sdkPackages = @(Get-ChildItem 'Library/PackageCache' -Directory | Where-Object Name -Like 'com.meta.xr.sdk.interaction*')
    $sdkSources = @(Get-ChildItem $sdkPackages.FullName -Recurse -Filter '*.cs')
    foreach ($required in @('PointableCanvas.cs', 'RayInteractable.cs', 'PokeInteractable.cs', 'FirstPersonLocomotor.cs', 'CharacterController.cs', 'PlayerLocomotor.cs')) {
        if (!($sdkSources | Where-Object Name -EQ $required)) { throw "Missing installed Interaction SDK source: $required" }
    }
    if ($sdkAssemblies.Count -eq 0) { throw 'Missing imported Interaction SDK / Building Blocks assemblies' }
    Write-Output "SDK inputs: $($sdkAssemblies.Count) imported assemblies; $($sdkSources.Count) installed package sources (no restore)."
    $engineReferences = (Get-ChildItem (Join-Path $UnityData 'Managed/UnityEngine') -Filter '*.dll').FullName
    Compile-Assembly 'LOBOver.Sim' 'Assets/LOBOver/Sim' $baseReferences
    $runtimeReferences = $baseReferences + $engineReferences + @((Join-Path $scratch 'LOBOver.Sim.dll'))
    Compile-Assembly 'LOBOver.Director' 'Assets/LOBOver/Runtime/Director' $runtimeReferences
    $worldReferences = $runtimeReferences + @((Join-Path $scratch 'LOBOver.Director.dll'), 'Library/ScriptAssemblies/Unity.TextMeshPro.dll', 'Library/ScriptAssemblies/Unity.InputSystem.dll', 'Library/ScriptAssemblies/UnityEngine.UI.dll')
    Compile-Assembly 'LOBOver.XR' 'Assets/LOBOver/Runtime/XR' $worldReferences
    Compile-Assembly 'LOBOver.XR.Android' 'Assets/LOBOver/Runtime/XR' $worldReferences 'UNITY_ANDROID;UNITY_6000_3_OR_NEWER'
    Compile-Assembly 'LOBOver.World' 'Assets/LOBOver/Runtime/World' $worldReferences
    # Compile the conditional Android reader too, without opening the Editor or changing build settings.
    Compile-Assembly 'LOBOver.Director.Android' 'Assets/LOBOver/Runtime/Director' $runtimeReferences 'UNITY_ANDROID;UNITY_6000_3_OR_NEWER'
    Compile-Assembly 'LOBOver.World.Android' 'Assets/LOBOver/Runtime/World' $worldReferences 'UNITY_ANDROID;UNITY_6000_3_OR_NEWER'
    # SDK assemblies are already installed/imported; this check never restores packages.
    $xrReferences = $worldReferences + @((Join-Path $scratch 'LOBOver.World.dll'),
        'Library/ScriptAssemblies/Oculus.VR.dll', 'Library/ScriptAssemblies/Unity.XR.OpenXR.dll',
        'Library/ScriptAssemblies/Unity.XR.Management.dll')
    foreach ($assembly in @('VR', 'MR', 'Start')) {
        $folder = 'Assets/LOBOver/Runtime/' + $assembly
        if (!(Test-Path -LiteralPath $folder)) { continue }
        Compile-Assembly ('LOBOver.' + $assembly) $folder $xrReferences
        Compile-Assembly ('LOBOver.' + $assembly + '.Android') $folder $xrReferences 'UNITY_ANDROID;UNITY_6000_3_OR_NEWER'
        $xrReferences += (Join-Path $scratch ('LOBOver.' + $assembly + '.dll'))
    }
    $editorReferences = $worldReferences + @((Join-Path $scratch 'LOBOver.World.dll'), 'Library/ScriptAssemblies/Unity.RenderPipelines.Core.Runtime.dll')
    Compile-Assembly 'LOBOver.Editor' 'Assets/LOBOver/Editor' $editorReferences
    $xrEditorReferences = $xrReferences + $editorReferences + @((Join-Path $scratch 'LOBOver.Editor.dll'),
        'Library/ScriptAssemblies/Unity.XR.Management.Editor.dll', 'Library/ScriptAssemblies/Unity.XR.OpenXR.Editor.dll',
        'Library/ScriptAssemblies/Unity.XR.OpenXR.Features.MetaQuestSupport.dll')
    Compile-Assembly 'LOBOver.XR.Editor' 'Assets/LOBOver/Runtime/Start/Editor' $xrEditorReferences
    $testReferences = $xrEditorReferences + @((Join-Path $scratch 'LOBOver.Editor.dll'), 'Library/ScriptAssemblies/UnityEngine.TestRunner.dll', 'Library/ScriptAssemblies/UnityEditor.TestRunner.dll')
    $testReferences += (Get-ChildItem 'Library/PackageCache/com.unity.ext.nunit*/net40/unity-custom/nunit.framework.dll').FullName
    Compile-Assembly 'LOBOver.EditModeTests' 'Assets/LOBOver/Tests/EditMode' $testReferences
    Compile-Assembly 'LOBOver.PlayModeTests' 'Assets/LOBOver/Tests/PlayMode' $testReferences
    Write-Output "PASS: $script:compiledCount offline assembly compilations; Unity import, tests and rendering still require the Editor."
    $fixtureProgram = Join-Path $scratch 'VerifyFixtures.dll'
    $fixtureResponse = Join-Path $scratch 'VerifyFixtures.rsp'
    $fixtureConfig = Join-Path $scratch 'VerifyFixtures.runtimeconfig.json'
    $fixtureJson = Join-Path $scratch 'Newtonsoft.Json.dll'
    foreach ($file in @($fixtureProgram, $fixtureResponse, $fixtureConfig, $fixtureJson)) { $generated.Add($file) }
    $fixtureArgs = @('-nologo', '-target:exe', '-langversion:9', '-nostdlib+', ('-out:"' + $fixtureProgram + '"'), ('-r:"' + (Join-Path $scratch 'LOBOver.Sim.dll') + '"'))
    foreach ($reference in $baseReferences) { $fixtureArgs += '-r:"' + $reference + '"' }
    $fixtureArgs += '-r:"' + (Join-Path $scratch 'LOBOver.VR.dll') + '"'
    $fixtureArgs += '"' + (Join-Path $PSScriptRoot 'VerifyFixtures.cs.txt') + '"'
    $fixtureArgs += '"' + (Join-Path $PSScriptRoot 'VerifyPresenter.cs.txt') + '"'
    $fixtureArgs | Set-Content -LiteralPath $fixtureResponse -Encoding UTF8
    & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll') ('@' + $fixtureResponse)
    if ($LASTEXITCODE -ne 0) { throw 'Fixture verifier did not compile' }
    Copy-Item -LiteralPath (Join-Path $UnityData 'DotNetSdkRoslyn/csc.runtimeconfig.json') -Destination $fixtureConfig
    Copy-Item -LiteralPath $jsonDll -Destination $fixtureJson
    & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') $fixtureProgram
    if ($LASTEXITCODE -ne 0) { throw 'Fixture replay failed' }
}
finally {
    # Only exact outputs from this invocation are removed; never traverse another workspace.
    foreach ($file in $generated) { if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file -Force } }
    if ((Get-ChildItem -LiteralPath $scratch -Force | Measure-Object).Count -eq 0) { Remove-Item -LiteralPath $scratch }
}
