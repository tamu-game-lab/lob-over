# TABLE_TEST.md — what to test and what to decide to close Phase 0

Phase 0 is closed when all five are true: (1) you have reproduced the orchestrator's checks on your own machine (section A); (2) a team that is not you has played Stage 1 and Stage 2 with the printed kit, on film (sections B and C); (3) the observation sheet (section D) is filled in and the decisions in section E groups 1 and 2 are written into `Docs/DECISIONS.md` with the scenario numbers to change; (4) WO-05a (simulation fixes) and WO-05b (retune from the test) are accepted; (5) the validator list (section E group 3) has gone to Phumpat and Dr. Rybkowski with the manual PDF. Then Phase 1 starts per `Docs/ROADMAP.md`.

## Measured baseline (accepted runs, commit 5aca6cd)

| Run | Finish day | Idle crew-days (exc / pipe / back) | Spent | Peak open trench | Collapse days lost | Rain days lost |
| --- | --- | --- | --- | --- | --- | --- |
| No events, push 1/2/3 (golden) | 34.3 | 8.3 (0 / 0 / 8.3) | $196,248 crew cost | 1,271 ft | — | — |
| No events, pull backfill Day 12 (golden) | 35.0 | 0 | $176,756 crew cost | 2,422 ft | — | — |
| Stage 1 scripted play (push) | 41.4 | 23.6 (3.1 / 7.1 / 13.4) | $229,476 | 1,987 ft | 3.06 (own excavator, lend by radio, F1 caught) | 4.0 (rain 1 + rework 3, no pump) |
| Stage 1 no action | lost at the deadline (t 46) | — | — | — | never cleared | — |
| Stage 2 scripted play (pull, box at 6+00 to 9+00, backfill Day 12, pump) | 38.9 | 11.8 (3.4 / 4.5 / 3.9) | $221,499 | 2,180 ft | 2.02 (box halves the clearing) | 2.5 (rain 1 + rework 1.5) |

Promise Day 35, deadline Day 47, budget $278,400 in both stages. A human team should land between the no-action loss and the scripts; a team that finishes Stage 1 before Day 39 or Stage 2 after Day 42 is worth a second look at the transcript (`Monitor/out/<stage>.<timestamp>.json`).

## A. Reproduce the checks yourself (15 minutes, Unity closed)

1. `npm --prefix Monitor test` → 74 tests pass. `node Monitor/src/sim/goldens.js Data/goldens/no_events.json` → 5 PASS. `npm --prefix Monitor run e2e` → 1/1 in about 2 minutes; look at `Monitor/out/e2e/*.png`.
2. `npm --prefix Monitor run host -- Data/Scenarios/stage1_texas.json`; open `http://localhost:7331/plan.html`, `/site.html`, `/facilitator.html`; press Start; watch 60 seconds: the day counter advances at 5 s per day; the collapse card reaches the Site page at about 25 s; the Plan inbox's Day 6 report reads "pipe +0 ft (720)" and the Plan page never shows the word collapse; the speed label on Plan reads "office time: 20 s per day" after the collapse and returns to "5 s per day" after a commit.
3. Open `Paper/out/stage1_texas/manual.pdf` and `cards.pdf`. Check the digits on the four permit cards (4821-5, 7306-6, 2590-6, 9147-1; the digit after the dash is the sum of the four digits mod 10) and on the inspection cards. Play `Voice/stage1_texas/pm_open_01.wav` and `foreman_pipe_arg_01.wav`.
4. From a phone on the same Wi-Fi: `http://<PC-IP>:7331/site.html`. If it does not load, allow node through the Windows firewall on port 7331.
5. Reload the Plan page mid-run: it must come back as the Plan seat, not as a mirror. If it comes back as a mirror, that is a WO-05a item.

## B. Session setup

- Four people: three players and you as facilitator on the host PC. Do not play a seat. Assign seats by lot and write down who sat where; keep the seats fixed through both stages (design: the same person feels push and then pull).
- Plan laptop on `/plan.html`, Site laptop or phone on `/site.html`, Rules with the printed Stage 1 kit (manual double-sided, cards and tokens on card stock, ledger landscape, pencil). Stage 2 kit printed and set aside. The job sign card face up.
- Phone video on a tripod covering the table and the Plan screen; clap once when you press Start so the video and the transcript can be aligned. The host writes `Monitor/out/<stage>.<timestamp>.json` at the result.
- A visible timer. Tell the players only: the three roles, that talking is the game, that the manual is Rules's only, that nothing in the game will tell them whether a choice was right, and that there is no score.
- Stage 2 only, until WO-05a is accepted: keep the host paused through the huddle and have Plan enter zone 1's permit code before you press Start (DECISIONS O10). Say nothing else about it.

## C. Protocol

