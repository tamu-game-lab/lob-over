# Three-seat playthrough of the slice

Written October 21, 2026. A beat-by-beat walkthrough of the three-minute slice (`Data/Scenarios/slice_texas.json`) that shows how Site, Rules and Plan cooperate, one handoff at a time. Use it three ways: as the script for a rehearsal, as the narration for stakeholders, and as the source of the guided mode (`Data/Guides/slice_guided.json`, WO-S7), which pauses the clock at each beat until every seat presses Ready.

> Later on October 21: this walkthrough is for the slice. Level 1's walkthrough is "How a run goes" in `Docs/LEVEL1.md`.

The numbers are from the measured push run (`Data/transcripts/slice_play.json`). Sample lines are for rehearsal only; in real play everyone uses their own words.

## Setup (10 minutes)

1. Host on the PC: `node Monitor/src/host/server.js Data/Scenarios/slice_texas.json --guide Data/Guides/slice_guided.json`, or the showcase's host page with Guided ticked. The facilitator page on the PC or a TV shows the narration.
2. Plan: a laptop on `plan.html`.
3. Rules: `rules.html` on a tablet or laptop (the manual, the cards, the ledger), or the printed slice kit.
4. Site: the headset in Team game (USB: `adb reverse tcp:7331 tcp:7331`, host `127.0.0.1`), or `site.html` on a laptop or phone.
5. Everyone in one room, able to see each other. The facilitator says only: three jobs, talking is the game, the manual belongs to Rules, and nothing will say whether a choice was right.

## Who holds what

| | Site | Rules | Plan |
| --- | --- | --- | --- |
| Sees | The site live: walls, water, crews, plates, boards, sky | The manual, the cards, the ledger | The planned lines, yesterday's reported feet, the money, the inbox |
| Can do | Go somewhere, look, flag, stop work, confirm | Diagnose, read procedures, issue permits, sign inspections | Every change to the plan and every payment |
| Cannot | Change the plan | See the site or the plan | See the site |

## Beat 0. Morning brief (Day 1, clock paused)

- Narration: Three people, one pipeline. Plan holds the schedule and the money but cannot see the site. Site stands on the site but cannot change anything. Rules holds the manual and the permits and sees neither. The PM has already written the plan.
- Plan hears the PM on the laptop: dig Day 1, pipe Day 2, backfill Day 4, promise Day 18. Plan reads the three planned lines and asks Rules for zone 1's permit.
- Rules finds zone 1's card, checks the digits (5 + 1 + 7 + 4 = 17, and the check digit is 7) and reads "five one seven four, seven" to Plan.
- Plan types `5174-7`; the zone 1 board in the field posts `TX-0311`.
- Site reads `TX-0311` off the board, so the handshake closes. Then Site goes to 4+00 to 7+00 and looks. Sample: "The clay at the bottom of the wall is soft, little slabs are falling off, and water is seeping in."
- Rules turns to the indicator table: seepage, a soft toe and high groundwater mean Type C ground, collapse likely in that stretch, and protection bought ahead applies there. Rules reads that aloud; nobody is told what to do.
- What it shows: the three-way split, and the permit handshake (Procedure P-2). In the push run the team keeps the PM's plan and buys nothing.

## Beat 1. The wall at 5+20 (Day 4)

- Narration: The pipe crew reaches the stretch Site looked at. Only the headset sees what happens next.
- Site hears Marcus shout the crew out of the trench, sees the slumped wall and the water, and presses Stop work for zone 1. Sample: "The wall at five-twenty fell in, water in the bottom, Marcus pulled his crew out. The excavator here has a Standard plate."
- Rules reads Procedure P-1 line by line, then the P1 page: pipe laying stops until the section is cleared; 3, 2 or 1 days by excavator size, and the excavator that clears it stops digging for those days. Option A clears with your own excavator; B is emergency shoring at $18,000 plus $150 a day, back in one day; C hires an outside excavator after its lead days. Rules applies the front-page rule: a full stop for 2 days with a Standard machine.
- Plan reads the LOB: backfill started Day 4 and is right behind pipe, so there is almost no buffer. Plan commits option A: lend the excavator to clear the wall. The radio says the order goes out at the next shift.
- What happens: the order will land at the next shift, because Site is not standing with the excavation crew.
- What it shows: nobody can resolve the collapse alone; seeing, knowing and acting are in three heads.

## Beat 2. Two foremen (Day 5)

