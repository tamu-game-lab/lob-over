# DECISIONS.md — decisions taken during implementation

Design decisions live in the design doc (`Docs/DESIGN_v0.md`, "Decisions needed"). This file records the smaller calls made while building, so nobody re-litigates them by accident, and the questions that are waiting for Bryan. The orchestrator appends; Bryan edits.

## Taken (October 14, 2026, kickoff)

| # | Decision | Why | Where it lives |
| --- | --- | --- | --- |
| K1 | The PC hosts the simulation; the Quest is a client; browser pages are the Plan seat and the 2D Site fallback for good | One protocol, one truth; Zoom and no-headset play for free; the Unity host can be skipped for the demo | `Docs/PROTOCOL.md`, `Docs/ROADMAP.md` |
| K2 | Phase 0 is HTML and Node first, Unity after the table test | The three seats and the kit can be table-tested in days; the C# port then has an oracle | `Docs/ORCHESTRATOR.md` |
| K3 | `rateDrop` is the fraction lost; a crew works at `1 - rateDrop` | Matches the workbook's "Base Rate Drop" column | `Data/catalog.json`, `Docs/SCENARIO_SCHEMA.md` |
| K4 | Authored events always fire; protection changes severity, not likelihood | Stage 2's identical seed must meet the same collapse; the catalog's likelihood multipliers are kept for a later random mode | scenario `rules.likelihoodRollsApply` |
| K5 | A trench box is bought for a stretch (from–to) and is in place while the pipe crew works inside it; a stretch that misses the collapse station is deviation F3 | Gives Plan a station to name and Site a thing to check | `protect` op |
| K6 | Durations under protection round to the nearest half day | Keeps the arithmetic on the front page readable | WO-02 |
| K7 | A conflict argument fires once per crew pair per stage; afterwards a trailing crew that catches its leader idles against it, bills and gets angrier, with no second argument | The foremen have had it out once; a re-firing argument would deadlock any plan with one hold | WO-02 |
| K8 | After a stop-work, any Plan commit lifts it for the zone's crews unless the commit holds them | The doc's "stands until Plan commits a plan or resumes" | WO-02 |
| K9 | Overhead (30%) applies to crew billing and protection daily fees; purchases, mobilization, fees and fines at face value | Simplest reading of "30% of direct spend"; the result card shows both | WO-02 |
| K10 | Codes are `dddd-c` with `c = digit sum mod 10`; Rules checks the digits before reading a code | Paper edgework the Rules seat can do without the build | `kit.checkDigit` |
| K11 | F1 applies to the first hold or lend delivered by radio, whichever crew it hits | Rewards Site's positioning instead of scripting one crew | scenario `deviations` |
| K12 | The drainage pump restores excavation and backfill to 100% during rain (workbook A2-D) and halves the new rework rule | Keeps the workbook's effect and adds the pull lesson | scenario A2 options |
| K13 | Stage 2 keeps Stage 1's contract (promise Day 35, budget $278,400, deadline Day 47) | The overlay compares like with like | `stage2_texas.json` |
| K14 | Stage 2's example plan moves backfill to Day 12 (not 11) | Day 12 is the zero-idle start with no events; with the collapse it still holds | design doc, `stage2_texas.json` note |
| K15 | An in-house lend moves the machine along the haul road the same day (`|Δft| / 1500` hours); the city's remobilization delay applies to `remob` after a demobilization and to hired machines' lead time | A machine 400 ft up the trench does not take a day to arrive; the order lag (next shift unless Site is there) already costs the day the design wants | WO-02, manual P1-A, P-4 |
| K16 | Rain rework is evaluated at the rain's end over the feet open at its start; a pump paid for before the rain ends halves it; the pump also restores excavation and backfill to full rate during the rain (workbook A2-D) | Lets the forecast matter: `pay` is allowed from the forecast day | WO-02 |
| K17 | F2 requires a boundary between zones; an unpermitted excavator at 0 ft idles and bills, with no overshoot or fine | O10: preserve F2 for the first real boundary | WO-05a |
| K18 | A Plan commit lifts stop-work through resume orders with normal order lag, unless it holds the crew | O11: stop-work lifts follow the same delivery and confirmation rules as other orders | WO-05a, K8 |
| K19 | An empty Plan commit acknowledges no change, restores full speed and records a no-change log entry | O13: Plan can close office time without changing its draft | WO-05a |
| K20 | The working title is LOB, Over | O1, Bryan, October 21 | everywhere |
| K21 | The construction numbers stand as recommended until Phumpat and Dr. Rybkowski validate them: $10,000 permit fine (O2), two zones open (O3), $5,000 plus the city's delay to remobilize (O4), one day of rework per 500 ft open with a pump halving it (O5), pump $4,000 plus $60 a day (O6), F1 to F3 in Texas and F4 to F5 for Stage 3 (O7), 3/2/1 clearing days (O8) | Bryan, October 21: easy to tune later | scenarios, catalog, `_validate` keys |
| K22 | The rework rule stays at one day per 500 ft for now; the measured Stage 1 push finish (Day 41.4) stands and the design doc's Stage 1 table follows the measured runs; revisit after the table test if Stage 1 feels hopeless | O9, Bryan, October 21 | `DESIGN_v0.md`, `TABLE_TEST.md` |
| K23 | Arguments are kept and expanded: four-turn two-foreman exchanges; arguments also when the trailing crew catches a waiting leader (permit boundary, hold point); two-line grumbles without a stop after the first argument of a pair | O12, Bryan, October 21: the two-voice argument is the most valuable moment | `VOICE.md`, WO-S10 |
| K24 | The headset's XR rig comes only from Meta Building Blocks (Camera Rig, Passthrough, Controller Tracking, Interactions Rig, Real Hands); no code builds, copies or reconfigures a rig; interaction goes through the Interaction SDK; no locomotion and no gravity; the builder edits only `[LOBOver]` roots | Bryan, October 21: blocks are more reliable, and the code-built rig fell through the world | `SITE_SEAT_V2.md`, WO-S6 |
| K25 | The headset seat starts in passthrough (team visible, the site as a model on the table) and warps into the full-scale site with a shader dissolve; the truck ride is removed | Bryan, October 21: players in one room must see and talk to each other; the warp reads well for stakeholders | `SITE_SEAT_V2.md`, WO-S8 |
| K26 | In a team game the headset is a client of the Node host on the PC (USB with `adb reverse`, or the LAN); the embedded simulation remains for the solo demo | Three seats must share one simulation | `SITE_SEAT_V2.md`, WO-S9 |
| K27 | Voices give claims, never conclusions; each voice reaches one seat; deviations hide in normal lines; no verdict voices | Bryan's concern that the headset player would only relay what a voice says | `VOICE.md` |
| K28 | A guided mode pauses the clock at each beat of `Docs/PLAYTHROUGH.md` until every seat is ready; for rehearsals and demonstrations, not for study sessions | Bryan, October 21: demonstrate the cooperation one handoff at a time | `PLAYTHROUGH.md`, WO-S7 |
| K29 | Level 1 is the next build: the slice's job and crews with one event (the collapse at 5+20, clearing itself after 2 days) and the argument it causes; Plan's only control is Hold or Go per crew, acting at once; no order lag, permits, codes, inspections, rain, field mistakes or money on screen | Bryan, October 21, after the second headset test: simple mechanics first, complexity added level by level | `LEVEL1.md`, `level1_texas.json`, WO-L1 |
| K30 | In Level 1 the headset player looks and talks only: from the hub they warp to a chosen crew and back; the only button in the site is Back | Bryan, October 21 | `LEVEL1.md`, WO-L2 |
| K31 | In Level 1 the Rules player has one printed page | Bryan, October 21 | `Paper/level1_rules.md` |
| K32 | Team game now: the headset joins the PC's game; `Play LOB Over.cmd` starts the host and opens the Plan page; the headset finds the host over USB (`adb reverse`) or by a Wi-Fi broadcast; the Quest build has no embedded simulation. Replaces K26's solo demo and host field | Bryan, October 21 | WO-L1, WO-L2 |
| K33 | The Level 1 hub is full passthrough with one panel (status and three crew buttons) within reach; K25's table model, PM standee and stake markers are deferred; the warp moves the site, never the rig | Bryan, October 21: touching the panel should take you in | WO-L2 |
| K34 | Real Hands stays out for now: it switches the headset to unpremultiplied alpha and draws hands with Meta's deprecated selective-passthrough shader, which conflicts with a passthrough-first scene and with the warp; revisit for the site later. Amends K24's block list | October 21 headset test: a passthrough hole and an unreadable panel | WO-L2, Check Building Blocks |
| K35 | A headset work order is accepted only after Bryan's check in the headset; Editor tests, screenshots and logcat are necessary but never sufficient | October 21: WO-S6 passed every machine check and failed in the headset | `ORCHESTRATOR_SHOWCASE.md` |
| K36 | A crew that has not started is not on site: holding it delays its start at no cost; once started, every day it stands is an idle crew-day | Otherwise a pull plan (a later start) would count as idle time | WO-L1 |

## Open (for Bryan; the orchestrator adds a line when a work order surfaces one)

| # | Question | Recommended answer | Needed by |
| --- | --- | --- | --- |
| O14 | Where do the three players talk? | In one room: the headset player sees the others through passthrough. Remote players use the web seats over Zoom; voice chat inside the headset is out of scope | Before any remote session |
| O15 | Is this the voice concern: the headset player repeating what a voice says instead of working out what changed and what the problem is? | Yes; `VOICE.md` "The problem to avoid" is written on that reading | Before WO-S10 |
| O16 | Live AI voices (players question a foreman) | Not now; if tried, `VOICE.md`'s principles bind them | After the showcase |

## How to add a line

Taken: number, one-line decision, one-line reason, where it lives. Open: the question, your recommended answer, when it is needed. Never delete a Taken line; strike it and add the replacement.