1. Stage 1, push. Press Start, no huddle. Note the wall-clock time at Start, at the collapse card, at Plan's first commit, at the argument, at the rain, at the result. Expected 6 to 8 minutes wall clock (41 days at 5 s plus the slow stretches); the design target is about 5.
2. Between stages, 3 minutes: leave the result card on the Plan screen. Ask one question only: "What do you want to change?" Do not answer questions about whether anything was right.
3. Stage 2, pull. Load Stage 2 (the host loads paused). Start the 30 s huddle timer; at 30 s press Start. Same timing notes.
4. Debrief, 10 minutes, each seat in turn, filmed: (a) "In your own words, what is the difference between the two plans?" (b) "What did the other two seats know that you did not, and when did you find out?" (c) "What would you change in the manual, the pages, or the rules?" Then one open question: "Which of the three jobs would you want next time, and why?"

## D. Observation sheet

Score each row during play or from the video. Pass criteria are the design's claims; a fail names the file to change.

| # | Claim under test | Watch for | Pass | If it fails, change |
| --- | --- | --- | --- | --- |
| 1 | A stage is about 5 minutes | Wall clock from Start to result; share of time in slow mode | ≤ 8 min, slow stretches ≤ 40% | `time.secondsPerDay*` in the scenario, or the rework rule (O9) |
| 2 | The loop closes without you (Site reports, Rules names P1, Plan commits, Site confirms) | Who speaks first after the collapse; whether Rules reads P-1 aloud; sim days consumed before the commit | Commit within 90 s wall clock (≤ 4.5 sim days) and no facilitator prompt | Manual front page or P-1 wording; indicator table rows |
| 3 | Deviation F1 is catchable | Does Site ride to the excavator and notice it did not move; does Plan radio again | Caught before the next daily report | F1 wording in `Docs/DESIGN_v0.md` and the manual's P-1 line 6; or drop F1 from Stage 1 |
| 4 | The argument reads as the cost of push | Site listens; Plan chooses hold or demobilize; the PM nag plays; no second argument | Resolved in one commit; players name the rate difference (250 vs 180) unprompted | Foremen lines in the scenario; conflict rule (O12) |
| 5 | Plan cannot see, Site cannot act | Count unprompted "what is happening out there" calls from Plan; count Site attempts to change the plan | ≥ 3 calls per stage; Site never asks the facilitator to move a machine | Plan page inbox text; Site page verbs |
| 6 | Rules has a job in Stage 1 | Hold-point handshakes (P-3) at zone ends: time per handshake; check digits read before a code; ledger filled | Each handshake ≤ 40 s; ledger has permit, pipe, inspection and backfill days for zones 1 and 2 at least | Procedure P-3 wording; card layout in `Paper/make_kit.py` |
| 7 | The forecast is a pull lesson | On Day 17, does anyone count open trench or tokens; Stage 2: pump rented before the rain | Stage 2 team rents the pump or holds excavation; Stage 1 team at least discusses the open feet | A2 page in the manual; token rule |
| 8 | Plan can acknowledge an event with no change | After the rain, Plan commits an empty draft; the clock returns to 5 s per day | Commit button usable with an empty draft | WO-05a item (O13) |
| 9 | No verdicts during play | Count "did we do it right?" questions; your answer is always nothing; what players look at on the result overlay | Players read the overlay as two plans, not as a grade; nobody asks for a score twice | Overlay labels in `Monitor/web/plan.html`; result card fields |
| 10 | The 2D Site page stands in for VR | Move, dismount, inspect wall/water/machine/board, flag, stop work, confirm, listen all used; indicator texts enough for Rules to find the page | Every verb used at least once across the two stages; no inspect text needs the facilitator | `Monitor/web/site.html`; indicator strings in WO-02 section 3 |
| 11 | The Plan page is a planning surface | Stage 2: drag backfill's start, hold with a day count, lend, protect 6+00 to 9+00, keypad; nack texts legible | Each tool used without instruction; the ± 150 ft band understood | `Monitor/web/plan.html` |
| 12 | A 30 s huddle is enough | What Stage 2 changed: backfill start day, protection and where, permit count | At least two of the three changed in 30 s | `plan.huddleSeconds` (45 or 60) |
| 13 | Pull is felt, not announced | Stage 2 finish, idle and peak open trench against Stage 1 on the overlay; the debrief answer (a) | Players name buffer, waiting, or open trench in their own words | Overlay design; debrief questions |
| 14 | Voice placeholders carry the tone | PM lines audible on Plan; foremen lines on Site; anything that made players laugh or miss information | No line missed because of audio | Line text in the scenario; regenerate with WO-05 |
| 15 | The build survives a session | Page reloads, phone drops, host uptime, transcript written at both results | No restart needed; two transcripts in `Monitor/out/` | WO-05a (reconnect as the same seat) |
| 16 | Site and Rules learn LOB too (the imbalance worry) | Debrief (a) and (b) per seat; whether Rules's ledger matches the Plan overlay at the end | Site and Rules describe the lines, not just their own job | Site trailer board; ledger strip; seat rotation for a second session |

