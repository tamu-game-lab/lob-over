# WO-05a — Simulation pass before the table test: O10, O11, O13 and the review follow-ups

## Goal

Three outcome-changing gaps found in the WO-02 review and the finish-line smoke are closed, the six minor follow-ups from that review are fixed, the printed fine is formatted, and the Plan page can send an empty commit. Everything already accepted keeps passing: goldens, determinism, the three scripted runs, the e2e. No new features.

## Read

- `AGENTS.md`
- `Docs/DECISIONS.md`: Open items O10, O11, O13 (the decisions, taken by Bryan, are the "Recommended answer" column) and Taken item K8
- `Docs/WORK_ORDERS/WO-02.md` section 3 (Orders, Hold / resume, Stop work, Deviations) and section 4 (scripts)
- `Docs/WORKLOG.md`, the "WO-02 REVIEW" line (the six minor follow-ups, quoted below so you need not search)
- The modules you will touch: `Monitor/src/sim/deviations.js`, `permits.js`, `orders.js`, `crews.js`, `money.js`, `sim.js`, `views.js`; `Monitor/web/plan.html` (the Commit button only); `Paper/make_kit.py` (currency formatting only)

## You may change

- `Monitor/src/sim/**`, `Monitor/test/**` (including `test/scripts/*.json` if a script needs an extra command to keep its expectations)
- `Monitor/web/plan.html`: only the Commit button's enabled state and label
- `Paper/make_kit.py`: only the currency formatter
- `Docs/DECISIONS.md`: move O10, O11, O13 from Open to Taken with one line each (K17, K18, K19), nothing else

## Build

1. **O10, F2 never fires at the job start.** `firstUnpermittedBoundary` means a boundary between two zones. At 0 ft with zone 1 unpermitted the excavator idles (`idleNoPermit`), bills, no overshoot, no fine, and F2 stays available for the first real boundary. The facilitator view's deviation state must say `armed` until then.
2. **O11, a stop-work lift lands by the order-lag rule.** When a Plan commit lifts a stop-work (K8), create a `resume` order per affected crew with the normal `landsT` (at once if Site is dismounted within 100 ft of that crew, else next shift). A commit that holds a crew replaces its stop-work with the hold, as before. Site's `confirm` applies to these orders like any other.
3. **O13, an empty commit is "no change".** `commit` with `ops: []` → ack, speed returns to `full`, change log entry `{day, ops: [], note: "no change"}`. On the Plan page the Commit button stays enabled with an empty draft and reads "Commit (no change)" then; it reads "Commit N changes" otherwise. Nothing else on the page changes.
4. **Review follow-ups** (from the WO-02 REVIEW worklog line): (a) an argument does not reset a held crew's mood; mood only falls after the crew works a full day; (b) the `demobFeeUsd` is charged at the `remob` commit, not when the order lands; (c) a radioed order that is in transit is not listed in Site's `orders[]` at Site's station until it lands; (d) a second `pay clear_own` or a second `lend` of the same machine before the first lands is nacked `"machine already committed"`; (e) a receiving crew without a machine of its own works at the lent machine's rate while it holds it (WO-02 Lend clause); (f) a stop-work on a zone does not touch a crew that has not started (`t < startDay - 1`); it starts on its day and then idles if the stop-work still stands.
5. **Currency formatting in the kit.** Every dollar figure `make_kit.py` prints uses thousands separators (`$10,000`), including the fine and any `_validate` placeholder values.
6. **Scenario rule switches.** The review found that `orderLagShifts`, `heldCrewBills`, `conflictRule`, `conflictBehaviour`, `unpermittedBoundary`, `pumpHalvesRework`, `likelihoodRollsApply` and `shiftsPerDay` are not read. Do not implement alternatives; instead make the loader (`Monitor/src/scenario/load.js` is out of scope, so do this in `sim.js` at `createSim`) throw a clear error if any of them differs from the authored value, so a future retune cannot silently do nothing.
7. **Tests.** Add to `Monitor/test/`: O10 (Stage 2, no codes entered, run to t 1: exc at 0 ft, `idleNoPermit`, `finesUsd 0`, F2 `armed`; then codes for zones 1 and 2 entered, run until exc reaches 30+00 with zone 3 unpermitted and Site elsewhere: overshoot 100 ft, fine charged once); O11 (stop-work at t 5.1 with Site at 7+20, Plan commit at 5.2 with no hold: backfill at ~250 ft resumes at t 6.0, pipe would resume at once if it were not stopped by the collapse; with Site standing at the backfill crew it resumes at 5.2); O13 (empty commit acks, speed `full`, change log entry); one test per follow-up (a) to (f); the rule-switch guard throws on a changed `orderLagShifts`.
8. Keep `test/scripts/stage1_pm_play.json` and `stage2_pull_play.json` passing their expectations; if O11 shifts a day, adjust the script's commands, not the expectations in WO-02. Record the new finish, idle and spend numbers in `NOTES`.

