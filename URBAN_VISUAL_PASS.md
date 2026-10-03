# Urban visual pass

## Street lighting

Added roadside poles with warm emissive fixtures and actual downward spotlights. Day/night and rainy-night environment changes explicitly control lamp and window emission; daylight turns off ground illumination. Up to 32 fixtures per sector, with only the nearest 4 spotlights active globally on Android (8 desktop), within 80m of the main camera. Shadows are disabled on these lights to limit cost. Sector disable/unload removes lights from the global scheduler. No baked lighting, bloom or real-world lamp-position data is claimed; fixture placement is procedural along mapped roads. C# compilation verified; Unity rendering and device performance remain unverified due to the previously recorded connection/licensing blockers.

## Revision after in-game feedback

Removed the rectangle-only balcony eligibility gate and reserved ladder clearance only on the actual longest edge. Smaller and irregular footprints can now receive balconies. Near-field buildings use the original footprint shell with flat masonry and new projecting dark window frames, mullions and warm emissive room panels instead of repeated painted windows or imported facades. Window and balcony floor/bay spacing is shared. Limits: 48 balcony buildings and 64 window buildings on mobile within 300m of sector origin; 24 balconies and 144 rooms maximum per building. These are emissive window surfaces, not real point lights or a bloom/postprocessing pass.

Updated runtime/editor compilation passes. Added small-irregular-footprint and illuminated-window checks. Actual Unity rendering/tests could not run: MCP editor connection unavailable; isolated batch preview first failed Package Manager startup in the sandbox and then failed outside it with exit 198, `No valid Unity Editor license found` / missing `com.unity.editor.headless` entitlement. The user-facing editor may still work; this does not prove its interactive license is invalid. No preview or Android visual/performance verification is claimed. `CityVisualPreview.Run` renders actual SectorWorld output using synthetic sample footprints once isolated batch licensing works.

The notes below describe the initial, superseded pass.

Implemented a first architectural/weapon pass, not a full photoreal city replacement.

- Eligible rectangular buildings (6–28m high) receive batched balcony slabs, slender railings and window surrounds. Existing GPS footprints, foundations and roofs remain unchanged.
- Maximum 24 dressed buildings per mobile sector, 60 on desktop; within 180m of the sector origin; up to 24 balconies per building. This is a fixed sector budget, not camera-distance streaming.
- Balconies are omitted near other footprints and road corridors. Edge midpoints remain clear for rooftop ladders. Solid additions have mesh collision so shots do not pass through the visible slabs or railings.
- Road shoulders use a lighter pavement colour; this is not a full sidewalk/junction reconstruction.
- Rifle fallback materials distinguish metal, polymer and coated lenses while preserving each textured material slot. First-person framing is closer; muzzle flash placement follows calibrated weapon bounds and uses a smaller, briefer flash.

Validation: runtime/editor C# compilation passes with existing obsolete API warnings. Run `Geo Sniper > Validate Balcony Geometry` in Unity for geometry assertions. Runtime visual review, collision/ladder traversal and physical Android profiling remain required. Muzzle point uses bounding-box estimation, not a per-model authored socket. Existing FBX meshes are retained; new high-fidelity weapon modelling/rigged hands are not included.

Visual review: load a fresh GPS mission near medium-rise buildings, inspect balcony clearances and ladders, fire each owned rifle, and compare GPU/CPU frame time on the target phone. Do not use this pass as evidence that the generated advertising art matches gameplay.
