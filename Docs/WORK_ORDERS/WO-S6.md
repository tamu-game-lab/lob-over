# WO-S6 Building Blocks foundation

## Goal

`Site.unity` keeps the Building Blocks Bryan added, and nothing in our code builds, copies or reconfigures an XR rig. The player no longer falls. The start panel stays where it appears and is pressed through the Interaction SDK. The scene builder rebuilds only the objects it owns.

## Read

- `AGENTS.md`, `Docs/SITE_SEAT_V2.md` ("What the test found" and "XR setup rules")
- `Assets/LOBOver/Editor/SiteSceneBuilder.cs`, `Assets/LOBOver/Runtime/Start/**` (StartPanel, `Editor/XRSceneSetup.cs`), `Assets/LOBOver/Runtime/VR/InspectorSeat.cs`, `Assets/LOBOver/Runtime/MR/DioramaSeat.cs`, and whatever creates the `ControllerPointer` root (grep)
- The Interaction SDK in `Library/PackageCache/com.meta.xr.sdk.interaction*`: `Runtime/Scripts/Interaction/Models/Locomotion/` (FirstPersonLocomotor, CharacterController, PlayerLocomotor), PointableCanvas, RayInteractable, PokeInteractable
- `Site.unity` roots as of October 21 (orchestrator's note). Keep, untouched: `OVRCameraRig` (holds `[BuildingBlock] OVRComprehensiveInteractionRig` and the Hand Tracking and Controller Tracking blocks), `[BuildingBlock] Passthrough`, `[BuildingBlock] Real Hands`. Block demos, to switch off but keep: `[BuildingBlock] Cube` (with `HandGrabInstallationRoutine`), `Poke Interaction` (with `[BuildingBlock] ISDK_PokeInteraction`). Old builder output, to replace: `ControllerPointer`, `Director`, `MRDiorama`, `StartPanel`, `FullScaleRoot`, `VRSite`, `Sun`, `Global Volume`.

## You may change

- `Assets/LOBOver/Editor/**`, `Assets/LOBOver/Runtime/**`, `Assets/LOBOver/Tests/**`
- `Assets/LOBOver/Scenes/Site.unity`, only through the builder and the setup menu items

## Build

1. **Non-destructive builder.** `LOB Over > Build > Site Scene` opens the existing `Site.unity` (it creates it only if missing, then stops with "add the blocks listed by Check Building Blocks") and rebuilds only roots named `[LOBOver] ...`: `[LOBOver] Lighting` (sun, volume), `[LOBOver] Director`, `[LOBOver] Hub` (floor collider, model anchor, PM standee placeholder), `[LOBOver] Site` (the full-scale world), `[LOBOver] Start` (start panel) and `[LOBOver] Preview` (desktop preview, Editor only). On its first run it removes the old builder roots listed above by exact name, after checking that each holds no `BuildingBlock` component and no `OVRCameraRig`. It never calls `NewScene` over an existing scene and never deletes or reparents anything else.
2. **No code-built rig.** Delete `XRSceneSetup`'s rig copy and passthrough handling, the `ControllerPointer`, and every line that adds, copies or reparents `OVRCameraRig`, `OVRPassthroughLayer`, controller or hand objects, or changes `OVRManager` settings in the scene. App-wide runtime settings (72 Hz, foveation) live in one `XRRuntimeSettings` component under `[LOBOver] Director`.
3. **`XRRig` service** (Runtime): finds the rig and its anchors from the blocks by component at runtime: centre eye, left and right controller anchors, left and right hand anchors, the passthrough layer, and the Interaction SDK ray and poke interactors. Every seat uses it. A missing piece logs one clear error naming the block.
4. **`LOB Over > Setup > Check Building Blocks`**: reports present or missing for Camera Rig, Passthrough, Controller Tracking (left, right), Interactions Rig, Hand Tracking (left, right) and Real Hands, detecting each by its `BuildingBlock` component or its main component, and lists block demo objects. The Site Scene builder and the Quest build items (WO-S5) call it and stop if anything is missing.
5. **`LOB Over > Setup > Configure Building Blocks`** (idempotent; logs every value it changes): switches off the Interactions Rig's locomotion (teleport, slide and turn interactors, the locomotor) and its gravity; sets the tracking origin to floor level; sets the passthrough layer to underlay and enabled at start; switches off the block demo objects (`SetActive(false)`). It changes component values on block objects; it never adds or deletes them.
6. **Floor.** `[LOBOver] Hub` has an invisible box collider, 20 m by 20 m, whose top is at y = 0 (the tracked floor). The full-scale site's ground has colliders.
7. **Start panel.** A world-space uGUI canvas made interactable with the Interaction SDK's `PointableCanvas` (ray and poke, controllers and hands). It appears once, 0.6 m in front of the head at chest height, after the first valid head pose, and then stays put: no following, no physics, not parented to the rig.
8. The existing modes (Solo VR Site, MR Diorama, Desktop in the Editor) keep working through the blocks until WO-S8 replaces VR Site and MR Diorama with the hub and the warp.
9. **Tests.** EditMode: running the builder twice leaves a test object named `[BuildingBlock] Test` and `OVRCameraRig` untouched (same instance ids, same parents); Check Building Blocks finds every block in `Site.unity`; a source scan finds no `AddComponent<OVR`, no `Instantiate` of an `OVRCameraRig`, and no `OVRInput`-driven raycast under `Assets/LOBOver`. PlayMode: the rig's world y stays within 1 cm for 3 s after start with no headset; the start panel's position does not change for 3 s.

## Acceptance checks

- Orchestrator through the Unity MCP: Check Building Blocks (all present); Configure Building Blocks; Site Scene twice; console clean; EditMode and PlayMode suites pass; Play in the Editor with the rig's y logged at 0 s and 3 s; the start panel visible and still.
- Build and Run Quest (WO-S5), then Bryan: no falling, the panel stays put, controllers and hands visible, ray and poke press the panel's buttons.

## Out of scope

The hub and the warp (WO-S8), team mode (WO-S9), voices (WO-S10).
