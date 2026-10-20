# VR_SLICE.md — a playable three-minute slice with the headset

Written October 20, 2026, after Phase 0. Section A (testing today's browser build with a team on a LAN) and the slice numbers stand. Sections B to E (the headset as a client of a LAN host) are superseded by `Docs/SHOWCASE.md`, which embeds the simulation in the headset; they are kept because WO-S3 and WO-S4 reuse their specifications. The design, protocol and rules do not change; the slice is a shorter scenario and a Unity client.

## The slice

`Data/Scenarios/slice_texas.json`: the Stage 1 world cut in half. 3,000 ft, two zones of 1,500 ft, Standard machines, excavation Day 1, pipe Day 2, backfill Day 4, promise Day 18, deadline Day 30, budget $172,320. Rules holds both permit cards. Every mechanic fires once, in this order (measured with the scripted plays in `Monitor/test/scripts/`):

| Sim day | What happens | Who acts | Procedure |
| --- | --- | --- | --- |
| Huddle (host paused) | Plan enters zone 1's permit code; Site rides to the seepage at 4+00 to 7+00 and looks | Plan, Site, Rules | P-2 |
| 4 (t 3.89) | Collapse at 5+20; Site stops work on zone 1; Rules reads P1; Plan lends the excavator | all | P-1 |
| 5 (t 4.1) | Backfill catches the stopped pipe crew; the foremen argue; Plan holds backfill | Site, Plan | P-6 |
| 5 to 6 | The lend went out by radio and was not carried out (F1); Site finds the excavator still digging, Plan radios again | Site, Plan | P-1 line 6 |
| 8 | Forecast in Plan's inbox; towers to the south in VR; zone 2's permit is due before excavation reaches 15+00 | Plan, Rules | P-2 |
| 9 to 10 | Rain, every crew at half speed; afterwards pipe laying waits for the rework (3 days unpermitted, 1.5 with a pump) | — | A2 page |
| 16 | Backfill reaches the end of zone 1: inspection handshake | Site, Rules, Plan | P-3 |
| 24 | Finish, result card | — | — |

| Scripted play | Finish day | Idle crew-days (exc / pipe / back) | Spent | Peak open trench | Collapse days lost | Rain days lost |
| --- | --- | --- | --- | --- | --- | --- |
| Push as the PM wrote it (`slice_play.json`) | 23.85 | 18.1 (3.1 / 6.2 / 8.9) | $128,357 | 1,281 ft | 3.18 | 3.0 |
| Pull in the huddle: backfill Day 8, trench box 4+00 to 7+00, pump at the forecast (`slice_pull_play.json`) | 22.35 | 9.1 (1.1 / 4.7 / 3.4) | $128,262 | 1,820 ft | 2.18 | 2.5 |
| Plan on autopilot, Site does nothing (`slice_autoplan.json`) | 24.8 | 20.0 | — | — | F1 missed | — |

Wall clock at the slice's pace (4 s per day, 15 s per day while an event waits for Plan): 96 s of full speed plus however long the three seats take to resolve the collapse, the argument and the rain. Three minutes when the team moves; five when it talks. The real stages keep 5 and 20 s.

## A. Test what exists today (browser seats, no headset)

Exact steps, Windows PC, PowerShell in `C:\Users\bryan\Documents\TAMU\02-Research\Creative Care\Unity\LOB-OVER`. Close the Unity Editor first.

1. One-time: `git add -A; git commit -m "Slice scenario, table-test docs, WO-05a"` (the slice files are untracked until then). Print `Paper\out\slice_texas\manual.pdf` (double-sided), `cards.pdf` and `tokens.pdf` (card stock), `ledger.pdf` (landscape), `jobsign.pdf`.
2. Voice placeholders for the slice (optional; the pages show the line text when a WAV is missing): `.venv\Scripts\python Tools\voice\make_voice.py Data\Scenarios\slice_texas.json`.
3. Start the host: `npm --prefix Monitor run host -- Data/Scenarios/slice_texas.json`. It prints the port (7331). Find the PC's LAN address with `ipconfig` (IPv4). If a phone cannot connect, allow node through the Windows firewall on 7331.
4. Open on the host PC: `http://localhost:7331/facilitator.html` (you). On the Plan laptop: `http://<PC-IP>:7331/plan.html`. On the Site laptop or phone: `http://<PC-IP>:7331/site.html`. Rules sits with the printed kit and nothing else.
5. The host starts paused. Say only: three jobs, talking is the game, the manual is Rules's, nothing will say whether a choice was right. Tell Rules to issue zone 1 (P-2) and Plan to type the code now; tell Site to ride the line and look at the walls. That is the huddle.
6. Press Start. Do not help. When the result card appears, press Reset for a second run with a pulled plan (backfill Day 8, a trench box over the seepage, the pump when the forecast lands) if the team wants to try it; the overlay in a reset run has no previous lines (that is Stage 2's feature), so compare the two result cards by hand.
7. Quick solo smoke without a team: start the host, open the facilitator page and the Plan page on the PC and the Site page on your phone, press Start, and play Site from the phone while glancing at Plan; nobody enters the permit for zone 2, so excavation idles at 15+00 from about Day 9 until you type `2936-0` on the Plan keypad. That alone shows the loop.

Until WO-05a is accepted, Plan must enter zone 1's code while the host is paused (DECISIONS O10) and a stop-work is lifted the instant Plan commits (O11). Neither affects the slice's chain.

## B. What the VR seat is, in this slice

The Quest is a client of the same Node host, on the same protocol as `site.html`. Nothing is simulated on the headset. The Site player:

- stands in the cab of the site truck on the haul road beside the trench, 1 ft = 0.3048 m, 30.5 m between station stakes; rides by picking a stake on the dashboard (fade, engine sound, arrive; no smooth vection), dismounts to stand at the stake; snap turns only;
- sees what the design says only Site sees: trench walls and water per station, pipe in the trench, protection, the three machines (LOB-Game's Excavator, Crane and Backfill prefabs with their animators) with a plate on each, crews of LOB-Game workers whose animation and posture follow the state (`working`, `idle`, `held`, `stopped`, `arguing` with the ConflictTalk clip, `demobilized`), mood 0 to 3, the zone boards with the permit number, the sky (clear, towers to the south, rain with LOB-Game's rain bed), the gate with trucks, the trailer board (live actual lines and the permit limits), the job sign;
- looks at a thing for two seconds to inspect it (wall, water, machine plate, pipe, protection, board, sky, ticket, crew); the clipboard on the left wrist shows the host's text and plays it if a WAV exists;
- has three physical buttons on the wrist, pressed by pointing the right controller and pulling the trigger: Flag (plants a flag at the stake), Stop work (the zone), Confirm (Done / Not carried out, for the order landed at this crew); Listen is automatic within 10 m of arguing or held crews (their lines play);
- hears the PM and the radio on the Plan page, not in the headset; event cards appear as a card on the dashboard with the facts and nothing else.

What is left out of the slice on purpose: hand tracking, the Unity host, the C# simulation, Stage 3 cities, recorded voice, the result overlay in VR (the Plan page shows it).

## C. Your manual steps (about 40 minutes, once)

1. **Export the assets from LOB-Game.** Open LOB-Game in Unity 6000.3.6f1. In the Project window select these folders and files, then Assets > Export Package…, tick **Include dependencies**, save as `C:\Users\bryan\Documents\TAMU\02-Research\Creative Care\Unity\LOB-OVER\Tools\import\LOB-Game-Site-Assets.unitypackage` (the folder is ignored by git):
   - `Assets/_Project/Art` (Prefabs: Equipment, Workers, Placeholders, Effects; Materials; ProjectSite with its Animation and Materials and ProjectPipeSegment)
   - `Assets/_Project/Animation` (Crane.controller, Excavator.controller)
   - `Assets/3D_Models/constructionWorkers_Anims_01`, `_02`, `_03`
   - `Assets/3D_Models/a-pile-of-sand`, `Assets/3D_Models/Construction Assets/Traffic Props`, `Assets/3D_Models/trafic-cone`, `Assets/3D_Models/water-barriers`
   - `Assets/Audio/Construction`, `Assets/Resources/SiteAudioBank.asset`, `Assets/PM`
   Include dependencies pulls the FBX sources the prefabs use (`3D_Models/Execavator`, `Mobile Crane`, `Full_Set/Level_1.fbx`, `pipe/source/PIPE.fbx`, the dozer mesh) and their textures. Do not export `_Project/Code`, `_Project/Scenes` or `_Project/UI`. Expect 300 to 600 MB.
2. **Import into LOB-OVER.** Open LOB-OVER (6000.3.8f1), Assets > Import Package > Custom Package…, pick the file, Import All. The assets land under the same paths (`Assets/_Project/Art/...`, `Assets/3D_Models/...`); leave them there. If the import reports missing scripts on a prefab (LOB-Game components), ignore it: WO-08 strips those components by script. Commit: `git add -A; git commit -m "LOB-Game site assets (own project, reused)"`.
3. **Newtonsoft.** Window > Package Manager > + > Add package by name: `com.unity.nuget.newtonsoft-json` (3.2.1). Or let the orchestrator add it to `Packages/manifest.json` at step 0.
4. **Quest 3S.** Developer mode on in the Meta Horizon app; USB-C cable; `adb devices` lists it (the Android SDK's platform-tools from the Unity Hub install, or `winget install Google.PlatformTools`). Meta Quest Link app installed on the PC for Editor play with the headset on.
5. **Memory.** Exactly one Unity Editor open (LOB-OVER) during Phase 1 runs; it is needed for the Unity MCP. Start Claude Code with `CLAUDE_CODE_DISABLE_BG_SHELL_PRESSURE_REAP=1` set in the PowerShell session: `$env:CLAUDE_CODE_DISABLE_BG_SHELL_PRESSURE_REAP=1; claude --model fable --effort max`.

## D. The work orders (dispatched by the orchestrator, in this order)

| WO | What exists when it is accepted | Verified by |
| --- | --- | --- |
| WO-06 | The host plays a Plan script on its own (`--autoplan`) so one person in the headset gets the whole chain; the slice scripts are fixtures; the e2e covers the slice | `npm test`, headless runs, e2e |
| WO-07 | `LOBOver.Client` assembly: WebSocket client, protocol types, site-state model, main-thread dispatch, reconnect, host-address panel, text HUD; works in the Editor against the Node host | a PlayMode test that starts the host and round-trips `moveTo` |
| WO-08 | `Site.unity` built by an editor script from the imported assets; a `SiteWorld` that spawns stations, boards, crews and machines from the first `state` and keeps them in step with every `state` | PlayMode test with a recorded state sequence; a Play-mode screenshot at 5+20 on Day 5 |
| WO-09 | The inspector's verbs on the Quest controllers: ride, dismount, gaze inspect, wrist clipboard, cards, SFX and voice, comfort settings | PlayMode tests on the input layer with simulated controllers; your run with Link |
| WO-10 | The APK: Android ARM64, OpenXR with the Meta feature set, host-address panel, 72 Hz; installed with adb | logcat shows `welcome` for seat `site`; your run on the headset |

Then the tests in `Docs/TABLE_TEST.md` apply to the slice with the headset in the Site seat: first you alone with `--autoplan` (section E), then three seats.

## E. Test steps once WO-10 is accepted

Solo, you in the headset, Plan on autopilot, no paper:

1. PC: `npm --prefix Monitor run host -- Data/Scenarios/slice_texas.json --autoplan Monitor/test/scripts/slice_autoplan.json`. Open `facilitator.html` on the PC.
2. Headset: launch LOB Over, type the PC's LAN address once on the start panel, confirm the dashboard reads "connected, seat site, slice_texas".
3. Facilitator: Start. Ride to 5+20 before Day 4 (the dashboard shows the day), look at the wall and the water; at the collapse press Stop work; at the argument stand with the foremen and listen; ride to the excavator on Day 5 and press Confirm → Not carried out; ride back to the backfill crew and Confirm → Done; watch the sky on Day 8; ride to 15+00 on Day 16 for the inspection. The Plan script does the rest. The result card appears on the Plan page on the PC at about Day 24.
4. Watch for: any discomfort during a ride (switch to the shorter fade if so), a gaze inspect that fires when you did not mean it, a button you could not hit, a card you could not read, a crew whose posture did not match the dashboard text.

Three seats: the same as section A, with the headset instead of `site.html` and no `--autoplan`. Film it. Fill in the observation sheet in `Docs/TABLE_TEST.md`.
