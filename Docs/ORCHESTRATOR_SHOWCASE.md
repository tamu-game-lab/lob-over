# ORCHESTRATOR_SHOWCASE.md — addendum for the showcase track (WO-05a, WO-S1 to WO-S4)

`Docs/ORCHESTRATOR.md` applies unchanged: dispatch with `bash Tools/astra.sh`, verify, accept or reject in at most two rounds, commit, worklog, stop conditions, token discipline. This addendum says how to verify each kind of work in this track. `Docs/ORCHESTRATOR_PHASE1.md` (Unity MCP, batch builds, adb) is retired; do not follow it.

## Step 0 additions

- `dotnet --version` must print 8.x or newer; if not, stop and tell Bryan (step 2 of `Docs/SHOWCASE.md`).
- After WO-05a is accepted, generate the JavaScript transcripts the C# port is measured against: for each of `slice_play`, `slice_pull_play`, `slice_autoplan`, `stage1_pm_play`, `stage2_pull_play`, `stage1_noaction` run `node Monitor/src/sim/headless.js Data/Scenarios/<stage>.json --script Monitor/test/scripts/<name>.json --out Data/transcripts/<name>.json` (no `--script` for `stage1_noaction`; the stage is the one the script name starts with). Commit `Data/transcripts/`. Regenerate and recommit whenever the JavaScript simulation changes.
- For the second goal only: run `Tools/import_lobgame_assets.ps1` (PowerShell) yourself before dispatching WO-S3; it copies about 1.7 GB from `..\LOB-Game\Assets` into `Assets\LOBGame\` with the `.meta` files, read-only on the source. Astra never touches LOB-Game. `Assets/LOBGame/` is gitignored; the script is the source of truth.

## Verifying web work (WO-S1)

Playwright against the static server the WO adds (`npm --prefix Monitor run showcase:serve`), never against `file://` (ES modules do not load from files). Open the landing page, start a showcase with the slice and autoplan on, open the Plan and Site tabs in the same browser context, press Start on the host tab, and check the WO's list. Screenshots at 1280×800 and at 390×844 (phone) into `Monitor/out/showcase/`; look at them. After Bryan publishes (his step 4), `curl -s -o /dev/null -w "%{http_code}" https://tamu-game-lab.github.io/lob-over-showcase/` is the last check of the first goal; if it is not 200 yet, say so and still print `READY_FOR_PAGES` once WO-S2 is accepted, because publishing is his step.

## Verifying the C# simulation (WO-S2)

`dotnet test Sim/LOBOver.Sim.Tests` from the repo root is the acceptance; read its summary, not its full output. A parity failure is a reject whose note quotes the first failing assertion (which transcript, which day, which field, expected versus actual). Never loosen a tolerance; if a rule is ambiguous in the JavaScript, the JavaScript is the specification.

## Verifying Unity work (WO-S3, WO-S4) without the Unity MCP

1. Compile check, only when the Editor is closed (no `Temp/UnityLockfile` in the project): `"C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe" -batchmode -nographics -quit -projectPath . -logFile Monitor/out/unity-compile.log`, wait for it to exit (up to 15 minutes on the first import of the copied assets), then `grep -c "error CS" Monitor/out/unity-compile.log` must be 0. Any `error CS` line is a reject with that line quoted. If the lockfile exists, skip this and go to 2.
2. Then stop for Bryan with a message of at most six lines: what to open, which menu item to run, what to press, what he should see in the first 60 seconds, and what to reply. His one-line reply is the check's result; record it in the worklog. If he reports something wrong, that is the rejection note's content, in his words plus the WO section it belongs to.
3. Never open, edit or review scene or prefab YAML; Astra builds scenes from editor scripts, and the only acceptable scene is one the menu item regenerates.

## Finish lines

- First goal: `READY_FOR_PAGES` when WO-05a, WO-S1 and WO-S2 are ACCEPTED.
- Second goal: `READY_FOR_HEADSET` when WO-S3 and WO-S4 are ACCEPTED with Bryan's Play-mode check recorded. Build and Run is his step.
