# WO-L5 The site materializes around the player

## Goal

Touching a crew button in the hub builds the site around the player: a wave runs outward from the feet; just behind its front the ground and everything on it appear as glowing wire and blueprint lines over the room, then fill in with their real surfaces; the sky closes last. Back runs the wave inward and the room returns. No mask, no sphere, no window: the room is visible exactly where the site is not yet drawn, and once the site is complete the view is the whole site. Bryan's words on the first build: a fisheye with a lens; I can only see that part; it should be immersive going in and out.

## Read

- `AGENTS.md`, `Docs/XR_RENDERING.md` (all of it), `Docs/LEVEL1.md` ("In the headset"), WO-L4 (SiteLit, SiteSky), `Docs/DECISIONS.md` K37 to K41
- `Assets/LOBOver/Runtime/Play/{WarpController,HubPanel,HostWorldPresenter,PlayPreview}.cs`, `Assets/LOBOver/Editor/PlaySceneBuilder.cs`, `Assets/LOBOver/Art/{SiteLit,SiteSky}.shader`, `Assets/LOBOver/Tests/**`
- The headset log the orchestrator pulled from Bryan's test, if it exists (`Docs/WORK_ORDERS/logs/headset-20261021.log`), for the stuck transition

## You may change

`Assets/LOBOver/Runtime/**`, `Assets/LOBOver/Editor/**`, `Assets/LOBOver/Art/**`, `Assets/LOBOver/Tests/**`, `Play.unity` only through the builder.

## Build

1. **Global warp values.** `Shader.SetGlobalFloat("_WarpProgress")` (0 = no site, 1 = all of it), `SetGlobalVector("_WarpOrigin")` (the player's tracked feet in world space, y at the site's ground), `SetGlobalFloat("_WarpRadius")` (the wave front: 160 m × ease-out of progress), `SetGlobalFloat("_WarpBand")` (6 m). Set every frame during a transition, and once to the resting values (progress 1, radius 1e6) when the site is fully there.
2. **`SiteLit` with the warp.** In the fragment: `d = distance(positionWS, _WarpOrigin)`. `d > _WarpRadius + _WarpBand`: `clip(-1)` (nothing drawn; the room shows). Inside the band: the blueprint state, a dark translucent base (premultiplied: rgb 0.02, 0.04, 0.06 at alpha 0.25) with lines at alpha 1 in `#39C6FF`: on the builder's static meshes the lines are the triangle edges, from barycentric coordinates the builder bakes into UV3 (one vertex per corner; the ground, trench cells, road and spoil) with `fwidth`-based thickness of 1.5 px; on imported models (machines, workers, props, trees) a world-space 0.5 m grid (triplanar) plus a Fresnel rim in the same colour. From `d = _WarpRadius` inward over half the band the blueprint blends into the normal lit result (alpha 1). Alpha written exactly as the compositor needs it (premultiplied).
3. **`SiteSky` with the warp.** The dome fades in over progress 0.6 to 1.0 (premultiplied) and out in reverse, so the room stays visible overhead until the ground has mostly filled in. Fog colour follows it.
4. **`WarpController`.** Remove the sphere, the dissolve and fade materials and `Art/Warp.shader`, `Art/WarpFade.shader`. In: place the site root as now (road under the feet, trench ahead), enable the site, keep passthrough on, run progress 0 to 1 over 1.5 s with the ease above, then disable passthrough and hide the hub. Out: enable passthrough, run 1 to 0 over 1.2 s, disable the site, show the hub. Crews and animators freeze during the wave as now. A watchdog snaps any transition to its end state after 3 s of wall time. Back is never refused: pressed during a warp-in it reverses from the current progress; the hub's crew buttons are disabled only while a transition runs. Each phase logs one line, `LOBOver Warp: in start | in done | out start | out done | watchdog`, with the progress value, through `Debug.Log` so it reaches `adb logcat -s Unity`.
5. **Hub panel.** Unchanged in size and placement (WO-L2 item 4); during a transition its status line reads "Warping..." and the buttons grey out; on return it shows the day again.
6. **Preview composites.** `PlayPreview` gains a menu item `LOB Over > Debug > Warp Frames` that, in Play, steps progress to 0.2, 0.45, 0.7, 0.9 and 1.0, renders the preview camera to an ARGB32 texture at each, composites it over `Monitor/out/unity/room.png` (a generated mid-grey wall with a 20 cm grid; create it if missing) by the frame's alpha, and writes `Monitor/out/unity/warp-<progress>.png`. These are what the orchestrator reviews.
7. **Tests.** EditMode: the baked UV3 barycentrics sum to 1 on every vertex of the static meshes; no object in `Play.unity` carries the old sphere or the removed shaders; a source scan finds no `ZTest Always` and no full-view mesh in `Assets/LOBOver`. PlayMode against the Node host on port 7333: pressing Marcus sets progress from 0 to 1 within 1.5 s ± 0.1 and the global values follow; passthrough stays enabled until progress reaches 1 and is disabled after; the composite at progress 0.45 has alpha 0 pixels in its upper half and alpha 1 pixels near the bottom centre; Back at progress 0.5 reverses and ends with the site inactive and passthrough on; a transition whose coroutine is stopped ends by the watchdog within 3 s; the four log lines appear in order.

## Acceptance checks

- Orchestrator through the MCP: compile clean; Play Scene twice; tests pass; Warp Frames written and reviewed: the wave front is a ring on the ground around the player with wire and blueprint behind it, the room (grid wall) visible beyond it and overhead until late, the full site at 1.0 with no room left; no window, no mask. Console clean. No build.
- Bryan, after building: (1) the hub is the whole room with a readable panel; (2) Connected; (3) touching Marcus builds the site around him in about 1.5 s, wire first then surfaces, the full view, no lens; (4) Back takes the site away the same way; (5) the site looks like a site (WO-L4) and the voices play when the wall falls.

## Out of scope

The table model, the standee, stake-to-stake warps, Stop work, the Plan page.

## Amendment 1 (orchestrator, 2026-10-22 01:30, answer to BLOCKED)

Item 7, the source scan: `Assets/LOBOver/Runtime/VR/ComfortFade.cs` and `Assets/LOBOver/Art/ComfortFade.shader` (the legacy Site.unity inspector seat's eye-locked mask, compiled only under `UNITY_EDITOR || !UNITY_ANDROID`, never in `Play.unity`) are exempt from the scan; the scan covers the warp path and everything the builder puts into `Play.unity`. Leave the legacy files untouched (stake-to-stake travel is out of scope).
