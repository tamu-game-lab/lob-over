# Headset seat v2

Revised October 21, 2026, after Bryan's first headset test of the showcase build. This file overrides the headset parts of `Docs/DESIGN_v0.md` ("The VR seat", the truck ride) and of WO-S4. The rest of the design stands: Site sees but cannot act, every order is confirmed in the field, no verdicts during play.

## What the test found

1. The player fell through the world at start, so the start panel seemed to fly up. Likely cause, to verify: the Interactions Rig block brings Interaction SDK locomotion (the scene contains its locomotor with a gravity factor), and nothing under the player has a collider.
2. Our scripts built or copied the rig, the passthrough layer and a controller pointer (`XRSceneSetup`, `ControllerPointer`) instead of relying on Meta's Building Blocks, which have proven more reliable in Bryan's projects.
3. The seat was VR only. In a three-person game in one room, the headset player must see and talk with the other two.

## XR setup rules (Building Blocks only)

1. The XR rig in `Site.unity` is made of Building Blocks that Bryan adds in the Editor: Camera Rig, Passthrough, Controller Tracking, Interactions Rig (which brings Hand Tracking) and Real Hands. They are in the scene as of October 21. Grab Interaction and Teleport are not needed now.
2. No code creates, copies, parents or reconfigures a camera rig, a passthrough layer, or controller or hand objects. Runtime code finds them by component (`OVRCameraRig`, `OVRPassthroughLayer`, the Interaction SDK interactors) and reads their anchors.
3. Pointing, pressing and selecting go through the Interaction SDK interactors that the Interactions Rig provides, for controllers and hands alike: ray at a distance, poke for buttons within reach. Our objects become interactable with Interaction SDK components (`RayInteractable` with a collider surface, `PokeInteractable`, `PointableCanvas` for uGUI panels) and react through its pointer events. No raycasts on `OVRInput`.
4. No locomotion and no gravity. `LOB Over > Setup > Configure Building Blocks` switches off the Interactions Rig's locomotion (teleport, slide, turn, the locomotor and its gravity) and sets the tracking origin to floor level. A floor collider at the tracked floor height exists in the hub and in the site regardless. The only way to move is the warp.
5. The scene belongs to Bryan. The builder edits only the objects it owns, whose root names start with `[LOBOver]`, inside the existing `Site.unity`. It never replaces the scene and never touches `[BuildingBlock]` objects, `OVRCameraRig`, or anything else it did not create. Demo objects that came with blocks (`[BuildingBlock] Cube`, `Poke Interaction`) are switched off, not deleted, and listed for Bryan.
6. `LOB Over > Setup > Check Building Blocks` reports each required block as present or missing and stops any build that lacks one, naming the block to add from the Building Blocks window.

## The experience

1. **Hub, in passthrough.** The player stands in the real room and sees the team, the Plan laptop and the paper. On the table, 0.6 m ahead at 0.75 m height (a wrist button re-places it), is the site as a 2 m model: trench states, water, crews and machines moving, zone boards, the sky. A PM standee at the model's near corner uses LOB-Game's PM portraits: `explaining` for the morning brief and while a PM line plays, `worriedSurprise` for a few seconds when an event card arrives, `clipboard` otherwise. It never shows `thumbsUp` or `crossedArms`, which LOB-Game uses as outcome reactions and which would read as verdicts here. The model shows only what the full-scale site shows: no plan, no money, no forecast numbers. In a solo demo for stakeholders the Plan panel may float above it; never in a team game.
2. **Warp in.** Point at a stake, a crew or a zone board on the model and select. The warp takes about 1.2 s: a ring of light opens at the player's feet, the site replaces the room from the ground outward through a noise-edged dissolve with a bright edge, and the player stands at that stake at full scale, facing the trench. The player does not travel; the world changes around them, so there is no vection. Real Hands keeps the player's own hands visible. The simulation receives `moveTo` for that station and `dismount` on arrival.
3. **In the site, in VR.** Look at a thing for two seconds to inspect it, or point at it and select. The wrist panel on the left hand or controller has Flag, Stop work, Confirm (Done or Not carried out) and Back to table, pressed by poke or ray. Foremen speak from where they stand. Stake markers along the trench warp to another stake with a short version of the effect (0.6 s). There is no truck.
4. **Warp out.** Back to table plays the dissolve in reverse; the room returns, and the model marks where the player was.
5. **Cards and prompts.** Event cards (facts only) float above the model in the hub and appear on the wrist panel in the site. Guide prompts (guided mode, `Docs/PLAYTHROUGH.md`) appear in the same places with a Ready button.

## Two modes

1. **Team game.** The headset is the Site seat of a game hosted by the Node host on the PC. The world is drawn from the host's `state` messages and every verb goes to the host. Over USB, run `adb reverse tcp:7331 tcp:7331` on the PC and use host address `127.0.0.1` (no Wi-Fi needed); wirelessly, use the PC's LAN address. Plan plays on `plan.html`, Rules on `rules.html` or the printed kit, the facilitator on `facilitator.html`.
2. **Solo demo.** The embedded C# simulation with Plan on autopilot, as built in WO-S3 and WO-S4, for one person or a stakeholder audience.

## The warp effect, technically

An alpha-write dissolve over the eye buffer, with passthrough as the underlay: passthrough shows wherever the eye buffer's alpha is 0, the same principle as Meta's selective passthrough. An inward-facing sphere around the head, drawn after everything else, writes alpha from a threshold on the radial distance from the player's feet plus noise, and writes an emissive colour in a thin band at the edge. It must support single-pass instanced stereo and hold 72 Hz on the Quest 3S. Parameters: duration, edge width, edge colour, noise scale. If the shader cannot run, the fallback is a 0.3 s fade through black.

## Comfort rules

1. No camera motion the player did not make with their head.
2. A warp never exceeds 1.2 s, and the player can look around during it.
3. The hub is the rest frame; Back to table ends a VR stint at any time.
4. Nothing follows the head except the warp sphere during a warp.

## For stakeholders

Cast the headset to a laptop or TV through the Meta Horizon app or Meta's casting page, so the room sees the warp and the site while the headset player talks with Plan and Rules.