## Acceptance checks

```
npm --prefix Monitor test                                                                          # all pass, including the new O10/O11/O13 and (a)-(f) tests
node Monitor/src/sim/goldens.js Data/goldens/no_events.json ; echo exit=$?                        # 5 x PASS, exit=0
node Monitor/src/sim/headless.js Data/Scenarios/stage2_texas.json --out Monitor/out/s2n.json && node -e "const r=require('./Monitor/out/s2n.json');console.log(r.result.finesUsd, r.snapshots[1].deviations ? JSON.stringify(r.snapshots[1].deviations) : 'see facilitator view')"   # 0 fines with nobody entering a code; F2 armed, not triggered
node Monitor/src/sim/headless.js Data/Scenarios/stage1_texas.json --script Monitor/test/scripts/stage1_pm_play.json --out Monitor/out/s1a.json && node Monitor/src/sim/headless.js Data/Scenarios/stage1_texas.json --script Monitor/test/scripts/stage1_pm_play.json --out Monitor/out/s1b.json && cmp Monitor/out/s1a.json Monitor/out/s1b.json && echo DETERMINISTIC
node -e "const r=require('./Monitor/out/s1a.json').result;console.log(r.finishDay, r.lost, r.idleCrewDays)"   # > 35, < 46, undefined
node Monitor/src/sim/headless.js Data/Scenarios/stage2_texas.json --script Monitor/test/scripts/stage2_pull_play.json --out Monitor/out/s2.json && node -e "const r=require('./Monitor/out/s2.json').result;console.log(r.finishDay, r.finesUsd, r.idleCrewDays)"   # < 46, 0, lower than Stage 1's
npm --prefix Monitor run e2e                                                                       # 1/1
python Paper/make_kit.py Data/Scenarios/stage2_texas.json && grep -c '\$10,000' Paper/out/stage2_texas/manual.txt   # >= 1
grep -c '\$10000' Paper/out/stage2_texas/manual.txt ; echo grep-exit=$?                           # 0, grep-exit=1
git status --short      # only Monitor/src/sim, Monitor/test, Monitor/web/plan.html, Paper/make_kit.py, Docs/DECISIONS.md
```

## Out of scope

No new verbs, no page work beyond the Commit button, no scenario number changes, no changes to `Docs/WORK_ORDERS/WO-02.md`. If a follow-up turns out to need a design decision, leave it, say so in NOTES, and finish the rest.

## Amendment 1 (orchestrator, 2026-10-20): the slice scripts

`Monitor/test/scripts/slice_*.json` and `Docs/VR_SLICE.md` were added on 2026-10-20, after this work order was written; item 8 names only the two stage scripts. Everything above is committed (7e9e9c3) and stands; this is not a rejection round. Under the rules above (chiefly O11: a stop-work lift lands by the order-lag rule) the two slice play scripts no longer play the chain `Docs/VR_SLICE.md` describes and measured (collapse Day 4, argument Day 5, rain Day 9, finish about Day 24).

Observed with `node Monitor/src/sim/headless.js Data/Scenarios/slice_texas.json --script Monitor/test/scripts/<name>.json --out Monitor/out/<name>.json`:

