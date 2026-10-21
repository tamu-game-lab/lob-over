# WO-S7 Guided three-seat playthrough on the web

## Goal

`node Monitor/src/host/server.js Data/Scenarios/slice_texas.json --guide Data/Guides/slice_guided.json`, and the showcase's host page with Guided ticked, run the slice as the beat-by-beat playthrough of `Docs/PLAYTHROUGH.md`. At each beat the clock pauses, each seat sees its own prompt, the facilitator page shows the narration, and the clock resumes when every connected seat presses Ready or the facilitator presses Continue. Rules gets a page of its own. No simulation changes.

## Read

- `AGENTS.md`, `Docs/PLAYTHROUGH.md`, `Data/Guides/slice_guided.json`, `Docs/PROTOCOL.md`
- `Monitor/src/host/core.js`, `autoplan.js`, `pacing.js`, `server.js`, `Monitor/web/*.html`
- `Paper/make_kit.py` (how the manual's placeholders are filled)

## You may change

- `Monitor/src/host/**`, `Monitor/src/showcase/**`, `Monitor/web/**`, `Monitor/test/**`, `Monitor/e2e/**`
- `Paper/make_kit.py` (an HTML output only)
- `Docs/PROTOCOL.md` (the guide messages and the rules seat), `Docs/SCENARIO_SCHEMA.md` (the guide file format)

## Build

1. **Guide engine** (`src/host/guide.js`, used by `core.js`, so both hosts get it). Beats run in order. A beat triggers on `start`, `event` (by id), `conflict` (by pair; the sim's conflict event id is `conflict_<trailing>_<leader>`), `t` (`atLeast`), `holdPoint` (the backfill crew within 10 ft of the zone's end, or idle at its hold point) or `result`. A beat whose trigger has not fired when a later beat's trigger fires is skipped and marked skipped. On a trigger the clock pauses and the host sends each seat `{t: "guide", v: 1, beat: {id, index, count, title}, prompt: <that seat's line>, rulesSection, ready: {site, plan, rules}}`; the facilitator also gets the narration and all prompts. While paused, seats may still send commands. A Site `moveTo` during a pause advances the clock by exactly that move's travel time and pauses again, so Site can go somewhere within a beat. `{t: "cmd", action: "ready"}` from a seat marks it ready; when every connected seat is ready, the clock resumes after `resumeDelaySeconds`; `ctrl continue` resumes at once. Without `--guide` nothing changes.
2. **Rules seat in the protocol.** `hello` with `seat: "rules"`. Its state view is `{stage, displayDay, speed}` only: no positions, no plan, no money. It receives guide messages and the job sign facts. Document the guide message, the `ready` action and the rules seat in `Docs/PROTOCOL.md`.
3. **Plan and Site pages.** A guide banner across the top: beat title, this seat's prompt, a Ready button that becomes "Waiting for the others" once pressed; hidden when no guide runs. **Facilitator page.** The narration in large type, the beat list (done, current, skipped), three ready lights, Continue.
4. **Rules page** (`web/rules.html`): the guide banner and Ready; the field manual as HTML with a button per section (front page rule, Texas, indicators, P1, A2, permits and hold points, lookup, procedures), the beat's `rulesSection` highlighted; the stage's permit and inspection cards drawn from the scenario (zone, number, code, "check the digits before you read it"); a digital zone ledger (stations across, the four day boxes per zone), editable and kept in the page only. `Paper/make_kit.py --html` writes `manual.html` with the same filled placeholders and section anchors; the showcase build copies `Paper/out/<stage>/manual.html` beside the PDF.
5. **Browser host** (`host.html`): a Guided checkbox and a guide picker (`slice_guided` preloaded from `Data/Guides`), an Open Rules tab button, and a Rules room-code link like the Site one.
6. **Tests.** Guide engine unit tests driven by `slice_play.json`: beats fire in order at t 0, 3.89, 4.14, 5.0, 7.0, 8.0, the zone 1 hold point, the zone 2 hold point and the result; skip logic; ready and continue; a `moveTo` during a pause advances exactly the travel time. An e2e (`e2e/guided.e2e.js`): the host with the guide; Plan, Site and Rules pages in one browser; at each beat the three pages show their own prompt; Playwright performs the `slice_play` actions for that beat and presses Ready on all three; the run reaches the result; a screenshot of each beat on each page goes to `Monitor/out/guided/`.

## Acceptance checks

```
npm --prefix Monitor test && npm --prefix Monitor run e2e          # all pass, guided included
python Paper/make_kit.py Data/Scenarios/slice_texas.json --html && ls Paper/out/slice_texas/manual.html
npm --prefix Monitor run showcase && ls Showcase/dist/rules.html Showcase/dist/Data/Guides/slice_guided.json
grep -rniE "well done|good job|should have|right choice|correct answer|behind schedule|ahead of schedule" Monitor/web/ ; echo grep-exit=$?   # 1
git status --short   # only the paths above
```

The orchestrator reads the beat screenshots: each seat sees only its own prompt; Plan never sees the site's facts; Site never sees money; the narration is on the facilitator page only. After acceptance it republishes the site with `Tools/publish_showcase.ps1`.

## Out of scope

Simulation changes, Unity (the headset gets the guide in WO-S9), voices.
