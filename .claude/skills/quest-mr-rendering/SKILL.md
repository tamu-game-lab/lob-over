---
name: quest-mr-rendering
description: Rules for anything drawn on the Quest in this project (passthrough compositing, alpha, URP settings, stereo shaders, the Quest 3S budget, readable panels, transitions). Load before writing or verifying a headset work order, a shader, a scene builder or a Quest build setting.
---

Read `Docs/XR_RENDERING.md` first; it is the source. Then, for the work in hand:

1. Passthrough shows where the eye buffer alpha is 0. Camera: Solid Color, alpha 0, HDR off, post-processing off; no camera skybox (a dome mesh instead); never cover the view with an alpha-0 mesh.
2. Premultiplied alpha: transparent pixels are (rgb × a, a); fully transparent is (0, 0, 0, 0). Do not touch `eyeFovPremultipliedAlphaModeEnabled`.
3. Shaders: single-pass instanced macros, URP includes, main light with shadows, SH ambient, fog; global properties for site-wide effects.
4. Budget: 300k triangles, 150 draw calls, one shadowed directional light (1 cascade, 2048, 60 m), MSAA 4x, render scale 1.0, foveation Low, no post, no SSAO.
5. Panels: text 1.5 cm or taller at 0.5 m, opaque, within reach; buttons 11 by 6 cm. Transitions change the world, never the player; they end within 2 s, have a 3 s watchdog, log every phase, and never block Back.
6. Head-attached hazards: the Interactions Rig's `SmoothMovementTunneling` and `WallPenetrationTunneling` objects must be inactive (their `TunnelingEffect` quads draw a grey tunnel when their drivers are off); every acceptance image is rendered through a camera parented to the centre eye anchor, never a free preview camera.
7. Verification: composite eye renders over `Monitor/out/unity/room.png` by alpha before judging any passthrough state; read `adb logcat -s Unity` from the device after a headset test; a headset work order is accepted only by Bryan in the headset (K35).
