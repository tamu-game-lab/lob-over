# WO-L8 The site in front of you

## Goal

One warp, one site, everything in view. From the hub a single button builds the site around the player with the wireframe effect; the three crews work in front of the player at real scale, grounded, with the pipe crew in the middle and the others nearer or farther as their real gaps change; the two foremen argue in front of the player; what anyone says appears in a speech bubble over the speaker and in a log panel the player can open and move; each simulated day passes as a sunrise-to-dusk cycle with a short dusk at the day boundary. Bryan's words on the fourth build: the equipment is extremely small and sometimes floating; all the action happens on my left; without the text it is hard to understand what is going on; three buttons made me expect three places when one site is enough.

## Read

- `AGENTS.md`, `Docs/XR_RENDERING.md`, `Docs/LEVEL1.md` ("In the headset", rewritten October 22), `Docs/VOICE.md` ("Principles"), `Docs/DECISIONS.md` K45 to K48, WO-L4 (ground, SiteLit, props), WO-L5 (the wave), WO-L7 (head camera, no tunnel)
- `Assets/LOBOver/Runtime/Play/**`, `Assets/LOBOver/Runtime/World/**` (CrewRig, StationCell, SiteWorld, SiteSky, WorldAssets), `Assets/LOBOver/Editor/{PlaySceneBuilder,SiteAssetBuilder,SiteGeometryBuilder}.cs`, `Assets/LOBOver/Tests/**`
- The Interaction SDK's `Grabbable`, `HandGrabInteractable`, `GrabInteractable`, `OneGrabTranslateTransformer`, `PointableCanvas`

## You may change

`Assets/LOBOver/Runtime/**`, `Assets/LOBOver/Editor/**`, `Assets/LOBOver/Art/**`, `Assets/LOBOver/Tests/**`, `Play.unity` only through the builder. Never a `[BuildingBlock]` object, never `Assets/LOBGame/**`. The simulation, the host and the protocol stay as they are; the headset sends only the commands `PROTOCOL.md` already has.

## Build

