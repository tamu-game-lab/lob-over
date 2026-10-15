# LOB, Over — Field Manual v0

*Rules seat only. Draft of October 14, 2026, for validation by Phumpat and Dr. Rybkowski. Values marked [to validate] are game hypotheses; everything else is from the LOB-Game catalog or the Disruption Event Library workbook.*

Typesetting notes for `Paper/make_kit.py`: each `##` heading starts a new page; `{{...}}` is filled from the scenario and catalog; section ids in parentheses after headings match `kit.manualPages`. Nothing in this manual says which option is right. It says what each option does and who can check the facts it depends on.

---

## How to use this manual (front_rule)

You are the spec-keeper. You cannot see the site and you cannot touch the plan. Site tells you what it sees; you find the page that matches; you read Plan the effects, the costs and the checks; you issue permits and sign inspections; you keep the zone ledger. The clock runs while you talk: at full speed a day passes every 5 seconds, during an event every 20 seconds.

Three habits:

1. **Make Site describe, not diagnose.** Ask for what it sees: water, wall, machine plate, sky, ticket, who is standing around. The indicator table turns descriptions into a page.
2. **Read procedures aloud, one line at a time.** The procedures at the back name the seat for each line. A procedure read aloud is faster than a discussion.
3. **Check the digits before you read a code.** Add the four digits of any code; the last digit of the sum must equal the digit after the dash (4821-5: 4+8+2+1 = 15). If it does not, the card is a misprint; say so.

### The one rule

**Days lost to an event = rate drop × duration.** The crew behind idles for whatever exceeds its buffer.

- Rate drop is the fraction of the normal rate the crew loses (a crew at 60% of normal has a 40% drop; a full stop is 100%).
- Buffer is the vertical gap in days between the two crews' lines at today's station. Plan reads it off the LOB; Site can estimate it as the distance between the crews divided by the trailing crew's feet per day.
- If days lost ≤ buffer, nobody idles. If days lost > buffer, the trailing crew idles for the difference unless Plan holds it (same idle, but ordered and without the argument) or demobilizes it.

Worked example (checked in simulation): late pipe (P2) at 50% for 3 days costs 1.5 days. With a 1 day buffer, backfill idles half a day. With 1.5 days of buffer it idles nothing.

| Drop | 1 day | 2 days | 3 days |
| --- | --- | --- | --- |
| 30% | 0.3 | 0.6 | 0.9 |
| 40% | 0.4 | 0.8 | 1.2 |
| 50% | 0.5 | 1.0 | 1.5 |
| 60% | 0.6 | 1.2 | 1.8 |
| 80% | 0.8 | 1.6 | 2.4 |
| 100% (stop) | 1.0 | 2.0 | 3.0 |

**Job sign (ask Site to read it):** city {{job.sign.cityCode}}, {{job.sign.pipe}}, {{job.sign.contractDays}} contract days, {{job.lengthFt}} ft in {{job.zoneCount}} zones of {{job.zoneLengthFt}} ft.

---

## City page: Texas (city_texas)

**Signature.** Soft saturated clay over a shallow water table. No rock at all.

| Hazard | Rating | What it means on this job |
| --- | --- | --- |
| Soil cohesion | V.High | Dangerously weak ground when wet; vertical cuts slump at the toe. Treat seeping stretches as OSHA Type C. |
| Bedrock depth | Low | Rock is too deep to reach. E2 cannot happen here. |
| Groundwater table | High | Expect seepage in low stretches; dewatering matters. |
| Precipitation intensity | V.High | Heavy rain is a matter of when. Open trench floods. |
| Temperature extremes | Med | Not a driver on this job. |
| Traffic congestion | Med | Lead days: catalog + {{city.leadDelayDays}}. Remobilizing a crew: {{city.remobilizationDelayDays}} day(s). |

**Situations that can occur here:** E1, P1, P2, P3, B1, A1, A2. Not E2.

**Protection here:** sloping the trench is allowed. A trench box works at full benefit (no running sand). Shoring works.

