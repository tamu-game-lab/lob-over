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

## Open (for Bryan; the orchestrator adds a line when a work order surfaces one)

| # | Question | Recommended answer | Needed by |
| --- | --- | --- | --- |
| O1 | Working title | LOB, Over (a radio sign-off; talk is the mechanic). Alternatives: LOB Radio; Three LOB | Kit cover (WO-04); can be changed in one place later |
| O2 | City fine for pushing past a permit boundary | $10,000 placeholder in the scenarios, marked `_validate` | Table test |
| O3 | Open-trench limit for the Texas city | 2 zones (3,000 ft) as the permit count; validators may prefer a footage rule | Table test |
| O4 | Demobilization fee and delay | $5,000 and the city's 1–3 days | Table test |
| O5 | Rain rework rule | 1 day per 500 ft open, pump halves | Table test |
| O6 | Drainage pump price | $4,000 + $60 a day placeholder | Table test |
| O7 | Which of F1–F5 are frequent enough on real jobs to teach | Keep F1, F2, F3 for the Texas stages; F4 and F5 for Stage 3 | Stage 3 authoring |
| O8 | P1 option A's "Day 2/1/0" column in the workbook versus the game's 3/2/1 clearing days | Keep 3/2/1 for both own and hired machines until Phumpat says otherwise | Table test |
| O9 | Stage 1 finish under the full rules (order lag, 2-day clearing, rain at 50% plus 1 day of rework per 500 ft open) lands later than the design doc's "36 to 38", likely 38 to 41 against the promise of 35 and the deadline of 47 | Let WO-02's scripted run report the number, then either keep it (push hurts more) or soften the rework rule to half a day per 500 ft; update the design doc's Stage 1 table from the measured run | After WO-02 |
| O12 | Conflict detection uses the leader's `rate x mult`, so a leader idling at a permit boundary or hold point (mult 1, moving 0 ft) never triggers an argument and the trailer silently idles against it; pipe can therefore never argue with excavation in these stages (crane 180 < excavator 220). Matches WO-02's formula; flagged against the design's "would cross its leader within the next day" | Keep as is for the table test (an argument fires only when both would move); revisit if the Stage 2 test wants an argument at a hold point | Stage 3 authoring |

## How to add a line

Taken: number, one-line decision, one-line reason, where it lives. Open: the question, your recommended answer, when it is needed. Never delete a Taken line; strike it and add the replacement.
