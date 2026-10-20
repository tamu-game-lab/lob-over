# WO-S3 — The site in Unity: LOB-Game's assets, the embedded simulation, an autoplan director, desktop preview

## Goal

Open the project, run **LOB Over → Build → Site Scene**, open `Assets/LOBOver/Scenes/Site.unity`, press Play: the slice runs by itself (Plan on autopilot through the C# `Autoplan`), the site shows LOB-Game's machines, workers, pipe and materials at 1 ft = 0.3048 m, every cell, board, crew and sky follows the simulation's Site view, the LOB chart with Plan's view hangs by the trailer, and a desktop camera lets anyone watch and try the Site verbs with the keyboard. No XR yet (WO-S4).

## Read

- `AGENTS.md`, `Docs/ORCHESTRATOR_SHOWCASE.md`, `Docs/SHOWCASE.md`
- `Docs/WORK_ORDERS/WO-08.md` sections 1 to 6: the world specification (stations, crews, sky, sound, trailer board, SiteWorld) applies verbatim, with `Director` in place of `SiteClient`
- `Docs/WORK_ORDERS/WO-S2.md` (the sim API you call), `Docs/PROTOCOL.md` "Per-seat views"
- The copied assets under `Assets/LOBGame/` (same relative paths as in LOB-Game: `_Project/Art/Prefabs/Equipment/*.prefab`, `_Project/Art/Prefabs/Workers/ProjectWorker.prefab`, `_Project/Art/ProjectSite/**`, `3D_Models/**`, `Audio/Construction/*`, `PM/*.png`). `Assets/LOBGame/_Project/Code` does not exist; strip missing-script components on instances you create.

## You may change

- `Assets/LOBOver/Runtime/World/**`, `Assets/LOBOver/Runtime/Director/**`, `Assets/LOBOver/Editor/**`, `Assets/LOBOver/Scenes/Site.unity` (builder output), `Assets/LOBOver/Prefabs/**`, `Assets/LOBOver/Art/**`, `Assets/StreamingAssets/Data/**` and `Assets/StreamingAssets/Voice/**` (copied by your editor script; gitignored), `Assets/LOBOver/Tests/**`

## Build

1. **Data sync.** `LOB Over → Build → Sync Data` copies `Data/catalog.json`, `Data/Scenarios/*.json`, `Monitor/test/scripts/*.json` and `Voice/**/*.wav` into `Assets/StreamingAssets/`; the scene builder calls it. On Android read StreamingAssets with `UnityWebRequest`; in the Editor with `File`.
2. **Director** (`Assets/LOBOver/Runtime/Director/Director.cs`): loads the catalog and the chosen scenario (default `slice_texas`), creates `LOBOver.Sim.Sim`, paces it with `Pacer` at the scenario's seconds per day, applies `Autoplan` with the scenario's default script (`slice_autoplan.json`; `stage1_pm_play`/`stage2_pull_play` minus their `site` entries for the stages), exposes `SiteView` and `PlanView` each frame, `Start/Pause/Reset/Load(scenarioId)`, `Speed`, `DisplayDay`, and a `Send(cmd)` for Site commands from the world. Events and voice messages from `Drain()` go to C# events. Transcripts are written to `Application.persistentDataPath/transcripts/`.
3. **World.** `SiteWorld` and `StationCell`, `CrewRig`, sky, sound, trailer board exactly as WO-08 sections 2 to 6 (same names, same assets, same status-to-animation mapping, same mood postures); fed by `Director` instead of a network client.
4. **Plan panel.** A world-space board (3 × 1.5 m) on the trailer's far wall drawing `PlanView`: planned lines per crew, reported actual lines (stepped), the promise and deadline rules, the budget strip, the last three inbox lines, the radio log, the Respond/keypad items shown as text only (no interaction in this WO). Showcase only: in the study the Plan seat is the web page; say so in a comment.
5. **Desktop preview.** `DesktopPreview` prefab: a fly camera (WASD, right mouse to look, Shift fast), keys: `1..9` ride to stake ×100 ft... use `[`/`]` previous/next stake, `D` dismount/mount, `I` inspect what the crosshair hits (same targets as WO-09 section 4), `F` flag, `S` stop work, `C` confirm first landed order, `N` confirm not carried out, `Space` start/pause, `R` reset, `L` listen. An on-screen text (top left) with day, speed, truck station, last inspection text, last card; nothing judging anything.
6. **Builder.** `LOB Over → Build → Site Scene` creates `Site.unity` with: light, ground, road, gate, trailer with the two boards, job sign, spoil piles, `Director`, `SiteWorld`, `DesktopPreview`, an `AudioListener` on the preview camera, URP volume as the project has. Idempotent: running it again rebuilds the same scene.
7. **Tests.** EditMode `SiteSceneBuilderTests` (object names and counts after the builder runs; the imported prefabs load from their paths). PlayMode `SiteWorldTests` feeding recorded Site views from `Data/transcripts/slice_play.json` (days 1, 4, 5, 9, 16): cell 5+20 slumped with standing water on day 4, the pipe crew `stopped`, backfill `arguing` on day 5, rain on day 9, board 2 "no permit posted" on day 1 and "TX-0312" on day 9, the trailer board shows three lines on day 16. `LOB Over → Debug → Screenshot` saves `Monitor/out/unity/<name>.png`.

## Acceptance checks

```
# Orchestrator, Editor closed: batchmode compile per ORCHESTRATOR_SHOWCASE.md -> no "error CS"
# Bryan, Editor open (the orchestrator stops and asks): Build → Site Scene; open Site.unity; Play; after 20 s the collapse at 5+20 (slumped wall, water, crane stopped), then two foremen face to face, rain after about 45 s, the result on the Plan panel after about 3 minutes; keys work; no console errors. His one-line reply is the result.
git status --short     # only Assets/LOBOver/** (StreamingAssets ignored)
```

## Out of scope

No XR, no controllers, no Plan interaction on the panel, no Android settings, no edits to `Assets/LOBGame/**`.
