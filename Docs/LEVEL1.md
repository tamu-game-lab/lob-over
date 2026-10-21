# Level 1

Written October 21, 2026, after Bryan's second headset test. Level 1 is the first playable: one PC, one headset, one printed page, about three minutes a run. It is what we build and test next. The slice and the stages stay in the repo unchanged; what they add beyond Level 1 (the order lag, permits and codes, inspections, rain, field mistakes, money, the hub model) comes back later, one level at a time.

Decisions K29 to K36 in `Docs/DECISIONS.md`. Scenario `Data/Scenarios/level1_texas.json` (WO-L1). Rules page `Paper/level1_rules.md`. Headset WO-L2.

## The job

- 3,000 ft of water main in Texas. Stakes every 100 ft; 5+20 is 520 ft from the start.
- Three crews with one Standard machine each, in this order along the trench: Excavation (Ray) 220 ft a day, Pipe (Marcus) 180, Backfill (Dana) 250. A crew never passes the crew in front of it.
- The PM's plan: excavation starts Day 1, pipe Day 2, backfill Day 4. The PM promised Day 18.
- One thing goes wrong. When pipe reaches 5+20 (about Day 4) the trench wall there falls in, and pipe laying stops for 2 days while it is cleared. Then pipe goes on by itself.
- If a crew catches the crew in front, the two foremen argue and both crews stop until Plan holds the crew behind. This happens once per pair; after that a crew that catches up just waits, and the foremen grumble.
- A crew that has not started is not on site yet: holding it delays its start and costs nothing. Once a crew has started, every day it stands still is an idle crew-day.
- The clock runs 4 s per day, and 15 s per day while the wall is down or the foremen are arguing.

## The seats

| | Site (headset) | Plan (PC) | Rules (paper) |
| --- | --- | --- | --- |
| Sees | The site live: the crews, the trench, the fallen wall, the water. Hears the foremen | The chart: the PM's planned lines and what the crews reported up to yesterday | One page: what Site might describe, what it is, how long it lasts, the crew speeds |
| Does | Picks a crew and warps to it, looks, comes back, talks | Start, and Hold or Go for each crew | Says what it is and how long it lasts; works out when one crew will catch another |
| Cannot | Change anything | See the site, or where the crews are today | See the site or the chart |

Nothing tells the team whether a choice was right. The PM's voice pushes to keep every crew moving.

## Starting a game

1. On the PC, double-click `Play LOB Over.cmd` in the LOB-OVER folder. A window opens (leave it open) and the Plan page opens in the browser. The first time, Windows asks whether Node may use the network: allow it on private networks.
2. Put on the headset and open LOB, Over. It finds the PC by itself: over the USB cable if one is plugged in, otherwise over Wi-Fi with the PC and the headset on the same network. Campus Wi-Fi often stops devices from seeing each other; then use the cable or a phone hotspot. The Plan page says "Headset connected".
3. Give the Rules player the printed page (the Plan page links it).
4. Plan presses Start.

## In the headset

- You start in your room (passthrough) and can see and talk to the others. A panel within arm's reach shows the day and three buttons: Ray, digging; Marcus, pipe; Dana, backfill.
- Touch a button, or point at it and pull the trigger. Your room dissolves into the site and you are standing on the road beside that crew. You do not move; the world changes around you.
- In the site you look and listen. Foremen talk from where they stand; when you arrive, the foreman greets you or says what is going on. The only button is Back, on your left wrist. It dissolves the site back into your room.

## On the PC

One page: Start, the chart, and one button per crew that reads Hold or Go. Hold and Go act at once. At the end the page shows the finish day against the promise, the idle crew-days for each crew, and what the crews actually did drawn over the plan. Run again replays the same world, with the previous run drawn faintly underneath.

## How a run goes

Push, the PM's plan:

