# LOB Crew Design Proposal v0

As of October 14, 2026. Author: Bryan Kim. Source of truth: the Claude doc "LOB Crew Design Proposal v0"; this file is its export for the repo.

LOB Crew (working title, see Decisions) is a three-player cooperative spin-off of LOB-Game. A VR field inspector, a monitor scheduler and a paper spec-keeper deliver one pipeline job twice on the same seeded site, first on the PM's compressed plan and then on their own buffered plan, and a third time in a new city. The seats split the way BOMBANANA splits its three monkeys: Site sees but cannot act, Rules knows but cannot act, Plan acts but cannot see. On the chart, Plan owns the planned lines, Rules owns the limits, and Site owns the actual lines. This is a proposal for discussion; the decisions it needs are listed at the end.

## What changed since the last proposal

- The VR seat is an inspector, not a superintendent (revised October 14, evening). It sees the true state of the site, reports it, holds a stop-work authority, and confirms whether the office's orders reached the crews. It no longer moves machines, places protection or enters codes; Plan does those as plan changes.
- The seats split as BOMBANANA splits its monkeys: Site sees but cannot act, Rules knows but cannot act, Plan acts but cannot see. On the chart, Plan owns the planned lines, Rules owns the limits (permit boundaries and hold points), Site owns the actual lines.
- The monitor's actual line is a day old, from the daily report; Site is live. Orders take time to reach the field, and field deviations (a crew that keeps working, a box at the wrong station) are events only Site can catch.
- Events slow the world instead of freezing it, so talk costs days. Four zones, the identical seed in Stage 2, rain rework that scales with open trench, and demobilization are decided or recommended below.
- Movement in VR is a site truck on the haul road with a stop at every station and a dismount to inspect. No free locomotion.
- The field manual follows the Keep Talking pattern and adds procedures that Rules reads aloud, Captain Sonar style.
- Stage 1 (push) has no huddle and the PM refuses preparation; Stage 2 (pull) adds a 30 second huddle and permits; Stage 3 is Florida, Las Vegas or Los Angeles. A conflict in Stage 1 stops the job and plays as an argument in VR.
- Workbook values are validated by Phumpat and Dr. Rybkowski; the list for them is under Numbers.

## The team and the three levers

A Line of Balance shows planned lines, actual lines, and the limits between them. Each seat owns one of the three, and the agency splits the way BOMBANANA splits its three monkeys. This is the design's core statement: learning the chart and playing the game are the same activity, seen from three sides.

| Seat | Platform | Owns on the LOB | Agency | In construction terms |
| --- | --- | --- | --- | --- |
| Site | Quest; the 2D Site page for players without a headset | The actual lines: what the crews really did today, seen live | Sees but cannot act: inspect, flag, stop work, confirm | Field inspector, the owner's representative |
| Plan | Monitor (web page) | The planned lines: start days, machine assignments and orders, holds and resumes, and the money | Acts but cannot see: every change to the plan and every payment | Scheduler and the PM's office |
| Rules | Paper | The limits: permit boundaries (open trench) and hold points | Knows but cannot act: diagnose, prescribe, issue permits, sign inspections | The spec book and the permit desk |

Everyone reads the LOB, from different sides. The monitor shows planned lines against reported actuals, which arrive at the end of each day, with uncertainty bands and money. The site trailer board in VR shows the live actual lines and the permit limits, never the plan. The paper ledger records permit, pipe, inspection and backfill days per zone. Site is the only seat that sees today; Plan is the only seat that can change tomorrow.

|  | Site only | Plan only | Rules only |
| --- | --- | --- | --- |
| Sees | The site live: trench walls and water, the sky, machine plates (Small, Standard, Heavy), pipe stencils and damage, protection in place or missing, crews working, idle or arguing, trucks at the gate, the permit board at each zone | Planned lines, reported actuals as of yesterday, uncertainty bands, budget, deadline, inbox (supplier dates, forecast percentages, inspector calendar, PM mail), catalog prices and lead days, the radio log of orders sent | Field manual (situation rules, response effects, required checks, procedures), city page, permit book with codes, hold-point checklists, open-trench limit |
| Lacks | The plan, the rules, the money, the forecast | The ground, the rules, whether an order was carried out | The ground, the plan, the money |

Two rules follow. No event can be diagnosed, decided or executed from one seat. No order is known to have worked until Site confirms it.

