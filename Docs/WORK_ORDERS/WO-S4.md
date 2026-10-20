# WO-S4 — Start panel, VR Site seat, MR Diorama, Android build settings

## Goal

Build and Run from Unity puts an app on the Quest 3S that starts on a panel with three choices: **VR Site** (the inspector seat at full scale, controllers), **MR Diorama** (passthrough, the site as a two-metre model on the table, the LOB chart above it, pointable), **Desktop** (WO-S3's preview, for the Editor). The simulation runs inside the app with Plan on autopilot; the slice is the default, the two Texas stages are selectable. Comfort first.

## Read

- `AGENTS.md`, `Docs/ORCHESTRATOR_SHOWCASE.md`, `Docs/SHOWCASE.md`
- `Docs/WORK_ORDERS/WO-09.md` sections 1 to 7: the VR Site seat specification applies verbatim with `Director.Send` in place of `SiteClient` calls and the voice from `StreamingAssets/Voice`
- `Docs/DESIGN_v0.md` "The VR seat"
- Meta XR SDK in `Packages/com.meta.xr.sdk.*`: `OVRCameraRig`, `OVRManager`, `OVRInput`, `OVRPassthroughLayer`; `Assets/Scenes/SampleScene.unity` has the project's Building Blocks (camera rig, controllers) whose settings you copy; do not add Interaction SDK or hand tracking
- `ProjectSettings/ProjectSettings.asset`, `Assets/XR/Settings/OpenXRPackageSettings.asset`, `Assets/Oculus/OculusProjectConfig.asset` (set by the Project Setup Tool; read, do not edit)

## You may change

- `Assets/LOBOver/Runtime/VR/**`, `Assets/LOBOver/Runtime/MR/**`, `Assets/LOBOver/Runtime/Start/**`, `Assets/LOBOver/Prefabs/**`, `Assets/LOBOver/Editor/SiteSceneBuilder.cs` (add the rig, truck, start panel, diorama root), `Assets/LOBOver/Editor/BuildChecks.cs`, `Assets/LOBOver/Tests/**`, `ProjectSettings/EditorBuildSettings.asset` (scene list: `Site.unity` first)

## Build

1. **Start panel.** A world-space panel 1.5 m ahead at eye height when the app starts (and in Play mode): three big buttons (VR Site, MR Diorama, Desktop), a scenario row (Slice, Stage 1, Stage 2), an Autoplan toggle (on), a Start button. Pointing a controller ray and pulling the trigger presses; in the Editor the mouse works. Chosen mode and scenario persist in PlayerPrefs. Desktop is hidden on Android.
2. **VR Site.** WO-09 sections 1 to 7 (truck cab with the rig, fade ride, dismount, gaze inspect with the clipboard, three wrist buttons, cards, voice, comfort), driven by `Director`. Recenter on the start panel's Start.
3. **MR Diorama.** `OVRPassthroughLayer` as underlay with the camera's background cleared to transparent (the Building Block "Passthrough" settings copied by script); `DioramaRoot` holds a second instance of `SiteWorld` at scale `2.0 m / (lengthFt × 0.3048)` (the slice's 914 m becomes 2 m) with the Plan panel floating 0.4 m above the far end at 0.6 m wide and the trailer board at the near end; placed 0.7 m in front of the user at 0.85 m height on Start, re-placed with the A button, moved by holding the grip and dragging. Pointing the ray at a crew, a cell or a board and pulling the trigger shows that target's `inspect` text on a label above it for 6 s (the diorama is a presenter's view: it may also show Plan's panel, which the full-scale seat never does; comment this in code). Wrist panel: Play/Pause, Speed (slice 4 s per day, half, double), Reset, scenario. Event cards float above the diorama for 10 s; the foremen's argument lines play from the model's position. Crews and machines keep their animations; particles scale with the root.
4. **Audio.** The same clips as WO-S3; in the diorama, attenuation ranges scaled with the root.
5. **Build checks.** `LOB Over → Build → Check Android Settings`: Android, ARM64, IL2CPP, min SDK 32, OpenXR loader with the Meta Quest feature group and the Quest 3S target, `Site.unity` first in the scene list, texture compression ASTC; each missing item is one line in the console telling Bryan where to set it; nothing is changed silently. `OVRManager.display.displayFrequency = 72` at start; fixed foveated rendering level 2.
6. **Tests.** PlayMode `StartPanelTests` (the three modes activate the right roots and nothing else), `DioramaScaleTests` (the root scale makes the slice 2.0 m ± 0.01 and Stage 1 4.0 m: longer jobs scale to 4 m, the table limit), `InputLayerTests` from WO-09 section 8 with the fake input.

## Acceptance checks

```
# Orchestrator, Editor closed: batchmode compile -> no "error CS"
# Bryan, Editor open: Build → Site Scene; Play with Quest Link: start panel usable with the controllers; VR Site: ride to 5+20, dismount, look at the wall, Stop work at the collapse, listen to the argument, Confirm at the excavator; MR Diorama: passthrough shows the room, the site on the table, pointing a crew shows its text, the chart updates by the day; no discomfort. Then Build Profiles → Android → Build and Run; the same on the headset. His reply is the result.
git status --short     # only Assets/LOBOver/**, ProjectSettings/EditorBuildSettings.asset
```

## Out of scope

No hand tracking, no scene understanding or table detection, no Plan interaction in the headset, no networking, no store packaging.
