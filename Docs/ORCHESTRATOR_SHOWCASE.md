# ORCHESTRATOR_SHOWCASE.md — addendum for the showcase track (WO-05a, WO-S1 to WO-S4)

`Docs/ORCHESTRATOR.md` applies unchanged: dispatch with `bash Tools/astra.sh`, verify, accept or reject in at most two rounds, commit, worklog, stop conditions, token discipline. This addendum says how to verify each kind of work in this track. `Docs/ORCHESTRATOR_PHASE1.md` (Unity MCP, batch builds, adb) is retired; do not follow it.

## Step 0 additions

- `dotnet --version` must print 8.x or newer; if not, stop and tell Bryan (step 2 of `Docs/SHOWCASE.md`).
- After WO-05a is accepted, generate the JavaScript transcripts the C# port is measured against: for each of `slice_play`, `slice_pull_play`, `slice_autoplan`, `stage1_pm_play`, `stage2_pull_play`, `stage1_noaction` run `node Monitor/src/sim/headless.js Data/Scenarios/<stage>.json --script Monitor/test/scripts/<name>.json --out Data/transcripts/<name>.json` (no `--script` for `stage1_noaction`; the stage is the one the script name starts with). Commit `Data/transcripts/`. Regenerate and recommit whenever the JavaScript simulation changes.
- For the second goal only: run `Tools/import_lobgame_assets.ps1` (PowerShell) yourself before dispatching WO-S3; it copies about 1.7 GB from `..\LOB-Game\Assets` into `Assets\LOBGame\` with the `.meta` files, read-only on the source. Astra never touches LOB-Game. `Assets/LOBGame/` is gitignored; the script is the source of truth.

## Verifying web work (WO-S1)

Playwright against the static server the WO adds (`npm --prefix Monitor run showcase:serve`), never against `file://` (ES modules do not load from files). Open the landing page, start a showcase with the slice and autoplan on, open the Plan and Site tabs in the same browser context, press Start on the host tab, and check the WO's list. Screenshots at 1280×800 and at 390×844 (phone) into `Monitor/out/showcase/`; look at them. After Bryan publishes (his step 4), `curl -s -o /dev/null -w "%{http_code}" https://tamu-game-lab.github.io/lob-over/` is the last check of the first goal; if it is not 200 yet, say so and still print `READY_FOR_PAGES` once WO-S2 is accepted, because publishing is his step.

## Verifying the C# simulation (WO-S2)

`dotnet test Sim/LOBOver.Sim.Tests` from the repo root is the acceptance; read its summary, not its full output. A parity failure is a reject whose note quotes the first failing assertion (which transcript, which day, which field, expected versus actual). Never loosen a tolerance; if a rule is ambiguous in the JavaScript, the JavaScript is the specification.

## Unity through the MCP (preferred when the Unity MCP is connected)

When Claude Code has the LOB-OVER Editor's MCP server (CoPlay "MCP for Unity", HTTP on 127.0.0.1:8081, registered as `unity-lobover`), you drive the Editor yourself and stop for Bryan only where a headset is on someone's head. Confirm the connection at step 0 with one console read; if the tools are not there, fall back to the section above. Exactly one Editor is open, on LOB-OVER; if the console read shows another project's path, stop and say so.

Loop for WO-S3 (repeat after every fix until it passes):

1. Read the console with errors only. Any `error CS` line: fix a one-liner yourself, otherwise dispatch Astra with the lines quoted (a rejection round).
2. If `Assets/TextMesh Pro` is missing, import the TMP essentials (menu `Window/TextMeshPro/Import TMP Essential Resources`, or have Astra call `TMP_PackageResourceImporter.ImportResources(true, false, false)` at the top of the scene builder so it never depends on a click again).
3. Execute `LOB Over/Build/Sync Data`, then `LOB Over/Build/Site Scene`. Read the console. If the item threw or `Assets/LOBOver/Scenes/Site.unity` does not exist afterwards, that console text is the rejection note.
4. Open `Site.unity`, enter Play, wait 25 s, read the console, take `LOB Over/Debug/Screenshot`, wait to 60 s, screenshot again, exit Play. Look at both PNGs: the first must show the slumped wall and standing water at 5+20 with the crane stopped and two foremen face to face; the second the rain. No exceptions in the console during Play. Desktop mode is the default in the Editor when no headset is present; if the start panel blocks, select Desktop and Start through the MCP's UI or send the start command the panel exposes.
5. Run the EditMode and PlayMode suites under `Assets/LOBOver/Tests` through the MCP test runner. All pass, or reject with the first failing test's message.
6. Accept, commit, push.

