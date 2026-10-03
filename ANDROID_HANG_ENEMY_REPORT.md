# Android movement hang and enemy return fire

## Findings and fixes

- Moving through a geographic world streamed adjacent sectors. Their generation synchronously baked a NavMesh from thousands of colliders, which could stall Unity's main thread. The primary sector now uses `UpdateNavMesh` asynchronously during play, and adjacent scenery-only sectors skip redundant NavMesh baking.
- Sector greenery placement scanned every map feature for each ground and roadside candidate. A 32 m spatial index now limits each query to nearby feature bounds and yields during index construction.
- Every enemy repeatedly requested a NavMesh path and copied path corners each frame. Navigation now repaths at staggered intervals, only while moving or when the target changes, and uses `steeringTarget`.
- A gunshot or near miss sent enemies into cover but left long cover and fire cooldowns. Cover selection also repeated expensive physics probes when one shot produced both noise and miss events. Enemies now remember the shooter, react at sniper distance, search cover once per threat, and become ready to return fire while crouching/peeking. Mobile cover search uses fewer candidates.
- World-sector free roam made the player invulnerable, so enemy hits could not hurt the player. Patrol combat is now lethal there; the separate GPS Range mode still explicitly enables nonlethal practice.

## Verification

- `Tools/check_code.ps1`: passes. Existing unrelated Unity API deprecation warnings remain.
- `ReviewRegressionChecks.Run`: passes, including the new close-miss response check.
- `MapWorldChecks.Run`: passes.
- No Android device was connected, so an on-device frame-time and enemy-damage check remains necessary. The code fixes address identified stalls and combat gating, but the exact cause of the reported device hang cannot be proved without an Android logcat and profiler capture.
