# WO-L6 Level 1 on a Mac and on GitHub Pages

## Goal

Bryan can present from a Mac: `Play LOB Over.command` does on macOS what `Play LOB Over.cmd` does on Windows, and the showcase site carries a no-headset Level 1 whose Plan page hosts the game in the browser with a phone or second laptop as the Site seat by room code. The headset still joins only a launcher-started host (it speaks WebSocket to a host on the same network; Pages serves files only).

## Read

- `AGENTS.md`, `Docs/LEVEL1.md` ("Starting a game"), `Play LOB Over.cmd`, `Monitor/src/host/launch.ps1`, `Monitor/src/host/server.js`, `Monitor/web/play.html`, `Monitor/web/host.html` (the browser host and room codes), `Monitor/web/lib/transport.js`, `Tools/publish_showcase.ps1`, `Showcase/`

## You may change

`Play LOB Over.command` (new, repo root), `Monitor/src/host/**`, `Monitor/web/**`, `Monitor/e2e/**`, `Monitor/test/**`, `Showcase/**`, `Tools/publish_showcase.ps1`, `README.md`. Not the simulation, the protocol, the scenarios or `Assets/`.

## Build

1. **`Play LOB Over.command`**, bash, committed with the executable bit (`git update-index --chmod=+x`): cd to its own folder; find node on PATH, `/opt/homebrew/bin` or `/usr/local/bin`, else print "Install Node.js 20 or newer from https://nodejs.org" and wait for a key; `npm ci` in `Monitor` when `node_modules` is missing; `adb reverse tcp:7331 tcp:7331` for each device `adb devices` lists (PATH or `~/Library/Android/sdk/platform-tools`), errors ignored; start `node Monitor/src/host/server.js Data/Scenarios/level1_texas.json --discovery`; once port 7331 answers, open `http://localhost:7331/play.html` with `open` (macOS), `xdg-open` (Linux) or `start` (Git Bash on Windows, so the orchestrator can test it); print the machine's IPv4 addresses and "Press Ctrl-C to end the game".
2. **README.md** section "Presenting from a Mac": clone with git (a downloaded zip carries Gatekeeper's quarantine; if the file will not open, right-click > Open, or run `bash "Play LOB Over.command"` in Terminal); allow node through the macOS firewall prompt; use a phone hotspot or the USB cable when the venue's Wi-Fi blocks discovery; `brew install android-platform-tools` for the cable.
3. **Pages.** `play.html` gains a browser-host mode when opened with `?host=browser`, and the showcase's index links it as "Level 1, no headset": the page runs `createHost` with `level1_texas` itself, Start hosts the game, a room code with a QR lets a phone or second laptop open `site.html` as the Site seat (the slice's room mechanism), voices from the published `Voice/level1_texas/`, and a one-line banner "The headset joins only a game started with Play LOB Over on a PC or Mac". `Showcase/dist` includes `play.html`, `img/` and `Voice/level1_texas/`.
4. **Tests.** `bash -n` on the `.command`; an e2e that runs `play.html?host=browser` (Start, Hold, Go, result) with `site.html` joined by room code in a second browser context; the existing Level 1 e2e unchanged.

## Acceptance checks

- `npm test` and the e2e pass; `bash "Play LOB Over.command"` from Git Bash on the PC starts the host and opens the page; after `Tools/publish_showcase.ps1`, the published site answers 200 for `play.html` and `Data/Scenarios/level1_texas.json`, and the live page in Chromium runs Level 1 to a result in browser-host mode.
- The Mac itself is checked by Bryan.

## Out of scope

The headset, the simulation, the stages' pages.
