# LOB, Over relay

The browser runs the simulation. This Cloudflare Worker forwards the existing protocol between that browser and its seats; it does not run a simulation or change seat views. Phones using the room QR continue to connect through PeerJS alongside relay seats.

## Deploy

Bryan signs in once with a free Cloudflare account. From `Relay/`, run:

```sh
npx wrangler login
```

The orchestrator then deploys from the repository root:

```sh
cd Relay
npm ci
npx wrangler deploy
```

On Windows PowerShell, use `npm.cmd` and `npx.cmd` in place of `npm` and `npx`. The equivalent one-line command in a shell supporting `&&` is `cd Relay && npm ci && npx wrangler deploy`. The package's `npm run deploy` also runs `wrangler deploy`.

Set `Data/relay.json` to `{"relay":"https://<worker>.workers.dev"}` using the deployed HTTPS URL, commit the configuration, republish the showcase and run Unity's Sync Data before building the headset. The showcase build copies the file to `Showcase/dist/Data/`; Sync Data must also copy it into StreamingAssets when the headset part of WO-L9 is implemented. The current configuration is a placeholder until deployment.

After deployment, `GET https://<worker>.workers.dev/rooms` should return `[]` with no hosts connected. Open Level 1 from the published Pages link and check for "Relay: connected". With the WO-L9 headset build, select that room in the hub; Plan should show "Headset connected".

## Local checks

From `Relay/`:

```sh
npm run dev
```

This runs `wrangler dev --local`. The automated checks start and stop their own local Wrangler instances; they do not require a separately running dev server. Run these from the repository root:

```sh
npm --prefix Relay test
node --test Monitor/e2e/relay.e2e.js
```

The relay test checks the room list, two seats, forwarding, broadcast, host conflict, hibernation and host-left cleanup. The browser test checks Quest welcome/state/commands, simultaneous PeerJS, rejoining and unavailable-relay play. These checks need an environment where Wrangler can read its parent directories; the restricted implementer sandbox cannot run them.

## Design

`Directory` is one Durable Object named `rooms`. Each room's `Room` object routes host registration to it. Directory owns the host sockets and their list entries together, making the second-host check atomic. Attachments hold the code, host name, connection time and session ID. Close and error callbacks mark the attachment closed before closing the socket; `list()`, `host()` and `hostInfo()` exclude it even if workerd still reports the socket as open. Removal is immediate, within WO-L9's ten-minute limit for hostless entries.

`Room` is one Durable Object per room code and owns the seat sockets. It records each seat's ID, seat type, client type and host session in its socket attachment. It forwards join, leave and message envelopes to Directory for delivery to the host, and delivers host messages to one seat or all seats in that session. When a host leaves, its seats close with reason "host left"; session IDs keep an old host's cleanup separate from a replacement host.

Both objects accept incoming sockets with WebSocket hibernation. Socket attachments survive hibernation; there are no outbound sockets or timers keeping objects awake. There is no application storage, KV, account system or message history, and message bodies are not logged. The SQLite migration registers the Durable Object classes; the Worker does not call the Storage API.

| Endpoint | Behaviour |
| --- | --- |
| `GET /rooms` | JSON `[{code, host, since}]` for active hosts, with CORS enabled. |
| `wss://<worker>/host/<CODE>` | One host per code; a second host receives 409. Optional `?name=` supplies the host label; the default is `Browser`. |
| `wss://<worker>/join/<CODE>?seat=site&client=quest` | Seats may be `site`, `plan`, `rules` or `facilitator`; clients may be `quest` or `web`. Unknown values receive 400; a hostless room receives 404. |

Codes contain six characters from `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`. A seat message reaches the host as `{"t":"relay","from":"<id>","seat":"site","client":"quest","data":<message>}`. Join and leave use the same identity fields with `"event":"join"` or `"event":"leave"`. The host sends `{"t":"relay","to":"<id>","data":<message>}` for one seat or `"to":"*"` for all seats; seats receive the bare message.
