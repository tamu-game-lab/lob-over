# ORCHESTRATOR_PHASE1.md — addendum for the VR slice (WO-06 to WO-10)

> Retired on October 20, 2026. The showcase track uses `Docs/ORCHESTRATOR_SHOWCASE.md` instead (no Unity MCP, no batch builds, no adb). Kept for the LAN study setup later.

`Docs/ORCHESTRATOR.md` still applies: dispatch with `bash Tools/astra.sh`, verify, accept or reject, two rounds, commit, worklog, stop conditions. This addendum changes what "verify" means once Unity is involved and adds the rules of the Unity Editor.

## Order and prerequisites

WO-05a (sim fixes) and WO-06 (host autoplay) first; they need no Unity. Then WO-07 → WO-08 → WO-09 → WO-10, one at a time. Before WO-07, check Bryan's manual steps from `Docs/VR_SLICE.md` section C are done: the imported assets exist (`ls Assets/_Project/Art/Prefabs/Equipment` shows 8 prefabs), `Packages/manifest.json` has `com.unity.nuget.newtonsoft-json` (add `"com.unity.nuget.newtonsoft-json": "3.2.1"` yourself if not; Unity resolves it when the Editor regains focus), `adb devices` lists the headset (WO-10 only). If the assets are missing, stop and tell Bryan; do not export them from LOB-Game yourself.

## The Unity Editor

- Exactly one Unity Editor instance, open on this project, for the whole Phase 1 run. The Unity MCP (`com.coplaydev.unity-mcp`, already in the manifest) needs it; `Docs/WORKLOG.md` says how it was registered for Claude Code in this project. Confirm the MCP responds (a read of the console or the scene hierarchy) at step 0 and record it in the worklog.
- Astra never runs Unity. It writes C#, editor scripts and tests; you run them. Every scene and prefab Astra produces must come from an editor script or a menu item (`LOB Over/Build/...`), so you can regenerate it and so nothing depends on hand edits in the Editor.
- After Astra's dispatch returns, give the Editor focus (or ask the MCP to refresh) so it compiles; read the console; a compile error is a reject with the first error line quoted.
- Run tests through the MCP's test runner (EditMode and PlayMode suites under `Assets/LOBOver/Tests`) or through the Unity CLI if the MCP lacks it; never both at once; never with a second Editor.
- Play mode checks: start the Node host from your shell first (`node Monitor/src/host/server.js Data/Scenarios/slice_texas.json --autoplan Monitor/test/scripts/slice_autoplan.json &`), then enter Play through the MCP, wait the stated seconds, trigger `LOB Over/Debug/Screenshot` through the MCP's menu execution, exit Play, read the PNG from `Monitor/out/unity/` (you can look at images), and read the console. Stop the host afterwards.
- Builds (WO-10) run through the MCP's menu execution with the Editor open; never `-batchmode` on this project while the Editor is open.
- Memory: Claude Code is started with `CLAUDE_CODE_DISABLE_BG_SHELL_PRESSURE_REAP=1`; if a dispatch shell is reaped anyway, the Codex process survives, so watch the log file and continue as in Phase 0.

## What only Bryan can verify

WO-09 and WO-10 end with a human in the headset. When a WO's acceptance says "Bryan in the headset", do everything else first, then print one message that says exactly what he should do and what to report, and stop the run (a stop condition, not a failure). Resume with the same `/goal` after he answers; his answer is recorded in the worklog as the check's result.

## Rejection notes for Unity work

Quote the console line, the test name and the assertion, or describe the screenshot (what is at the station, what is missing). Do not open prefab or scene YAML to review; review behaviour.

## Finish line for the slice

Print `READY_FOR_HEADSET_TEST` when WO-05a, WO-06, WO-07, WO-08, WO-09 and WO-10 are ACCEPTED, the APK is installed, and `Docs/WORKLOG.md` has a "VR slice complete" entry with the commit hash and the build file name.
