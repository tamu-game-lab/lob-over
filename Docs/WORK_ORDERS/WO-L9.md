# WO-L9 The headset joins by link

## Goal

A game started from the GitHub Pages link joins the headset, with nothing installed and nothing run on the laptop. The Plan page shows a room code; the headset's hub panel lists open rooms; touching one joins. The messages travel through a small relay on Cloudflare (Workers and Durable Objects, free plan), so a Mac, a PC or a phone and the Quest only need internet, which is also the answer to campus Wi-Fi: each device reaches the relay on its own, nothing has to see anything else on the local network. The launchers (`Play LOB Over.cmd`, `.command`) keep working for an offline room.

Bryan's words: could we just use the page so we can do it in a link instead of installing and a cmd file on the Mac. Reason it was not there already: a Pages site serves files only; the headset speaks WebSocket to a host, and a browser cannot listen for connections. The relay is the piece in between.

## Read

- `AGENTS.md`, `Docs/PROTOCOL.md`, `Docs/LEVEL1.md` ("Starting a game"), WO-L6, WO-L2 (the host connection), WO-L8 (the hub panel; WO-L9's headset part runs after WO-L8 is accepted)
- `Monitor/web/play.html`, `Monitor/web/lib/{transport,level1-browser}.js`, `Monitor/src/host/core.js` and `clients.js` (how a connected seat is represented), `Monitor/e2e/**`
- `Assets/LOBOver/Runtime/Client/{HostConnection,DiscoveryPacket}.cs`, `Assets/LOBOver/Runtime/Play/HubPanel.cs`, `Assets/LOBOver/Editor/DataSync.cs`
- Cloudflare Workers: `wrangler` CLI, Durable Objects with WebSocket hibernation (the `Relay/README.md` you write records the commands)

## You may change

`Relay/**` (new), `Monitor/web/**`, `Monitor/e2e/**`, `Monitor/test/**`, `Data/relay.json` (new), `Showcase/**`, `README.md`; after WO-L8 is accepted also `Assets/LOBOver/Runtime/Client/**`, `Assets/LOBOver/Runtime/Play/HubPanel.cs`, `Assets/LOBOver/Editor/DataSync.cs`, `Assets/LOBOver/Tests/**`. Not the simulation, the protocol's message shapes, or the scenarios.

## Build

1. **Relay** (`Relay/`, a Worker with one Durable Object class `Room`, `wrangler.toml`, `src/index.js`, `package.json` with `deploy` and `dev` scripts, `README.md`): `GET /rooms` returns JSON `[{code, host, since}]` for rooms whose host socket is open (a KV-free design: a second Durable Object `Directory` holds the list, updated by rooms on host connect and close); `wss://<worker>/host/<CODE>` is opened by the page that runs the simulation, one per room (a second host for the same code is refused with 409); `wss://<worker>/join/<CODE>?seat=site|plan|rules|facilitator&client=quest|web` is opened by a seat. The relay gives each joined socket an id and forwards: a seat's text frames to the host as `{"t":"relay","from":"<id>","seat":"site","client":"quest","data":<the message>}`, the host's `{"t":"relay","to":"<id>","data":<message>}` to that seat as the bare message, and `{"t":"relay","to":"*",...}` to every seat; a join or a close reaches the host as `{"t":"relay","event":"join"|"leave","from":"<id>",...}`; a seat whose host closes receives a close with reason "host left". Codes are 6 characters from `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`; rooms with no host for 10 minutes disappear from the list. No storage, no accounts, no logging of message bodies. Hibernation keeps the free plan's limits comfortable.
2. **Browser host over the relay.** `play.html?host=browser` (the Pages Level 1) opens the relay host socket for its room code (the code it already shows with the QR) and treats each relayed seat as a client of `createHost` the way `LobTransport.host` treats a PeerJS connection; the headset status line reads "Headset connected" for a `quest` client. The PeerJS path stays for phones that scan the QR (it needs no relay); both may be used at once. The relay URL comes from `Data/relay.json` (`{"relay": "https://<worker>.workers.dev"}`), which the showcase build copies to `dist/Data/`. If the relay is unreachable the page says so in the status line and runs on without it.
3. **Headset over the relay** (after WO-L8): `HostConnection` keeps the USB loopback first (1 s), then asks the relay for `/rooms` every 3 s and shows them on the hub panel under the status line ("Room K7MX2Q · Bryan's MacBook · 2 min ago"), each a button; touching one opens `wss://<relay>/join/<CODE>?seat=site&client=quest` and from then on the connection behaves exactly as the local one (same `hello`, `welcome`, `state`, `voice`, `cmd`). The relay URL is read from `Data/relay.json` in StreamingAssets (Sync Data copies it). The old UDP discovery is kept as the third option. A lost relay socket retries every 2 s and the panel says "Reconnecting".
4. **Deploy.** `cd Relay && npm ci && npx wrangler deploy` after Bryan has run `npx wrangler login` once on that PC (it opens a browser; a free Cloudflare account). The orchestrator records the deployed URL in `Data/relay.json`, commits it, republishes the site and runs Sync Data. `README.md` gains "Playing by link" (open the Pages link, choose Level 1, put on the headset, touch the room) and the relay's deploy note.
5. **Tests.** Relay: `npx wrangler dev` in a test, a host and two seats over `ws`, forwarding both ways, the room list, the 409, the host-left close. Web: an e2e where `play.html?host=browser` opens the relay host socket against `wrangler dev` and a simulated `quest` seat (a Node `ws` client) receives `welcome` and `state`, sends a `cmd`, and the page shows "Headset connected". Headset (after WO-L8): PlayMode against `wrangler dev`: the hub lists the room and joining it reaches `welcome`; the USB path still wins when a loopback host answers.

## Acceptance checks

- Orchestrator: relay tests and e2e pass against `wrangler dev`; after deploy, `GET https://<worker>/rooms` answers `[]`; the live Pages page opened in Chromium shows "Relay: connected" and a Node `quest` client joining by its code turns the status to "Headset connected" and receives `state`; the site republished; `Data/relay.json` committed. Headset part: PlayMode passes; the head-camera capture of the hub shows the room list. No Quest build (K41); Bryan builds and joins from the Mac's browser.
- Bryan: open the Pages link on the Mac, choose Level 1, put on the headset (no cable, any Wi-Fi with internet), touch the room, see Connected, play.

## Out of scope

Voice chat, accounts, more than one headset per room (allowed by the relay, untested), the stages' pages.
