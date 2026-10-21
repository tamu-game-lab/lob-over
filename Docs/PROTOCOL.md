# PROTOCOL.md — one host, one JSON-over-WebSocket protocol, three seats

Protocol version 1. The host owns the only simulation. Clients are thin: they render the view the host sends and send commands. The same protocol serves Phase 0 (Node host, browser pages) and Phase 1 (Unity host, Quest app, browser pages), so nothing a client does depends on who hosts.

## Transport

- WebSocket, text frames, one JSON object per frame, UTF-8. Path `/ws`. Default port `7331`. The host also serves static pages over HTTP on the same port: `/plan.html`, `/site.html`, `/rules.html`, `/facilitator.html`.
- Every message has `"t"` (type) and `"v": 1`. Unknown types are ignored and logged; unknown fields are ignored.
- The host sends `state` to every client at 4 Hz while the clock runs, and at once after any command that changed state. Clients never compute game state; they render.
- Times: `day` is a float in simulated days from the stage start (`0.0` = the morning of Day 1; the display day is `Math.floor(day) + 1`). `speed` is `"full"` (5 s per day), `"slow"` (20 s per day, during an unresolved event), or `"paused"`.
- Stations: feet from the start of the job as integers; the display form `7+20` means 720 ft. Zones are 1-based.

## Client -> host

```jsonc
{"t":"hello","v":1,"seat":"plan"|"site"|"rules"|"facilitator","client":"web"|"quest","name":"optional"}
// reply: {"t":"welcome","v":1,"seat":"...","stage":"stage1_texas","protocol":1,"state":{...}}
// A second client for a seat that is already taken is accepted as a mirror (read-only) and told so: "mirror": true.

{"t":"cmd","v":1,"id":"c-17","seat":"plan","action":"commit","ops":[ ...PlanOp ]}
{"t":"cmd","v":1,"id":"c-18","seat":"site","action":"moveTo","stationFt":720}
{"t":"cmd","v":1,"id":"c-19","seat":"rules","action":"ready"} // any player seat during a guide beat
{"t":"ctrl","v":1,"id":"c-20","action":"start"|"pause"|"resume"|"continue"|"reset"|"load","stage":"stage2_texas"}   // facilitator only
```

Every `cmd`/`ctrl` gets exactly one `ack` or `nack` with the same `id`.

### Plan ops (sent together in one `commit`; the host applies all or none)

