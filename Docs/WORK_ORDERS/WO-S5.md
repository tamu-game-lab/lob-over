# WO-S5 — One menu item that builds the APK and runs it on the headset

## Goal

`LOB Over → Build → Build and Run Quest` builds `Builds/LOBOver-<yyyyMMdd-HHmm>.apk` (Android, the project's current player settings) and installs and starts it on the connected Quest in one go, so the orchestrator can trigger it through the Unity MCP and Bryan only puts the headset on. `LOB Over → Build → Build Quest APK` does the same without running.

## Read

- `AGENTS.md`, `Docs/ORCHESTRATOR_SHOWCASE.md`
- `Assets/LOBOver/Editor/BuildChecks.cs` (WO-S4's Android settings check; call it first and abort on any failure)
- `ProjectSettings/EditorBuildSettings.asset` (scene list; `Site.unity` must be first and enabled)

## You may change

- `Assets/LOBOver/Editor/QuestBuild.cs` (new), `Assets/LOBOver/Editor/BuildChecks.cs` (only to expose a `static bool Passes(out string report)` if it does not have one), `.gitignore` (`Builds/` if missing)

## Build

1. `QuestBuild.cs`: two `[MenuItem]`s under `LOB Over/Build/`. Both: run the Android settings check and abort with its report in the console if it fails; make sure `Assets/LOBOver/Scenes/Site.unity` exists (else log "run LOB Over/Build/Site Scene first" and abort); if the active build target is not Android, switch it with `EditorUserBuildSettings.SwitchActiveBuildTarget` and return (the switch reimports; the user re-runs the item); create `Builds/`; `BuildPipeline.BuildPlayer` with the enabled scenes, `BuildTarget.Android`, `BuildOptions.None` for the APK item and `BuildOptions.AutoRunPlayer` for the Build and Run item; log the summary (result, size, time, output path) as one line; on failure log the first error step.
2. Before Build and Run, check `adb devices` through `System.Diagnostics.Process` using the Android SDK's `platform-tools/adb` that Unity's `AndroidExternalToolsSettings.sdkRootPath` points at; if no device is listed, log "no Quest connected (developer mode on, cable in, allow USB debugging in the headset)" and build without running.
3. Write `Builds/last-build.txt` with the output path, the git short hash and the time; the start panel's `BuildInfo` (WO-S4) may read the hash from the resources file it already uses.

## Acceptance checks

```
# Orchestrator through the Unity MCP: execute LOB Over/Build/Check Android Settings -> no failures in the console
# execute LOB Over/Build/Build and Run Quest with the headset connected -> console shows the one-line summary with "Succeeded", Builds/LOBOver-*.apk exists, the app starts on the headset (adb logcat -d | grep -c LOBOver > 0 within 30 s)
git status --short     # only Assets/LOBOver/Editor/QuestBuild.cs (+ BuildChecks.cs, .gitignore)
```

## Out of scope

No signing beyond the debug keystore, no settings changes other than the build target switch, no IL2CPP or SDK installation (if the check reports them missing, Bryan installs them through the Hub).
