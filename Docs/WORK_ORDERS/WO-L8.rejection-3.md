# WO-L8 rejection 3 (2026-10-22 13:40), the third round, authorized by Bryan

Round 2 was verified through the MCP (`Docs/WORKLOG.md`, 13:16 to 13:27): compile clean; Check passes; Play Scene twice identical; EditMode 37/50; PlayMode 21/22; the head view at entry 210,974 triangles and 97 draw calls. Bryan authorized one more round for the three open items and ruled on the open question O17 (now K50 in `Docs/DECISIONS.md`, carried into `WO-L8.md` Amendment 1). Fix exactly these five; everything under "Passed" stays.

## Failed

1. **No mesh reduction on skinned meshes.** `SiteMeshBudgetBuilder` rewrites each worker's skinned mesh with a `boneWeights` array of another length than its vertices; Unity logs `Mesh.boneWeights is out of bounds` twice per Play Scene build, and twelve EditMode tests fail on that line as an unhandled log message (BuildingBlocksTests `RebuildTwicePreservesBlocksRigAndUnownedRoots`; SitePresentationTests `CrewShadowsFollowTheHeadDistanceAndRestoreAfterAMachineChange`, `EveryRuntimeSiteRendererUsesTexturedApprovedShader`, `GroundAndPhysicalDetailsMatchTheSpecifiedDimensions`, `PipeStationPreviewStaysInsideQuestBudget`, `PlayRebuildHasIdenticalGeometryMaterialsAndArtBytes`, `SkyRejectsNonDaylightNamesAndAlignsTheLight`; StagePresentationTests `BuilderAndRuntimeObjectsRestOnTheSameTerrain`, `EveryVariantWorkerAndPropIsGroundedByItsPhysicalGeometry`, `GeometryMatchesDrawnBoundsForEveryMachineWorkerAndProp`, `OneSiteButtonAndLogHaveSpecifiedDimensionsAndGrabComponents`; WarpTests `PlaySceneContainsNoRemovedTransitionRendererOrShader`). Required (Bryan): workers keep their imported meshes; the skinned-mesh reduction path is removed; the fence batching may stay if it holds. No console error during a build.

2. **The small excavator.** `StagePresentationTests.AllMachineVariantsAndWorkersHavePhysicalBounds` reads `exc/small` roof 2.48 m against 2.8 (11 percent off). Required (Bryan): the small excavator is scaled to 2.8 m to the cab roof within 10 percent like the others (scale by the roof height, or by length and roof together, as the builder already does for the variants that pass); the test passes with the 10 percent tolerance.

3. **The far-bank check never measures the boom.** `StageHostTests.DayFiveStageSpeechLogAttentionAndDuskThroughTheHead` reads "pipe far-bank edge" −4.70 m because the crane's renderer bounds cover its boom over the trench. Required (Bryan): the far-bank check, in the builder's placement, at runtime and in both tests (`BuilderAndRuntimeObjectsRestOnTheSameTerrain` and the Day-5 PlayMode test), measures the lowest 1.5 m of each machine's bounds (tracks, wheels, body), never the boom: the near edge of that slab within 0 to 3 m of the far trench edge.

4. **K50, the stage.** Bryan's ruling on O17: the machines on the far bank at real size, the trench 7 m ahead (not 6), the crew slot cap 18 m instead of 30 so the excavator stays at the edge of the view. Required: `StageLayout.MaxSlot` 18, the site placed with the trench centreline 7 m ahead of the head, the slot formula otherwise unchanged (8 m + 0.02 m per foot of real gap, the 0 ft backfill case at −4 m); the tests follow (the 6 m assertion becomes 7; `SlotsFollowRealGapsAndCap` caps at ±18; the Day-5 angle check stays at 65 degrees).

5. **The five captures.** After the fixes, `Monitor/out/unity/l8-{hub,site,argument,log,dusk}.png` (raw and composite) exist from the PlayMode test; the orchestrator reviews them against the goal sentence.

## Passed (keep)

Everything from rejection 2's "Passed" list, plus round 2's renderer-bounds measure, the `CanvasEvents` object with the EventSystem and PointableCanvasModule under `[LOBOver] Client`, the destroyed-camera guard and the three asserted WO-L4 views, the head-view count with the render count, the loader on the far bank beside the trench on the ground, the 21 PlayMode and 37 EditMode tests that passed.

## Scope note

Unity cannot run in your sandbox; compile with `Assets/LOBOver/Tests/CompileOffline.ps1`. Ignore `.agents/` and `Docs/WORK_ORDERS/logs/` in the scope check. Do not start or stop any host or node process.
