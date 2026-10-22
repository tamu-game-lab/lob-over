# WO-L9 rejection 1 (2026-10-22 15:10), the relay and web parts

Astra could not start wrangler in its sandbox (it reads the parent directories and is denied). The orchestrator ran the same commands on the PC with the pre-installed wrangler 4.152.0: `wrangler dev --local` starts, the Worker serves `/rooms`, both tests run. Two things fail and one part is unfinished; fix exactly these, everything else stays. You still cannot run wrangler; reason from the outputs quoted here and keep the tests as they are except where named.

## Failed

1. **`/rooms` keeps listing a room after its host closed.** `Relay/test/relay.test.js` (`cd Relay && npm.cmd test`) fails at line 53, `await poll(async () => (await list()).length === 0)`, with `Timed out waiting for test condition`, after the host socket was closed and both seats had received their `host left` close (those assertions passed, so `Directory.webSocketClose` ran and `Room.hostLeft` closed the seats). `Directory.list()` filters `this.ctx.getWebSockets()` by `readyState === OPEN`, and under local workerd the closed host socket still reads as open afterwards. Required: a host entry disappears on close by its own state, not by `readyState`: mark the attachment (`serializeAttachment({ ...attachment, closed: true })`) in `webSocketClose` and `webSocketError` before closing, and have `list()`, `host()` and `hostInfo()` skip closed attachments; the rest of the test (the replacement host named "Browser", the empty list) then runs.

2. **The relay e2e references a variable the Site page does not define.** `node --test Monitor/e2e/relay.e2e.js` fails at line 69, `await phone.waitForFunction(() => !!state && mirror)`, with `ReferenceError: mirror is not defined` (the page is `site.html` joined by the room link). Everything before it passed: "Relay: connected", the room listed with the page's code, the quest seat's `welcome` (mirror false, stage level1_texas) and `state` (truck, no plan, no budget), "Headset connected", the `cmd` ack and the dismounted state. Required: wait on what `site.html` exposes for a mirrored seat (the `welcome` message's `mirror` field as the page stores it, or the connection line it shows), never a global the page lacks; the rest of the e2e (PeerJS coexistence, rejoin, host-left, the unavailable relay) then runs.

3. **README unfinished.** Required (WO-L9 item 4): `README.md` gains "Playing by link" (open the Pages link, choose Level 1, put on the headset, touch the room) and the relay's deploy note (`npx wrangler login` once, `cd Relay && npm ci && npx wrangler deploy`, the URL into `Data/relay.json`, republish, Sync Data), and `Relay/README.md` records the commands and the design (Directory and Room objects, hibernation, no storage).

## Passed (keep)

The Worker and its two Durable Objects (routing both ways, broadcast, the 409 for a second host, 400 for an unknown seat or client, 404 for a hostless room, CORS on `/rooms`, the hibernation idle in the test), `wrangler.toml` with the SQLite migration, the package scripts, the test helpers (the port reservation, the Windows spawn and taskkill), `LobTransport.relayHost` and the page's relay status line, `Data/relay.json` with the placeholder, the showcase build copying it, the status text "Waiting for headset or Site browser".

## Scope note

Your sandbox has no network and cannot start wrangler; the orchestrator runs `cd Relay && npm.cmd test` and `node --test Monitor/e2e/relay.e2e.js` and quotes the results. Keep the pre-installed devDependencies; add none. WO-L8 round 4 runs in parallel and owns `Assets/**`; ignore its changes, `.agents/` and `Docs/WORK_ORDERS/logs/` in the scope check. Do not start or stop any host or node process other than the wrangler dev and test servers your tests start and stop themselves.