## E. Decisions to make (the answer sheet)

Group 1, yours, before the Stage 2 test. Each has a recommendation already in `Docs/DECISIONS.md`; say yes or change it, then dispatch WO-05a.

| # | Question | Recommendation |
| --- | --- | --- |
| O10 | F2 at the job start fires the $10,000 fine on the first tick of Stage 2 | Yes: exclude 0 ft from F2; an unpermitted excavator idles and bills at the start |
| O11 | A stop-work lift has no radio lag | Yes: the lift lands at the next shift unless Site is at the station, like every order |
| O13 (new) | Can Plan acknowledge an event with an empty commit | Yes: an empty commit is "no change, work with the dropped rate" (workbook option A everywhere), logged in the change log; the Commit button stays enabled with an empty draft |
| Env | Unity open during orchestrator runs reaped the dispatch shells; the npm Codex binary cannot start its sandbox; PowerShell blocks `npm.ps1` | Close Unity before runs (or set `CLAUDE_CODE_DISABLE_BG_SHELL_PRESSURE_REAP=1`); run `codex doctor` and add the Defender exclusions it names; `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` |

Group 2, yours, after the test, from the sheet above.

| # | Question | What the data says | Recommendation |
| --- | --- | --- | --- |
| O9 | Stage 1 finishes Day 41.4, not the doc's 36 to 38; the rain rework (1 day per 500 ft) is the biggest lever (4 days lost) | Push loses 2.5 days to pull with events and wins 0.7 without; that is the lesson | Keep 1 day per 500 ft for the first test; if row 1 fails or Stage 1 feels hopeless, halve it (Stage 1 lands near Day 39.5, Stage 2 near 38.1); update the Stage 1 table in the design doc from the measured run either way |
| O12 | A leader idling at a boundary never triggers an argument | Not reachable in the Texas stages | Keep; revisit for Stage 3 |
| O1 | Title | — | LOB, Over, unless the players say it better |
| Pace | 5 s per day, 20 s during events, 30 s huddle | Row 1 and row 12 | Change only the scenario values |
| F1 in Stage 1 | Is the collapse plus F1 plus the argument on Day 6 too much at once | Row 2, 3, 4 | If the team drowned, move F1 to Stage 2 or delay backfill's start to Day 6 in Stage 1 |
| Manual Stage 1 | The Texas page states the city's 2-zone limit while the PM issued all four permits | WO-04 note | Keep (it is the push tension) and add one line to the PM's Day 1 letter: "The city grumbled; I got all four" |
| Cosmetic | The fine prints as "$10000" | WO-04 note | Fix in WO-05a |

Group 3, for Phumpat and Dr. Rybkowski, with the manual PDF. `node Monitor/src/scenario/validate.js Data/Scenarios/stage1_texas.json` prints the full `_validate` list; the questions in plain words:

| # | Question | Current value |
| --- | --- | --- |
| O2 | City fine for excavating past a permit boundary | $10,000 |
| O3 | Open-trench limit for a Texas municipality: zones, or feet at the end of the day | 2 zones (3,000 ft) |
| O4 | Fee and delay to bring a demobilized crew back | $5,000 and 1 day (Texas) |
| O5 | Pumping and re-bedding after heavy rain: 1 day per 500 ft of open trench before pipe laying resumes; a pump halves it | as stated |
| O6 | Drainage pump rental | $4,000 plus $60 a day |
| O7 | Which field deviations happen often enough to teach: order not received, pushing ahead without a permit, wrong station, wrong delivery, late arrival | F1, F2, F3 in Texas |
| O8 | Clearing a collapse with your own excavator: 3 / 2 / 1 days by Small / Standard / Large, lender loses the same days (the workbook's option A column reads 2 / 1 / 0) | 3 / 2 / 1 |
| E1, B1 | Durations where the workbook's two sheets disagree (E1 1 or 3 days; B1 2 or 3) | 3 and 3 |
| Lend | Is borrowing a backfill loader for excavation (E1-C, E2-C) and an excavator for backfill (B1-A) realistic, or should borrowing be limited to like machines | allowed |
| City rows | The Florida, Las Vegas and Los Angeles game values (lead delay, remobilization delay, open-trench limit, situation list) | hypotheses in `Data/catalog.json` |
| Job sign | Pipe class on the sign | 12 in PVC DR18 water main |

Group 4, Phase 1 go/no-go, after the test: start WO-06 (GameLogic reuse investigation) and WO-09 (VR site scene against the Node host) in parallel; decide whether the demo needs the Unity host (WO-08) at all, since the Node host plus the Quest client is a complete setup.
