# Voices

Written October 21, 2026. Who speaks, what a voice may say, and the line sheet for the slice. Applies to every scenario; WO-S10 implements it.

## The problem to avoid

The headset player hears voices the other two cannot. If a voice says what happened and what to do, the headset player repeats it, the other two act on it, and nobody works out what is different from the plan or what the problem actually is. The game becomes relay without reasoning. A voice must hand the team something to work out together, never the answer.

## Principles

1. **Claims, not conclusions.** A voice says what the speaker sees, feels or wants. It never names the situation code, the cause in manual terms, the days lost, the response options, or what to do. LOB-Game's PM event lines already work this way ("A trench wall came down. The pipe crew has stopped.").
2. **Every claim is checkable by another seat.** A foreman's number is a rate, a day or a station that Plan can find on the LOB or Rules can run through the front-page rule. A claim nobody can check is noise.
3. **Speakers are partial.** Each foreman argues for his crew and blames the other crew or the schedule. Both are partly right; the team reconciles them with the LOB and the manual.
4. **Problems hide in normal lines.** Deviations are never announced. The excavation foreman whose order never arrived sounds cheerful about a good digging day. Only a Site player who knows what the office ordered hears the problem, and only Plan can tell Site what was ordered.
5. **Each voice reaches one seat.** Foremen are heard in the headset only (or on `site.html`). The PM and the radio play on the Plan laptop; the PM also appears as the hub standee for the morning brief. Rules hears nothing; Rules has paper. Whatever matters crosses the table in someone's own words.
6. **Once only.** A line plays once. Listen near a crew replays that crew's last line. No transcript is shown during play; the end-of-stage change log lists who said what and when.
7. **No verdict voices.** No line responds to whether a decision was good. The PM pushes against holds, demobilizations and purchases whatever the outcome; the end line states facts.

## Cast

| Speaker id | Name | Character | Heard on | Placeholder voice |
| --- | --- | --- | --- | --- |
| `foreman_exc` | Ray | Excavation foreman, steady, proud of his footage | headset, `site.html` | American male |
| `foreman_pipe` | Marcus | Pipe foreman, careful, quick to anger about safety | headset, `site.html` | a second American male, clearly different from Ray |
| `foreman_back` | Dana | Backfill foreman, impatient, paid by the foot | headset, `site.html` | American female |
| `pm` | the PM | Pushes the schedule, hates idle crews | Plan laptop; hub standee for the morning brief | older American male |
| `radio` | the dispatcher | Relays orders, flat | Plan laptop | neutral American female, radio treatment |

Names are spoken in the lines so players can say "Dana says...". Kokoro voice ids live in `Tools/voice/voices.json`.

## Triggers

Existing: `stageStart`, `prepRefused`, `holdCommitted`, `demobCommitted`, `orderLanded`, `orderNotDone`, `conflict`, `heldDay1` to `heldDay3`, `stageEnd`.

New in WO-S10:
- `event` with `eventType` (P1, A2, ...): a foreman's reaction where the event happens.
- `arrive` with `crew` and `status`: Site stands within 30 m of that crew; at most once per crew per day. For `working` this is the everyday chatter that can hide a deviation.
- `sky` with `state` (`towersSouth`): a foreman remarks on the weather when it turns.
- `holdPoint` with `zone`: the backfill foreman reaches a zone end and asks for the sign-off.
- `conflict` with `pair` (`back-pipe`, `pipe-exc`) and `cause` (`caughtUp`, `leaderStopped`, `leaderWaiting`): the argument. The most specific match wins (pair and cause, then pair, then any).
- `conflictRepeat` with `pair`: a two-line grumble when a trailing crew catches its leader again after the first argument.

A line is either one `text` with a `speaker`, or an exchange `turns: [{speaker, text}, ...]` played in order from each speaker's position, 0.3 s apart.

## Arguments (O12, expanded)

1. The first argument of each crew pair in a stage stops both crews and is a four-turn exchange (K7, unchanged).
2. Arguments also fire when the trailing crew catches a leader that is waiting at an unposted permit boundary or a hold point. The projection uses the leader's actual movement over the last hour of simulated time instead of its machine rate. Holding permits too tightly then has a voice: the pipe crew catches the parked excavator, and the two foremen argue about the paper.
3. After the first argument, a trailing crew that catches up again triggers a two-line grumble (`conflictRepeat`) without stopping anything, at most once a day per pair.
4. The numbers that matter (rates, days, stations) are spoken in the argument, never shown on a screen.

## Slice line sheet

For `Data/Scenarios/slice_texas.json`. Kept lines stay as they are: `pm_open_slice`, `pm_prep_refused_01`, `pm_hold_01`, `pm_demob_01`, `pm_end_slice`, `radio_ack_01`, `radio_notdone_01`, `foreman_held_01` to `foreman_held_03` (Dana). Removed, replaced by the exchanges below: `foreman_pipe_arg_slice`, `foreman_back_arg_01`. For the two Texas stages, use the same lines with "seven-twenty" in place of "five-twenty" and "fifteen hundred" for the zone 1 boundary.