## What each player can do

Site's verbs are an inspector's verbs: look, report, stop, confirm. Everything that changes the plan belongs to Plan, and everything that releases work belongs to Rules. Site never moves a machine, places protection, holds a crew beyond a safety stop, or enters a code.

**Site (VR)**

| Verb | How | What it changes |
| --- | --- | --- |
| Ride and dismount | The site truck runs along the haul road and stops at any station stake; dismount to stand at the stake. No walking beyond arm's reach | Where Site is, which decides what it can see and which crew an order reaches at once |
| Inspect | Look at a trench wall, a pipe, a machine plate, a delivery ticket or the sky for two seconds; the clipboard reads the indicator out | Turns a range on the monitor into a fact; reveals an event's variant (the game hides E1 and P3 variants until seen) |
| Flag | Plant a flag at a station | An unlabeled marker on the monitor's LOB and in the ledger; Plan has to ask what it is |
| Stop work | Raise the red flag at a station; the crews in that zone halt within the hour and keep billing | A safety hold that stands until Plan commits a plan or resumes; the inspector's one authority |
| Confirm | After an order, watch the crew and press confirm, or report that the order was not carried out | Closes the loop; an unconfirmed order is the commonest field deviation |
| Listen | Stand with the foremen at a conflict or an idle crew; their lines give the cause | The only account of why, which Plan needs to choose between holding and demobilizing |

**Plan (monitor)**

| Verb | How | What it changes |
| --- | --- | --- |
| Set a start day | Drag a crew's first point | The vertical gap between crews |
| Hold or resume a crew | A flat segment from a chosen day; the commit sends a radio call | The crew stops or restarts at its next shift, or at once if Site is at its station |
| Lend a machine | Reassign a machine between crews on the graph | Two slopes change after the city's remobilization delay of 1 to 3 days |
| Order a machine | Catalog with lead days (3 to 10) and $25,000 mobilization per order | A future slope, after arrival |
| Place protection | Buy it and name the station; the simulation places it | Only placed protection softens an event; a wrong station is a deviation Site must catch |
| Demobilize or remobilize | Send an idle crew home, or call it back | Daily billing stops; a fee and a delay to return |
| Pay for a response | Alternate supplier, outside excavator, emergency shoring, pump | An event's duration or severity |
| Commit | Finalize the draft as one transaction | The orders go out over the radio; the crews follow at their next shift |
| Enter a permit code | Type the code Rules reads out | The zone's board in VR posts the permit number and excavation may enter; nothing says whether that was wise |
| Clear a hold point | Type the inspection card number Rules reads out | Backfill may close the zone |
| Radio the field | Resend an order that Site reports as not carried out | Costs the rest of the shift |
| Read the inbox | Supplier dates, forecasts, inspector calendar, PM mail, the daily report | Time-stamped, second-hand facts for the others to verify |

**Rules (paper)**

| Verb | How | What it changes |
| --- | --- | --- |
| Diagnose | Match Site's indicators and the city page to a situation page | Names the event, its rate drop and its duration |
| Prescribe | Read the response options, their effects and the check each one needs | Frames the decision for Plan and Site |
| Read the procedure | The manual's numbered checklists (stop work, zone handshake, lend, resume, demobilize), one line per seat, read aloud | Keeps three seats in order while the clock costs days |
| Compute days lost | Front-page rule and lookup table | Tells Plan the buffer needed before the LOB shows the damage |
| Issue a permit | Hand out a permit card and read its code to Plan; the card returns when the zone is backfilled and signed | Caps open trench. This is the pull lever |
| Sign a hold point | Run the checklist with Site (tracer wire, bedding, test result), then read the card number to Plan | Backfill may close the zone |
| Keep the zone ledger | Mark permit, pipe, inspection and backfill days per zone on the strip | A paper line of balance, compared with the monitor's in the debrief |

## The cooperation loop

Every event runs the same cycle, and each step needs a different seat: Site sees and stops, Rules names and prescribes, Plan decides and commits, Rules releases, Site confirms, the world moves, and the next event or deviation fires. Orders do not reach the field by themselves, so the ring closes only when Site confirms. Between events the same handshake closes each zone, which keeps Rules and Site busy while Plan watches the money.

Worked example, Texas, a trench collapse (P1):

