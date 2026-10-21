# WO-L1 Level 1 on the PC

## Goal

Double-clicking `Play LOB Over.cmd` starts a Level 1 game: the Node host with `Data/Scenarios/level1_texas.json`, the Plan page with Start, the chart and Hold or Go, the printable Rules page, the foremen's and the PM's lines, and a host the headset can find over USB or Wi-Fi. The slice and the stages behave exactly as before.

## Read

- `AGENTS.md`, `Docs/LEVEL1.md` (all of it), `Paper/level1_rules.md`, `Docs/VOICE.md` ("Principles", "Triggers", "Slice line sheet"), `Docs/PROTOCOL.md`
- `Monitor/src/sim/*.js`, `Monitor/src/host/core.js`, `Monitor/src/host/server.js`, `Monitor/web/plan.html` (its chart), `Data/Scenarios/slice_texas.json`, `Tools/voice/`, `Paper/make_kit.py`

## You may change

`Monitor/src/**`, `Monitor/web/**`, `Monitor/test/**`, `Monitor/e2e/**`, `Data/Scenarios/level1_texas.json` (new), `Monitor/test/scripts/level1_*.json` (new), `Tools/voice/**`, `Voice/level1_texas/**` (generated), `Paper/make_kit.py`, `Paper/out/level1/**` (generated), `Play LOB Over.cmd` (new, repo root); additions only to `Docs/PROTOCOL.md` and `Docs/SCENARIO_SCHEMA.md`; and the "Measured runs" table of `Docs/LEVEL1.md`.

## Build

1. **Scenario** `level1_texas.json`, from the slice: the same job, crews, machines, start days, promise (Day 18) and clock (4 s a day, 15 s slow). One event, the P1 collapse at 5+20 with `durationDays: 2`, hitting pipe. No rain, forecast, deviations, inbox or protection. Permits issued and entered before Day 1, as Stage 1 does (`heldBy: "pm"`, no codes for anyone to read); inspections not required; money kept in the simulation but shown nowhere. The rule flags of step 2. The lines of `Docs/LEVEL1.md` "Voices".
2. **Simulation rules**, each behind a scenario flag that the slice and the stages do not set, so their transcripts stay byte-identical:
   1. A P1 with `durationDays` ends by itself after that many days.
   2. `rules.orderLag: "none"`: every order lands at once.
   3. `time.slowUntil: "eventEnd"`: slow while an event is active or an argument is unresolved, full speed otherwise.
   4. `rules.holdBeforeStartDelaysStart: true`: a hold on a crew that has not started moves its start to the moment it is resumed; until then it is not on site and accrues no idle time.
   5. `rules.planControlsClock: true`: the Plan seat may send `ctrl start` and `ctrl reset`.
3. **Voices**, only what Level 1 uses from `Docs/VOICE.md` "Triggers": a line may be an exchange (`turns`); triggers `event` with `eventType`; `arrive` with `crew` and `status` (Site dismounted within 30 m of that crew, at most once per crew per day); `conflict` with `pair` and `cause` (`leaderStopped` when the leader is not moving, otherwise `caughtUp`); `conflictRepeat` with `pair` (the trailing crew catches its leader again after the first argument; at most once a day per pair; nothing stops). `voice` messages carry the turns. Generate the new WAVs with `Tools/voice`, one file per turn (`<lineId>_<n>.wav`), cast as in `VOICE.md`, into `Voice/level1_texas/`.
4. **Plan page** `Monitor/web/play.html` (new; `plan.html` stays for the stages). Header: "LOB, Over · Level 1", the day, and the Site status: "Waiting for the headset", "Headset connected" (seat `site`, client `quest`) or "Site connected in a browser" (client `web`). Start, which becomes Run again after the result. The chart from `plan.html`: the PM's planned lines dashed, the reported lines solid up to yesterday, stations across and days down. One row per crew: name, foreman, speed, planned start, and one button that reads Hold, or Go while Plan holds that crew. Rejections in plain words; while the foremen are arguing, any command other than holding the crew behind is refused with "The field won't take that order right now", so Plan has to ask Site what is going on. PM lines play as audio, without captions. At the result: the finish against Day 18, idle crew-days per crew, the actual lines over the plan; Run again keeps the previous run faint underneath. A "Rules page" link to the printable page. Nothing else: no money, inbox, catalog, codes, radio log, JSON or debug tables.
5. **Rules page**: `Paper/make_kit.py --page Paper/level1_rules.md --out Paper/out/level1` writes `rules.pdf` (one letter page) and `rules.html`.
6. **Start script** `Play LOB Over.cmd` (repo root, Windows): checks for Node and says how to install it if missing; runs `npm ci` in `Monitor` when `node_modules` is missing; starts `node Monitor/src/host/server.js Data/Scenarios/level1_texas.json --discovery`; runs `adb reverse tcp:7331 tcp:7331` when adb (on the PATH or in Unity's Android SDK) lists a device, ignoring errors; opens the Plan page in the default browser; prints the PC's address and "Close this window to end the game". `--discovery` broadcasts `LOBOVER 1 <port> <hostname>` by UDP to port 7332 every 2 s on each IPv4 interface.
7. **Tests.** Unit tests for each rule of step 2 and each new trigger. Scripted runs `Monitor/test/scripts/level1_push.json` (no action until the argument, then hold backfill; Go two days later) and `level1_pull.json` (hold backfill on Day 1, Go on Day 9): record the finish, the idle crew-days per crew and whether an argument fired, and write them into the "Measured runs" table of `Docs/LEVEL1.md`. An e2e for `play.html`: Start, Hold, Go, the result, Run again, with `site.html` connected as `site`. A discovery test: a UDP listener receives the packet. `npm test` with the existing goldens unchanged.

## Acceptance checks

- `npm test` and the e2e pass; the slice and stage goldens are byte-identical.
- The orchestrator runs `Play LOB Over.cmd`: the Plan page opens; `site.html` connected as `site` turns the status to "Site connected in a browser"; Start runs a game to the result; Run again works.
- The measured table is filled in; the pull run has no argument and fewer idle crew-days than the push run.

## Out of scope

The headset (WO-L2), the GitHub Pages showcase, money on screen, and everything `Docs/LEVEL1.md` leaves for later levels.