```json
[
  {"id": "marcus_collapse", "trigger": "event", "eventType": "P1", "turns": [
    {"speaker": "foreman_pipe", "text": "Wall's moving! Out of the hole, everybody out, now!"},
    {"speaker": "foreman_pipe", "text": "Okay. Everybody's out, nobody's hurt. That whole side let go at five-twenty, and there's water coming in at the bottom."},
    {"speaker": "foreman_pipe", "text": "I'm not putting a man back down there until somebody cleans that up."}]},
  {"id": "arg_back_pipe_stopped", "trigger": "conflict", "pair": "back-pipe", "cause": "leaderStopped", "turns": [
    {"speaker": "foreman_back", "text": "Marcus, why is your crew standing on the bank? I've got a loader full of dirt and nowhere to put it."},
    {"speaker": "foreman_pipe", "text": "Because my wall's in the ditch, Dana. Push dirt onto my bedding and you can explain it to the inspector."},
    {"speaker": "foreman_back", "text": "The schedule had me here today. I lay two-fifty a day. You lay one-eighty on a good day."},
    {"speaker": "foreman_pipe", "text": "Then the schedule was wrong before the wall ever moved. Somebody tell the office."}]},
  {"id": "arg_back_pipe_caught", "trigger": "conflict", "pair": "back-pipe", "cause": "caughtUp", "turns": [
    {"speaker": "foreman_back", "text": "Marcus, I'm on your heels again. Pick it up or get out of my way."},
    {"speaker": "foreman_pipe", "text": "I lay one-eighty a day, Dana, same as every day. You lay two-fifty. Do the math."},
    {"speaker": "foreman_back", "text": "The math says I sit here and get paid to watch you."},
    {"speaker": "foreman_pipe", "text": "Take it up with whoever wrote the start dates."}]},
  {"id": "arg_pipe_exc_waiting", "trigger": "conflict", "pair": "pipe-exc", "cause": "leaderWaiting", "turns": [
    {"speaker": "foreman_pipe", "text": "Ray, why are you parked? I'm about to run out of open trench."},
    {"speaker": "foreman_exc", "text": "The board at fifteen hundred says no permit. I'm not digging into a fine."},
    {"speaker": "foreman_pipe", "text": "So we both sit here and bill. Who's holding the paper?"},
    {"speaker": "foreman_exc", "text": "Ask the office. All I know is the board's blank."}]},
  {"id": "grumble_back_pipe", "trigger": "conflictRepeat", "pair": "back-pipe", "turns": [
    {"speaker": "foreman_back", "text": "Caught up again, Marcus."},
    {"speaker": "foreman_pipe", "text": "I heard you the first time."}]},
  {"id": "grumble_pipe_exc", "trigger": "conflictRepeat", "pair": "pipe-exc", "turns": [
    {"speaker": "foreman_pipe", "text": "Still parked, Ray?"},
    {"speaker": "foreman_exc", "text": "Still no paper."}]},
  {"id": "ray_working", "trigger": "arrive", "crew": "exc", "status": "working", "speaker": "foreman_exc",
   "text": "Morning, inspector. Good ground today. We'll make our footage if nobody bothers us."},
  {"id": "marcus_idle", "trigger": "arrive", "crew": "pipe", "status": "idle", "speaker": "foreman_pipe",
   "text": "Nothing to lay until that wall's cleaned up. My guys are standing around."},
  {"id": "dana_working", "trigger": "arrive", "crew": "back", "status": "working", "speaker": "foreman_back",
   "text": "Dirt's going in fine. Just keep me fed."},
  {"id": "marcus_sky", "trigger": "sky", "state": "towersSouth", "speaker": "foreman_pipe",
   "text": "Look at that sky to the south. If that comes through, every open foot of this ditch turns into a creek."},
  {"id": "dana_holdpoint_1", "trigger": "holdPoint", "zone": 1, "speaker": "foreman_back",
   "text": "Zone one's ready to close. Tracer wire's down and the bedding's in. I need the sign-off before I push dirt."},
  {"id": "dana_holdpoint_2", "trigger": "holdPoint", "zone": 2, "speaker": "foreman_back",
   "text": "That's the last of it. Zone two's ready for the sign-off."}
]
```

How the sheet follows the principles: Marcus at the collapse gives the station, the water and his refusal, not the clearing days or the options. The argument carries the rates (250 against 180) and the blame on the schedule, which is the push lesson, for Plan to find on the LOB. Ray's morning line is the deviation tell. Marcus's sky line points at open trench without a number; Rules counts the tokens. Dana's hold-point line claims two of the three checklist items, which Site must see for itself, and the third (compaction) is only in Plan's inbox.

## Not now

A live voice agent (players question a foreman and an LLM answers in character) would sharpen the problem above: players would ask it what to do. If it is ever tried, it answers only from its own crew's view, under these principles, and never about the plan, the manual or the money.
