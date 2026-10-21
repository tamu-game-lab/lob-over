# Rendering on the Quest with passthrough

Written October 21, 2026, after two headset builds failed on compositing and look. The facts below are what every headset work order has to respect. `.claude/skills/quest-mr-rendering/SKILL.md` points the orchestrator here; work orders list this file under Read.

## How passthrough reaches the eye

1. The Passthrough block's `OVRPassthroughLayer` is an underlay: the compositor draws the cameras' image behind the app's eye buffer and uses the eye buffer's alpha to blend. Alpha 0 shows the room, alpha 1 shows the app, values between blend.
2. The eye buffer uses premultiplied alpha (`OVRManager.eyeFovPremultipliedAlphaModeEnabled`, on by default). A pixel that should show the room is (0, 0, 0, 0). A half-transparent pixel is (rgb × a, a). Never write colour with alpha 0 and never disable premultiplied alpha (the Real Hands block does; that is why it is out, K34).
3. Everything that touches the alpha channel matters:
   - The XR camera clears to Solid Color with alpha 0. A Skybox clear writes alpha 1 everywhere and hides the room.
   - HDR off on the camera and on the Mobile render pipeline asset. The 11-11-10 HDR colour format has no alpha channel; with it the whole view goes opaque.
   - Post-processing off on the XR camera (and no Volume with Vignette, Bloom or Tonemapping in the scene). URP's post-processing writes alpha 1 unless Alpha Processing is on in the pipeline asset; off is simpler and cheaper.
   - Opaque shaders write alpha 1, which is correct for the full-scale site once it is fully there. Transparent shaders with `Blend SrcAlpha OneMinusSrcAlpha` leave a sensible alpha behind.
   - MSAA resolves alpha normally. Render scale and upscaling keep alpha.
4. Anything meant to show the room must not be drawn at all (`clip()` or not rendered); do not cover the view with a mesh that writes alpha 0, because any mistake in that mesh (position, stuck animation, culling) cuts the view into a window. The October 21 build did this with a 4 m sphere around the head and the player saw the site through a hole.
5. The sky in the site is a dome mesh with its own shader, never the camera skybox, so it can fade in and out like the rest of the site.
6. Turn the passthrough layer off (`enabled = false`, `hidden = true`) only when the site is fully opaque, and on again before anything starts to dissolve. While it is on, the compositor pays for it; while it is off, alpha does nothing.

## Stereo and shaders

- Single-pass instanced rendering. Every hand-written shader has `#pragma multi_compile_instancing`, `UNITY_VERTEX_INPUT_INSTANCE_ID`, `UNITY_VERTEX_OUTPUT_STEREO`, `UNITY_SETUP_INSTANCE_ID`, `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO` and `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX`, and uses URP's `Core.hlsl`, `Lighting.hlsl` and `Shadows.hlsl` transforms, never `UNITY_MATRIX_VP` by hand.
- Lit shaders go through `UniversalFragmentPBR` (or `UniversalFragmentBlinnPhong` where cheaper) with `GetMainLight(shadowCoord)` so they receive the directional shadows and the SH ambient, and `MixFog` for the fog.
- Global values for an effect over the whole site go through `Shader.SetGlobalFloat` and `Shader.SetGlobalVector`, read by every site shader; no per-material loops in C#.
- Vulkan on the Quest compiles `clip()` fine. Avoid dependent texture reads in loops, `discard` in a depth prepass (there is none) and more than three texture samples per layer.
- Shader Graph is allowed when a graph is easier to keep than HLSL, with the same stereo rules (Shader Graph handles them), alpha clip on, and the warp values as global properties (exposed off).

## Budget for the Quest 3S at 72 Hz

- About 300k visible triangles, 150 draw calls (static batching on for the builder's static geometry, GPU instancing on materials for repeated props).
- One directional light with soft shadows, one cascade, 2048 shadow map, shadow distance 60 m. No point or spot lights.
- MSAA 4x (cheap on a tile GPU). Render scale 1.0 (0.8 blurs text and distant detail). Fixed foveated rendering Low; Medium and High blur the edge of the view enough that testers call it a lens.
- Ambient from a gradient (trilight) or a baked sky probe. Linear fog from 300 m to 2,500 m into the horizon colour; it is what gives the flat ground depth.
- No screen-space ambient occlusion, no post-processing, no realtime reflection probes.
- Textures 1024² or 2048², ASTC 6x6; normal maps on every ground and prop material, because flat colour without normals is what reads as a drawing.

## Readable and comfortable

- Text at 0.5 m: cap height at least 1.5 cm (2.5 cm for titles), TextMeshPro SDF, high contrast on an opaque panel. Buttons at least 11 by 6 cm.
- Nothing moves the player: the world changes in place (the site root is moved before it appears; the rig and the camera are never moved). Transitions last 1 to 2 s. No vignette is needed because there is no vection.
- A transition always finishes: a watchdog snaps it to its end state after 3 s, Back is never blocked, and each phase logs one line (`adb logcat -s Unity`) so a stuck state can be read from the device.

## Seeing the real thing before a build

The Editor cannot show passthrough. For every screenshot of a passthrough state, render the eye view to an ARGB32 texture and composite it over a stand-in room image (`Monitor/out/unity/room.png`, a mid-grey wall with a 20 cm grid) using the eye buffer's alpha; the result is what the compositor would show. Review those, not the raw frame. Quest Link with "Passthrough over Link" shows the real compositor in the headset but is not required.