**Open-trench limit:** the city allows {{city.openTrenchLimitZones}} zone(s) open to excavation at a time (open = permit posted and not yet backfilled and signed) [to validate; comparable rules elsewhere: 300 ft at the end of each day in Sacramento County, 200 ft at any time in Framingham].

**Locates:** Texas811 ticket, 48 hours before digging; 14-day ticket life; 18 in tolerance zone each side of a marked line (hand-dig inside it).

**Permits in this stage:** {{permits.heldByText}}.

---

## City pages for Stage 3 (city_florida, city_las_vegas, city_los_angeles)

Drafts from the Hazard Matrix; the game values are hypotheses [to validate].

| | Florida | Las Vegas | Los Angeles |
| --- | --- | --- | --- |
| Signature | Running sand below the water table. Everything is a dewatering problem. | Caliche hardpan and extreme heat. Bone-dry, no water hazards at all. | Logistics level. Kind ground, brutal traffic, deep water table. |
| Soil cohesion | V.High | Med | Med |
| Bedrock depth | Med | V.High | Med |
| Groundwater table | V.High | Low | Low |
| Precipitation intensity | High | Low | Low |
| Temperature extremes | Low | V.High | Med |
| Traffic congestion | Low | Low | V.High |
| Likely situations | P1, A1, A2 | E2, B1 (heat dries the backfill), A2 as heat | P2, P3, E1 |
| Protection notes | A trench box's benefit is halved in running sand; shoring keeps its full effect | Sloping allowed; rock, not walls, is the hazard | No trench sloping (congested right of way); +2 lead days; limited shift hours |
| Lever that matters | Distance: short open trench | Slope: machine size and moves | Time: lead days and holds |

Full matrix for reference (severity of the hazard, not the quantity):

| Sub-factor | Texas | New York | Chicago | Los Angeles | Las Vegas | Florida | Bangkok | Seoul |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Soil cohesion | V.High | Low | Med | Med | Med | V.High | V.High | Med |
| Bedrock depth | Low | V.High | High | Med | V.High | Med | Low | V.High |
| Groundwater table | High | Low | Med | Low | Low | V.High | V.High | Med |
| Precipitation intensity | V.High | Med | Med | Low | Low | High | V.High | V.High |
| Temperature extremes | Med | Med | V.High | Med | V.High | Low | Med | High |
| Traffic congestion | Med | V.High | High | V.High | Low | Low | V.High | High |

---

## What am I looking at? (indicators)

Ask Site for the words in the left column. Then turn to the page.

