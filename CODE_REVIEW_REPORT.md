# Gameplay, lobby, and weapon alignment review

Review date: 26 September 2026.

**Fix status:** The 16 findings below have now been addressed in code. See [FIX_REPORT.md](FIX_REPORT.md) for implementation details, regression results, and remaining validation limits. The original findings and line numbers below describe the pre-fix code.

This is a source-code review of the Unity project, focused on gameplay, enemy animation, weapon presentation, lobby deployment, and their supporting systems. Runtime and editor assemblies compiled successfully using `Tools/check_code.ps1`. Compilation reported obsolete Unity API warnings, an unused field, and a USG0001 analyzer informational message; no compiler errors were reported.

At the time of the original review, no new Unity play-mode session, Android device test, or visual capture had been performed, and no gameplay source had been changed. Subsequent fixes and Unity validation are documented separately. Neither report claims exhaustive coverage of every file, device, or game mode.

P1 = high priority: incorrect gameplay or deployment. P2 = medium priority: visual defects, usability, resource use, or verification gaps.

## Enemy and gun alignment

### 1. P1 — Disarmed rifles continue following the enemy's hand

**Confirmed code defect.** `EnemyBot.Disarm()` detaches the rifle, adds a Rigidbody, and applies an impulse, but leaves `EnemyWeaponAnchor` active. The anchor stops only on death; it does not check `isDisarmed`. Its LateUpdate continues writing the dropped object's position and rotation.

**Effect:** a supposed dropped weapon can snap back to the hand or fight physics. This directly explains one form of enemy/gun misalignment.

**Locations:** `Assets/Scripts/EnemyBot.cs:262`, `Assets/Scripts/EnemyWeaponAnchor.cs:33`.

**Reproduce:** shoot an enemy's arm without killing them, then observe the dropped rifle while the enemy moves.

**Fix:** disable/detach the pose controller before transferring ownership to physics; clear the registered muzzle reference as appropriate.

### 2. P2 — Both enemy and lobby rifles use a guessed wrist offset

**Confirmed implementation limitation; visual severity needs capture.** Both pose scripts put the rifle origin 0.165 m forward, 0.025 m down, and 0.028 m sideways from the right wrist, using the character root's axes rather than the hand's rotation. Enemy finger references are collected but never used in the pose calculation.

**Effect:** wrist turns, different rigs, and different carry poses cannot maintain a consistent palm/trigger grip. Changing a single offset may improve one pose and break another.

**Locations:** `Assets/Scripts/EnemyWeaponAnchor.cs:57`, `Assets/Scripts/LobbySniperPose.cs:116`, `Assets/Scripts/UrbanCombatMission.cs:708`.

**Fix:** author a right-hand socket and weapon grip marker for each supported rig/weapon; align those transforms instead of using a root-space constant.

### 3. P2 — Left-hand and shoulder contact are not constrained

**Confirmed implementation limitation.** While aiming, `EnemyWeaponAnchor` points the rifle toward the player without solving either arm to the new grip locations. In carry/lobby poses, it estimates orientation from the left hand but clamps the vertical span to ±0.15 m. There is no actual support-hand position constraint or shoulder-stock constraint in these controllers.

**Effect:** the barrel can point at the target while the hands remain in their authored animation positions; the support hand floats or clips through the fore-end, especially with elevation differences and crouching.

**Locations:** `Assets/Scripts/EnemyWeaponAnchor.cs:66`, `Assets/Scripts/LobbySniperPose.cs:120`.

**Fix:** use calibrated grip/support/stock markers and compatible carry/aim animations, with arm IK where necessary.

### 4. P2 — Skeleton correction and rifle placement have no explicit execution order

**Runtime risk.** `ArmyAnimation.LateUpdate()` corrects hip translation, `EnemyBot.LateUpdate()` rotates the spine, and `EnemyWeaponAnchor.LateUpdate()` reads the hands and places the gun. These classes do not declare relative execution ordering.

**Effect:** if weapon placement runs before a later skeleton correction, the final rendered hands and gun use different poses. This can produce offsets or jitter. The anchor also snaps position while smoothing rotation, which can expose grip mismatch during quick turns.

**Locations:** `Assets/Scripts/ArmyAnimation.cs:133`, `Assets/Scripts/EnemyBot.cs:1502`, `Assets/Scripts/EnemyWeaponAnchor.cs:33`.

**Fix:** finish animation/root correction/aim pose before applying weapon attachment in a defined final stage.

### 5. P2 — First-person left arm is deliberately collapsed to zero scale

**Confirmed behavior.** `CalibrateTacticalHands()` sets the left arm, elbow, and wrist scales to zero. This hides the support arm instead of correcting its alignment. All equipped weapons pass through this calibration path when the matching bones exist.