Loop for WO-S4: steps 1, 3 (the builder now adds the rig, truck, start panel and diorama root), 4 with the start panel visible in Play (screenshot it), 5, then `LOB Over/Build/Check Android Settings` (every reported failure that a setting fixes, Astra fixes in the checker's own suggestions or you set through the MCP; if a module is missing from the Hub, stop and tell Bryan). Accept the machine-checkable part, commit, push.

Then WO-S5 (the Build and Run menu item): dispatch, verify the item exists, and with the headset plugged in execute `LOB Over/Build/Build and Run Quest` through the MCP. When the app starts on the headset, stop with a four-line message: put the headset on, try VR Site (ride, dismount, look, stop work, listen, confirm) and MR Diorama (room, table model, point at a crew, chart), reply `OK` or one line of what was wrong. His reply closes WO-S4; print `READY_FOR_HEADSET`.

Rules that stay: never edit scene or prefab YAML; every scene change goes through the builder; Astra never runs Unity; one Editor; commit and push after each acceptance.

## Verifying Unity work (WO-S3, WO-S4) without the Unity MCP

1. Compile check, only when the Editor is closed (no `Temp/UnityLockfile` in the project): `"C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe" -batchmode -nographics -quit -projectPath . -logFile Monitor/out/unity-compile.log`, wait for it to exit (up to 15 minutes on the first import of the copied assets), then `grep -c "error CS" Monitor/out/unity-compile.log` must be 0. Any `error CS` line is a reject with that line quoted. If the lockfile exists, skip this and go to 2.
2. Then stop for Bryan with a message of at most six lines: what to open, which menu item to run, what to press, what he should see in the first 60 seconds, and what to reply. His one-line reply is the check's result; record it in the worklog. If he reports something wrong, that is the rejection note's content, in his words plus the WO section it belongs to.
3. Never open, edit or review scene or prefab YAML; Astra builds scenes from editor scripts, and the only acceptable scene is one the menu item regenerates.

## Publishing (second goal, before any Unity work)

One public repository holds everything: `https://github.com/tamu-game-lab/lob-over` (Bryan created it with a blank README). Source lives on `main`, the built site on `gh-pages`, Pages serves `https://tamu-game-lab.github.io/lob-over/`. Steps, in order, all run by you (ops, not feature code; `.claude/settings.json` allows `git push`, `powershell`, `gh`, `dotnet`):

1. `git remote get-url origin || git remote add origin https://github.com/tamu-game-lab/lob-over.git`, then `git fetch origin`.
2. If `git log --oneline origin/main` shows exactly one commit and `git show --stat origin/main` touches only `README.md`, the remote holds nothing of value: `git push --force -u origin main`. Otherwise `git merge --allow-unrelated-histories -X ours origin/main -m "Merge GitHub initial commit"` and `git push -u origin main`. Bryan may have to complete a GitHub sign-in window the first time; wait for it.
3. Local path fix: the showcase test server and e2e assume the old subpath. In `Monitor/src/showcase/serve.js` change `/lob-over-showcase` to `/lob-over`; in `Monitor/e2e/showcase.e2e.js` change the base to `http://localhost:7340/lob-over/`; grep `Monitor/` for any other `lob-over-showcase`; run `npm --prefix Monitor run e2e`; commit "Showcase: one repository, /lob-over path".
4. `Sim/NuGet.Config` now points at nuget.org again (it was cleared for Astra's offline sandbox): run `dotnet restore Sim/LOBOver.Sim.Tests` and `dotnet test Sim/LOBOver.Sim.Tests -c Release` once with network; commit if the lock files changed.
5. Publish: `powershell -ExecutionPolicy Bypass -File Tools/publish_showcase.ps1` (it builds `Showcase/dist`, pushes it to `gh-pages` through a worktree in `Showcase/.publish`, and prints the URL).
6. Pages: `gh auth status`; if signed in, `gh api --method POST -H "Accept: application/vnd.github+json" /repos/tamu-game-lab/lob-over/pages -f "source[branch]=gh-pages" -f "source[path]=/"`; a 409 means Pages already exists, then `gh api --method PUT ... /repos/tamu-game-lab/lob-over/pages -f "source[branch]=gh-pages" -f "source[path]=/"`. If `gh` is missing or not signed in, print for Bryan: "GitHub → lob-over → Settings → Pages → Build and deployment → Source: Deploy from a branch → Branch: gh-pages, / (root) → Save" and continue; re-check at the end of the run.
7. `README.md`: add one line under the title, "Play it in the browser: https://tamu-game-lab.github.io/lob-over/ (the field manual PDF is linked on the landing page)"; commit and push.
8. Verify: poll `curl -s -o /dev/null -w "%{http_code}" https://tamu-game-lab.github.io/lob-over/` every 30 s for up to 10 minutes after Pages is enabled; 200 is the check. Then open the live landing page, host, Plan and Site with Playwright once (same checks as WO-S1, against the live URL) and keep the screenshots in `Monitor/out/showcase-live/`. Record the URL and the gh-pages commit in the worklog.

After every accepted Unity work order in this goal, `git push` as well, so the public repository stays current.

## Round 3 (October 21): Building Blocks, guided playthrough, hub and warp, team game, voices

Bryan tested the headset build and asked for: Meta Building Blocks instead of code-built rigs, no falling, a passthrough hub with a shader warp into the site, the headset as a seat in a three-person game, a guided beat-by-beat playthrough, and expanded two-foreman arguments. The design is in `Docs/SITE_SEAT_V2.md`, `Docs/VOICE.md`, `Docs/PLAYTHROUGH.md` and `Data/Guides/slice_guided.json`; the work orders are WO-S6 to WO-S10; the decisions are K20 to K28 in `Docs/DECISIONS.md`.

Before anything else:
1. If the open scene has unsaved changes, save it through the MCP. Commit Bryan's `Assets/LOBOver/Scenes/Site.unity` exactly as it is ("Site.unity: Bryan's Building Blocks") with the new docs and work orders, and push.
2. Do not run `LOB Over > Build > Site Scene` until WO-S6 is accepted. The old builder replaces the whole scene and would delete Bryan's blocks.
3. Never add, remove or reparent `[BuildingBlock]` objects or `OVRCameraRig`, by MCP or by script. If a block is missing or broken, stop and ask Bryan to fix it in the Building Blocks window.

Four goals, one stop each. A goal is complete when its token is printed after the request to Bryan; do not keep waiting after that. The next goal starts with his replies.
- Goal A: WO-S6 (Unity) and WO-S7 (web) in parallel, since they touch disjoint folders; republish the site after WO-S7; Build and Run Quest; ask Bryan, in at most six lines, for the WO-S6 headset check (no falling, the panel stays, controllers and hands, ray and poke) and for a laptop run of the guided playthrough; print `ROUND3_A_WAITING`.
- Goal B: record Bryan's replies and fix what he reports (through Astra); WO-S8; Build and Run; ask for the hub and warp check; print `ROUND3_B_WAITING`.
- Goal C: record replies; WO-S9; Build and Run; ask for the three-seat guided playthrough with the headset, following `Docs/PLAYTHROUGH.md`; print `ROUND3_C_WAITING`.
- Goal D: record replies; WO-S10 (regenerate the transcripts after the JavaScript changes and before the C# port); Build and Run; ask Bryan to listen to the slice in the headset; print `ROUND3_D_WAITING`. After his reply: record it, push, print `READY_FOR_PLAYTHROUGH`.

A WO whose acceptance needs Bryan is committed as "accepted, pending Bryan" once its machine checks pass, and closed by his reply at the start of the next goal.

> Later on October 21: Goals B to D are on hold since Bryan's second headset test; Round 4 replaces them.

## Round 4, Level 1 (October 21)

Bryan's second headset test of the WO-S6 build failed: the start panel sat behind a passthrough hole and could not be read, and touching it played a click while nothing happened (its top buttons only change a selection). He then chose a much simpler first playable, `Docs/LEVEL1.md` (K29 to K36): one event with Hold or Go; the headset player looks and talks only; Rules gets one page; the team game now. WO-S8 to WO-S10 wait until Level 1 plays well.

Rules for this round:
0. First commit the Level 1 documents (`Docs/LEVEL1.md`, `Paper/level1_rules.md`, WO-L1, WO-L2, the decisions and these addenda) and Bryan's `Play.unity` exactly as he made it (save it through the MCP first if it is dirty), and push.
1. A headset work order is accepted only after Bryan's check in the headset (K35). Machine checks come first and must pass, but they never accept a headset work order. Commit it as "accepted, pending Bryan".
2. Before dispatching WO-L2, confirm through the MCP that `Assets/LOBOver/Scenes/Play.unity` exists and holds the Camera Rig, Passthrough, Controller Tracking, Hand Tracking and Interactions Rig blocks (today's Check Building Blocks reads only Site.unity and still requires Real Hands; WO-L2 changes both). If it has Real Hands, a second passthrough layer or a demo block, stop and tell Bryan which block to remove in the Building Blocks window (K34); never remove blocks yourself. Never run the old Site Scene builder on Play.unity.
3. Verify the team connection yourself before asking Bryan: run `Play LOB Over.cmd`, enter Play in the Editor on Play.unity, and confirm Connected, a warp to Marcus (screenshot) and Back.
4. Leave `Play LOB Over.cmd` running and the app on the headset for Bryan's check.
5. Ask Bryan in at most eight lines for the PC check (double-click, Start, Hold, Go, the result) and WO-L2's five headset checks, each to be answered OK or with what went wrong.

One goal: WO-L1 and WO-L2 dispatched in parallel (disjoint folders; WO-L2's host tests run once WO-L1 is in), each verified and accepted or rejected (max two rejection rounds), committed and pushed; Build and Run Quest; the request to Bryan; then print `LEVEL1_WAITING`. The next goal starts with his replies and fixes whatever failed before anything new.

## Finish lines

- First goal: `READY_FOR_PAGES` when WO-05a, WO-S1 and WO-S2 are ACCEPTED.
- Second goal: `READY_FOR_HEADSET` when the site answers 200 at its Pages URL, WO-S3 and WO-S4 are ACCEPTED with Bryan's Play-mode check recorded, and `main` is pushed. Build and Run is his step.
