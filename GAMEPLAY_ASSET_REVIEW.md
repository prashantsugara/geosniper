# Gameplay asset review — 19 September 2026

## Scope and outcome

Inspected 42 imported model assets under Resources/Models, reviewed the gameplay
spawn/presentation paths, and extended the isolated Unity geometry regression suite.
This is an asset-quality pass, not certification that the prototype is release-ready.
The detailed source inventory is in `Logs/GameplayGeometryAudit.md`.

## Fixes implemented

- Unified the three sniper models' gameplay/lobby geometry calibration. They now
  use explicit stock-to-muzzle lengths (1.25 m, 1.08 m, 1.02 m), a consistent grip
  origin, and the same orientation corrections. The old first-person path replaced
  imported scale with a heuristic while the lobby used measured bounds.
- Adjusted first-person placement for the normalized grip origin. Device/FOV
  playtesting is still required, especially during reload and recoil.
- Disabled imported weapon colliders so visual-only weapons cannot intercept
  movement or raycasts.
- Corrected the procedural fallback rifle barrel's cylinder axis/dimensions and
  normalized the complete fallback rifle rather than just its receiver.
- Added a finite/nonempty-volume guard before scaling an imported building facade;
  invalid geometry falls back to the existing procedural building.
- Changed fallback bone matching to prefer explicit anatomical names over aliases.
  Near-horizontal civilian bind-pose arms are lowered before recording rest poses.
- Removed `female_civilian_v1` from moving civilian selection: it has only static
  meshes and no skeleton, so it cannot use the walking animator. Source retained.
- Removed the 1.33-million-triangle IndianSoldier from the automatic lobby fallback.
  Source retained; the existing lighter Soldier remains the fallback.

## Findings that remain

| Priority | Finding | Required next work |
|---|---|---|
| High | MK12 source has 123 renderers; M24 has 26 | Bake material-aware combined meshes/atlases and measure draw calls on Android. Preserve scope/glass and any animated parts. |
| High | No LODGroup found in the 42 imported sources | Author and validate lower-detail meshes; check scoped target visibility so distant enemies do not disappear. |
| High | Unrigged female civilian cannot walk | Rig and weight the asset, supply idle/walk/run animations, or replace it with a compatible licensed rig. |
| High | Fallback locomotion is procedural, not a finished animation set | Authored locomotion, turns, hit reactions, death, and weapon-specific hand IK; test feet on slopes and stairs. |
| Medium | Oversized/legacy and duplicate source assets remain in Resources | Audit references and move confirmed-unused source assets outside Resources before release; do not delete art blindly. |
| Medium | Vehicle movement collision uses full-model box bounds | Review hull/cabin compound shapes where empty space matters for cover; retain predictable movement collision. |
| Medium | Source styles and textures vary | Establish consistent material, texel-density, lighting and character art standards. |

The 42 source models had no empty/non-finite mesh bounds, missing material slots,
or missing bone references in the inventory. This does **not** establish correct
UVs, winding, self-intersections, textures, physical anatomy, or animation quality.
Some source materials are deliberately replaced by runtime materials.

## Verification

Run `Tools/run_review_checks.ps1` and wait for the isolated Unity process to exit.
Reports and asset renders are written under `.utmp/review-unity/Logs`.
The suite covers character placement at four headings, vehicle orientation and
collider bounds, moving civilian rig availability, three sniper lengths/axes,
invalid-volume rejection, precise shot distances, cover, civilian hit blocking,
difficulty/noise isolation, traffic lanes, and existing combat checks.

## Professional-release gates not yet met

- Android device playthroughs: startup, memory pressure, suspend/resume, thermal
  throttling, frame pacing, and rendering across representative phones.
- Gameplay sessions across every mission and difficulty, with measured completion
  rates, time-to-detection, time-to-kill and failure reasons.
- First-person weapon framing, muzzle effects and hand placement under every
  zoom/reload/recoil state and supported aspect ratio.
- Terrain-aware foot contact, collision/cover accuracy and traversal checks across
  generated sectors, including slopes, roofs and narrow streets.
- Audio, effects, accessibility, onboarding and consistent art direction.

No Android device was connected for this pass. No production APK or publishing
claim is made. The test AdMob App ID remains configured.
