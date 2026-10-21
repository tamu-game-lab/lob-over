# Work orders

One file per bounded job for Astra. The orchestrator dispatches them in order with `bash Tools/astra.sh WO-xx`, verifies with the acceptance checks written in the WO, and records the decision in `Docs/WORKLOG.md`. Rejections are written as `WO-xx.rejection-N.md` beside the WO. Logs of each dispatch go to `logs/`.

| WO | Title | Depends on | Folders |
| --- | --- | --- | --- |
| WO-00 | Scaffold `Monitor/`: package, scripts, seeded RNG, test runner | — | `Monitor/` |
| WO-01 | Scenario and catalog loader + validator | WO-00 | `Monitor/src/scenario/` |
| WO-02 | Reference simulation (JS) + headless runner + golden parity | WO-01 | `Monitor/src/sim/`, `Monitor/test/` |
| WO-03 | Node host (WebSocket + static), Plan page, 2D Site page, facilitator page, Playwright e2e | WO-02 | `Monitor/src/host/`, `Monitor/web/`, `Monitor/e2e/` |
| WO-04 | Printed kit generator (manual, cards, ledger, tokens) to PDF | WO-01 | `Paper/` |
| WO-05 | Voice line batch with Kokoro | WO-01 | `Tools/voice/`, `Voice/` |
| WO-05a | Simulation pass before the table test (O10, O11, O13, review follow-ups) | WO-03 | `Monitor/src/sim/`, `Paper/make_kit.py` |
| WO-S1 | Web showcase: in-browser host, seats in tabs, phone by room code, autoplan, GitHub Pages | WO-05a | `Monitor/`, `Showcase/`, `Tools/publish_showcase.ps1` |
| WO-S2 | C# simulation as a plain .NET library with parity tests | WO-05a (transcripts) | `Assets/LOBOver/Sim/`, `Sim/` |
| WO-S3 | The site in Unity from LOB-Game's assets, embedded simulation, autoplan director, desktop preview | WO-S2, copied assets | `Assets/LOBOver/` |
| WO-S4 | Start panel, VR Site seat, MR Diorama, Android build checks | WO-S3 | `Assets/LOBOver/` |
| WO-S5 | One menu item that builds the APK and runs it on the headset | WO-S4 | `Assets/LOBOver/Editor/QuestBuild.cs` |
| WO-S6 | Building Blocks foundation: no code-built rig, no falling, a non-destructive builder | WO-S5 | `Assets/LOBOver/` |
| WO-S7 | Guided three-seat playthrough on the web, Rules page | WO-S1 | `Monitor/`, `Paper/make_kit.py` |
| WO-S8 | On hold, Level 1 first. Passthrough hub and warp into the site | WO-S6 | `Assets/LOBOver/` |
| WO-S9 | On hold, Level 1 first. Team game in the headset (client of the Node host), guide prompts | WO-S7, WO-S8 | `Assets/LOBOver/` |
| WO-S10 | On hold, Level 1 first. Voices and arguments (O12 expanded), JS and C# in parity | WO-S9 | `Monitor/src/sim/`, `Assets/LOBOver/`, `Tools/voice/`, scenario lines |
| WO-L1 | Level 1 on the PC: scenario, rule flags, voices, Plan page, Rules page, start script, discovery | WO-S7 | `Monitor/`, `Data/`, `Tools/voice/`, `Paper/`, `Play LOB Over.cmd` |
| WO-L2 | Level 1 in the headset: Bryan's Play.unity, passthrough hub, host connection, warp, voices | WO-L1, Bryan's Play.unity | `Assets/LOBOver/` |
| WO-L3 | The Plan seat feels like a game: drafting board, radio console, PM portrait, sound | WO-L1 | `Monitor/web/play.html`, `Monitor/web/img/` |
| WO-L4 | The site looks real: SiteLit shader, textures, ground, trench, props, sky, light, budget | WO-L2 | `Assets/LOBOver/`, `Tools/import_lobgame_assets.ps1` |
| WO-L5 | The site materializes around the player (wave, wire and blueprint, no mask), watchdog, logs, preview composites | WO-L4 | `Assets/LOBOver/` |
| WO-06 to WO-10 | Superseded (VR seat as a client of a LAN host); their world and verb sections are cited by WO-S3 and WO-S4 | — | — |
| WO-07 | Quest client core (WebSocket, protocol, site-state model, HUD) | WO-06, imported assets | `Assets/LOBOver/Runtime/Client/` |
| WO-08 | The site in VR: scene built by script, world in step with `state` | WO-07 | `Assets/LOBOver/Runtime/World/`, `Assets/LOBOver/Editor/` |
| WO-09 | The inspector's verbs on the Quest controllers | WO-08 | `Assets/LOBOver/Runtime/VR/` |
| WO-10 | Quest build and install | WO-09 | `Assets/LOBOver/Editor/QuestBuild.cs`, `Builds/` |

Phase 1 is the VR slice (`Docs/VR_SLICE.md`, `Docs/ORCHESTRATOR_PHASE1.md`); the C# port and the Unity host are Phase 2 (`Docs/ROADMAP.md`).

## Template for new work orders

```
# WO-xx — <title>
Goal (one paragraph: what exists when this is done and how the orchestrator will know)
Read: exact files/sections
You may change: exact paths
Build: numbered, concrete, no design choices left open
Acceptance checks: commands with expected output; the orchestrator runs these verbatim
Out of scope: what not to do
```