**Location:** `Assets/Scripts/SniperPresentation.cs:1211`.

**Effect:** no visible supporting left hand; the rifle can appear unsupported. Zero-scaling skinned bones also needs visual checking for collapsed geometry.

**Fix:** restore the arm and calibrate an appropriate first-person pose for each rifle.

### 6. P2 — Weapon normalization relies on whole-model bounds

**Visual risk.** `WeaponGeometry.Configure()` derives length and grip from aggregate bounds. Any skinned renderer switches the orientation branch to identity. The first-person M24 uses the FPS arms/rifle asset, while the lobby uses a separate M24 asset.

**Effect:** included arms/accessories can influence normalization, and lobby/first-person presentation can differ despite the same weapon label. A skinned mesh alone does not establish a correct barrel axis or grip point.

**Locations:** `Assets/Scripts/WeaponGeometry.cs:8`, `Assets/Scripts/GeoSniperGame.cs:571`, `Assets/Scripts/SniperPresentation.cs:1092`.

**Fix:** store per-asset barrel direction, physical scale, grip, and muzzle markers. Validate the actual assets before assigning numerical corrections.

## Gameplay defects

### 7. P1 — Switching weapons retains the previous weapon's ballistic stats

**Confirmed code defect.** Mission initialization applies starting-weapon damage, damage upgrades, noise radius, and scope upgrades. `SelectWeapon()` later updates the index/model/ammo but does not rebuild those settings.

**Effect:** start with Barrett and switch to MK12: the MK12 keeps Barrett-derived damage and noise settings. The reverse gives the Barrett MK12-derived settings. Scope upgrades also remain tied to the initial weapon.

**Locations:** `Assets/Scripts/UrbanCombatMission.cs:191`, `Assets/Scripts/UrbanCombatMission.cs:4375`.

**Fix:** use one equip function that applies the complete weapon configuration both at startup and during switching.

### 8. P1 — Reselecting a weapon gives an immediate full magazine

**Confirmed code defect.** `SelectWeapon()` always assigns `ammo = weapon.MaxAmmo`, even when selecting the already-equipped gun. No reload duration is started by that assignment. Keyboard selection is handled repeatedly in Update, causing redundant equips/model rebuilds too.

**Locations:** `Assets/Scripts/UrbanCombatMission.cs:3207`, `Assets/Scripts/UrbanCombatMission.cs:3285`, `Assets/Scripts/UrbanCombatMission.cs:4386`.

**Reproduce:** fire several rounds, then press the current weapon's number or tap its slot. The magazine refills without completing a reload.

**Fix:** ignore same-weapon selections, retain per-weapon ammunition, and centralize input handling.

### 9. P1 — Keyboard crouch is overwritten immediately

**Confirmed code defect.** Pressing C toggles `UrbanPlayer.IsCrouching`; the next synchronization block replaces it with `Controls.IsCrouching`. The keyboard branch never updates that control value, and a controls component is created during initialization.

**Location:** `Assets/Scripts/UrbanPlayer.cs:95`.

**Reproduce:** use C without first changing crouch through the on-screen button.

**Fix:** make one component own stance state; have keyboard and touch send commands to that owner.

### 10. P1 — Knife takedowns can kill through walls or behind the player

**Confirmed code defect.** Nearby target selection checks distance (3.2 m) and height difference only. `ExecuteKnifeKill()` applies 9999 damage directly without a line-of-sight or facing check. The separate spherecast slash path does not protect the automatic takedown path.

**Locations:** `Assets/Scripts/UrbanCombatMission.cs:3224`, `Assets/Scripts/UrbanCombatMission.cs:2515`.

**Fix:** require unobstructed reach and the intended facing/stealth conditions, and revalidate them at execution.

### 11. P1 — Solid cover does not reliably block bullets

**Confirmed code defect.** `SniperHitQuery.TryCast()` searches for targets up to 0.9 m beyond the first solid obstruction. Arcade ballistics additionally starts an enemy query 0.15 m past a non-enemy impact and searches another 2.2 m. Neither constitutes a material/thickness penetration model.

**Effect:** a target close behind a solid wall can take damage despite the central shot being blocked. Penetrable objects are also skipped in the target search, so the arcade damage-reduction branch is not reliably reached for targets behind those objects.

**Locations:** `Assets/Scripts/SniperHitQuery.cs:93`, `Assets/Scripts/BallisticsSystem.cs:75`.

**Fix:** enforce nearest solid obstruction; implement intentional penetration separately with explicit material, thickness, and damage rules.

