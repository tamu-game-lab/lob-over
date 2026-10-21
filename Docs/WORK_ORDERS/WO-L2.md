# WO-L2 Level 1 in the headset

## Goal

`Assets/LOBOver/Scenes/Play.unity`, made by Bryan from Building Blocks, becomes the headset's Level 1 seat. It starts in full passthrough with one readable panel, finds the PC's game by itself, warps to a chosen crew and back with a shader dissolve, and shows and plays the live game from the host. No menus, no modes, no embedded simulation, no truck. Accepted only after Bryan's headset check (K35).

## Read

- `AGENTS.md`, `Docs/LEVEL1.md` (all of it), WO-L1 (host, discovery packet, voice turns), `Docs/PROTOCOL.md`, `Docs/SITE_SEAT_V2.md` ("XR setup rules", the warp and comfort notes; its table model, standee, stake markers and wrist panel are not in Level 1)
- `Assets/LOBOver/Editor/{SiteSceneBuilder,BuildingBlocksSetup,QuestBuild}.cs`, `Assets/LOBOver/Runtime/**`, `Assets/LOBOver/Tests/**`
- The Interaction SDK's `RayInteractable`, `PokeInteractable` and `PointableCanvas`

## Precondition

`Play.unity` exists, made by Bryan from an empty scene with these blocks only: Camera Rig, Passthrough, Controller Tracking, Hand Tracking, Interactions Rig. If it is missing, stop and ask him. `Site.unity` stays as it is and leaves the build list.

## You may change

`Assets/LOBOver/Editor/**`, `Assets/LOBOver/Runtime/**`, `Assets/LOBOver/Tests/**`, `Assets/LOBOver/Art/**`, `Assets/Plugins/Android/AndroidManifest.xml`, `ProjectSettings/EditorBuildSettings.asset` (Play.unity only), and `Play.unity` only through the builder and the setup menu items.

## Build

1. **Blocks.** `Check Building Blocks`, `Configure Building Blocks` and the Quest build gate work on Play.unity instead of Site.unity (today they are hard-wired to `SiteSceneBuilder.ScenePath` and require Real Hands). Check no longer requires Real Hands; it fails when it finds Real Hands, an `EnableUnpremultipliedAlpha` component, more than one `OVRPassthroughLayer`, or a demo block, and names what Bryan should remove in the Building Blocks window (K34). `Configure Building Blocks` works on Play.unity as before (locomotion and gravity off, floor-level origin) and also enables the passthrough layer at start and gives the centre eye camera a transparent background (solid colour, alpha 0) with HDR off. It changes values only.
2. **Builder.** `LOB Over > Build > Play Scene` opens Play.unity and rebuilds only `[LOBOver]` roots: Lighting; Hub (a floor collider with its top at y = 0, and the panel); Site (the full-scale world, inactive at start, without the truck, the cab or the plan panel); Client (connection and voice); Warp; Preview (the desktop preview for Editor screenshots, excluded from builds). Block objects are never touched. Running it twice changes nothing else.
3. **Client.** A `HostConnection` replaces the embedded simulation in Play.unity, in the Editor and on the headset. `ClientWebSocket` to `ws://<host>:<port>/ws`; `hello` as seat `site`, client `quest`. Finding the host: try `127.0.0.1:7331` (USB with `adb reverse`) for 1 s; otherwise listen on UDP 7332 for `LOBOVER 1 <port> <hostname>` and connect to the sender; after a loss, retry every 2 s. `state` messages become the same `SiteView` the world presenter uses now; `voice` messages queue for playback; commands go out as `cmd`. Android: the INTERNET, ACCESS_WIFI_STATE and CHANGE_WIFI_MULTICAST_STATE permissions, a multicast lock while listening, cleartext traffic to local addresses allowed.
4. **Hub.** Full passthrough; nothing is drawn except the panel. The panel appears once, after the first valid head pose, 0.55 m ahead and 0.15 m below eye height, tilted to face the eyes, within reach of a finger; it does not follow, and it re-places itself after a system recenter. 0.40 m wide; opaque dark background; title text at least 2.5 cm tall, other text at least 1.5 cm; buttons at least 11 by 6 cm. Content: "LOB, Over"; one status line ("Looking for the game. Start it on the PC with Play LOB Over." / "Connected to <hostname>. Waiting for Plan to press Start." / "Day <n>"); three buttons, enabled only while connected: "Ray · digging", "Marcus · pipe", "Dana · backfill". Ray and poke, controllers and hands, through the Interaction SDK. A press plays a click and starts the warp at once.
5. **Warp** (`Art/Warp.shader`, URP, single-pass instanced; `WarpController`). In: read the chosen crew's current station; move the site root (never the rig or the camera) so that the road beside that station is under the player's feet at y = 0 and the trench lies ahead in the player's facing direction; enable the site behind an inward-facing sphere around the head that writes alpha 0, so passthrough still shows; over 1.0 s the sphere dissolves from the feet upward with a noise edge and a thin bright rim; then disable the passthrough layer and hide the hub. Out: the reverse, ending with the site hidden and the hub shown. Send `moveTo` (that station) and then `dismount`. Nothing moves during the dissolve. If the shader cannot load, use a 0.3 s fade through black.
6. **In the site.** The existing world presenter, driven by the host's views. Back on the left wrist (hand or controller), by poke or ray, warps out. Voice: each turn plays from its speaker's crew position (3D sound, clear within about 40 m, silent beyond about 120 m); exchanges never overlap, the next waits for the current one; an `arrive` line plays as it comes. Clips come from StreamingAssets as now (run Sync Data before the build so `Voice/level1_texas/` is included), one per turn as `<lineId>_<n>.wav`, with the scenario id taken from the host's `welcome`.
7. **Remove** from Play.unity and the Android build: the start menu (modes, scenarios, Autoplan, Start), MR Diorama, the truck and cab, and the embedded simulation. Delete code that nothing uses any more; the C# simulation project and its tests stay.
8. **Tests.** EditMode: the builder twice leaves the block objects untouched (instance ids, parents); Check Building Blocks fails on a test scene with Real Hands or two passthrough layers; the discovery parser reads a packet. PlayMode, against a Node host the test starts on port 7333 with `level1_texas.json`: it connects as `site` and receives `welcome` and a `state`; pressing Marcus through the Interaction SDK's event ends with the head within 0.5 m of the road beside pipe, exactly one `moveTo` sent, the site active and passthrough disabled; Back returns to the hub with passthrough enabled; the centre eye camera's background is transparent at start; the rig stays above y = -0.01 m for 3 s; a two-turn exchange plays its turns in order without overlap.

