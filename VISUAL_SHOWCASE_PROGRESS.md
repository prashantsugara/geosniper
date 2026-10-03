# Playable visual showcase — first environment pass

Integrated into the existing GPS sector generator, not a replacement fictional city:

- Nearby detailed buildings now receive batched physical window surrounds, projecting sills, plinths, floor bands and cornices.
- Original footprints, roads and rooftop spawn positions are preserved. Trim remains below the roof; it does not add rooftop obstacles.
- Daylight ambient fill reduced to separate lit faces from shaded faces.
- Added Geo Sniper > Visual Review > Capture Running Game. In Play mode, load a GPS mission and capture from street and roof positions in day and night. Output: Logs/VisualReview.
- Extended Geo Sniper > Validate Balcony Geometry to cover the new architecture mesh.

Verification: C# compile check; Unity geometry checks and runtime screenshots must still be run in the licensed editor. No Android performance or visual acceptance claim yet.

Remaining: authored rifle/hand animation, richer street surfaces and props, visual inspection of facade/ladder intersections, real device profiling and comparison against the reference. This pass alone does not reach the reference quality.
