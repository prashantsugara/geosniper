# Gameplay, lobby, and alignment fixes

26 September 2026

All 16 findings in CODE_REVIEW_REPORT.md have been addressed in code. This report records the fixes and the scope of verification; it is not a guarantee that the game has no remaining defects.

## Enemy and weapon alignment

The largest confirmed attachment bug was ambiguous bone matching: searching for `handr` matched `LeftHandRing1`, attaching an enemy rifle to a finger on the wrong hand. Bone lookup now matches normalized bone-name suffixes in priority order, selecting the actual right wrist.

| Original findings | Fix |
| --- | --- |
| 1: Disarmed gun follows enemy | Disable the weapon anchor, clear the muzzle, detach the gun and hand control to its Rigidbody. Size its dropped collider to the imported model. |
| 2–3: Wrist offsets and unsupported left hand | Shared grip, support, stock and muzzle markers; palm registration; two-bone arm solving; shoulder placement while aiming. Lobby and enemies use the same attachment system. |
| 4: Unspecified update order | Animation runs before enemy correction and final weapon/arm placement. |
| 5: Collapsed first-person left arm | Remove both zero-scale paths and fit both arms to each weapon using a dedicated grip controller. |
| 6: Whole-model weapon calibration | Exclude separate skinned arms from rifle dimensions; derive grip and barrel position from weapon geometry, with fallbacks for other assets. Use consistent M24 models in lobby and gameplay. |

The animation root also now resolves to the imported skeleton hierarchy instead of moving the outer placement transform.

## Gameplay

| Original findings | Fix |
| --- | --- |
| 7: Stale weapon stats | Apply selected weapon damage, upgrades, noise and scope settings on equip. In-flight projectiles retain their launch configuration. |
| 8: Magazine refill exploit | Preserve ammunition separately for each weapon; ignore reselecting the active weapon; prevent swaps during reload, chambering and bullet camera. |
| 9: Keyboard crouch overwritten | Make player stance authoritative and synchronize touch controls with it. Sprint exits crouch consistently. |
| 10: Invalid knife takedowns | Validate distance, height, forward cone and unobstructed line of sight both when selecting and executing a takedown. |
| 11: Bullets bypass cover | Respect nearest solid geometry; remove arbitrary wall traversal; permit only measured thin recognized penetrable surfaces, checking exit thickness and intervening colliders. |
| 12: Invisible scoped controls | Gate touch and mouse hit regions using the same visibility conditions as their controls. Block invalid weapon slots. |
| 13: Repeated shot mesh baking | Share one query version across aim-assist rays and cache each skin bake within that query; refresh for subsequent shots. |

Blocked/dead player states clear buffered fire. Range mode explicitly starts a noncombat observation session and preserves that intent on restart. Holdout duration now uses difficulty in the correct direction: casual assistance does not lengthen required survival while harder missions do not shorten it.

## Lobby and deployment

| Original findings | Fix |
| --- | --- |
| 14: GPS Range inherits stale state | Prepare every deployment mode explicitly, including campaign, stage, duel and range fields. |
| 15: Failures silently launch another mode | Mission/map failures return to a visible failure state, clean up partial worlds and GPS work, and retain the requested mode/location for retry. Offline practice is an explicit separate action. Prevent soldier dragging from consuming failure-overlay clicks. |
| 16: Material leak | Track and destroy lobby rifle material instances when switching rifles and tearing down the lobby; register soldier materials with stage cleanup. |

## Validation

- Runtime and editor assemblies compile with no errors. Existing obsolete Unity API warnings, an unused field warning and an analyzer informational message remain.
- The isolated Unity broader regression suite passed all 20 reported checks, including campaign progression, rewards, exact/skinned hit detection, cover, damage events, difficulty, traffic, model placement and weapon calibration.
- The isolated Unity focused suite passed all eight groups: animation, loadout, occlusion, deployment, stance, knife, enemy poses, and lobby/first-person grips.
- Enemy pose checks sampled two shipped rigs over six clips, three animation times and three aim elevations (108 combinations). Right-hand contact was within numerical precision; the worst left-hand gap was 2.42 cm on the shorter ch35 rig, below the 5 cm check limit. Arm solving preserves bone lengths instead of stretching the skeleton.
- All three rifles passed lobby checks at four character rotations and first-person hand-contact checks.
- Rendered enemy pose images were inspected for idle, firing and running. These isolated pose fixtures do not reproduce the full gameplay environment or its material setup.
- Captured and inspected the running lobby with the textured operator and M24. The rifle is held by both hands. Capture: `.utmp/review-unity/Logs/LobbyPreview.png`. The isolated editor logged an internal Unity Search indexing exception during startup; capture nevertheless completed successfully. This editor diagnostic is not fixed by the gameplay changes.
- Updated older test expectations to the current 50-mission campaign and current animation/acoustic behavior. The synthetic skin-motion test now checks deformation before damage reactions can rotate the test target.

Reproduce with `Tools/run_review_checks.ps1` and `Tools/run_gameplay_fix_checks.ps1`. The latter requires the former's isolated project setup. Outputs are under `.utmp/review-unity/Logs/ReviewRegressionChecks.txt` and `GameplayFixChecks.txt`.

## Remaining validation limits

No Android device build, touch-device playthrough, live GPS/map-provider integration test, or sustained frame-time/memory profile was performed. Automated contact measurements do not prove every animation blend, reload frame, recoil frame or finger pose looks natural. Test a complete mission and lobby-to-mission-to-lobby cycle on the target device before release.
