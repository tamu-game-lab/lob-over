# WO-L7 No tunnel, head-camera verification, chart orientation

## Goal

The grey circle Bryan saw in three headset builds is gone for good, verification can never miss it again, and the Plan page's chart runs days across and stations up.

## The cause (found October 22)

The Interactions Rig block's `Locomotor` carries two Interaction SDK comfort effects, `SmoothMovementTunneling` and `WallPenetrationTunneling`. Each is a driver script in `Oculus.Interaction.Locomotion` plus a `TunnelingEffect` (namespace `Oculus.Interaction`) that draws a full-view quad 0.3 m in front of the centre eye (`ZTest Always`, transparent queue) with a feathered circular hole. `Configure Building Blocks` disables every Behaviour in `Oculus.Interaction.Locomotion`, so it switched off the drivers, which normally hide the quads every frame, but left the `TunnelingEffect` components running with their serialized defaults: the wall-penetration one shows a 60° hole in a grey (0.547) mask permanently. That is the "fisheye": the hub panel was hidden behind it except through the hole, and the site was visible only through the hole. Editor verification missed it three times because every screenshot came from a separate preview camera, which saw the quads edge-on as a "black plate" beside the foreman.

## Read

- `AGENTS.md`, `Docs/XR_RENDERING.md` (all of it), `Docs/DECISIONS.md` K42 and K43
- `Assets/LOBOver/Editor/BuildingBlocksSetup.cs`, `Assets/LOBOver/Runtime/Play/PlayPreview.cs`, `Assets/LOBOver/Editor/PlaySceneBuilder.cs` (the preview camera), `Assets/LOBOver/Tests/**`
- `Library/PackageCache/com.meta.xr.sdk.interaction*/Runtime/Scripts/Interaction/Models/Locomotion/Tunneling/*.cs`, `Runtime/Prefabs/Locomotion/{Locomotor,TunnelingEffect}.prefab`
- `Monitor/web/play.html` (the board), `Monitor/web/plan.html` (the chart), their e2e tests

## You may change

`Assets/LOBOver/Editor/**`, `Assets/LOBOver/Runtime/**`, `Assets/LOBOver/Tests/**`, `Play.unity` only through Configure and the builder; `Monitor/web/play.html`, `Monitor/web/plan.html`, `Monitor/e2e/**`, `Monitor/test/**`; `Docs/XR_RENDERING.md` only the lines named below.

## Build

1. **Configure Building Blocks** also sets inactive every GameObject under the blocks that carries a `TunnelingEffect` (`SetActive(false)`; values only, nothing added or removed), logging each. **Check Building Blocks** fails when any `TunnelingEffect` in the scene is on an active object with an enabled `MeshRenderer`, naming the object. If Bryan has already unchecked the two objects by hand, Configure changes nothing and Check passes.
2. **Verification camera.** The Editor preview renders through the head: the preview camera becomes a child of the rig's centre eye anchor with local pose identity, the centre eye camera's field of view and clipping planes, so anything attached to the head (tunnels, masks, panels) appears in every screenshot and composite exactly as the headset shows it. The old free preview pose is kept only as a second, explicitly named "survey" view for the WO-L4 stats, never for acceptance images. `XR_RENDERING.md` "Seeing the real thing before a build" gains one sentence saying so.
3. **Tests.** EditMode: a scene with an active `TunnelingEffect` fails Check and passes after Configure; Configure on Play.unity is idempotent. PlayMode: with the rig at a simulated head pose, a frame rendered through the head camera at progress 0 (hub) contains no pixel within 0.03 of the tunnel grey (0.547, 0.547, 0.547) and the composite over `room.png` shows the grid everywhere except the panel; a frame at warp progress 1 contains none either.
4. **Chart orientation**, `play.html` and `plan.html`: days across (left to right, Day 1 at the left), stations up (0+00 at the bottom, 30+00 at the top), zone bands horizontal, the day ruler vertical and sliding right, the PM's dashed plan and the inked actuals in the same crew colours. Labels: "Day" along the bottom, station marks along the left. The result stamp and the faint previous run follow. The e2e assertions on the board update accordingly.

## Acceptance checks

- Orchestrator through the MCP: compile clean; Check on the committed Play.unity reports the two tunnel objects (if Bryan has not already turned them off), Configure turns them off, Check passes, Play Scene twice; EditMode and PlayMode pass; the head-camera screenshots at the hub and in the site show no tunnel, no mask, no black plate; composites reviewed.
- `npm test` and the e2e pass; screenshots of the flipped board before Start, mid-run and at the result in `Monitor/out/play/`.
- No Quest build (K41). Bryan builds and checks: the whole room in the hub, the panel readable, the site filling in around him with no circle.

## Out of scope

Everything else in Level 1.
