# ROADMAP.md — from the table test to the Quest

Phase 0 (WO-00 to WO-05) ends at `READY_FOR_TABLE_TEST`: three browser seats, one Node host, the printed kit, placeholder voice. Bryan runs the table test (three laptops or one laptop plus a phone, the printed kit, two stages, about 25 minutes with the huddle and debrief), fixes the scenario numbers and manual text that the test exposes, and only then does Unity work start. Everything below is written so it can be split into work orders the same way.

## Phase 0.5 — after the table test (one short run)

- WO-05b: retune `Data/Scenarios/*.json` and `Paper/manual_v0.md` from the table-test notes (Bryan edits, Astra regenerates the kit and voice, the orchestrator re-runs the e2e). No code changes expected.
- Decide Stage 3's city and author `stage3_<city>.json` from the Hazard Matrix row once Phumpat confirms it.

## Phase 1 — Unity host, VR seat, Quest client

Principle: the browser pages stay the Plan seat and the 2D Site fallback for good. Unity adds the VR seat and, in the end, hosts the simulation so the demo runs with one PC and one Quest. The protocol does not change.

| WO | Title | What it proves |
| --- | --- | --- |
| WO-06 | GameLogic reuse investigation | A written answer: which of LOB-Game's pure C# GameLogic (determinism contract, seeding, `ToPlanningView`, machine catalog loading) can be brought in as a local package without dragging the single-player UI along, and what the spin-off's additions (orders with lag, stop-work, zones and permits, deviations, per-seat views) need from it. Output: `Docs/GAMELOGIC_REUSE.md` with a file list and a go/no-go per piece. No code. Read-only access to `..\LOB-Game`. |
| WO-07 | C# simulation + parity | `Assets/LOBOver/Sim/` as a pure C# assembly (asmdef, no UnityEngine references) implementing `Docs/PROTOCOL.md` semantics, plus an EditMode test that loads `Data/Scenarios/stage1_texas.json` and `Data/goldens/no_events.json`, runs the headless scripts, and matches the Node transcripts within the golden tolerances. The JS sim stays as the oracle. Data lives in `Data/` and is copied into `Assets/StreamingAssets/Data/` by an editor script at build time. |
| WO-08 | Unity host | A `Host` scene: `websocket-sharp` or `System.Net.WebSockets` server on 7331 speaking the protocol, pacing identical to the Node host, the three web pages served from `Monitor/web/` (copied into StreamingAssets). The Playwright e2e from WO-03 passes unchanged against the Unity host running in the Editor (Play mode) on the PC. |
| WO-09 | VR site scene | `Assets/LOBOver/Scenes/Site.unity` built with Meta XR Building Blocks (camera rig, controllers; hands later): a 6,000 ft trench with 100 ft station stakes, four zone boards, the haul road, the site truck with snap turns and station stops, three crews with machines (LOB-Game's assets imported by the orchestrator into `Assets/LOBOver/Art/`, with their licences noted), a site trailer with the actual-lines board, the job sign. Wrist clipboard: Flag, Stop work, Confirm; gaze-dwell inspect (2 s) with read-aloud text; the argument staging at a conflict. Driven entirely by `state` messages; no game logic in the scene. Orchestrator verification via Unity MCP: open scene, enter Play mode with the Node host running, confirm the client registers as `seat: site`, read the console. |
| WO-10 | Quest client build | Android build for Quest 3S (ARM64, OpenXR, Meta XR), the Site scene as a client that connects to the host PC over the local network (IP entered once on a start panel, remembered), 72 Hz, comfort settings. Verification: APK installs with `adb`, logcat shows the `welcome` message; a human run confirms no nausea triggers (vehicle only, snap turns). |
| WO-11 | Polish and research logging | Mood animation for held crews (visibly angrier, no text), trucks at the gate, water and slump decals keyed to `trench[]` state, the result overlay in VR on the trailer board; one JSONL research log per session (every message with wall-clock and `t`) in `Monitor/out/sessions/` plus the phone-video sync marker (a visible and audible clap cue at Start). |

Order: WO-06 → WO-07 → WO-08 (host) in parallel with WO-09 (scene; it can run against the Node host) → WO-10 → WO-11. The Unity host (WO-08) is the one piece that can be skipped for the demo if time runs short: the Node host on the PC plus the Quest client is a complete setup.

## Phase 2 — Stage 3 and the study

- Three city scenarios, seats rotate, the result overlay compares a team's three runs.
- Hand tracking on the Quest 3S (the four Site verbs map without redesign).
- Recorded voice replaces Kokoro placeholders (same file names).
- Study instruments: pre/post questions on push vs pull, the session logs, the phone video.

## Decisions this roadmap assumes (change them in `Docs/DECISIONS.md`, not here)

- PC hosts; Quest is a client. One protocol, three clients.
- The JS simulation is the oracle for the C# port until the port passes parity; after that the C# sim is the source of truth and the JS one is retired or kept for the table-test kit.
- Browser pages are permanent, not a prototype.
- Controllers first; hands later.
