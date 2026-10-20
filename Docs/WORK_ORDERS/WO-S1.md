# WO-S1 — Web showcase: the simulation in the browser, three seats in tabs, phone by room code, GitHub Pages

## Goal

A static site, built into `Showcase/dist/`, that needs no server: the facilitator page runs the simulation in the browser and the Plan and Site pages connect to it over `BroadcastChannel` (same browser) or a PeerJS room code (a phone). Plan can run on autopilot. The landing page links the seats, the field manual PDF and a one-screen "how to play". The Node host keeps working unchanged for LAN sessions and gets the same `--autoplan`. A publish script pushes the built site to a public GitHub repository for Pages.

## Read

- `AGENTS.md`, `Docs/PROTOCOL.md`, `Docs/SHOWCASE.md`
- `Monitor/src/host/server.js`, `pacing.js`, `clients.js` (what the host does per message), `Monitor/src/sim/headless.js` (script application), `Monitor/web/*.html` (how pages open their WebSocket), `Monitor/src/scenario/load.js` and `Monitor/src/common/catalog.js` (they use `fs`; the browser cannot)

## You may change

- `Monitor/src/host/**`, `Monitor/src/showcase/**`, `Monitor/src/scenario/load.js`, `Monitor/src/common/catalog.js` (add object-based loaders; keep the `fs` ones), `Monitor/web/**`, `Monitor/e2e/**`, `Monitor/test/**`, `Monitor/package.json` (scripts; one dependency allowed: none, PeerJS is loaded from a pinned CDN URL in `web/lib/`), `Showcase/**`, `Tools/publish_showcase.ps1`, `.gitignore` (add `Showcase/dist/`, `Showcase/.publish/`)

## Build

1. **Host core.** Extract from `server.js` and `pacing.js` a transport-agnostic `src/host/core.js`: `createHost({ scenario, catalog, pacing, autoplan })` with `attach(transport)`, `start/pause/resume/reset/load`, per-seat registration and mirrors, 4 Hz state broadcast, message routing, transcript capture, exactly the behaviour the Node host has now. `server.js` becomes a thin WebSocket transport over the core (the WO-03 e2e must still pass).
2. **Autoplan.** `src/host/autoplan.js`: loads a script (headless shape), applies non-`site` entries when the paced sim reaches their `t`; used by `server.js --autoplan <file>` and by the browser host (a checkbox and a file picker with the three slice scripts and the two stage scripts preloaded from `Data/`). The facilitator page shows "autoplan: next at t 4.00".
3. **Browser loaders.** `loadScenarioFromObject(obj, catalogObj)` and `loadCatalogFromObject(obj)`; the browser host fetches `Data/catalog.json` and `Data/Scenarios/<id>.json` with relative URLs (the site lives under a subpath on Pages; never a leading slash).
4. **Transports.** `web/lib/transport.js`: `ws` (existing behaviour, `?ws=host:port`), `local` (`BroadcastChannel('lob-over-<runId>')`, default when the page was opened from the host tab), `room` (PeerJS `DataConnection` to the host's peer id; `?room=ABCD`; PeerJS pinned version vendored into `web/lib/peerjs.min.js` from `https://unpkg.com/peerjs@1.5.4/dist/peerjs.min.js`, fetched by the orchestrator if your sandbox cannot). The pages' code paths above the transport do not change.
5. **Pages.** `web/index.html` (landing): title "LOB, Over", one paragraph, buttons: Start a showcase (opens `host.html`), Plan seat, Site seat, Field manual (PDF), How to play (a `howto.html` with the three seats and the loop in one screen, facts only). `web/host.html` = the facilitator page plus: scenario picker (slice, stage1, stage2), autoplan checkbox, "Open Plan tab" and "Open Site tab" buttons, the room code in large type with a QR image (draw it yourself in canvas or vendor a tiny QR module; no network), Start/Pause/Reset. The Site and Plan pages gain nothing but the transport.
6. **Build.** `npm run showcase` → `Showcase/dist/`: `index.html`, `host.html`, `plan.html`, `site.html`, `howto.html`, `lib/`, `src/` (the ES modules the pages import: sim, host core, scenario, common), `Data/catalog.json`, `Data/Scenarios/*.json`, `Voice/**/*.wav` (if present), `manual/<stage>.pdf` from `Paper/out/<stage>/manual.pdf` (if present), `.nojekyll`. No bundler; imports stay relative. `npm run showcase:serve` serves `Showcase/dist/` on 7340 with a 20-line Node static server (correct MIME for `.js`, `.json`, `.wav`, `.pdf`).
7. **Publish.** `Tools/publish_showcase.ps1`: runs the build and pushes `Showcase/dist/` to the `gh-pages` branch of this repository's `origin` through a git worktree in `Showcase/.publish/`, commit "Showcase <date> from <short hash>", prints `https://tamu-game-lab.github.io/lob-over/`. (Amended October 20 after acceptance: one repository, `tamu-game-lab/lob-over`, source on `main` and the site on `gh-pages`, instead of a separate `lob-over-showcase` repository. The script was rewritten by Bryan's side; Astra's version targeted the separate repository.)
8. **Tests.** `e2e/showcase.e2e.js` against `showcase:serve`: landing loads; host tab with slice and autoplan; Plan and Site tabs via `local`; Start; the collapse card on Site and not on Plan; with the host's fast setting the result appears on Plan; a Site tab opened with `?room=<code>` in a second browser context connects over PeerJS and receives `state` (skip this assertion with a note if PeerJS's public server is unreachable from the test machine). Unit tests for `autoplan.js` and the object loaders. The WO-03 stage1 e2e still passes.

## Acceptance checks

```
npm --prefix Monitor test && npm --prefix Monitor run e2e                 # all pass incl. showcase.e2e.js
npm --prefix Monitor run showcase && ls Showcase/dist                      # index.html host.html plan.html site.html howto.html lib src Data manual .nojekyll
(npm --prefix Monitor run showcase:serve & echo $! > Monitor/out/serve.pid; sleep 2; curl -s -o /dev/null -w "%{http_code}\n" http://localhost:7340/index.html; curl -s -o /dev/null -w "%{http_code}\n" http://localhost:7340/Data/Scenarios/slice_texas.json; kill $(cat Monitor/out/serve.pid))   # 200 200
(node Monitor/src/host/server.js Data/Scenarios/slice_texas.json --autoplan Monitor/test/scripts/slice_autoplan.json & echo $! > Monitor/out/host.pid; sleep 2; curl -s http://localhost:7331/facilitator.html | grep -c autoplan; kill $(cat Monitor/out/host.pid))   # >= 1
grep -rniE "well done|good job|should have|right choice|correct answer|behind schedule|ahead of schedule" Monitor/web/ ; echo grep-exit=$?   # 1
git status --short     # only Monitor/**, Showcase/**, Tools/publish_showcase.ps1, .gitignore
```

The orchestrator also looks at the screenshots (landing, host with the room code, Plan, Site at phone width).

## Out of scope

No visual redesign of the Plan and Site pages beyond the transport line, no accounts, no analytics, no Unity.
