# WO-S3 — rejection 2 (orchestrator, 2026-10-20 20:55, through the Unity MCP)

Rejection 1 is fixed: the console is clean, `LOB Over/Build/Site Scene` saves `Assets/LOBOver/Scenes/Site.unity` and logs its line, Play in Desktop mode runs the slice (collapse, argument card at t 4.1, rain card) with no exception, and the world is generated at runtime (30 station cells, three crews at the recorded stations). One defect remains, in the builder's materials.

## Everything built from a LOB-Game ProjectSite material renders magenta

Commands: Play, Desktop, Start through `StartPanel.Begin()`; `LOB Over/Debug/Screenshot` at t 5.05, t 13.5 and t 4.13 (`Monitor/out/unity/site-20261020-204519.png`, `-204554.png`, `-205010.png`); shader report through `execute_code`.

Observed: the ground banks, subsoil, the stretch before the trench, the road, the trailer, the timber posts and the truck frame are solid magenta (Unity's error shader) in every screenshot; the crane, the spoil piles, the workers, the sky and the overlay text render. The report shows why: the materials the builder loads straight from LOB-Game use the Built-in pipeline's `Standard` shader, which URP (`PC_RPAsset`, the project's pipeline) cannot render:

```
Standard :: Soil            @ Assets/LOBGame/_Project/Art/ProjectSite/Materials/Soil.mat            used by FullScaleRoot/FarBank, NearBank, Subsoil, BeforeTrench (and the MRDiorama copies)
Standard :: AccessRoad      @ .../ProjectSite/Materials/AccessRoad.mat      used by FullScaleRoot/Road
Standard :: Repair_SiteCabin@ .../ProjectSite/Materials/Repair_SiteCabin.mat used by FullScaleRoot/Trailer
Standard :: Timber          @ .../ProjectSite/Materials/Timber.mat          used by FullScaleRoot/TimberPost x2
Standard :: StorageSteel    @ .../ProjectSite/Materials/StorageSteel.mat    used by VRSite/FrontPillar, RoofRail, MRDiorama/TruckFrame, Face
Shader Graphs/PhysicalMaterial3DsMax :: Material #29 @ Assets/LOBGame/3D_Models/trafic-cone/source/vlc.fbx (traffic cone; verify it renders, convert the same way if it is magenta)
```

The builder's own materials (`Cloud`, `Ink`, `Rain`, `Water`, `WetSoil`) use URP shaders and are fine.

Expected: no object the builder creates carries a material on `Standard`, `Standard (Specular setup)`, `Legacy Shaders/*` or `Hidden/InternalErrorShader`; the ground, road, trailer and posts show their textures.

File: `Assets/LOBOver/Editor/SiteAssetBuilder.cs`, `Material(name)` (returns the LOB-Game material as is) and `CopyModel` (keeps the copied prefabs' materials as they are).

Fix: never hand a LOB-Game material to a renderer; hand a URP copy. In `Material(name)`, load the LOB-Game material, create or refresh `Assets/LOBOver/Art/Converted/<name>.mat` from it and upgrade the copy with the pipeline's own converter: `UnityEditor.Rendering.MaterialUpgrader.Upgrade(copy, new UnityEditor.Rendering.Universal.StandardUpgrader("Standard"), UnityEditor.Rendering.MaterialUpgrader.UpgradeFlags.None)` (or, if that API is unavailable in URP 17.3, a `Universal Render Pipeline/Lit` material with `_BaseMap` from `_MainTex`, `_BaseColor` from `_Color`, `_BumpMap`, `_MetallicGlossMap`, `_Smoothness` from `_Glossiness`, and the tiling and offset copied). Apply the same conversion to every material of every renderer in the prefabs `CopyModel` writes to `Prefabs/Imported/` and in the cell and crew prefabs, replacing the references in the copies; `Assets/LOBGame/**` stays untouched. Idempotent: rebuilding reuses the converted copies. Add to `SiteSceneBuilderTests` an assertion that, after `Build()`, no material referenced by `WorldAssets`, by the prefabs under `Assets/LOBOver/Prefabs/` or by any renderer in the built scene uses one of the shaders named above.

Not a reject, for the record: the console warns once "Only one instance of OVRCameraRig should be created at once!" during the scene build and the three rig cameras stay active in Desktop mode (the desktop camera still draws the Game view); that belongs to WO-S4's pass.
