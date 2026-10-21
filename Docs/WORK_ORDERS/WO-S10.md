# WO-S10 Voices and arguments

## Goal

`Docs/VOICE.md` is implemented: the cast with names, the new triggers, exchanges played turn by turn from each speaker, arguments when a trailing crew catches a waiting leader (O12), grumbles after the first argument without stopping the job, and the slice's line sheet, generated with Kokoro. The JavaScript and C# simulations stay in parity.

## Read

- `AGENTS.md`, `Docs/VOICE.md` (whole), `Docs/SCENARIO_SCHEMA.md`
- `Monitor/src/sim/conflict.js` and the voice selection code, `Assets/LOBOver/Sim/**` (the C# port), `Tools/voice/make_voice.py`, `Data/Scenarios/slice_texas.json`

## You may change

- `Monitor/src/sim/**`, `Monitor/src/scenario/**` (validation of turns and trigger qualifiers), `Monitor/test/**`, `Monitor/web/**` (turn-by-turn playback)
- `Assets/LOBOver/Sim/**`, `Sim/**`, `Assets/LOBOver/Runtime/**` (turn-by-turn spatial playback)
- `Tools/voice/**`
- `Data/Scenarios/slice_texas.json`, `stage1_texas.json`, `stage2_texas.json`: the `lines` arrays only, exactly as `VOICE.md` gives them (the stages with "seven-twenty")
- `Docs/SCENARIO_SCHEMA.md`

## Build

1. **Schema.** A line has either `speaker` and `text`, or `turns: [{speaker, text}]`; trigger qualifiers `eventType`, `crew`, `status`, `state`, `zone`, `pair`, `cause`. The validator checks them.
2. **Simulation, JavaScript first.** New triggers `event`, `arrive`, `sky`, `holdPoint`, `conflictRepeat`; conflict causes `caughtUp`, `leaderStopped`, `leaderWaiting`. The O12 projection uses the leader's movement over the last hour of simulated time. The first argument per pair per stage stops both crews (unchanged); later catches emit `conflictRepeat` at most once a day per pair, with no stop. Voice messages carry the line id; clients look up the turns in the scenario.
3. **Parity.** The orchestrator regenerates `Data/transcripts`. Port every change to the C# simulation; parity passes on all transcripts plus a new script `slice_permit_late.json`, in which Plan enters zone 2's code only after the excavator reaches 15+00, so the pipe-excavation argument must fire.
4. **Lines.** Apply `VOICE.md`'s slice sheet to `slice_texas.json`, and the same lines with "seven-twenty" to the two stages.
5. **Voice files.** `make_voice.py` renders turns as `<lineId>_<n>.wav`; `voices.json` gains the cast's names and three clearly different foremen (two male, one female); regenerate the slice and both stages.
6. **Playback.** The web pages and Unity play turns in order, 0.3 s apart; Unity plays each turn from its speaker's position (the model's crews in the hub, full scale in the site).

## Acceptance checks

```
npm --prefix Monitor test
dotnet test Sim/LOBOver.Sim.Tests -c Release     # parity on all transcripts, slice_permit_late included
```

- A headless `slice_play` run shows: the collapse exchange at t 3.89, the back-pipe argument as four turns, Ray's arrive line on Day 6, the sky line on Day 8, Dana's hold-point line at zone 1; `slice_permit_late` shows the pipe-excavation argument.
- A Kokoro WAV exists for every turn; the Unity tests pass; Bryan listens to the slice in the headset. His reply is the result.

## Out of scope

Live voice agents, recorded voices, Stage 3 lines.
