# WO-L8 rejection 4 (2026-10-22 15:00), the fourth round, authorized by Bryan under K51 to K53

Round 3 was verified through the MCP (`Docs/WORKLOG.md`, 13:50 to 13:58): compile clean; Check passes; Play Scene twice identical; no console error anywhere; EditMode 50/53; PlayMode 20/22; through the head camera the stage reads as K50 asks (the trench 7 m ahead, the crane and the loader on the far bank at real size with wheels and tracks on the ground, the excavator at the right edge, the foremen beside the trench). Bryan ruled K51 to K53 (`Docs/DECISIONS.md`, carried into `WO-L8.md` Amendment 2) and authorized this round for the four remaining items. Fix exactly these; everything under "Passed" stays.

## Failed

1. **The budget (K51).** The stage budget is 500,000 triangles and 150 draw calls in every view the player can look at; the 300,000 guideline stays for the hub. Round 3 read 353,578 triangles across the trench, 349,418 in the Day-5 head view and 424,953 in the entry view through the head camera alone, all with the workers' full meshes, which stay. Required: `SitePresentationTests.PipeStationPreviewStaysInsideQuestBudget` asserts every logged stage view under 500,000 triangles and 150 draw calls and keeps logging each; `StageHostTests.DayFiveHeadViewStaysInsideQuestBudget` asserts the same and logs its number; no LOD work on workers or machines in this round (a follow-up only if the Quest 3S frame time shows it).

2. **The far-bank rule and grounding (K52).** Round 3 read "pipe far-bank edge" −3.64 m because the crane's hook and cable hang below 1.5 m over the trench. Required: grounding and the far-bank rule read only the renderers whose bounds touch the ground (lowest point within 0.3 m of the surface under them); booms, hooks, cables and animated parts are excluded from the placement checks in the builder, at runtime and in both tests, and from `GeometryMatchesDrawnBoundsForEveryMachineWorkerAndProp` (the excavator's animated "dirt" part failed it in round 3 at −1.89 against 2.44). The near edge of that ground-touching set within 0 to 3 m of the far trench edge.

3. **Machine scale (K53).** Round 3 read exc/standard roof 2.84 m against 3.2 after the small one was refitted. Required: a machine is scaled by one dimension, its length (within 10 percent of the plate's length); the roof height is logged, not asserted, in `AllMachineVariantsAndWorkersHavePhysicalBounds` and in the builder's warning line.

4. **The five captures.** `StageHostTests` stopped at the far-bank assertion in rounds 1 to 3, so only `l8-hub*.png` exists. After the fixes `Monitor/out/unity/l8-{hub,site,argument,log,dusk}.png` (raw and composite) exist from the PlayMode test; the orchestrator reviews them against the goal sentence.

## Passed (keep)

Everything from rejection 3's "Passed" list, plus round 3's stage: the trench 7 m ahead, the 18 m slot cap, the workers with their imported meshes (no reduction), the lower-body idea now replaced by K52's ground-touching set, the EventSystem and PointableCanvasModule, the 50 EditMode and 20 PlayMode tests that passed, no console error at build or in Play.

## Scope note

Unity cannot run in your sandbox; compile with `Assets/LOBOver/Tests/CompileOffline.ps1`. Ignore `.agents/`, `Docs/WORK_ORDERS/logs/` and `Relay/` (WO-L9 runs in parallel and owns `Relay/`, `Monitor/`, `Data/relay.json`, `Showcase/` and `README.md`) in the scope check. Do not start or stop any host or node process.