1. **Hub.** The panel keeps its status line and gets one button, "Go to the site", in place of the three crew buttons. The Back button on the wrist returns to the hub as now.
2. **Stage layout.** On entry the site root is placed so the player stands on the road with the trench 6 m in front, running left to right across the view, excavation to the right (the direction of work), backfill to the left. The player's facing direction at the moment of the warp defines "front"; the site is placed by the tracked head position (the head's x and z, the floor at y = 0). Crew slots along the trench, measured from the point straight ahead of the player: pipe at 0; excavation at +(8 m + 0.02 m per foot of real gap), capped at +30 m; backfill at −(8 m + 0.02 m per foot of real gap), capped at −30 m; a gap of 0 ft (backfill has caught pipe) puts backfill at −4 m. Slots move with the simulation at no more than 1 m/s (the machines crawl, the player never moves). The trench cells in front take their look from the real cells around each front: undug beyond excavation, open between excavation and pipe (with the collapse and water at the pipe front when the event is live), piped between pipe and backfill, backfilled beyond backfill. The station stake nearest each crew shows that crew's real station (`5+20`) in 6 cm letters; the stakes between them are unlabelled. The rest of the site (road, spoil, fences, props, trees, sky, haze) stays as WO-L4 built it, extending at least 120 m each way so the view never ends.
3. **Scale and grounding.** Machines at real size, set by the builder from each prefab's bounds: Standard excavator 9.5 m long, 3.2 m to the cab roof; Large 11 m and 3.6 m; Small 7.5 m and 2.8 m; Standard crane 12 m and 3.8 m; Heavy 14 m and 4.2 m; backfill loaders 6 m long and 3.2 m tall (Small 5 m, Large 7 m); pipe 0.3 m outside diameter in 6 m lengths; workers 1.8 m. Every machine, worker, pile and prop rests on the ground: the builder and the runtime place each by the ground height function the ground mesh was generated from (`GroundHeight(x, z)`), lowest point of the bounds on the surface, machines on the far bank, workers beside the trench. Nothing floats and nothing sinks; a test checks every placed object's bounds against the ground within 5 cm.
4. **Attention.** Looking at a crew for 2 s (gaze dwell on its machine or workers) sends `moveTo` for that crew's real station, so the host's position logic and the `arrive` lines follow attention; a thin ring under the crew brightens while it is the attended one. On entry the attended crew is pipe.
5. **Speech.** A `voice` turn plays from its speaker's position as now, and for its duration plus 2 s a speech bubble floats 0.4 m above the speaker's head: the speaker's name in the crew colour and the text in white on an opaque dark card, billboarded to the player, scaled with distance so the cap height stays about 2 cm at 10 m, at most two bubbles at once (the exchange's current and previous turn). The two arguing foremen meet at the spot between their slots and face each other, as the ConflictTalk animation does now.
6. **Log panel.** A world-locked panel, 0.5 m by 0.35 m, opaque, that opens in front of the player (1 m ahead, 0.15 m below eye height, facing the eyes, placed from the head pose at the moment it opens) from a wrist button "Log", and closes from the same button or its own close button. It lists the last twenty lines in order: day stamp, speaker in the crew colour, text; it also shows the current day and the clock state (Running, Field slow). The player can grab it with a controller or a hand (Interaction SDK `Grabbable` with `HandGrabInteractable` and `GrabInteractable`, translate and rotate, no scale) and leave it anywhere; it stays where it is left and reopens where it was unless it is out of view, in which case it is re-placed in front. Text 1.5 cm cap height or more.
7. **Day and night.** The sun travels from 20° above the eastern horizon to 20° above the western over each simulated day (the sun elevation follows the fraction of the day; the directional light and the sky dome follow the sun), and the day boundary is a dusk: over the last 15% of the day the light warms and dims to 25% and the sky deepens, then the new day opens at dawn brightness within 0.3 s, with the day number shown for 1.5 s as a 20 cm caption 2 m in front of the player ("Day 6"). The same at the slow pace (15 s a day). Never fully dark, and no flashes faster than 0.3 s.
8. **The wave, stronger.** The warp in runs 2.5 s and the blueprint band is 14 m wide, so the wire-and-blueprint phase is readable on the machines and the people in front before the surfaces fill; the warp out runs 1.5 s. A machine or worker that enters the view later (a crew whose slot was beyond the stage) spawns with the same wire-to-surface effect over 0.6 s in place.
9. **Tests.** EditMode: each machine prefab's bounds match the sizes above within 10%; every placed object rests on the ground within 5 cm. PlayMode against the host on 7333 at Day 5: the three crews are in front of the simulated head (within ±60° of the facing direction) at the slot distances the formula gives; excavation is to the right of pipe and backfill to the left; a `voice` message shows a bubble above the speaker and a log line; the Log button opens the panel within 1.2 m of the head and facing it; gaze dwell on the excavation crew sends one `moveTo` for its station; a day boundary dims the light to about 25% and back; the hub has exactly one crew-independent button. Screenshots through the head camera (WO-L7): the hub panel; the site at entry; the argument; the log panel open; a dusk frame.

## Acceptance checks

- Orchestrator through the MCP: compile clean; Play Scene twice; tests pass; the head-camera screenshots reviewed against the goal sentence (all three crews in front and at real scale, nothing floating, a bubble readable, the panel in reach, the dusk); no console errors; stats within the WO-L4 budget. No Quest build.
- Bryan, after building: the site builds around him with the wire phase visible; the three crews work in front of him at real size on the ground; Marcus's words appear over his head and in the log; the panel opens in front and can be grabbed and moved; the days pass as dusks; the argument happens in front of him.

## Out of scope

Stop work, flags, confirmations, the table model, stake-to-stake warps, the Plan page.

## Amendment 1 (October 22, K50, after rounds 0 to 2)

Bryan's rulings, which replace the matching numbers above:

- The trench runs across 7 m in front of the player (item 2 said 6 m); the machines stand on the far bank at real size, the near edge of each machine within 3 m of the far trench edge.
- The crew slot cap is 18 m instead of 30 m (item 2), so the excavator stays at the edge of the view; the formula is otherwise unchanged.
- The far-bank check, in the builder, at runtime and in the tests, measures the lowest 1.5 m of each machine's bounds (tracks, wheels, body), never the boom.
- Workers keep their imported meshes: no mesh reduction on skinned meshes (the fence batching may stay).
- The small excavator is scaled to 2.8 m to the cab roof within 10 percent like the other variants.
- The Day-5 "in front" check of the PlayMode test is within 65 degrees of the facing direction (rejection 1).
