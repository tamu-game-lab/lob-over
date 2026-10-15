# SCENARIO_SCHEMA.md — one file per stage drives the simulation, the pages, the voice and the printed kit

Schema version 1. Files live in `Data/Scenarios/<stageId>.json`. Every number that players will feel is here or in `Data/catalog.json`, so a stage can be retuned without touching code, and every discrepancy between seats is authored, never accidental. Fields whose values await Phumpat and Dr. Rybkowski carry a sibling key `"_validate": "<what to confirm>"`; validators ignore keys that start with `_`.

Conventions: feet are integers from the job start (`720` prints as `7+20`); days are display days (Day 1 is the first morning; the simulation's `t = day - 1`); money is whole US dollars; crews are `exc`, `pipe`, `back`; zones are 1-based.

## Top level

| key | type | meaning |
| --- | --- | --- |
| `schemaVersion` | 1 | — |
| `id` | string | file stem, e.g. `stage1_texas` |
| `title`, `subtitle` | string | shown on the facilitator page and the kit cover |
| `city` | string | key into `cities` in `Data/catalog.json` (lead delay, remobilization delay, slope refusal, hazard row) |
| `seed` | integer | the RNG seed; Stage 2 repeats Stage 1's seed on purpose |
| `mode` | `"push"` or `"pull"` | label only; behaviour comes from `plan`, `permits`, `events` |
| `job` | object | `lengthFt`, `zones[] {id, fromFt, toFt}`, `stationEveryFt` (100), `sign {cityCode, pipe, contractDays}` (the job sign in VR and on the kit card) |
| `time` | object | `secondsPerDayFull` (5), `secondsPerDaySlow` (20), `slowUntil` (`"commit"`: slow speed holds until Plan commits after an event), `shiftsPerDay` (1) |
| `crews[]` | objects | `id`, `name`, `machine` (catalog key), `startDay`, `locked` (true: `setStart` is refused) |
| `plan` | object | `locked` (Stage 1 true), `huddleSeconds` (0 or 30), `preparationAllowed` (false: `protect`/`order` before Day 1 are refused with the PM's line), `promisedFinishDay`, `deadlineDay`, `budgetUsd` |
| `permits` | object | `heldBy` (`"pm"`: all zones open at start; `"rules"`: zones open when Plan enters the card's code), `openZoneLimit` (max zones open to excavation that are not yet backfilled and signed), `cards[] {zone, number, code}` |
| `inspections` | object | `required` (true), `checklist[]` (the three items Rules checks with Site, same for every zone), `cards[] {zone, number}`; backfill may not pass a zone's end until Plan has entered the zone's number |
| `rules` | object | world rules, see below |
| `ground` | object | `soil`, `cohesion`, `groundwater`, `rain` (the Hazard Matrix row, as printed on the city page), `segments[] {fromFt, toFt, type, seepage, indicator{wall, water}}`, `defaultIndicator{wall, water}` |
| `events[]` | objects | authored situations, see below |
| `deviations[]` | objects | authored field deviations F1–F5, see below |
| `inbox[]` | objects | `{day, from, subject, body}` delivered to the Plan page at the start of `day` (daily reports are generated, not listed) |
| `lines[]` | objects | voice line sheet: `{id, speaker, trigger, text}`; `speaker` is `pm`, `radio`, `foreman_exc`, `foreman_pipe`, `foreman_back`; `trigger` names a host event (`stageStart`, `holdCommitted`, `demobCommitted`, `prepRefused`, `orderLanded`, `orderNotDone`, `conflict`, `heldDay1`, `heldDay2`, `heldDay3`, `stageEnd`) |
| `kit` | object | `permitCodeDigits` (4), `checkDigit` (`"digitSumMod10"`: the digit after the dash is the sum of the four digits mod 10), `openTrenchTokenFt` (500), `manualPages[]` (which manual sections to print for this stage) |
| `result` | object | `overlayPreviousStage` (Stage 2: `"stage1_texas"`), `showChangeLog` (true) |

## `rules`

| key | value (Texas stages) | source |
| --- | --- | --- |
| `orderLagShifts` | 1 | decided: a radio order lands at the crew's next shift unless Site is at that crew's station |
| `stopWorkReachHours` | 1 | decided |
| `heldCrewBills` | true | decided |
| `moodStepDays` | 1 | a held or stopped crew's mood rises one level per day held, max 3 |
| `overheadRate` | 0.3 | LOB-Game Sandbox |
| `mobilizationUsd` | 25000 | catalog |
| `demobFeeUsd` | 5000 | recommended, `_validate` |
| (remobilization delay) | not a key in the file: read `cities[city].remobilizationDelayDays` from the catalog; applies to `remob` only (an in-house `lend` moves along the haul road the same day) | catalog, decision K15 |
| `conflictRule` | `"crossWithinNextDay"` | LOB-Game run engine: the trailing crew would cross its leader within the next day; no distance margin |
| `conflictBehaviour` | `"stopAndArgue"` | decided: the job stops, the two foremen argue at the station, Plan resolves by `hold` or `demob` |
| `rainReworkDaysPer500Ft` | 1 | accepted, `_validate` |
| `pumpHalvesRework` | true | accepted, `_validate` |
| `permitFineUsd` | 10000 | placeholder, `_validate` |
| `unpermittedBoundary` | `"idleAndBill"` | decided; crossing is deviation F2 |
| `uncertaintyBandFt` | 150 | Plan's reported-line band before a report lands (SR11-style) |
| `likelihoodRollsApply` | false | authored events always fire; protection changes severity only, so Stage 2's identical seed stays comparable |

## `events[]`

| key | meaning |
| --- | --- |
| `id` | unique, e.g. `ev_p1` |
| `type` | workbook situation: `E1`, `E2`, `P1`, `P2`, `P3`, `B1`, `A1`, `A2` |
| `trigger` | `{"kind":"crewReaches","crew":"pipe","ft":720}` or `{"kind":"day","day":18}` or `{"kind":"zoneSigned","zone":2}` |
| `stationFt`, `zone` | where it shows in VR; `zone` is where stop-work applies |
| `hits` | crew id or `"all"` |
| `rateDrop` | 1.0 = full stop, 0.5 = half speed (workbook) |
| `durationDays` | number, or `durationDaysByExcavator {small, standard, large}` for P1 (the clearing machine's size) |
| `severityByProtection` | multipliers applied to duration when protection covers `stationFt`: `{"trench_box":0.5,"trench_shoring":0.4,"trench_slope":0.7}` (catalog); durations round to the nearest half day |
| `options[]` | `{key, label, cost{usd, perDayUsd}, effect{...}, check{seat, text}}`: the response options Plan may `pay` for, with the check the manual prints and the seat that verifies it. No `whenRight`/`whenWrong` text anywhere in the file (R5) |
| `card` | `{title, facts[]}`: what the event message shows; facts only |
| `cardWhenProtected` | optional `{title, facts[]}` used instead of `card` when protection covers `stationFt` |
| `visibleTo` | seats that receive the `event` message at once (usually `["site"]`); Plan learns from the report or by voice |
| `forecast` | for A2: `{inboxDay, text, skyFromDay}`; the host generates the inbox item from `text` on `inboxDay` (do not repeat it in `inbox[]`), and the event's `options` become payable from that day |
| `rework` | for A2: `{kind:"openTrench", daysPer500Ft, pumpHalves}` |
| `hiddenVariant` | for E1/P3: text the manual branches on, revealed only by `inspect` |

## `deviations[]`

| key | meaning |
| --- | --- |
| `id`, `type` | `F1` order not received, `F2` pushing ahead, `F3` wrong station, `F4` wrong delivery, `F5` late arrival |
| `when` | `{"kind":"firstRadioOrder","ops":["hold","lend"]}` (F1: the first matching op, in commit order, that lands by radio), `{"kind":"firstUnpermittedBoundary"}` (F2), `{"kind":"protectStretchMisses","eventId":"ev_p1"}` (F3), `{"kind":"firstLend"}` (F5) |
| `behaviour` | F1: the order stays `landed` but the crew ignores it until Site `confirm`s `notDone` and Plan `radio`s, or Site stands at the station; F2: the crew crosses `overshootFt` into the next zone once, drawing `rules.permitFineUsd`, unless Site is at the boundary or the crew is held; F3: nothing happens, which is the point; F5: the lent machine arrives `extraDays` late |
| `caughtWhen` | what counts as caught for the result card: F1 a `confirm notDone` before the next report; F2 a `stopWork` or `hold` before the crossing; F3 an `inspect protection` at the wrong stretch before the event; F5 a `confirm notDone` on the lend |

## Validation (WO-01)

`node Monitor/src/scenario/validate.js Data/Scenarios/<file>.json` must check: every machine key exists in the catalog; zones tile `[0, lengthFt]` without gaps; `startDay` ≥ 1; permit and inspection cards cover every zone exactly once with distinct codes whose check digits are correct; every `trigger.crew` and `hits` is a crew id; every `options[].check.seat` is `site`, `plan` or `rules`; every `lines[].trigger` is a known trigger; no key anywhere named `whenRight`, `whenWrong`, `correct`, `verdict`; `_validate` keys allowed anywhere and listed in the output so they can be handed to the validators.