| Site says | With the city page saying | Treat as | Page |
| --- | --- | --- | --- |
| Water seeping from the lower wall; a puddle along the toe; clay soft to the thumb; small slabs let go | Groundwater High or V.High | Type C ground; collapse likely here; protection bought ahead applies to this stretch | P1 (before it happens) |
| The wall has slumped into the trench; pipe crew out of the trench; standing water | any | Trench collapse | P1 |
| Machine stopped, operator out of the cab, an orange flag or paint line, someone on a phone | any | Buried utility strike or locate | E1 |
| Bucket bouncing, sparks or dust, the excavator backing off the same spot | Bedrock depth High or V.High | Hard rock refusal | E2 |
| Pipe crew standing at an empty rack; a truck not at the gate; the foreman looking at a ticket | any | Late delivery | P2 |
| Pipe on the ground beside the truck, boom down, slings on the ground | any | Dropped pipe. Ask Site to inspect the joint: cracked is damage; whole is a safety hold only | P3 |
| A tester kneeling on the backfill with a gauge; backfill crew waiting | any | Compaction test (result arrives in Plan's inbox) | B1 |
| A dip or a crack line in a backfilled stretch; water standing on finished grade | Groundwater High or V.High | Settlement | A1 |
| Towers to the south; Plan's inbox says 70% | Precipitation High or V.High | Rain within a day; count the open trench now | A2 |
| Pipe stencil does not match the purchase order (Site reads the stencil, Plan reads the order) | any | Wrong delivery (deviation F4) | P2, option B |
| A box or a lent machine standing at a stake Plan did not name | any | Wrong station (deviation F3); Plan radios the right stake | Procedure P-4 |
| A held crew still working; a stopped excavator still digging | any | Order not received (deviation F1); Plan radios again | Procedure P-1, line 6 |
| Two foremen facing each other, crews standing | any | Conflict: the trailing crew has caught its leader | Procedure P-6 |

---

## E1 — Buried utility or existing pipeline strike (E1)

| | |
| --- | --- |
| Hits | Excavation |
| Cause | Excavation equipment contacts and damages a buried third-party utility, or the project's own pipe laid earlier |
| Rate | 40% drop: excavation at 60% of normal |
| Duration | 3 days (the Situations sheet says 1; the game uses 3) [to validate] |
| Hidden consequence | Slower digging lets the pipe crew catch up to the excavator |

| Option | What it does | Check (who) |
| --- | --- | --- |
| A. Work with the dropped rate | Excavation at 60% for 3 days, no cost | Pipe's buffer behind excavation on the LOB (Plan) |
| B. Hire more equipment | Rate back up after arrival; price + $25,000 mobilization; lead days from the catalog + this city's delay | Lead days (Rules, city page); budget (Plan) |
| C. Borrow the backfill loader | Excavation back up; backfill's line goes flat while its machine is away (it drives along the haul road the same day) | Backfill's buffer behind pipe (Plan); the loader actually moved (Site) |
| D. Buy protection equipment | Rate reset to 100% at once; the most expensive line in the catalog | Budget (Plan) |

Ask Site: is the line marked and is anyone hand-digging inside the tolerance zone? Ask Plan: how many days of buffer does pipe have behind excavation right now?

---

## E2 — Unforeseen ground conditions, hard rock refusal (E2)

| | |
| --- | --- |
| Hits | Excavation |
| Cause | The excavator hits continuous hard rock or bedrock |
| Rate | 60% drop: excavation at 40% of normal |
| Duration | 2 days |
| Hidden consequence | Same risk as E1, faster. Borrowing a machine from backfill moves the delay to them |

| Option | What it does | Check (who) |
| --- | --- | --- |
| A. Work with the dropped rate | Excavation at 40% for 2 days, no cost | Pipe's buffer (Plan) |
| B. Buy the machine to remove the condition | Reset to 100%; the most expensive line | Budget (Plan) |
| C. Borrow the backfill loader | Excavation up; backfill flat while it is away | Backfill's buffer (Plan); the move happened (Site) |
| D. Hire more equipment | Up after lead days; price + mobilization | Lead days (Rules); budget (Plan) |

Cannot occur where bedrock depth is Low (Texas). Ask Site for the machine plate: a Large excavator shortens everything on this page.

---

## P1 — Trench collapse / cave-in (P1)

| | |
| --- | --- |
| Hits | Pipe laying, full stop |
| Cause | Sidewall failure from inadequate shoring or sloping; wet, weak ground |
| Duration | 3, 2 or 1 days to clear with a Small, Standard or Large excavator. The excavator that clears it stops digging for the same days |
| Hidden consequence | Clearing it with your own machine moves the delay to excavation; the cost is not money, it is that crew falling behind |

| Option | What it does | Check (who) |
| --- | --- | --- |
| A. Clear with your own excavator | Pipe at 0% until cleared, then 100%. Excavation stops for the clearing days (3/2/1 by size); the machine drives to the collapse the same day, but the order itself lands at the next shift unless Site is standing with the excavation crew | The plate on the excavator on site (Site). Excavation's lead over pipe in days (Plan): if the lead is less than the clearing days, excavation will not be the only crew waiting |
| B. Emergency shoring | Work resumes in 1 day. $18,000 plus $150 a day for the rest of the job | Budget (Plan) |
| C. Hire an outside excavator | Clears in 3/2/1 days by size after the catalog lead days + this city's delay; price + $25,000 mobilization | Lead days (Rules); budget (Plan) |

**Before it happens.** Where Site reports seepage and a soft toe (indicator table, first row), protection bought ahead changes the collapse, not the chance of it in this game: a trench box ($14,000 + $90 a day) halves the clearing time; shoring ($32,000 + $180 a day) cuts it to 0.4; sloping ($6,000) to 0.7 and is refused in New York, Los Angeles and Bangkok. The box must cover the stretch Site named; Site confirms where it stands.

The front-page rule for backfill: with a Standard excavator, 2 days lost. Ask Plan: what is backfill's buffer behind pipe today? Ask Site: is the backfill crew still moving?

---

## P2 — Late pipe delivery / supply chain lag (P2)

| | |
| --- | --- |
| Hits | Pipe laying |
| Cause | Pipe, fittings or specialty joints arrive late from the supplier |
| Rate | 50% drop |
| Duration | 3 days |
| Hidden consequence | The crew behind keeps working at full speed and can run into the stalled pipe crew unless it is slowed too |

| Option | What it does | Check (who) |
| --- | --- | --- |
| A. Wait for the delivery | 50% for 3 days, then 100%; 1.5 days lost | Backfill's buffer (Plan) |
| B. Alternate supplier | 50% for 1 day, then 100%; premium price [to validate]. Site reads the stencil on arrival against Plan's order: a mismatch is a wrong delivery (F4) | Budget (Plan); the stencil (Site) |
| C. Slow excavation and backfill to match | All three throttled; nobody catches anybody; the whole job slides | Does any crew have its own deadline? (Plan) |

Ask Site: is a truck at the gate, and what does its ticket say? Ask Plan: how many feet of pipe are on the rack (the daily report says)?

---

## P3 — Pipe dropped during lifting / rigging failure (P3)

| | |
| --- | --- |
| Hits | Pipe laying |
| Cause | A rigging failure drops a pipe section during the lift |
| Rate | 80% drop: pipe at 20% of normal while rigging is held for inspection |
| Duration | 2 days (the Situations sheet says 1 to 2) |
| Hidden consequence | Like P2 but worse: backfill can catch up before the pipe issue is even fixed |

| Option | What it does | Check (who) |
| --- | --- | --- |
| A. Hire another mobile crane | Rate up after arrival; price + mobilization; crane lead days are the longest in the catalog (7 to 10) | Lead days (Rules); budget (Plan) |
| B. Hire a safety inspection | Resets the rate to 100% at once when the slowdown is a safety hold; does nothing for a cracked pipe; expensive | Site inspects the joint: cracked or whole |
| C. Slow excavation and backfill to match | All three throttled | Own deadlines? (Plan) |
| D. Work with the dropped rate | 20% of normal for 2 days; 1.6 days lost | Backfill's buffer (Plan) |

Ask Site first: is the dropped section cracked? Replacement sticks are a purchase and a wait; a whole pipe is a safety hold only.

---

## B1 — Poor backfill compaction density, QC test failure (B1)

| | |
| --- | --- |
| Hits | Backfill |
| Cause | A density test on placed backfill fails to meet spec (too wet after rain, too dry in heat, poor lift thickness) |
| Rate | 30% drop: backfill at 70% while it reworks the lift |
| Duration | 3 days (the Situations sheet says 2; the game uses 3) [to validate] |
| Hidden consequence | No crew behind to collide with, but paving over it first means tearing up new pavement to fix it later |

| Option | What it does | Check (who) |
| --- | --- | --- |
| A. Borrow the excavator | Backfill up; excavation's line goes flat while its machine is away (same-day move along the line) | Has excavation finished, or does it have room to spare? (Plan) |
| B. Hire more equipment | Up after lead days; price + mobilization | Lead days (Rules); budget (Plan) |
| C. Hire a compactor | Reset to 100%; expensive | Budget (Plan) |
| D. Work with the dropped rate | 70% for 3 days; 0.9 days lost | Spare days to the deadline (Plan) |

The hold point is the place to catch this: Procedure P-3 before any zone is signed. Ask Site: did the tester's gauge go in the trench or on top of it?

---

## A1 — Trench movement and water-driven settlement (A1)

| | |
| --- | --- |
| Hits | All crews |
| Cause | After backfill, groundwater movement, poor compaction or thin bedding lets the trench settle or shift |
| Rate | 30% drop for all three |
| Duration | 2 days |
| Hidden consequence | All three slow together so nobody catches up, until an uneven fix speeds two crews and leaves one behind, which makes a new collision |

| Option | What it does | Check (who) |
| --- | --- | --- |
| A. Work with the dropped rate | All three at 70% for 2 days | Spare days across the whole site (Plan) |
| B. Hire more equipment | Speeds whichever crews get it; price + mobilization each | Budget (Plan) |
| C. Borrow equipment | Moves speed from one crew to another; the workbook names no source | Which crew has room? (Plan) |
| D. Buy a pump to drain | Excavation and backfill back to 100%; pipe laying stays at 70% | Is pipe laying the crew everyone is waiting on? (Plan reads the LOB) |

Ask Site: where is the water standing, on finished grade or in the open trench?

---

## A2 — Extreme weather (A2)

| | |
| --- | --- |
| Hits | All crews |
| Cause | Heavy rain, high wind, extreme heat or freezing make work unsafe or flood the trench |
| Rate | 50% drop for all three |
| Duration | 2 days |
| Hidden consequence | Like A1 with a bigger slowdown. New rule [to validate]: every 500 ft of trench open on the rain day adds 1 day of pumping and re-bedding before pipe laying resumes in that stretch |

| Option | What it does | Check (who) |
| --- | --- | --- |
| A. Work with the dropped rate | All three at 50% for 2 days | Spare days (Plan) |
| B. Hire more equipment | Speeds whichever crews get it; price + mobilization | Budget (Plan) |
| C. Borrow equipment | The workbook names no source | Which crew has room? (Plan) |
| D. Rent a drainage pump now | Excavation and backfill back to 100% during the rain; pipe laying stays at 50%; the rework after the rain is halved. ${{protection.drainage_pump.priceUsd}} plus ${{protection.drainage_pump.dailyUsd}} a day [to validate] | Count the open-trench tokens (Rules); is the sky to the south dark (Site); forecast percentage in the inbox (Plan) |

The forecast arrives a day ahead in Plan's inbox. The rework after the rain scales with how much trench is open when it starts, which is the pull lesson in one event. Ask Rules (yourself): how many tokens are on the table?

---

## Permits and hold points (permits_holdpoints)

**Permits.** A zone opens to excavation when Plan enters the zone's permit code and the zone board posts the permit number. The city allows {{city.openTrenchLimitZones}} zone(s) open at a time. Excavation that reaches an unpermitted boundary waits there and bills; excavation that crosses one is pushing ahead (deviation F2) and draws a city fine of ${{rules.permitFineUsd}} [to validate].

In this stage the permits are {{permits.heldByText}}. A permit card returns to the deck when its zone is backfilled and signed.

**Hold points.** Backfill may not close a zone until you have signed its inspection card and Plan has entered the card's number. Before you sign, run the checklist with Site:

{{inspections.checklistBullets}}

The compaction result arrives in Plan's inbox the evening backfill reaches the zone end. A failed result is situation B1.

**Codes on the cards in this stage** (check the digits before you read one aloud):

{{permits.cardsTable}}

{{inspections.cardsTable}}

---

## Lookup tables (lookup)

**Machines (LOB-Game catalog).** Idle machines bill. Mobilization $25,000 per order; a released machine refunds half its price.

| Machine | ft/day | $/day | Price | Lead days |
| --- | --- | --- | --- | --- |
| Excavator Small | 165 | $900 | $18,000 | 3 + city |
| Excavator Standard | 220 | $1,200 | $30,000 | 5 + city |
| Excavator Large | 330 | $1,800 | $55,000 | 7 + city |
| Crane Standard | 180 | $1,800 | $45,000 | 7 + city |
| Crane Heavy | 270 | $2,800 | $80,000 | 10 + city |
| Backfill Small | 180 | $1,400 | $20,000 | 3 + city |
| Backfill Standard | 250 | $1,800 | $30,000 | 5 + city |
| Backfill Large | 375 | $2,600 | $50,000 | 7 + city |

City lead delay: Texas, Florida 0; Las Vegas 1; Los Angeles, New York, Bangkok 2 [to validate]. Remobilization delay: 1, 1, 2, 3 days in the same order.

**Protection.**

| Kind | Price | Per day | Effect on a collapse | Notes |
| --- | --- | --- | --- | --- |
| Slope the trench | $6,000 | — | clearing time × 0.7 | Refused in New York, Los Angeles, Bangkok |
| Trench box | $14,000 | $90 | × 0.5 | Benefit halved in running sand (Florida) |
| Trench shoring | $32,000 | $180 | × 0.4 | |
| Emergency shoring | $18,000 | $150 | resume in 1 day | After the collapse only |
| Drainage pump | ${{protection.drainage_pump.priceUsd}} | ${{protection.drainage_pump.dailyUsd}} | rain rework × 0.5 | [to validate] |

**Holding versus demobilizing a crew.**

| | Hold | Demobilize |
| --- | --- | --- |
| Billing | Continues: machine $/day × 1.3 overhead | Stops from the next day |
| Return | At once on resume (next shift unless Site is there) | Fee ${{rules.demobFeeUsd}} + {{city.remobilizationDelayDays}} day(s) [to validate] |
| Crew mood | Worsens each day held; visible in the field, never in the report | — |
| Two-day gap, Standard backfill crew | $4,680 billed ($1,800 × 1.3 × 2) | ${{rules.demobFeeUsd}} fee, and the crew is back {{city.remobilizationDelayDays}} day(s) after the call |

**Money.** Daily overhead is 30% of crew billing. Budget and deadline are on the job sign and the PM's letter. The job is lost when funds go below zero or the deadline passes.

---

## Procedures (procedures)

Read aloud, one line at a time, the seat named first. Do not skip a line because it seems obvious; the lines are the handshake.

**P-1 Stop work**

1. Site: raise the red flag at the station and say the zone. ("Stop work, zone one.")
2. Site: report what you see in words from the indicator table. Do not name the situation.
3. Rules: name the page and read Plan the rate, duration, options and checks.
4. Plan: read the buffer off the LOB and the budget; say which option and which holds; commit.
5. Site: ride to each crew the orders touch; confirm each order as carried out, or report it as not carried out.
6. Plan: if an order was not carried out, radio it again; Site confirms again.
7. Rules: write the days lost in the ledger. Say "clear to resume" only when Site has confirmed the section is cleared; Plan resumes.

**P-2 Zone handshake (permit)**

1. Plan: say which zone excavation will enter next and when.
2. Rules: count open zones against the city limit; if there is room, take the zone's permit card, check its digits, read the code.
3. Plan: enter the code; read back the permit number the board posted.
4. Site: at the next stop, read the number off the zone board. If it differs, say so.
5. Rules: write the permit day in the ledger and place one open-trench token per 500 ft as excavation advances.

**P-3 Hold point (inspection)**

1. Site: report that backfill is at the zone end and read the checklist items you can see (tracer wire, bedding).
2. Plan: read the compaction result from the inbox.
3. Rules: if all three items pass, sign the inspection card and read its number.
4. Plan: enter the number.
5. Site: confirm backfill moved on. Rules: write the inspection day, remove that zone's tokens, return its permit card.

**P-4 Lend a machine**

1. Plan: say which machine, to which crew, for how many days, and where it must go (station).
2. Rules: read the lender's line going flat for the lend days; the machine moves the same day, but the order lands at the next shift unless Site is with the lending crew.
3. Plan: commit.
4. Site: ride to the receiving station; confirm the machine arrived at the right stake (a wrong stake is F3), or report not carried out.

**P-5 Resume or demobilize**

1. Plan: for each held crew, say resume or demobilize.
2. Rules: for demobilize, read the fee and the return delay; for resume, remind that the order lands next shift unless Site is at the station.
3. Plan: commit.
4. Site: confirm the crew moved, or that it packed up.

**P-6 Conflict (the argument)**

1. Site: ride to the two foremen; listen; report what each one says.
2. Rules: read the front-page rule with today's numbers: the trailing crew's feet per day against the leader's.
3. Plan: hold the trailing crew for a number of days, or demobilize it; commit.
4. Site: confirm the crew stood down.
5. Rules: write the idle days in the ledger. Do not say whose fault it was; the lines will.