1. Plan presses Start. Site warps to Marcus and watches pipe go in; at the excavator, Ray says the ground is good and they will make their footage.
2. About Day 4 the wall at 5+20 falls in. Site sees it and hears Marcus get his crew out. Site describes it. Rules finds it on the page: a collapse, pipe stops for 2 days. Plan's chart will not show anything until tomorrow's report.
3. Dana's backfill started Day 4 right behind pipe. Within a day it reaches Marcus's crew and the two foremen argue: 250 ft a day against 180, and whoever wrote the start dates. Both crews stop. Site hears it and says so. Plan holds backfill, which ends the argument.
4. When should Plan press Go for backfill? Rules works it out from the page: pipe needs about 17 days for 3,000 ft plus the 2 days lost, backfill needs 12, so backfill can start well after pipe and still finish right behind it. Too early and Dana waits behind Marcus again; too late and the job finishes later.
5. The result: the finish day against Day 18, and the idle crew-days.

Pull, run again on the same world: the team holds backfill before Day 4 and presses Go when pipe is far enough ahead. The wall still falls, but Dana is far behind, so there is no argument. Starting backfill early barely changes the finish, because pipe sets the pace; mostly it buys idle crew-days and a fight. That is Level 1's lesson.

Measured runs (filled in by WO-L1 from `Monitor/test/scripts/level1_push.json` and `level1_pull.json`):

| Run | Finish (t, display day) | Idle crew-days (exc, pipe, back) | Argument |
| --- | --- | --- | --- |
| Push: hold backfill at the argument, Go two days later | | | |
| Pull: hold backfill on Day 1, Go on Day 9 | | | |

## Voices

Foremen are heard only in the headset; the PM only on the Plan page. Each line plays once. An exchange plays turn by turn, each from its speaker's crew, 0.3 s apart, and exchanges never overlap. The texts follow `Docs/VOICE.md`: claims, not conclusions.

| Id | When | Who | Text |
| --- | --- | --- | --- |
| `pm_open_level1` | Start | PM | Morning. Three thousand feet, three crews. Dig Day 1, pipe Day 2, backfill Day 4. We promised the city Day 18, so keep everybody moving. |
| `pm_hold_level1` | Plan holds a crew | PM | You're holding a crew? We promised Day 18. I hope you have a reason. |
| `pm_end_level1` | End | PM | That's the run. Look at where the lines ended up against what we promised. |
| `marcus_collapse` | The wall falls | Marcus | Three turns, as in `VOICE.md` |
| `arg_back_pipe_stopped` | Backfill catches a stopped pipe crew | Dana, Marcus | Four turns, as in `VOICE.md` |
| `arg_back_pipe_caught` | Backfill catches a working pipe crew | Dana, Marcus | Four turns, as in `VOICE.md` |
| `grumble_back_pipe` | Backfill catches pipe again, at most once a day | Dana, Marcus | Two turns, as in `VOICE.md` |
| `ray_working` | Site arrives at a working excavation crew | Ray | Morning, inspector. Good ground today. We'll make our footage if nobody bothers us. |
| `marcus_working` | Site arrives at a working pipe crew | Marcus | Pipe's going in. One-eighty a day if the walls behave. |
| `marcus_idle` | Site arrives at a stopped pipe crew | Marcus | Nothing to lay until that wall's cleaned up. My guys are standing around. |
| `dana_working` | Site arrives at a working backfill crew | Dana | Dirt's going in fine. Just keep me fed. |
| `dana_idle` | Site arrives at a backfill crew waiting behind pipe | Dana | We're right on Marcus's heels. Nothing to fill until he lays more pipe. |
| `foreman_held_01` to `03` | Each day backfill is held after it started | Dana | As in the slice |

## Later levels (proposed; decided after Level 1 plays well)

1. Level 2: orders reach a crew at its next shift unless Site is standing there, and Site gets a Stop button.
2. Level 3: permits and the open-trench limit, with Rules holding the cards.
3. Level 4: rain, the forecast and the pump.
4. Then inspections at zone ends, field mistakes (an order that never arrived), money, and the Stage 3 cities. The full design stays in `Docs/DESIGN_v0.md`.
