# WO-L3 The Plan seat feels like a game

## Goal

`Monitor/web/play.html` becomes the site office described in `Docs/LEVEL1.md` "On the PC": a drafting board, a radio console, a PM with a face, a calendar, sound. Same protocol, same information rules, same tests passing. Bryan's words on the first version: it works, it does not feel like a game.

## Read

- `Docs/LEVEL1.md` ("The seats", "On the PC", "How a run goes", "Voices"), `Docs/VOICE.md` ("Principles"), `Docs/PROTOCOL.md` (Plan view, `state`, `voice`, `result`)
- `Monitor/web/play.html`, `Monitor/web/lib/transport.js`, `Monitor/e2e/` (the Level 1 e2e), `Monitor/src/host/core.js` (what the Plan view carries)
- `Assets/LOBGame/PM/` (the portraits `explaining_Final.png`, `clipboard_Final.png`, `worriedSurprise.png`)

## You may change

`Monitor/web/play.html`, `Monitor/web/img/**` (new), `Monitor/web/lib/**`, `Monitor/e2e/**`, `Monitor/test/**`, `Tools/publish_showcase.ps1` (only to include `img/`). Not the simulation, the host, the protocol or the scenarios.

## Build

1. **Look.** One screen that fits 1366 by 768 and scales up to 1920 by 1080 without scrolling. Warm paper and ink for the board, dark steel for the console, safety orange for the controls. Crew colours used everywhere a crew appears: excavation `#E0A800`, pipe `#2E86C1`, backfill `#D35400`. One display font for titles and one text font, both system or bundled; no CDN.
2. **Top bar.** "LOB, Over · Level 1 · Texas water main"; a calendar tile with the day number that flips with a tick each day; a clock pill reading Paused, Running or "Field is slow today" (from `speed`), with a short radio hiss when it turns slow and nothing on the page saying why; a radio icon with the Site status as now (waiting, headset, browser); Start, which becomes Run again after the result; a mute button.
3. **The board** (left, about 60%). The Line of Balance as a drafting board: stations across (0+00 to 30+00, the two zones shaded), days down, grid in light ink. The PM's planned lines dashed in crew colours. The reported lines inked solid: each new day's segment draws over 400 ms with a pen-stroke easing, from the day the report arrives. A ruler across the board slides down to today. Before Start the board shows the plan alone with the title "The PM's plan: promised Day 18".
4. **The console** (right). Three crew cards: crew colour bar, foreman's name, machine, speed in ft a day, planned start day, and a two-position switch labelled GO and HOLD. Flipping it sends the hold or resume as now, clunks, and chirps; a lamp beside it shows the order state (Go, or "Holding since Day n"). Refusals from the host appear as a strip of red tape across the card with the host's words for 3 s ("The field won't take that order right now"). Below the cards, the PM: the `explaining` portrait while a line plays, `clipboard` otherwise, `worriedSurprise` for 4 s whenever the clock turns slow; a speech bubble with the line's text while its audio plays (the audio as now from `Voice/level1_texas/`). The portraits are copied once into `Monitor/web/img/` by name.
5. **The report strip** (bottom). Each day's report line types itself in over 600 ms with a teletype click: "Day 6 · Excavation +220 ft (1,320) · Pipe +0 ft (520) · Backfill +0 ft (400)", newest on the right, the last five visible.
6. **The result.** A stamp lands on the board (scale-in, 300 ms): "Finished Day 20 · Promised Day 18", then the idle crew-days per crew count up from 0 over a second; the actual lines stay inked over the dashed plan; Run again restarts at Day 1 and keeps the previous run's lines at 25% under the new ones with a small key.
7. **Sound.** All UI sounds synthesized in WebAudio (click, clunk, chirp, tick, teletype, hiss), no sample files; the PM voice as now. Nothing plays before the first click (browser autoplay). Mute silences everything including the PM, and the bubble still shows the text.
8. **Rules link** to the printable page stays, in the top bar.
9. **What stays out.** No live crew positions, no site facts, no money, no events named, no JSON, no debug tables, no verdicts (the stamp states numbers; it never says good or bad).
10. **Tests.** The existing Level 1 e2e passes with the new ids (update it); add checks that the clock pill turns slow without naming a cause, that a refused switch shows the red tape and returns to its real state, that the PM bubble shows the line text, and that the page has no scrollbar at 1366 by 768. Screenshots of the plan before Start, mid-run, the result and Run again go to `Monitor/out/play/`.

## Acceptance checks

- `npm test` and the e2e pass; the Level 1 goldens untouched.
- The orchestrator opens the page against `Play LOB Over.cmd` (or a host on another port) in Chromium at 1366 by 768 and 1920 by 1080: no scrollbar, the board inks day by day, the switch flips with sound, the stamp lands; screenshots reviewed.
- A grep over `play.html` finds no site facts, money, event names or verdict words.

## Out of scope

The host, the simulation, the headset, the stages' pages (`plan.html`, `site.html`, `rules.html`), the GitHub Pages showcase.
