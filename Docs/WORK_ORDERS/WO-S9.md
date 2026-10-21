# WO-S9 Team game in the headset

## Goal

On the start panel, Team game connects the headset to the Node host on the PC as the Site seat. The hub, the warps and the site are driven by the host's state; every verb goes to the host; voices and cards come from the host's messages; the guided playthrough's prompts and Ready appear in the headset. A full guided slice can then be played by Plan on `plan.html`, Rules on `rules.html` or paper, and Site in the headset.

## Read

- `AGENTS.md`, `Docs/PROTOCOL.md` (with WO-S7's guide additions), `Docs/SITE_SEAT_V2.md` "Two modes", `Docs/PLAYTHROUGH.md` "Setup"
- The solo `Director`, `SiteView.From` in the C# sim, WO-S8's `SiteSeat`

## You may change

- `Assets/LOBOver/Runtime/**`, `Assets/LOBOver/Editor/**`, `Assets/LOBOver/Tests/**`
- `Monitor/src/host/**` only if the host needs a fix for the Quest client; say so in NOTES

## Build

1. **One source interface.** `ISiteSource` (Site view, events, voice, inspection replies, guide, `Send(cmd)`, status) implemented by the solo `Director` and a new `RemoteSource`: `System.Net.WebSockets.ClientWebSocket` to `ws://<host>:<port>/ws`; `hello` with seat `site`, client `quest`; a receive loop into a queue pumped on the main thread; reconnect with backoff; the state's view parsed with `SiteView.From`. If `ClientWebSocket` fails on the device, stop and report; do not vendor a library without the orchestrator.
2. **Start panel.** Team game with a host field (default `127.0.0.1`, remembered), a port (7331), Connect and a status line; a poke keypad for the address. Solo demo unchanged.
3. **Voices.** In team mode, play `Voice/<stage>/<lineId>.wav` from StreamingAssets when the host's voice message names it, from the speaker's position (foremen) or the standee (PM).
4. **Guide.** The Site prompt above the model in the hub and on the wrist panel in the site, with a Ready button (poke and ray) that sends the `ready` action.
5. **Editor helper.** `LOB Over > Team > Reverse USB Port` runs `adb reverse tcp:7331 tcp:7331` with the Android SDK's adb and logs the result.
6. **Tests.** PlayMode `TeamModeTests` starts the Node host on port 7332 with the slice and the guide, connects, checks `hello` and `welcome`, warps to 5+20 and sees `truck.stationFt` 520 in the next state, receives the first guide message and sends `ready`; teardown stops the host.

## Acceptance checks

- Unity MCP: tests pass. Play in the Editor in Team game against a local host: the facilitator page shows the Site seat connected; Plan on `plan.html` in a browser commits the lend at the collapse; the event card and the guide prompt appear in the Editor's hub; screenshots.
- Bryan: the full guided slice with three people (Plan laptop, Rules page or paper, headset over USB), following `Docs/PLAYTHROUGH.md`. His reply is the result.

## Out of scope

New voice lines (WO-S10), Stage 3, recording and analytics.