| op | fields | effect in the simulation |
| --- | --- | --- |
| `setStart` | `crew`, `day` | moves a crew's start day; applies at once; nack `"plan locked"` when the plan or the crew is locked, `"crew already started"` after its start |
| `hold` | `crew`, `fromDay` (optional, default the order's arrival), `days` (optional; absent = until `resume`) | flat segment: the crew stops at the order's arrival and bills while held |
| `resume` | `crew` | clears a hold or a stop-work on that crew (Site cannot lift a stop-work; any Plan commit after a stop-work lifts it for the zone's crews unless the commit holds them) |
| `lend` | `machine`, `toCrew`, `days` | the machine leaves its crew at the order's arrival, drives along the haul road (same day) and works for `toCrew`, returns after `days`; lending the excavation crew's machine to `pipe` during a collapse is the `clear_own` response |
| `order` | `machine` (catalog key), `rush`: false | buys a machine: price + $25,000 mobilization, arrives after lead days (+ city lead delay) |
| `protect` | `kind` (catalog protection key), `fromFt`, `toFt` | buys protection for a stretch; it is in place when the crew working that stretch starts there; wrong stretch = deviation F3 |
| `demob` | `crew` | the crew stops billing from the next day; `remob` brings it back after the city delay and the fee |
| `remob` | `crew` | see above |
| `pay` | `item` (an `options[].key` of an event that is active or whose forecast has landed, e.g. `"emergency_shoring"`, `"drainage_pump"`), `machine` (for `hire_excavator`: a catalog key) | pays for a response option at once; its effect is the event's authored one; nack `"no such response now"` otherwise |
| `enterPermit` | `zone`, `code` | opens a zone to excavation if the code matches the zone's card; a code not on file -> `nack reason "code not on file"` (a keypad rejection, not a judgement) |
| `enterInspection` | `zone`, `number` | clears the zone's hold point so backfill may close it |
| `radio` | `orderId` | re-sends an order Site reported as not carried out; it lands at once if Site is at that crew's station, else next shift; costs nothing but the clock |

An order (hold, resume, lend, order, protect, demob, remob) becomes an `Order` object with an `id`, a `crew`, `issuedDay`, `landsDay` (next shift, or `issuedDay` if the Site client is at that crew's current station at commit time), and `status`: `sent`, `landed`, `confirmed`, `notCarriedOut`.

### Site actions

| action | fields | effect |
| --- | --- | --- |
| `moveTo` | `stationFt` | the truck drives to the stake; arrival after `|Δft| / 1500` simulated hours (one zone per hour of game time; tune later) |
| `dismount` / `mount` | — | standing at the stake enables `inspect`, `flag`, `stopWork` |
| `inspect` | `target`: `"wall"|"water"|"pipe"|"machine"|"protection"|"board"|"sky"|"ticket"|"crew"` | the host returns `inspection` with the indicator text for that target at Site's station (from the scenario's ground data and current state); reveals hidden variants |
| `flag` | `stationFt`, `note` (optional, free text ≤ 40 chars) | places an unlabeled marker that appears on the Plan page's LOB at that station |
| `stopWork` | `zone` | crews in the zone halt within one simulated hour, keep billing, mood worsens each day held; stands until Plan commits (a commit lifts it for the zone's crews unless it holds them) |
| `confirm` | `orderId`, `outcome`: `"done"|"notDone"` | closes the order loop; `notDone` sets the order to `notCarriedOut` and appears on Plan's radio log |
| `listen` | — | at a conflict or an idle crew, returns the foremen's lines (`voice` with `lineIds`) |

## Host -> client

```jsonc
{"t":"state","v":1,"day":5.25,"displayDay":6,"speed":"slow","stage":"stage1_texas","view":{ ...per-seat view }}
{"t":"ack","v":1,"id":"c-17","appliedDay":5.25}
{"t":"nack","v":1,"id":"c-17","reason":"plan locked"}
{"t":"event","v":1,"eventId":"ev_p1","day":5.0,"zone":1,"stationFt":720,"card":{"title":"Trench wall collapse at 7+20","facts":["Pipe crew stopped.","Water in the trench bottom."]},"seats":["site"]}
// event cards go to the seats listed; the Plan seat learns of most events only from the daily report or from Site by voice
{"t":"report","v":1,"day":6,"feetByCrew":{"exc":1320,"pipe":720,"back":500},"spendUsd":31200,"notes":["Pipe crew: 0 ft today"]}
{"t":"inspection","v":1,"id":"c-18","target":"wall","text":"Wet clay. The toe of the wall has slumped into the trench; standing water 6 in deep."}
{"t":"voice","v":1,"lineIds":["pm_open_01"],"seats":["plan","site"]}   // clients play Voice/<stage>/<lineId>.wav if present, else show the text
{"t":"result","v":1,"stage":"stage1_texas","finishDay":37.2,"promisedDay":35,"budgetUsd":278400,"spentUsd":262900,"idleCrewDays":9.5,"peakOpenTrenchFt":1310,"finesUsd":0,"deviationsCaught":1,"deviationsMissed":0,"events":[...],"changeLog":[...],"lines":{"planned":[...],"actual":[...]},"previousRun":null|{...}}
```

## Per-seat views (what `state.view` contains)

**Plan** (`seat: "plan"`): `plannedLines` (per crew: start day, segments with slope from the machine rate, holds), `responses[]` (the `pay` options available right now: `key`, `label`, `cost`, `eventTitle`; empty when no event is active or forecast), `reportedLines` (actual feet per crew as of the last daily report; never today's), `budget` (`budgetUsd`, `spentUsd`, `committedUsd`, `deadlineDay`, `promisedDay`), `inbox[]` (dated items from the scenario plus daily reports), `radioLog[]` (orders with status), `catalog` (machines, protection, prices, lead days incl. the city delay), `permits` (per zone: `entered: true|false`, `number` if entered; never whether entering was wise), `inspections` (per zone: cleared or not), `flags[]` (station, day; no text unless Site typed a note), `draft` (ops not yet committed), `uncertainty` (ranges per crew from SR11-style bands; narrows when a report lands), `pmLines[]` (text of PM voice lines played so far).

**Site** (`seat: "site"`): `truck` (`stationFt`, `moving`, `dismounted`), `crews[]` (`id`, `stationFt` live, `working|idle|held|stopped|arguing|demobilized`, `machinePlate` ("Standard"), `moodLevel` 0-3), `trench[]` per 100 ft station (`state`: `undug|open|piped|backfilled`, `water: none|seep|standing`, `wall: sound|slumped`, `protection: none|box|shoring|slope`, `pipeDamage: none|cracked`), `boards[]` per zone (`permitNumber|null`, `inspectionNumber|null`), `sky` (`clear|towersSouth|rain`), `gate` (`trucksWaiting`), `activeEvent` (facts only, if Site is within the zone or has inspected it), `orders[]` landed at Site's current station awaiting confirmation. No plan, no money, no forecast percentages.

**Facilitator** (`seat: "facilitator"`): everything, including hidden variants, pending events and deviations, the two other views side by side. Never shown to players; used by Bryan during table tests and by Playwright during verification.

**Rules** (`seat: "rules"`): exactly `{stage, displayDay, speed}` in `state.view`. No positions, plan, money, daily reports, field events or result overlay. On welcome and reset/load it also receives `kit`: `{t:"kit", v:1, stage, title, job:{sign:{cityCode,pipe,contractDays},lengthFt,zones}, permits:{cards}, inspections:{cards,checklist}, manualPages}`. These are authored job sign, card and ledger facts, not live simulation state. The page loads the filled manual from `manual/<stage>.html`; signatures, checkboxes and ledger notes remain in that page and reset with the run. Rules can send `ready`; simulation commands are unavailable to it.

## Guided playthrough (WO-S7)

The Node host accepts `--guide Data/Guides/slice_guided.json`. The browser host offers Guided and a guide picker. Enabling Guided resets the run and turns Autoplan off; it can be enabled again explicitly. The selected guide must match the selected scenario. Without a guide, the existing clock and commands are unchanged.

On Start, and at each subsequent trigger, the host pauses and sends each player only their own prompt:

```json
{"t":"guide","v":1,"beat":{"id":"b0_brief","index":0,"count":9,"title":"Morning brief"},"prompt":"Find zone 1's permit card. Check the digits, then read the code to Plan.","rulesSection":"permits_holdpoints","ready":{"site":false,"plan":false,"rules":false}}
```

`index` is zero-based. Only the facilitator's message adds `narration`, `prompts:{site,plan,rules}`, and `beats:[{id,title,status}]`, where status is `upcoming`, `current`, `done` or `skipped`. No guide source or other seat's prompt is sent to a player. Between beats, `beat` and `rulesSection` are null and `prompt` is empty; player banners hide. The facilitator keeps the beat list. Joining a running guide receives its current message.

Beats trigger in order on start, authored event id, conflict pair (`back-pipe` corresponds to event id `conflict_back_pipe`), simulation time, backfill within 10 ft of a zone end (including idle at the hold point), or result. The host observes the completed simulation tick: conflict events carry the beginning-of-tick time and pause at the end of that 0.01-day tick. If a later trigger fires before an earlier trigger, the earlier beat is skipped. Triggers reached during a paused trip are retained until the current beat ends.

`cmd ready` marks the sending seat ready, broadcasts the updated ready map and acknowledges once. Once every connected primary player seat is ready, the host waits the guide's `resumeDelaySeconds` of real time, then resumes. Read-only mirrors and the facilitator do not count; no connected players means no automatic resume. A newly connected unready seat cancels a pending countdown. Readiness resets for every beat. `ctrl continue` ends the current beat immediately; any already-triggered next beat pauses immediately. Start/Resume cannot bypass a current guide beat. Reset/load cancel pending countdowns. With no active beat, Ready/Continue return `nack: "no guide beat"`.

Commands remain available during guide pauses. A valid Site `moveTo` while paused advances only the simulation travel duration (`|Δft| / 1500 / 24` days), including the fractional final tick, then pauses again. Other commands do not advance the clock. Guided Site receives only `{t:"result",v:1,stage}` at the result; Plan and the facilitator receive the full overlay. Rules receives the result beat, with its own prompt.

## Daily report timing

At each day boundary the host emits `report` for the day that just ended and advances `reportedLines` for Plan. Site's view is live at all times. This is the one authored asymmetry everything else depends on; do not "fix" it.

## Phase 1 note

The Unity host implements the same messages; the Quest client replaces `site.html`. The C# port is accepted only when the headless Stage 1 transcript (`state` snapshots at each day boundary plus every `event`, `report`, `result`) matches the Node host's transcript for the same scenario and seed within the golden tolerances.

## Level 1 additions (WO-L1)

`rules.planControlsClock: true` allows the primary Plan connection to send `ctrl start`
and `ctrl reset`. Other controls remain facilitator-only; mirrors remain read-only.
The host starts paused. The opening PM line is delivered on Start, once per run.
During an argument, Plan may only commit one `hold` for the trailing crew; other
orders return `The field won't take that order right now` without naming the problem.
Site may observe (`moveTo`, `mount`, `dismount`, `inspect`, `listen`), but cannot change work.

For this mode, Plan's view contains `plannedLines`, `plannedPolylines` (the original
PM plan), `reportedLines` (through yesterday), `held` (booleans keyed by crew),
`promisedDay`, `deadlineDay`, and authored `crews` with `id`, `name`, `foreman`,
`startDay`, `rate` (ft/day). `project` and `plan.locked` retain their meanings.
`siteClient` is the primary Site connection's hello `client`: `quest`, `web`, or
`null` when disconnected. `previousRun` contains the prior run's actual polylines,
or null. These additions carry no live field positions to Plan. Reports omit money.
Site crews add `onSite`: false before their start or while an unstarted crew is held.

At the result Plan receives only `t`, `v`, `stage`, `finishDay`, `promisedDay`,
`idleDays` (per crew), `lines` (`planned`, `actual`), and `previousRun` with the same
public result fields. Site receives only `{t:"result",v:1,stage}`. The facilitator
retains the full result; money remains internal to the simulation and facilitator.
Reset retains the previous completed run for the faint chart overlay.

New voice messages retain `lineIds` and add `turns`, in speaking order:

```json
{"t":"voice","v":1,"day":3.89,"lineIds":["marcus_collapse"],"seats":["site"],"turns":[{"speaker":"foreman_pipe","text":"Wall's moving! Out of the hole, everybody out, now!","clip":"marcus_collapse_1.wav","stationFt":520.2}]}
```

The host also includes the authored line objects in `lines`, including `turns` for
exchanges. Play each `turns[].clip` from `/Voice/<stage>/`, at its speaker's crew
position (`stationFt` is omitted for PM/radio). Do not overlap exchanges; leave
0.3 real seconds between turns. No captions during play. PM reaches Plan only;
foremen reach Site only. Arrivals can repeat on a new display day, at most once
per crew per day; repeat grumbles at most once per pair per day. Other lines play
once per run. Listen near a crew replays its last exchange.

`/play.html` is the Level 1 PC page. Its Rules link serves `/level1/rules.pdf`;
`/level1/rules.html` is also available. Existing stage pages are retained.
With `--discovery`, the Node host sends UTF-8 `LOBOVER 1 <port> <hostname>` to UDP
7332 every two real seconds, using the broadcast address of each non-loopback
IPv4 interface. USB uses `adb reverse tcp:7331 tcp:7331`.
