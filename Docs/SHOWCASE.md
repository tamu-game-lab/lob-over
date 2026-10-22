# SHOWCASE.md — the stakeholder vertical slice

Revised October 20, 2026. This replaces the "VR slice as a client of a local host" track in `Docs/VR_SLICE.md` sections B to E (kept for the study setup later). The showcase has three pieces, each self-contained, none needing a server on a LAN:

1. **A web page on GitHub Pages.** The simulation runs in the browser. One tab is the Plan seat, another the Site seat, a third the facilitator; a phone can join the same run as the Site seat by room code. Plan can run on autopilot, so one presenter can show the whole chain alone. Links to the field manual (PDF) and the design summary sit on the landing page.
2. **The field manual as a PDF**, generated from the scenario (already done by `Paper/make_kit.py`), published with the page.
3. **A Quest 3S app you build yourself with Build and Run.** The simulation is embedded (a C# port proven against the JavaScript one), so the headset needs no PC. Two modes on the start panel: **VR Site**, the inspector seat at full scale, and **MR Diorama**, passthrough with the site as a two-metre model on the table in front of you, the LOB chart floating above it, crews moving, the collapse, the argument, the rain, pointable with the controller. The diorama is the stakeholder mode; the VR seat is the study mode.

The three-minute slice (`Data/Scenarios/slice_texas.json`, measured in `Docs/VR_SLICE.md`) is the default scenario in all three; the two Texas stages remain selectable.

## What you do (numbered, in order)

1. PowerShell in `LOB-OVER`, Unity closed: `git add -A; git commit -m "Showcase plan, slice, work orders"`.
2. `dotnet --version`. If it fails: `winget install Microsoft.DotNet.SDK.8`, reopen PowerShell. (The C# simulation is built and tested with `dotnet`, outside Unity, so the orchestrator can verify it.)
3. Launch and paste the first goal (below). Walk away; it runs WO-05a, WO-S1, WO-S2 with no Unity. Two to four hours.
4. When it prints `READY_FOR_PAGES`: one public repository holds everything, `https://github.com/tamu-game-lab/lob-over` (source on `main`, the built site on `gh-pages`). The second goal connects the local repository to it, pushes `main`, publishes `gh-pages` with `Tools\publish_showcase.ps1`, and turns Pages on through `gh` if it is installed and signed in; otherwise it tells you the three clicks (Settings → Pages → Deploy from a branch → `gh-pages` → `/ (root)`). Be at the PC for the first push: Git may open a GitHub sign-in window. The site is `https://tamu-game-lab.github.io/lob-over/`.
5. Launch the second goal (below). It publishes the site, then copies the LOB-Game art into this project, writes the Unity code and the scene builder, and stops when it needs you. One to two days with your checks in between.
6. When it says "check in Unity": open LOB-OVER in Unity 6000.3.8f1, menu **LOB Over → Build → Site Scene**, open `Assets/LOBOver/Scenes/Site.unity`, press **Play**. Desktop preview: the chain plays by itself (collapse Day 4, argument Day 5, rain Day 9, finish Day 24); WASD and mouse to look. Report what you saw in one line; the run continues.
7. When it prints `READY_FOR_HEADSET`: headset plugged in, developer mode on; File → Build Profiles → Android → **Build and Run**. Put on the headset, pick MR Diorama. That is the stakeholder demo. Pick VR Site for the inspector seat.
8. Each later change to a scenario, the manual or the voice lines: `python Paper\make_kit.py Data\Scenarios\slice_texas.json`, `.venv\Scripts\python Tools\voice\make_voice.py Data\Scenarios\slice_texas.json`, `Tools\publish_showcase.ps1`, and Build and Run again. Nothing else.

## The two goals

First (web and simulation, Unity closed):

```
/goal Read Docs/ORCHESTRATOR.md and Docs/ORCHESTRATOR_SHOWCASE.md and follow them. Dispatch WO-05a, then WO-S1, then WO-S2 through bash Tools/astra.sh; verify each with its acceptance checks; accept or reject (max two rounds); commit each; keep Docs/WORKLOG.md current. Done when all three are ACCEPTED and READY_FOR_PAGES is printed, or a stop condition is reported to me.
```

Second (publish, then Unity; Unity may be open or closed, the orchestrator checks):

```
/goal Read Docs/ORCHESTRATOR.md and Docs/ORCHESTRATOR_SHOWCASE.md and follow them, including the addendum's "Publishing" section: connect origin to https://github.com/tamu-game-lab/lob-over.git, push main, publish gh-pages with Tools/publish_showcase.ps1, enable Pages through gh if available, put the Pages link in README.md, and verify the site answers. Then run Tools/import_lobgame_assets.ps1 and dispatch WO-S3 then WO-S4 through bash Tools/astra.sh; verify what can be verified without Unity, then stop and ask me to check in Unity exactly as the addendum says; accept or reject (max two rounds); commit and push each; keep Docs/WORKLOG.md current. Done when the site is live, both WOs are ACCEPTED and READY_FOR_HEADSET is printed, or a stop condition is reported to me.
```

## What is in and what is out

In: the three seats' views and verbs as designed; the slice and both Texas stages; the no-verdict rule; the manual PDF; the result card; voice placeholders; LOB-Game's machines, workers, pipe, site materials and sounds; passthrough diorama; the inspector seat.

Out, until after the showcase: the Node host for LAN study sessions (still works, unchanged), the paper kit at the table (the PDF stands in), hand tracking, Stage 3 cities, recorded voice, a shared Plan seat on a second PC (the room code covers a phone Site seat; a second Plan device is Phase 2).

## Work orders

| WO | What exists when it is accepted | Verified by |
| --- | --- | --- |
| WO-05a | The simulation fixes already decided (O10, O11, O13) | `npm test`, headless runs |
| WO-S1 | The web showcase: in-browser host, three seats in tabs, room code for a phone, autoplan, landing page, manual PDF, `Showcase/dist/`, publish script | Playwright against a static server; the Pages URL after you publish |
| WO-S2 | The C# simulation as a plain .NET library with xunit parity tests against the JavaScript transcripts, consumed by Unity through an asmdef | `dotnet test` |
| WO-S3 | The site in Unity from LOB-Game's assets, driven by the embedded simulation and an autoplan director; desktop preview with keyboard verbs | compile check; you in Play mode |
| WO-S4 | Start panel with the three modes; VR Site seat; MR Diorama; Android build settings | compile check; you with Build and Run |


## Third goal (when the Unity MCP is connected; replaces the Editor checks you were asked to do by hand)

Preparation, once: in Unity (LOB-OVER open, ECLIPSE closed) open Window → MCP for Unity and confirm the server is running on 127.0.0.1:8081; register it for Claude Code with the command that window shows for "Claude Code" (HTTP transport), or `claude mcp add --transport http unity-lobover http://127.0.0.1:8081/mcp`; `/mcp` inside Claude Code must list it as connected. Import TMP Essentials if Unity asks (Window → TextMeshPro → Import TMP Essential Resources). Start Claude Code with `$env:CLAUDE_CODE_DISABLE_BG_SHELL_PRESSURE_REAP=1; claude --model fable --effort max`.

```
/goal Read Docs/ORCHESTRATOR.md and Docs/ORCHESTRATOR_SHOWCASE.md, section "Unity through the MCP", and follow it. Commit the pending Docs changes and WO-S5 first. Using the unity-lobover MCP, verify and fix WO-S3 (console, Sync Data, Site Scene, Play with screenshots at 25 s and 60 s, tests), then WO-S4 (start panel, Android settings check), dispatching Astra through bash Tools/astra.sh for any fix larger than one line; accept, commit and push each. Then dispatch WO-S5, verify it, and with the headset connected execute LOB Over/Build/Build and Run Quest through the MCP. Stop only when the app is running on the headset and ask me for the VR Site and MR Diorama check in four lines. Done when READY_FOR_HEADSET is printed or a stop condition is reported to me.
```

## Round 3 goals (October 21)

Paste one goal per session, in order; each ends with a token and a short request to you. Start Claude Code with `$env:CLAUDE_CODE_DISABLE_BG_SHELL_PRESSURE_REAP=1; claude --model claude-opus-5-5 --effort max`, the LOB-OVER Editor open with the MCP running.

Goal A:
```
/goal Read Docs/ORCHESTRATOR.md and Docs/ORCHESTRATOR_SHOWCASE.md (sections "Unity through the MCP" and "Round 3") and follow them, starting with the three "Before anything else" steps. Run Goal A: WO-S6 and WO-S7 in parallel through bash Tools/astra.sh, verified as the addendum says, accepted or rejected (max two rounds), committed and pushed; republish the site after WO-S7; Build and Run Quest; then ask me for the headset check and the laptop playthrough and print ROUND3_A_WAITING. Done when ROUND3_A_WAITING is printed or a stop condition is reported to me.
```

Goal B:
```
/goal Follow Docs/ORCHESTRATOR.md and Docs/ORCHESTRATOR_SHOWCASE.md "Round 3", Goal B. My replies to Goal A are below; record them and fix what I report first. Then WO-S8, Build and Run, ask me for the hub and warp check, and print ROUND3_B_WAITING. Done when ROUND3_B_WAITING is printed or a stop condition is reported to me. My replies:
```

Goal C:
```
/goal Follow Docs/ORCHESTRATOR.md and Docs/ORCHESTRATOR_SHOWCASE.md "Round 3", Goal C. My replies to Goal B are below; record them and fix what I report first. Then WO-S9, Build and Run, ask me for the three-seat guided playthrough with the headset, and print ROUND3_C_WAITING. Done when ROUND3_C_WAITING is printed or a stop condition is reported to me. My replies:
```

Goal D:
```
/goal Follow Docs/ORCHESTRATOR.md and Docs/ORCHESTRATOR_SHOWCASE.md "Round 3", Goal D. My replies to Goal C are below; record them and fix what I report first. Then WO-S10, Build and Run, ask me to listen to the slice in the headset, and print ROUND3_D_WAITING. Done when ROUND3_D_WAITING is printed or a stop condition is reported to me. My replies:
```

## Round 4 goal, Level 1 (October 21)

Round 3's Goals B to D are on hold. Before pasting:
1. In Unity: File > New Scene > Empty. In Building Blocks add Camera Rig, Passthrough, Controller Tracking, Hand Tracking and Interactions Rig. No Real Hands, no demo blocks. Save as `Assets/LOBOver/Scenes/Play.unity`.
2. Keep the Editor open with the MCP running and the headset plugged in.
3. Start Claude Code with `$env:CLAUDE_CODE_DISABLE_BG_SHELL_PRESSURE_REAP=1; claude --model claude-opus-5-5 --effort max`.

```
/goal Read Docs/ORCHESTRATOR.md and Docs/ORCHESTRATOR_SHOWCASE.md (sections "Unity through the MCP" and "Round 4") and follow them. Confirm Play.unity holds the five blocks, then run WO-L1 and WO-L2 through bash Tools/astra.sh, verified as Round 4 says (max two rejection rounds each), committed and pushed; Build and Run Quest; start Play LOB Over.cmd and leave it running; then ask me for the PC check and the five headset checks and print LEVEL1_WAITING. Done when LEVEL1_WAITING is printed or a stop condition is reported to me.
```

## Round 5 goal, Level 1 looks and feels right (October 21)

Before pasting:
1. Optional, 5 minutes, makes the site look better: in Unity's Package Manager (My Assets) import the free "Terrain Sample Asset Pack" (Unity Technologies) and a free skybox set such as "AllSky Free" (rpgwhitelock). WO-L4 uses any terrain layers and cubemap skybox it finds under `Assets/`; without them it generates its own textures and sky.
2. Keep the Editor open with the MCP running and the headset plugged in (only for the log of your last test).
3. Start Claude Code with `$env:CLAUDE_CODE_DISABLE_BG_SHELL_PRESSURE_REAP=1; claude --model claude-fable-5-1 --effort max` (or `claude-opus-5-5` if Fable's credits run out).

```
/goal Read Docs/ORCHESTRATOR.md and Docs/ORCHESTRATOR_SHOWCASE.md (sections "Unity through the MCP" and "Round 5") and follow them, starting with rule 0. Run WO-L3 and WO-L4 in parallel, then WO-L5, through bash Tools/astra.sh, each verified in the Editor as Round 5 says (max two rejection rounds), committed and pushed. Do not build for the Quest. Finish with Sync Data, the ASTC override, a push, and print LEVEL1_READY_TO_BUILD with my build steps. Done when LEVEL1_READY_TO_BUILD is printed or a stop condition is reported to me.
```

When it prints `LEVEL1_READY_TO_BUILD`: in Unity, `LOB Over > Build > Build and Run Quest`; double-click `Play LOB Over.cmd`; put on the headset.

## Round 6 goal, the tunnel, the chart, the site in front of you, the web version (October 22)

If not done already, in Unity, Play.unity: expand `[BuildingBlock] Camera Rig` > `[BuildingBlock] OVRComprehensiveInteractionRig` > `Locomotor`, select `SmoothMovementTunneling` and `WallPenetrationTunneling`, untick the checkbox at the top of the Inspector for each, save the scene. That alone removes the circle from the next build; WO-L7 makes it permanent.

```
/goal Read Docs/ORCHESTRATOR.md and Docs/ORCHESTRATOR_SHOWCASE.md (sections "Unity through the MCP", "Round 5" rules, "Round 6" and "Finishing a goal") and follow them, starting with Round 6 step 1. Run WO-L7 alone, then WO-L8 and WO-L6 in parallel, through bash Tools/astra.sh, each verified in the Editor through the head camera as the orders say (max two rejection rounds), committed and pushed. Do not build for the Quest. Finish with Sync Data, the ASTC override, the site republished, a push, and my build steps. Done when the last line of your final message is exactly ROUND6_READY_TO_BUILD or exactly STOPPED.
```

## Round 7 goal, WO-L8 round 4 and the headset by link (October 22)

Before pasting, once: in PowerShell in the repo folder run `npx wrangler login` (it opens a browser; sign in or create a free Cloudflare account and approve; Node is already on the PC). Then paste:

```
/goal Read Docs/ORCHESTRATOR.md and Docs/ORCHESTRATOR_SHOWCASE.md (sections "Unity through the MCP", "Round 5" rules, "Round 7" and "Finishing a goal") and follow them, starting with Round 7 step 1. I authorize WO-L8 round 4 under K51 to K53. Run WO-L8 round 4 and WO-L9 (relay and web) in parallel, then WO-L9's headset part, through bash Tools/astra.sh, verified as the orders say (max two rejection rounds for WO-L9). Do not build for the Quest. Finish with Sync Data, the ASTC override, the site republished, the relay deployed, every host stopped, a push, and my steps. Done when the last line of your final message is exactly ROUND7_READY_TO_BUILD or exactly STOPPED.
```

When it prints `ROUND7_READY_TO_BUILD`: in Unity, `LOB Over > Build > Build and Run Quest`; on any machine open https://tamu-game-lab.github.io/lob-over/ and choose "Level 1, no headset" (the name changes to "Level 1" once the relay is in); put on the headset and touch the room it shows.