## Acceptance checks

- Orchestrator through the MCP: compile clean; Check and Configure Building Blocks pass; Play Scene twice; EditMode and PlayMode pass; Play in the Editor with `Play LOB Over.cmd` running shows Connected, a warp to Marcus (screenshot) and Back; Build and Run Quest; the host log shows the headset joining as `site`, client `quest`.
- Bryan, in the headset, with `Play LOB Over.cmd` running on the PC: (1) the whole room is visible and the panel is sharp and readable; (2) the panel says Connected; (3) touching Marcus dissolves the room into the site beside the pipe crew; (4) Back on the wrist dissolves it back into the room; (5) after Plan presses Start, the crews move and Marcus's lines play when the wall falls. Accepted only when all five pass.

## Out of scope

The table model, the PM standee, stake markers, Stop work, flags, confirmations, the Real Hands block, the GitHub Pages showcase.

## Amendment 1 (orchestrator, 2026-10-21): where Play.unity is

Bryan saved the scene at `Assets/Scenes/Play.unity` (not `Assets/LOBOver/Scenes/Play.unity`). Use that path everywhere this work order says Play.unity: the Check and Configure Building Blocks menu items, the Quest build gate, `LOB Over > Build > Play Scene`, the build list (`ProjectSettings/EditorBuildSettings.asset`: Play.unity only), and the tests. Do not move or rename it. "You may change" therefore includes `Assets/Scenes/Play.unity`, only through the builder and the setup menu items, exactly as before. Orchestrator's MCP read of it on October 21: roots `Directional Light`, `[BuildingBlock] Camera Rig` (holding Controller Tracking left and right, Hand Tracking left and right and `[BuildingBlock] OVRComprehensiveInteractionRig`), `[BuildingBlock] Passthrough` (the only `OVRPassthroughLayer`, enabled); no Real Hands, no `EnableUnpremultipliedAlpha`, no demo block.