- `slice_play`: lost at the deadline (t 29), idle 72.2 crew-days; before 7e9e9c3 it finished at 23.85 with 18.1 idle and $128,357 spent. Cause: the `stopWork` at t 3.9 now holds exc (869 ft) and back (238 ft) until the lift's `resume` orders land at t 5.0 (Site stands at 5+20, within 100 ft of pipe only); the "Crews stopped" argument moves from t 4.14 to t 7.14, after the script's `listen` (4.3) and `hold back` (4.35), so pipe and back argue to the deadline and every later commit is nacked `conflict: hold or demobilize the trailing crew` (c-22, c-23, c-24, c-25). Also at t 5.05 Site stands at 11+00 while exc is at about 880 ft, so `confirm o-2` (c-14, c-16) and `confirm o-3` (c-20) are nacked `order not landed here`, and the radioed lend lands at 6.0 instead of 5.05. The order ids also shifted: the lift's resume orders are o-3, o-4, o-5 and the hold is o-6.
- `slice_pull_play`: finish 23.4, idle 12.2, $134,801 (before: 22.35, 9.1, $128,262); `confirm o-5` at 5.05 (c-10, c-12) nacked `order not landed here` for the same reason, the lend lands at 6.0, the argument moves from 10.2 to 9.8.
- `slice_autoplan`: unchanged (24.8, idle 19.967).

### Build (continued)

9. Re-choreograph `slice_play.json` and `slice_pull_play.json` so that, under the rules above, they play the chain VR_SLICE.md describes. Commands only (times, stations, order ids, an extra ride, mount/dismount or listen), as item 8 allows; keep each script's plan ops (the pushed plan with its lend and hold in `slice_play`; `setStart back 8`, the trench box 4+00 to 7+00, both permits at the start, the pump at the forecast and the hold in `slice_pull_play`), the `stopWork` at the collapse, and the F1 flow at the excavator (`confirm notDone`, `radio`, `confirm done`). Target outcomes:
   - `slice_play`: collapse card at t 3.89 (unchanged); "Crews stopped" fires with t < 5.0 (Day 5; for that, Site can ride to the backfill crew and dismount before Plan's commit so the lift lands there at once); Site `listen`s and Plan holds the trailing crew after the argument, not before; heavy rain at t 8 (unchanged); `finishDay` between 22.5 and 25.5; `lost` undefined; no `nack` message in the transcript.
   - `slice_pull_play`: no `nack`; finishes before `slice_play` with fewer idle crew-days; if the argument still occurs, the hold answers it within half a day.
   - `slice_autoplan`: unchanged (24.8) unless the lend's order id in a no-Site run is no longer o-2; then fix the id only.
10. Add to `Monitor/test/sim-runs.test.js` one test per slice script asserting these outcomes (event `t`s, zero nacks, finish and idle relations, autoplan finish 24.8 within 0.05), so the slice chain is guarded like the stage scripts.
11. In NOTES report, per slice script, the new measured finish day, idle crew-days (total and exc / pipe / back), spend, peak open trench and the deviation outcome, in the columns of the VR_SLICE.md table; the orchestrator refreshes that table.

### Acceptance checks (in addition to the ones above, which must still pass)

```
npm --prefix Monitor test                                                                              # all pass, including the three slice-chain tests
node Monitor/src/sim/headless.js Data/Scenarios/slice_texas.json --script Monitor/test/scripts/slice_play.json --out Monitor/out/sp.json
node Monitor/src/sim/headless.js Data/Scenarios/slice_texas.json --script Monitor/test/scripts/slice_pull_play.json --out Monitor/out/spp.json
node Monitor/src/sim/headless.js Data/Scenarios/slice_texas.json --script Monitor/test/scripts/slice_autoplan.json --out Monitor/out/sa.json
node -e "for (const n of ['sp','spp','sa']) { const r=require('./Monitor/out/'+n+'.json'); console.log(n, r.result.finishDay, r.result.lost, r.result.idleCrewDays, 'nacks', r.messages.filter(m=>m.message.t==='nack').length, 'events', r.messages.filter(m=>m.message.t==='event').map(m=>m.t+':'+m.message.card.title).join(', ')) }"
   # sp: finish 22.5..25.5, lost undefined, nacks 0, events 3.89 collapse / <5.0 Crews stopped / 8 Heavy rain; spp: finish < sp's, idle < sp's, nacks 0; sa: 24.8 undefined 19.967 nacks 0
git status --short      # only Monitor/test/scripts/slice_*.json and Monitor/test/sim-runs.test.js (plus files of the original scope only if a slice outcome needs a rule fix, which you then list in NOTES)
```
