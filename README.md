# LOB, Over — kickoff kit (v0, October 14, 2026)

Play it in the browser: https://tamu-game-lab.github.io/lob-over/ (the field manual PDF is linked on the landing page)

Three-seat cooperative spin-off of LOB-Game. A VR field inspector (Site), a web scheduler (Plan) and a paper spec-keeper (Rules) deliver one 6,000 ft pipeline job on the PM's pushed plan, then on their own pulled plan, on the same seeded site. The lesson is push versus pull, learned from consequences; nothing in the game says right or wrong.

This folder is everything the orchestrator (Claude Code, Fable 5.1) and the implementer (Codex, Astra) need to start. Its contents go into the root of the new Unity project, beside `Assets/`. Setup is `Docs/SETUP_CHECKLIST.md`; the launch is section D there.

## Presenting from a Mac

1. Install Node.js 20 or newer from https://nodejs.org, then clone with git: `git clone https://github.com/tamu-game-lab/lob-over.git`. A downloaded zip carries Gatekeeper's quarantine. If the launcher will not open, right-click > Open, or run `bash "Play LOB Over.command"` in Terminal from the repository folder.
2. Double-click `Play LOB Over.command`. It installs dependencies on the first run, starts Level 1 and opens the Plan page. Allow node through the macOS firewall prompt. Leave Terminal open; press Ctrl-C there to end the game.
3. Put the Mac and headset on the same network. When the venue's Wi-Fi blocks discovery, use a phone hotspot or the USB cable. For the cable, install adb with `brew install android-platform-tools`, connect the headset and allow USB debugging, then run the launcher.

For a phone or second laptop as Site, open [Level 1, no headset](https://tamu-game-lab.github.io/lob-over/play.html?host=browser), share the Site room code or QR, and press Start. Keep the Plan tab open. The headset joins only a game started with Play LOB Over on a PC or Mac.

To build the complete showcase locally, run `node Showcase/build.mjs`. The publishing script uses this build too.

## What is where

| Path | What | Who writes it |
| --- | --- | --- |
| `CLAUDE.md`, `AGENTS.md` | Orchestrator context; implementer rules (Codex reads AGENTS.md on its own) | fixed |
| `Docs/DESIGN_v0.md` | The design, exported from the Claude doc "LOB Crew Design Proposal v0" (source of truth) | Bryan, via the doc |
| `Docs/ORCHESTRATOR.md` | The operating procedure for the run: dispatch, verify, accept or reject, commit, stop conditions, finish line | fixed |
| `Docs/PROTOCOL.md`, `Docs/SCENARIO_SCHEMA.md` | Host–seat wire protocol; scenario file format | fixed; amended only by decision |
| `Docs/WORK_ORDERS/WO-00..05.md` | Phase 0 work orders with acceptance checks | orchestrator amends, Astra executes |
| `Docs/ROADMAP.md` | Phase 1 (C# port, Unity host, VR scene, Quest client) and Phase 2 | — |
| `Docs/WORKLOG.md`, `Docs/DECISIONS.md` | Running record; decisions taken and questions open | orchestrator |
| `Data/catalog.json` | LOB-Game's machine and protection catalog, the workbook's situation numbers, the Hazard Matrix | Bryan + validators |
| `Data/Scenarios/stage1_texas.json`, `stage2_texas.json` | The two demo stages: crews, plan, permits, events, deviations, inbox, voice lines, kit codes | Bryan + validators |
| `Data/goldens/no_events.json` | Crew-model golden runs the simulation must reproduce (oracle: `Tools/reference/make_goldens.py`) | generated |
| `Paper/manual_v0.md` | The field manual text (Rules seat); `Paper/make_kit.py` (WO-04) typesets it and the cards from the scenario | Bryan + validators |
| `Monitor/` | Node host, JS reference simulation, Plan page, 2D Site page, facilitator page (WO-00 to WO-03) | Astra |
| `Tools/astra.sh` | Dispatch one work order to Codex and log it | fixed |
| `Tools/voice/`, `Voice/` | Kokoro voice placeholders from the scenario's line sheet (WO-05) | Astra |
| `.claude/settings.json` | Pre-approved commands for the unattended run (optional) | Bryan |

## Phase 0 in one paragraph

Before any Unity work, a Node host runs the reference simulation and serves the Plan page and a 2D Site page; Rules plays from the printed kit; the three seats are table-tested over a LAN or Zoom. The simulation is proven against golden runs and a scripted Stage 1 playthrough, the pages against a Playwright test, and every player-facing surface against the no-verdict rule. Finish line: `READY_FOR_TABLE_TEST`. Phase 1 then ports the simulation to C# with the JS run as the oracle, builds the VR site scene with Meta XR Building Blocks, and ships the Quest client; the PC hosts throughout.

## The two launch lines

```
claude --model fable --effort max
/goal Read Docs/ORCHESTRATOR.md and follow it from step 0. You orchestrate; Astra implements through bash Tools/astra.sh. Dispatch WO-00 through WO-05 in order, verify each with its acceptance checks, accept or reject (max two rejection rounds), commit each accepted WO, keep Docs/WORKLOG.md current. Done when WO-00..WO-05 are ACCEPTED and you have printed READY_FOR_TABLE_TEST, or when a stop condition is reported to me with the exact question. Do not write feature code yourself.
```