1. Day 6. The world drops to a quarter speed. In VR the wall at 7+20 has slumped into the pipe crew's working zone and the pipe foreman is out of the trench. Site dismounts, raises the stop-work flag for zone 1 (both crews in it halt within the hour, still billing), and reports: "Collapse at seven-twenty. Pipe crew stopped. Water in the bottom. Excavator on site is a Standard."
2. Rules reads Procedure 1, Stop work, line by line, then the Texas page (soil cohesion V.High, groundwater High: wet clay) and the P1 page. Pipe laying stops until the section is cleared: 3, 2 or 1 days by the size of the excavator that clears it, and that excavator stops digging for the same days.
3. Rules applies the front-page rule: two days lost against backfill's one day of buffer means backfill idles one day unless it is held. Plan reads the LOB and the budget. Every 20 seconds of this talk costs a day.
4. Plan commits: lend the Standard excavator to clear the section and hold backfill for two days. The orders go out as radio calls. The event card states what happened and nothing more; in Stage 2 the trench box bought in the huddle simply makes the slump a smaller one, and the team finds out what it was spared only in the debrief.
5. Site rides to the backfill crew. The hold reaches them at once because Site is standing there; otherwise it would land at their next shift. Site confirms both orders. If the excavator has not moved by the next morning (deviation F1), Site reports it and Plan radios again.
6. Rules writes the days in the ledger and issues no permit, since zone 1 is still open. When the section is cleared Plan resumes zone 1, Site confirms that work restarted, and the world runs at full speed until the next event.

(Diagram in the Claude doc: the cooperation loop, six steps around one event. Steps 1 and 5 are Site, 2 and 4 are Rules, 3 is Plan, 6 is the simulation.)

Steps 1 and 5 belong to Site, 2 and 4 to Rules, 3 to Plan; the simulation does step 6. The same ring runs for a zone handshake without an event: Site reports the zone, Rules checks the list, Plan enters the number, Site confirms the crew moved on.

## The VR seat

> Revised October 21, 2026: the headset seat now starts in passthrough and warps into the site; the truck ride is gone. `Docs/SITE_SEAT_V2.md` overrides this section where they differ.

Site is an inspector. The seat is built around noticing, with one authority, because free movement and a long verb list are what make VR tiring and what unbalanced the first draft. Site never changes the plan: moving a machine, placing protection, holding a crew beyond a safety stop and entering codes are Plan's.

- **Movement.** A site truck runs on the haul road beside the trench and stops at any station stake; the player dismounts to stand at the stake. The cab is a fixed frame of reference, turns are snap turns, and there is no free locomotion. Riding is not idle time: an order reaches a crew at once only when Site is standing there, so Site chooses where to be.
- **What only Site sees.** The site as it is today: trench walls and water, protection in place or missing, machine plates, pipe stencils and damage, delivery tickets, the sky, trucks queued at the gate, the permit number posted at each zone board, and crews working, idle or arguing. Held crews get visibly angrier the longer they stand, and the daily report never says so. The monitor's actual line is yesterday's feet; Site is live.
- **The clipboard.** Three buttons on the wrist: Flag, Stop work, Confirm. Inspection needs no button; looking at an indicator for two seconds reads it out. Controllers first; the four verbs map to hand tracking on the Quest 3S later without redesign.
- **Field deviations.** A new event class, one or two per stage, that only Site can catch:
  1. F1, order not received: a crew keeps working after a hold, or keeps digging after a stop. An order lands at the next shift unless Site is there.
  2. F2, pushing ahead: excavation crosses a zone boundary without a permit, and the city fines it. Natural in Stage 1, where every permit was issued; an authored deviation in Stage 2.
  3. F3, wrong station: protection or a lent machine arrives at the wrong stake.
  4. F4, wrong delivery: the pipe stencil does not match the purchase order (a P2 variant).
  5. F5, late arrival: a lent or ordered machine has not shown up when the plan says it has (the remobilization delay).
- **The argument.** At a conflict the foremen meet at the station and say why: the schedule put them a day apart, and a crane lays 180 ft a day while a loader backfills 250. Site listens and reports; Plan chooses between holding the crew and demobilizing it; Site confirms whichever happened.

## The field manual

The manual is the workbook rewritten the way the Keep Talking manual is written. Site describes what it sees, the manual branches on that description plus the city, and nothing in it names the choice. Ben Kane's rule applies: an expert role that is only dense reading does not make good play, so every page ends in a question for another seat.