- Narration: Backfill was started close behind pipe. With pipe stopped, the loader runs into it, and the foremen say why.
- Site goes to the two foremen and listens: Dana's loader has nowhere to put dirt; Marcus's wall is in the ditch; Dana lays 250 ft a day, Marcus 180; Dana says the schedule put her there. Both crews stop. Site relays it, numbers included.
- Rules reads Procedure P-6 and puts the numbers through the front-page rule: backfill gains 70 ft a day on pipe and pipe has stopped, so backfill idles unless it is held. The hold-or-demobilize table: a two-day hold of a Standard backfill crew bills $4,680; sending it home costs $5,000 and a day to return.
- Plan holds backfill for two days. The PM says "You are holding a crew that bills eighteen hundred a day." Because Site is standing with the backfill crew, the hold lands at once, and Site confirms it.
- What it shows: the cost of push, felt as an argument between the people who live it. The slope mismatch is spoken, not shown. Site's position decides how fast an order lands.

## Beat 3. Did the orders land? (Day 6 morning)

- Narration: A new shift. The office believes its orders reached the crews. Only Site can check.
- Plan reads the radio log: the lend went out yesterday and shows as landed. Plan asks Site to check the excavator.
- Site goes to the excavator at 9+00. Ray says good morning, good ground, they will make their footage. He is still digging. Sample: "Ray's still digging at nine hundred. He never got the order." Site confirms the order as not carried out.
- Plan radios the order again; because Site is standing there, it lands at once, and Site confirms it done.
- Rules writes the days in the ledger (Procedure P-1, line 6).
- What it shows: a field deviation (F1). The daily report would have shown the excavator's feet as if all was well; only the field can catch it.

## Beat 4. Weather and the next zone (Day 8)

- Narration: Two signals at once: a forecast in the office and dark towers over the site. And excavation is closing on the end of zone 1.
- Plan reads the inbox: 70% chance of heavy rain tomorrow through the next day. Plan asks how much trench is open.
- Site looks south, hears Marcus about the ditch turning into a creek, and counts the open stretch between the excavator and the backfill crew.
- Rules reads the A2 page: every crew at half speed for two days, then a day of pumping and re-bedding per 500 ft open before pipe resumes, halved by a pump ($4,000 plus $60 a day). Rules counts the open-trench tokens. When Plan asks for zone 2, Rules checks the city limit (two zones open) and reads `2936-0`.
- Plan enters zone 2's code and decides about the pump. In the push run the team keeps the money.
- What it shows: the forecast as a pull signal, open trench as work in progress, and the permit as a cap on it.

## Beat 5. Rain (Day 9)

- Narration: Rain. Every crew at half speed for two days, and the open trench fills.
- Site says where the water stands and how far it runs. Rules works out the rework days. Plan commits a change or commits with no change to carry on.
- What it shows: the open trench is paid for in rework days.

## Beat 6. Closing zone 1 (about Day 16)

- Narration: Backfill reaches the end of zone 1. Nothing closes without a check from the field, the office and the spec book.
- Site hears Dana say zone 1 is ready, tracer wire down, bedding in; Site looks for both and says what is actually there.
- Plan reads the compaction result from the inbox.
- Rules runs Procedure P-3 with both, signs zone 1's inspection card and reads `8403-5`; Plan enters it; backfill moves into zone 2; Rules removes zone 1's tokens and returns its permit card.
- What it shows: the hold point as a pull handshake that releases work.

## Beat 7. Closing zone 2 (about Day 23)

- The same handshake at 30+00 with inspection card `6251-4`.

## Beat 8. The lines (Day 23)

- Narration: The job is done. Nobody was told whether any call was right. Look at where the lines ended up against the promise.
- The push run's result: finish Day 23.0 against the promise of Day 18 and the deadline of Day 30; 17.2 idle crew-days (excavation 4.1, pipe 5.2, backfill 8.0); $125,533 of $172,320; peak open trench 1,059 ft; the field deviation caught.
- Plan shows the overlay: planned against actual, the flat stretches, the change log. Rules compares the paper ledger with it. Site names the moment remembered from the field.

## The second run (pull), in short

Reset the same world. In the huddle: backfill starts Day 8, a trench box goes on 4+00 to 7+00 (Site named the stretch in Beat 0; Site confirms the box stands where Plan said, which is the F3 check), and the pump is rented when the forecast lands. The collapse comes as wall movement against the box (2.2 days lost instead of 3.2); the argument comes later, on Day 11; the rain costs 2.5 days instead of 2.0, because more trench is open even with the pump. Finish Day 22.3; idle crew-days 10.1 instead of 17.2; but peak open trench 1,451 ft instead of 1,059. Holding backfill back moved the buffer into open trench. Pull is not free; where the buffer sits has a cost, and that is the Stage 3 lesson.

## Timings for rehearsal

At the slice's pace (4 s a simulated day), the clock runs about 96 s in total; in guided mode each beat adds the time the team talks, typically 30 to 90 s. A full guided run takes 8 to 12 minutes, the pull rerun 5 to 8 more.