### 12. P2 — Hidden touch controls remain interactive while scoped

**Confirmed code mismatch.** Drawing hides jump, sprint, knife, and weapon slots while scoped, but touch/mouse hit testing still accepts their rectangles without the same visibility condition.

**Locations:** `Assets/Scripts/MobileCombatInput.cs:354`, `Assets/Scripts/MobileCombatInput.cs:440`, `Assets/Scripts/MobileCombatInput.cs:613`.

**Effect:** touching apparently empty scope space can jump, switch weapons, or trigger a knife action that drops the scope.

**Fix:** share an availability/visibility predicate between rendering and input.

### 13. P2 — Shot queries can do substantial repeated mesh work

**Performance risk, not a measured frame-rate result.** Aim assist can invoke up to 17 full hit queries. Each query refreshes target colliders, synchronizes physics, allocates/sorts hit arrays, and can bake skinned meshes and test their triangles. The reviewed code does not cache a posed mesh across those rays.

**Locations:** `Assets/Scripts/SniperHitQuery.cs:13`, `Assets/Scripts/SniperHitQuery.cs:52`, `Assets/Scripts/EnemyHitboxes.cs:162`.

**Fix:** profile on the target Android device; broad-phase cull targets and reuse pose/query data per shot before expensive mesh intersection.

## Lobby and deployment

### 14. P1 — GPS Range inherits stale campaign/duel launch state

**Confirmed code defect.** The GPS Range branch calls `StartPreferredLocationMission()` without resetting `campaignNodeToStart`, `stageIndexToStart`, or `isPvPDuel`. Other mode branches set these fields explicitly. `StartMission()` uses those fields to choose the mission.

**Locations:** `Assets/Scripts/GeoSniperGame.cs:107`, `Assets/Scripts/GeoSniperGame.cs:169`.

**Reproduce:** play or prepare a campaign/duel, return to the lobby, then select GPS Range. The previous contract flags can determine the launch. On a fresh session, this branch also does not explicitly initialize a dedicated range mode.

**Fix:** construct explicit launch settings for every mode, including range setup.

### 15. P1 — Deployment failures silently change the requested experience

**Confirmed code behavior.** Startup errors can clear campaign/duel intent and retry free combat, then start offline practice. `LoadingFailed()` immediately or subsequently calls `AutoFixAndDeploy()`, which starts an offline sector.

**Locations:** `Assets/Scripts/GeoSniperGame.cs:714`, `Assets/Scripts/GeoSniperGame.cs:756`.

**Effect:** choosing a real location or campaign contract can lead to a different mode/generated map instead of an actionable error. This also contradicts README statements that failures retain the selected location and never automatically substitute offline practice.

**Fix:** preserve launch intent and show Retry Same Location plus an explicit Offline Practice choice.

### 16. P2 — Lobby rifle material allocations are not cleaned up

**Confirmed resource-lifecycle gap.** Each rifle replacement allocates three materials in `ApplyLobbyRifleMaterials()`. Those materials are not registered in `lobbyStageMaterials` or explicitly destroyed when the rifle is replaced. Character materials are likewise created separately from the tracked stage materials.

**Locations:** `Assets/Scripts/GeoSniperGame.cs:482`, `Assets/Scripts/GeoSniperGame.cs:533`, `Assets/Scripts/GeoSniperGame.Lobby.cs:58`.

**Effect:** repeated weapon selection can accumulate runtime material allocations; the practical memory impact needs profiling.

**Fix:** cache shared lobby weapon materials or track and destroy owned instances on replacement/teardown.

## Verification gaps and recommended order

The existing animation check expects imported enemy prefabs to already contain Animation components/clips and an exact `leftfoot` bone. The current importer disables embedded animation on swat/ch35 and runtime code adds external clips. This check needs updating to exercise the actual runtime assembly path; compilation alone does not prove those checks pass. See `Assets/Editor/ArmyAnimationChecks.cs:14` and `Assets/Scripts/Editor/MixamoEnemyPostprocessor.cs:39`.

Fix gameplay correctness first: weapon stat switching, magazine refill exploit, crouch ownership, knife visibility, solid-cover handling, and deployment state. Then fix disarm ownership and establish a single ordered character/weapon pose pipeline. Calibrate both enemy rigs and all three rifles using markers, then restore the first-person support arm.

For acceptance, capture both enemy rigs at idle, walking, sprinting, crouching, aiming uphill/downhill, taking damage, disarming, and dying. Measure grip/support-hand separation after the final pose update. Check all lobby rifles through a full operator rotation. Exercise mission transitions, failed downloads, owned/locked loadouts, scoped touch input, and Android frame time during near-miss shots.