1. **Front page, one rule.** Days lost to an event = rate drop × duration. The crew behind idles for whatever exceeds its buffer. Checked in simulation: late pipe (P2) at 50% for 3 days costs 1.5 days; a 1 day buffer idles backfill half a day, 1.5 days idles nothing.
2. **City pages**, one per city: the Hazard Matrix row, the level signature, which situations can occur (Texas has no rock, so no E2; Las Vegas has no water or rain), and how responses change here (congested cities add 1 to 2 lead days and forbid sloping the trench; running sand halves a trench box's benefit and calls for shoring). The city's open-trench limit.
3. **Indicator tables** ("What am I looking at?"), keyed by Site's words and the city rating. Water seeping and a wall that slumps when touched, with groundwater High or V.High: treat as Type C, collapse likely (P1), box or shoring, no sloping where the city is congested. Machine stopped, operator out of the cab, orange flag on a line: E1. Pipe on the ground beside the truck, boom down: P3; inspect the joint, cracked is damage, whole is a safety hold only. Forecast 70% on the monitor and towers to the south in VR: A2 within a day.
4. **Situation pages** (E1, E2, P1, P2, P3, B1, A1, A2): cause, what it hits, rate drop, duration, hidden consequence, then options A to D with their effect on rate, other crews, cost and days, and the check each needs with the seat that verifies it. The workbook's When Right and When Wrong verdicts are not printed (R5); the checks they imply are.
5. **Permit and hold-point rules**: the open-trench limit, the checklist before backfill (tracer wire placed, bedding placed, compaction test passed), and what Site must confirm before Rules signs.
6. **Lookup tables**: days lost by drop and duration, machine rates, lead days by city, and the costs of holding or demobilizing a crew.
7. **Procedures**: numbered checklists that Rules reads aloud, one line per seat, the way Captain Sonar's crew calls its turns. Stop work (Site raises the flag, Site reports, Rules names, Plan commits, Site confirms, Rules logs). Zone handshake. Lend a machine. Resume. Demobilize. A procedure read aloud is faster than a discussion, and the clock is running.

Sample situation page, P1 trench collapse. Prices and multipliers are the game's own values (site\_catalog\_v2); durations are the workbook's.

|  |  |
| --- | --- |
| Hits | Pipe laying, full stop |
| Cause | Sidewall failure from inadequate shoring or sloping |
| Duration | 3, 2 or 1 days to clear with a Small, Standard or Heavy excavator; the clearing excavator stops digging for those days |
| Hidden consequence | Clearing with your own machine moves the delay to excavation |
| A. Clear with your own excavator | Pipe at 0% until cleared, then 100%. Check: an excavator on site (Site reads the plate), and excavation's lead over pipe of at least the clearing days (Plan reads the LOB) |
| B. Emergency shoring | $18,000 plus $150 a day, work resumes in 1 day. Check: budget (Plan) |
| C. Hire an outside excavator | Catalog price, 3, 2 or 1 days by size, plus this city's lead days. Check: lead days (Rules' city page) and budget (Plan) |
| Before it happens | A trench box ($14,000 plus $90 a day) cuts likelihood to 0.35 and severity to 0.5; shoring ($32,000 plus $180 a day) to 0.2 and 0.4; sloping ($6,000) to 0.45 and 0.7, not allowed in New York, Los Angeles or Bangkok |

## Paper kit

Everything on paper is generated from the stage's scenario file, so the codes, stations and city match the build. One stage fits in an envelope.

| Item | Contents | Used for |
| --- | --- | --- |
| Field manual | About 12 letter pages, stapled: front-page rule, eight city pages, indicator tables, eight situation pages, permit rules, lookup tables | Diagnosis and prescription |
| Permit cards | Four numbered cards per stage, each with a 4-digit code and a check digit; all pre-signed by the PM in Stage 1 | The open-trench cap |
| Inspection cards | One per zone, with the hold-point checklist and a card number | Clearing hold points |
| Zone ledger strip | Stations across, crews down; boxes for permit day, pipe day, inspection day, backfill day | The paper LOB for the debrief |
| Open-trench tokens | One token per 500 ft; placed when a permit goes out, removed when the zone is signed | A glanceable work-in-process count |
| Pencil and job sign card | The city code, pipe class and contract days, matching the sign in VR | Edgework: the facts every page keys on |

