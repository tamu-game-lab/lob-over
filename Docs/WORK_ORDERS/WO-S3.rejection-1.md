# WO-S3 — rejection 1 (orchestrator, 2026-10-20 20:45, through the Unity MCP)

Both failures happen in the Editor (LOB-OVER, 6000.3.8f1, TMP Essential Resources imported into `Assets/TextMesh Pro`). Fix them in `Assets/LOBOver/Editor/SiteAssetBuilder.cs` and `Assets/LOBOver/Art/`; nothing else changes.

## 1. `LOB Over/Build/Site Scene` throws before saving the scene

Command: `execute_menu_item LOB Over/Build/Site Scene` (after `Sync Data`, which works and logs "LOB Over: catalog, scenarios, scripts and voice synced to StreamingAssets.").

Observed in the console: `Unable to load font face for [LegacyRuntime]. Make sure "Include Font Data" is enabled in the Font Import Settings.` followed by `NullReferenceException: Object reference not set to an instance of an object`; `Assets/LOBOver/Scenes/Site.unity` is not created and the line "LOB Over: Site Scene built. Press Play, choose a mode, then Start." never appears. Stack trace (Editor.log) appended below.

Expected: the scene saved and that log line.

File: `Assets/LOBOver/Editor/SiteAssetBuilder.cs`, `Font()`, line 90: `TMP_FontAsset.CreateFontAsset(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"))` returns null in Unity 6000.3 (the TMP font engine cannot load the built-in font's face), then `font.name = ...` dereferences it.

Fix (addendum "Unity through the MCP", step 2): depend on the TMP Essential Resources instead of a hand-made setup. At the start of `SiteAssetBuilder.Build()`, if `Assets/TextMesh Pro/Resources/TMP Settings.asset` does not exist, call `TMP_PackageResourceImporter.ImportResources(true, false, false)` and `AssetDatabase.Refresh()`. Then use `TMP_Settings.defaultFontAsset` (LiberationSans SDF from the essentials) as the site font for every text and drop the hand-made `TMP_Settings` under `Art/Resources` (lines 75 to 87) and the `SiteFont` creation (lines 88 to 96); if a dedicated font asset is wanted, create it from the essentials' `LiberationSans SDF` source font, never from LegacyRuntime. Delete the generated `Assets/LOBOver/Art/Resources/` folder (it holds that hand-made `TMP Settings.asset`, a second "TMP Settings" in a Resources folder).

## 2. Fourteen TMP shader errors

Command: `read_console types=[error]`.

Observed: 14 lines like `Shader error in 'TextMeshPro/Distance Field': Couldn't open include file 'TMPro_Properties.cginc'. at Assets/TextMesh Pro/Shaders/TMP_SDF.shader(126)` (also Mobile/Distance Field Overlay, 2 Pass, SSD, Masking, both Surface shaders).

Cause: `Assets/LOBOver/Art/Text/TMPro_Properties.cginc` and `TMP_SDF-Mobile.shader` were copied with the package's own `.meta` files (GUIDs `3997e2241185407d80309a82f9148466` and `fe393ace9b354375a9cb14cdbbc28be4`). When the essentials were imported, Unity skipped those two assets because their GUIDs already existed, so `Assets/TextMesh Pro/Shaders` has no `TMPro_Properties.cginc` and no `TMP_SDF-Mobile.shader`, and every TMP shader's include fails. No code references `Art/Text`.

Fix: delete `Assets/LOBOver/Art/Text/` (the six files and `Text.meta`). The orchestrator re-imports the essentials through the Editor afterwards so the two files land in `Assets/TextMesh Pro/Shaders`.

Expected after both fixes: console without `Shader error` and without the exception; `Site Scene` saves `Assets/LOBOver/Scenes/Site.unity` and logs its line; the WO-S3 loop continues (Play screenshots, tests).

## Stack trace of the NullReferenceException (Editor.log)

```
1260:NullReferenceException: Object reference not set to an instance of an object
1261-  at LOBOver.Editor.SiteAssetBuilder.Font () [0x00095] in C:\Users\bryan\Documents\TAMU\02-Research\Creative Care\Unity\LOB-OVER\Assets\LOBOver\Editor\SiteAssetBuilder.cs:91 
1262-  at LOBOver.Editor.SiteAssetBuilder.Build () [0x00051] in C:\Users\bryan\Documents\TAMU\02-Research\Creative Care\Unity\LOB-OVER\Assets\LOBOver\Editor\SiteAssetBuilder.cs:32 
1263-  at LOBOver.Editor.SiteSceneBuilder.Build (System.Boolean saveScene) [0x00005] in C:\Users\bryan\Documents\TAMU\02-Research\Creative Care\Unity\LOB-OVER\Assets\LOBOver\Editor\SiteSceneBuilder.cs
1264-  at LOBOver.Editor.SiteSceneBuilder.BuildMenu () [0x00008] in C:\Users\bryan\Documents\TAMU\02-Research\Creative Care\Unity\LOB-OVER\Assets\LOBOver\Editor\SiteSceneBuilder.cs:23 
1265-UnityEditor.EditorApplication:ExecuteMenuItemInternal_Injected(ManagedSpanWrapper&, Boolean)
1266-UnityEditor.EditorApplication:ExecuteMenuItemInternal(String, Boolean)
1267-UnityEditor.EditorApplication:ExecuteMenuItem(String)
1268-MCPForUnity.Editor.Tools.ExecuteMenuItem:HandleCommand(JObject) (at .\Library\PackageCache\com.coplaydev.unity-mcp@da7b9ae4aeeb\Editor\Tools\ExecuteMenuItem.cs:38)
1269-MCPForUnity.Editor.Tools.CommandRegistry:ExecuteCommand(String, JObject, TaskCompletionSource`1) (at .\Library\PackageCache\com.coplaydev.unity-mcp@da7b9ae4aeeb\Editor\Tools\CommandRegistry.cs:29
1270-MCPForUnity.Editor.Services.Transport.TransportCommandDispatcher:ProcessCommand(String, PendingCommand) (at .\Library\PackageCache\com.coplaydev.unity-mcp@da7b9ae4aeeb\Editor\Services\Transport\T
```
