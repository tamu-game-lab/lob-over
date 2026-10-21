# WO-S8 Passthrough hub and warp into the site

## Goal

The headset seat follows `Docs/SITE_SEAT_V2.md` "The experience". It starts in passthrough with the site model on the table and the PM standee, warps into the full-scale site at a selected stake with a shader dissolve, moves between stakes by short warps, and warps back to the table. All interaction goes through the Interactions Rig. The solo demo (embedded simulation, Plan on autopilot) drives it; team mode comes in WO-S9.

## Read

- `AGENTS.md`, `Docs/SITE_SEAT_V2.md` (whole), `Docs/VOICE.md` ("Cast" and "Principles", for where voices play)
- WO-S6's `XRRig`; the existing `DioramaSeat` (MR), `InspectorSeat` (VR), `SiteWorld`, `Director`
- LOB-Game's PM portraits in `Assets/LOBGame/PM` and their intended use in LOB-Game's `MD Files/_sr_design.md` (explaining for briefings, clipboard for planning stops, worriedSurprise for events)

## You may change

- `Assets/LOBOver/Runtime/**`, `Assets/LOBOver/Editor/**`, `Assets/LOBOver/Art/**` (the warp shader and its materials), `Assets/LOBOver/Tests/**`

## Build

1. **One seat.** A state machine `SiteSeat` (Hub, WarpingIn, Site, WarpingStake, WarpingOut) replaces the separate VR Site and MR Diorama modes. The start panel offers Solo demo (the scenario row and the Autoplan toggle as now), Team game (disabled until WO-S9) and, in the Editor only, Desktop.
2. **Hub.** Passthrough on; the full-scale site hidden. The model (the existing diorama `SiteWorld`) is placed once, 0.6 m ahead at 0.75 m height facing the player; a wrist button re-places it. Foremen's voices play from the model's crew positions at low volume. The PM standee is a 0.35 m billboard of the PM portrait on a small stand at the model's near-left corner: `explaining` at stage start and while a PM line plays, `worriedSurprise` for 4 s when an event card arrives, `clipboard` otherwise, with a slight scale pulse while a line plays. It never shows `thumbsUp` or `crossedArms`.
3. **Selecting on the model.** Every stake (each 100 ft), each crew and each zone board on the model has a `RayInteractable`. Select warps in to that station (a crew: the stake nearest to it; a board: the zone's start). Hover shows only the label: `5+20`, `Ray's excavator`, `Zone 1 board`.
4. **The warp** (`Art/Warp.shader`, `WarpController`): the alpha-write dissolve of `SITE_SEAT_V2.md`; 1.2 s in, 1.2 s out, 0.6 s stake to stake; the ring at the feet and the bright edge; passthrough fades with it. The full-scale site is enabled before the dissolve starts and the model hidden after it ends. It sends `moveTo` and, on arrival, `dismount`. It places the player at the stake on the road side, facing the trench, by moving the rig's tracking space, never the camera, with the floor at the site's ground. Fallback: a 0.3 s fade through black if the shader is unsupported.
5. **In the site.** Gaze-dwell inspect (2 s, as now) and ray select on the same targets. The wrist panel on the left hand anchor (or the left controller when one is held) has Flag, Stop work, Confirm (Done, Not carried out) and Back to table as `PokeInteractable` buttons that also accept the ray. Stake markers (a 1.2 m post with the station text) each have a `RayInteractable` for stake-to-stake warps. Remove the truck and the cab.
6. **Cards and prompts.** Event cards above the model in the hub and on the wrist panel in the site; an empty guide prompt slot in both places for WO-S9.
7. **Tests** (PlayMode, fake input through the Interaction SDK interactable events). Selecting stake 5+20 on the model runs Hub, WarpingIn, Site; sends `moveTo` 520, then `dismount`; puts the head within 0.5 m of the stake; turns passthrough off. Back to table runs WarpingOut, Hub, with passthrough on. Stake to stake sends exactly one `moveTo`. The rig never goes below the floor. The standee never shows `thumbsUp` or `crossedArms`. The warp sphere exists only during warps.

## Acceptance checks

- Unity MCP: console clean; EditMode and PlayMode pass; Play in the Editor in Solo demo with screenshots in the hub (model and standee), at 0.3 s, 0.6 s and 0.9 s into a warp in, in the site at 5+20 during the collapse, and after Back to table.
- Bryan, after Build and Run: the room and the model on the table; the warp in and out feel comfortable; stake warps work; hands and controllers work; the collapse and the argument at 5+20. His reply is the result.

## Out of scope

Team mode and the guide in the headset (WO-S9), new voice lines (WO-S10), hand gestures beyond what the Interactions Rig provides.