## World rules

- **Time.** 5 seconds per simulated day at full speed (the Sandbox runs 6). When an event fires the world slows to 20 seconds per day until Plan commits, then returns to full speed (decided). Crews under a stop-work order idle and bill; every other crew keeps working. Talk costs days, which is why a procedure read aloud beats a discussion. A 6,000 ft job takes 34 to 39 days plus the slow stretches, about 5 minutes.
- **Job.** 6,000 ft in four zones of 1,500 ft (decided). Every crew starts with one Standard machine: excavator 220 ft a day, crane 180, backfill 250. The pipe crew is the slowest, which is what makes push collide.
- **Reporting.** The monitor is a planning surface fed by the daily report: its actual line steps forward once per simulated day with the day's feet, never the cause, the ground, or whether an order was obeyed. Site is live and carries the why. The plan is revised on the monitor; the reasons to revise it arrive by voice.
- **Orders.** A commit goes out as a radio call and reaches a crew at the start of its next shift, or at once when Site is standing at that crew's station (decided).
- **No verdicts during play.** Nothing dings. The world shows consequences: a held crew bills and gets visibly angrier, an excavator at an unpermitted boundary idles, rework shows up as days, a fine shows up in the budget. Event cards state what happened and nothing else. The comparison of what would have happened, the plan-versus-actual overlay and the monitor's change log (every commit, with its day) appear only at the end of the stage. This follows R5 and the reference games, where a wrong call is discovered later, not announced.
- **Permits.** A zone opens to excavation only when its permit code is entered on the monitor; the zone board in VR posts the permit number. In Stage 1 the PM has issued every permit. From Stage 2 Rules holds the cards, as many as the team chooses within the city limit, and a card returns when its zone is backfilled and signed. Excavation that reaches an unpermitted boundary idles and bills; excavation that crosses one is deviation F2 and draws a city fine.
- **Hold points.** Backfill cannot close a zone until Rules signs the inspection card and Plan enters its number. A failed compaction test (B1) arrives as an inbox result.
- **Stop work.** Site's red flag halts the crews in a zone within the hour. They keep billing, and they get angrier the longer they stand (decided). The hold stands until Plan commits a plan or resumes.
- **Conflicts.** When a trailing crew would cross its leader within the next day (the engine's existing rule), the job stops. The two foremen meet at the station and argue, and the lines say why. Site listens and reports. Plan resolves by holding the crew (idle billing continues: $1,800 a day for backfill plus 30% overhead) or demobilizing it. Site confirms. The PM nags against both.
- **Demobilizing.** Recommended values, to validate: a demobilized crew stops billing from the next day; calling it back costs a $5,000 remobilization fee (one fifth of the catalog's $25,000 mobilization for a new machine) plus the city's remobilization delay of 1 to 3 days (the catalog's values by logistics rating). The current build lists release as missing, so this is new engine work.
- **Rain.** A2 halves every crew's rate for 2 days (workbook). New rule, accepted: each 500 ft of trench open on the rain day adds 1 day before pipe laying can resume in that stretch (pumping and re-bedding); a drainage pump bought ahead halves it. Values to validate.
- **Money.** One live budget. Daily overhead is 30% of direct spend, idle machines still bill, mobilization is $25,000 per order. Deadline is the PM plan's finish plus 12 days; budget is finish × daily fleet cost × 1.3 plus $60,000 (the Sandbox formulas).
- **Win and lose.** The job is lost if funds go below zero or the deadline passes. Finishing is the win. There is no score; the stage's result shows finish day against the PM's promise, money against budget, crew-days idle, peak open trench, deviations caught and missed, fines, and each event with what softened it. Stage 2's result draws Stage 1's lines faintly underneath.
- **Uncertainty.** The monitor shows ranges (SR11). An inspection in VR narrows them at once; the monitor's range narrows a day later when the report lands. Unlike the single-player game, this information costs the Site player's time and position rather than money.

## Stages

The demo is Stages 1 and 2. Stage 3 follows with the same build and new scenario files. Stage 2 runs the identical seed (decided), so the overlay shows one world under two plans. Seats stay fixed through Stages 1 and 2, so the same person feels push and then pull; seats rotate for Stage 3 and replays.

**Stage 1, push, Texas, about 5 minutes.** No huddle. The PM's voice opens the stage: the plan is locked, crews start on Days 1, 2 and 5 with Standard machines, nothing is bought ahead ("not in the budget"), every permit is already issued. The clock runs. One deviation is authored (F1, the pipe crew keeps working after the hold); pushing ahead (F2) needs no authoring, since every permit was issued.

| Day | Event | What the team does |
| --- | --- | --- |
| 6 | P1 collapse at 7+20, in the seepage zone (the same station Training 8 uses) | The loop above; pay or lend |
| 7 | Backfill, started on Day 5, catches the stopped pipe crew | Conflict stop, argument in VR, hold or demobilize |
| 18 | A2 rain, every crew at 50% for 2 days | Open trench floods; rework scales with open feet |
| 36 to 38 | Finish | Result against the PM's promise of Day 35 |

**Stage 2, pull, Texas, identical seed, about 5 minutes.** A 30 second huddle, then the same world. Site rides the line and finds seepage between 6+00 and 9+00. Rules reads the Texas page (groundwater High, rain V.High) and sets the permit count. Plan moves backfill's start to Day 12 and buys a trench box at 6+00; Site confirms it was placed there (F3 if Plan named the wrong stake). The collapse still fires at 7+20 but on the protected branch; the rain on Day 18 meets less open trench; no crew collides. The result card overlays both runs: a later or equal finish, fewer idle crew-days, less rework, deviations caught.

**Stage 3, a new city.** Same levers, new hazards, and each city stresses one seat.

| City | Signature (workbook) | Likely events | Lever that matters |
| --- | --- | --- | --- |
| Florida | Running sand below the water table | P1, A1, A2 | Distance (Rules): short open trench; shoring, since a box's benefit is halved in running sand |
| Las Vegas | Caliche hardpan and extreme heat, bone dry | E2, B1 (heat dries the backfill) | Slope (Site): machine size and moves |
| Los Angeles | Kind ground, severe traffic | P2, P3, E1 | Time (Plan): 2 extra lead days, no trench sloping, limited shift hours |

The event lists are hypotheses from the Hazard Matrix and the site catalog; the construction science side should confirm them.

## Numbers

The machine catalog is LOB-Game's own (equipment\_catalog.json, as recorded in \_pmo\_P1\_spec.md and \_sim\_l0\_audit\_fleet.md). The spin-off reuses it unchanged.

| Machine | Rate (ft/day) | Daily cost | Price | Lead days (normal / rush) |
| --- | --- | --- | --- | --- |
| Excavator Small | 165 | $900 | $18,000 | 3 / 2 |
| Excavator Standard | 220 | $1,200 | $30,000 | 5 / 3 |
| Excavator Large | 330 | $1,800 | $55,000 | 7 / 4 |
| Crane Standard | 180 | $1,800 | $45,000 | 7 / 4 |
| Crane Heavy | 270 | $2,800 | $80,000 | 10 / 5 |
| Backfill Small | 180 | $1,400 | $20,000 | 3 / 2 |
| Backfill Standard | 250 | $1,800 | $30,000 | 5 / 3 |
| Backfill Large | 375 | $2,600 | $50,000 | 7 / 4 |

Mobilization is $25,000 per order, a released machine refunds half its price, and congested cities add 0 to 2 lead days. Rush orders cost 1.75 times the price and are not exposed in the current UI.

Push against pull on a 6,000 ft job with the default fleet and no events, simulated with these rates. Crew cost is daily machine cost times days on site times 1.3 overhead; mobilization and events are excluded.

| Plan | Finish (day) | Idle crew-days | Crew cost | Peak open trench |
| --- | --- | --- | --- | --- |
| Push: starts on Days 1, 2, 3 | 34.3 | 8.3 (backfill) | $196,000 | 1,271 ft |
| Pull by time: backfill starts Day 12 | 35.0 | 0 | $177,000 | 2,422 ft |
| Pull by slope: Small backfill loader at 180 ft a day | 35.3 | 0 | $181,000 | 1,451 ft |
| Pull by distance: digging released within 1,000 ft | 35.3 | 2.5 (excavation) | $185,000 | 998 ft |

Push finishes about one day earlier and pays $11,000 to $19,000 for an idle backfill crew. Each pull plan keeps the buffer in a different place, and each place has a cost: open trench, a slower machine, or an idle excavator. Which one is worth it depends on the city's hazards, which is the Stage 3 lesson.

What still needs validation from the construction science side:

- Every situation in the workbook is marked "practitioner rule of thumb". E1 and P1 could cite regulation instead (Texas811 and Railroad Commission tolerance-zone rules; OSHA Subpart P).
- The Situations and Response Option sheets disagree on duration: E1 is 1 or 3 days, B1 is 2 or 3 days. The game data uses 3 for both.
- Borrowing a backfill loader for excavation (E1-C, E2-C) and lending an excavator to backfill (B1-A): realistic, or should borrowing be limited to like machines?
- Demobilization: $5,000 to remobilize a crew plus 1 to 3 days by city (recommended above).
- Rain rework: 1 day per 500 ft of open trench before pipe laying resumes, halved by a pump bought ahead (recommended above).
- Order lag: a radio order lands at the crew's next shift unless the inspector is at the station (recommended above).
- The open-trench limit for a Texas city. Examples elsewhere: Sacramento County allows 300 ft at the end of each work day, Framingham 200 ft at any time with backfill or steel plates at day's end.
- The field deviations F1 to F5: which of them happen on a real job often enough to teach.

## Departures from LOB-Game's locked decisions

The stakeholders will read this against the single-player game's rules. These are the places where the spin-off breaks or bends one, with the reason.

| LOB-Game rule | This proposal | Why |
| --- | --- | --- |
| Single-player; VR and co-op out of scope (01\_design\_direction) | Three seats, one in VR | The spin-off's purpose |
| SR43: lending leaves the player surface | Plan lends a machine as a plan change on the graph | Four workbook situations rely on borrowing |
| SR18: no planning countdown | A 30 second mobilization countdown opens Stage 2 | Sets the 5 minute pace; the run itself has no planning timer |
| SR46: an event freezes the world and a second card shows what would have happened | An event slows the world to a quarter speed until Plan commits; the card states facts only, and the comparison waits for the end of the stage | Talk costs days, and nothing dings during play; both decided October 14 |
| Run engine: a conflict stops time until the plan is legal | The job stops and the foremen argue in VR; resolving it costs idle days or a demobilization | Decided October 14; push's cost has to be felt, not read |
| R5: When Right and When Wrong never appear to players | The manual prints the checks and who verifies them, never the verdicts | Keeps R5 while giving the paper seat the solution logic |
| Decision 41: the approve and reject verb is out | Hold points are a three-seat handshake, and field deviations are inspection, not packet hunting | A different verb; flagged in case it reads as the same one |
| P1: paid information narrows a range | A Site inspection narrows it for free, at the cost of the player's time and position | What an inspector on site actually does |
| Release and demobilize are missing from the build | Needed to resolve the argument | The audit already lists it as a real expert move |

## Build approach

Implementation is the next conversation. The shape it will take:

- A new Unity 6 project, with LOB-Game's pure C# GameLogic brought in as a local package. Its determinism contract, seeding and ToPlanningView (one view per seat of one truth) are what the design relies on.
- One simulation, one protocol. The PC hosts the simulation (the Unity Editor in Play mode during development, a desktop build at the demo) and speaks one JSON-over-WebSocket protocol to two kinds of client: the Quest app for the VR seat and the browser pages for the Plan seat and for a 2D Site seat. The 2D Site page covers players without a headset and remote play over Zoom. In Phase 0, before any Unity work, a small Node host runs the reference simulation on the same protocol so the three seats can be table-tested with the printed kit; the C# port must reproduce its golden runs before it replaces it.
- Paper's authority enters as typed codes: permit codes and inspection numbers on the monitor.
- One scenario file per stage drives the simulation, the monitor's inbox, the voice lines and the printed kit, so every discrepancy between seats is authored and none is accidental.
- Voice. The scenario file carries a line sheet (id, speaker, trigger, text) for the PM, the radio and the foremen. Placeholders are generated on the Windows PC with Kokoro (Apache-2.0, `pip install kokoro soundfile`, plus the espeak-ng installer for fallback), 24 kHz WAV per line, by Claude Code or Astra in one batch; the same sheet regenerates after every text change. ElevenLabs (`pip install elevenlabs-mcp`, `ELEVENLABS_API_KEY`) is the optional step up for distinct foremen voices, and recorded lines replace either later.
- Claude Code orchestrates and verifies; Astra implements bounded work orders: the web monitor, the scenario schema and validator, the kit and voice generators, and specified C# extensions.
- Order of work: a paper table test of Stage 1 (one afternoon, before any code), then a Stage 1 vertical slice, then Stage 2, then Stage 3.

## Decisions needed

- [x] Four zones of 1,500 ft with a hold point each.
- [x] Events slow the world; talk costs days.
- [x] Stage 2 runs the identical seed.
- [x] Rain rework scales with open trench; values to validate.
- [x] Demobilization: $5,000 and 1 to 3 days, pending validation.
- [x] Validation by Phumpat and Dr. Rybkowski, from the list under Numbers.
- [x] Stop work: held crews keep billing and get visibly angrier the longer they stand.
- [x] Orders land at the crew's next shift unless Site is at its station.
- [x] Reporting: the monitor is a planning surface fed once a day by the report; the why arrives by voice from Site.
- [x] No verdicts during play: facts at the event, consequences in the world, comparison and change log at the end of the stage.
- [x] Permit codes entered by Plan; the zone board posts the permit number, never a green or red.
- [x] Deviations: one or two per stage; pushing past a permit boundary draws a city fine (read from the October 14 answer; confirm).
- [x] Controllers first; hand tracking on the Quest 3S later.
- [x] Voice: AI placeholders from the line sheet now; recorded lines later.
- [x] The site trailer board shows the actual lines and the permit limits.
- [ ] Working title. Recommendation: LOB, Over (a radio sign-off, since talk is the mechanic). Alternatives: LOB Radio; Three LOB.
- [ ] The amount of the city fine for pushing past a permit boundary (for Phumpat and Dr. Rybkowski).

## Sources

Project documents, read in the LOB-Game MD Files folder on October 14: \_sr\_design.md, \_pmo\_locked\_decisions.md, \_pmo\_P1\_spec.md, \_sim\_l0\_audit\_fleet.md, \_sim\_l0\_audit\_protection.md, \_sim\_l0\_research\_timing.md, \_sr\_inv\_findings.md, 01\_design\_direction.md, and the LOB\_Disruption\_Event\_Library\_Final7 workbook.

- [Keep Talking and Nobody Explodes (Wikipedia)](https://en.wikipedia.org/wiki/Keep_Talking_and_Nobody_Explodes)
- [Ben Kane on designing Keep Talking and Nobody Explodes (Voices of VR)](https://voicesofvr.com/?p=927)
- [Bomb Defusal Manual](https://www.bombmanual.com/web/)
- [BOMBANANA! on Steam](https://store.steampowered.com/app/4656000/BOMBANANA/)
- [Captain Sonar rulebook](https://rulespal.com/captain-sonar/rulebook)
- [Operation: Tango, asymmetric co-op design (PlayStation Blog)](https://blog.playstation.com/?p=347233)
- [Harris, Hancock and Scott 2016, Leveraging Asymmetries in Multiplayer Games](https://uwaterloo.ca/touchlab/references/leveraging-asymmetries-multiplayer-games-investigating)
- [Hopp and Spearman 2004, To Pull or Not to Pull](https://iaorifors.com/paper/45676)
- [Tommelein, Riley and Howell 1999, Parade Game](https://pure.psu.edu/en/publications/parade-game-impact-of-work-flow-variability-on-trade-performance/)
- [OSHA 1926 Subpart P Appendix A, soil classification](https://www.osha.gov/laws-regs/regulations/standardnumber/1926/1926SubpartPAppA)
- [Texas811 excavator FAQ](https://texas811.org/?p=1512)
- [Railroad Commission of Texas, Chapter 18](https://www.rrc.texas.gov/media/ap2ck4pb/chapter18-all-effective-feb12-2018.pdf)
- [Sacramento County Section 19, maximum length of open trench](https://saccountyspecs.saccounty.gov/Documents/PDF%20Documents%202016/Specifications/Section%2019.pdf)
- [Framingham trench and street opening standards](https://framinghamma.gov/DocumentCenter/View/42651/2021-Construction-Standards-6---Trench-and-Street-Openings)
- [Unity Relay with Netcode for GameObjects](https://docs.unity.com/relay/relay-and-ngo)
