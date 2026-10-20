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
