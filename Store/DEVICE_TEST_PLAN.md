# Android release acceptance tests

Record device model, Android version, RAM, GPU, app version/code, artifact hash and pass/fail evidence. All boxes below remain unverified on the final release artifact.

- Clean install and upgrade with preserved progress; cold/warm startup; no fatal logcat exceptions, ANRs or blank loading screen.
- Minimum supported Android 8/API 26 and modern Android 16/API 36; low/mid/high-tier devices, notches and different landscape aspect ratios.
- Android 16 KB page-size emulator/device: `adb shell getconf PAGE_SIZE` must return 16384. Validate native ELF load alignment and APK zip alignment for ALL `.so` files, including Unity, IL2CPP and MapLibre. Check the final AAB via bundletool and generated APK via `zipalign -c -P 16 -v 4 app.apk`; inspect [official instructions](https://developer.android.com/guide/practices/page-sizes). No final native-library certification has been performed.
- Offline campaign, all 14 nodes and AI duel; no GPS dependency. Check failure/retry, objective counts, reward-once, unlocks, intro stationary enemies and progressively harder later nodes.
- Background/resume, screen lock, interruptions and Android Back: no damage/timer advance while tactical map is open, no audio overlap, controls recover. Confirm enemy bullets/extraction transitions do not bypass suspension.
- Touch aim/scope/fire, sensitivity extremes, volume persistence, 30/60 FPS persistence, recoil, hit detection, roof/street spawn geometry and weapon orientation.
- Location: not requested at startup; explanation before explicit GPS action. Test denied, approximate-only, precise, revoked, stale/unavailable GPS. Place search and offline modes remain usable.
- Maps: offline, slow network, server throttling, no results, invalid responses, interrupted download; helpful retry, no infinite spinner. Clear-location action deletes only local location/map data and preserves progression.
- AdMob/UMP: EEA/UK and other applicable regions using SDK debug geography on test devices, consent acceptance/rejection, privacy-options reopening, consent server failure and restart persistence. Consent choice must gate requests according to SDK `CanRequestAds()`.
- Rewarded ads: test video actually displays on Android, cancel/no-fill/offline grants nothing, completed SDK reward grants exactly once, repeated taps do not overlap ads. Test interstitials only at transitions and check cooldown.
- Run release ad IDs with registered test devices; do not click live ads during development. Remove debug-geography/device overrides from the published configuration as appropriate.
- Long play session and repeated level/world changes: memory, thermal throttling, battery, shader stutter, CPU/GPU frame time and crash behavior. Establish measured budgets before selecting defaults.
- Launcher icon on square/circle/squircle masks; store icon exact 512×512. Genuine screenshots match actual gameplay.
- Privacy-policy/support links and deletion controls work. Scan final merged manifest for unexpected permissions, backup behavior, exported activities and debug flags.
- Signed AAB installs via Play internal testing; production runtime excludes MCP/debug bridges. Complete Play pre-launch report and any account-required closed test.

Pass/fail results and unresolved defects must be recorded before setting `deviceTestingCompleted` to true. Editor checks alone are insufficient.

## Android touch and gyro aiming pass

- On a fresh install, verify scoped gyro defaults off and touch aim works with no sensor permission prompt.
- Settings > Touch & Gyro Aiming: persist look/scope/gyro speed and vertical inversion across restart. On a phone without a gyro, verify the gyro toggle is unavailable and touch still works.
- With scoped gyro enabled, verify horizontal and vertical direction in supported landscape orientations; confirm there is no snap when entering scope. Check both vertical inversion choices.
- Compare gentle tracking at 30 and 60 FPS, with and without hold-breath slow motion. Physical direction, drift and comfort require device verification.
- Open the tactical map, trigger bullet camera, background/resume, and leave the mission while scoped. Phone movement must not move aim while blocked or cause a resume jump.
- Move, aim and fire with multiple fingers; release all touches and interrupt with Android notifications. No movement or fire may remain held. Sprint toggle should survive normal finger release but reset on focus loss.
- Inspect the aiming panel on a notched phone and a compact landscape screen for legibility and reachable controls.

## Android graphics presets

- Settings > Graphics & Performance: compare Performance, Balanced (fresh-install default), and High on low/mid-range Android phones. Record frame times, peak memory, heat and battery behavior over 20 minutes; preset names are not performance guarantees.
- Set 60 FPS, change graphics preset and restart; verify both choices persist independently. Check 30 FPS separately.
- Start missions, change weather, restart and stream adjacent sectors; confirm selected shadows persist. Toggle presets while rain exists and verify density updates without changing weather/audio state.
- Inspect touch targets on compact/notched landscape phones. Check target visibility, cover and aiming across presets.

## Android confirmed-hit feedback

- Enable Hit Haptics in Settings > Touch & Gyro Aiming. Verify fresh installs default off; toggle survives restart.
- Body hits show HIT; headshots use a larger gold marker and HEADSHOT label. Verify scoped and hip-fire hits, fast follow-up shots, and marker expiry in slow motion.
- Misses, hitting walls/civilians/dead actors, and taking enemy fire must not produce confirmed-hit feedback. Bullet-camera and result screens keep their own presentation.
- Verify haptics on hardware with Android touch feedback on/off. Unsupported devices must continue combat without errors; notifications/backgrounding must not produce delayed pulses.
- Check compact landscape screen readability and ensure the new marker does not obstruct target tracking.

## Rain visibility repair — 27 September 2026

- Rain now uses a dedicated shader loaded from Resources, with a single opacity path instead of compounded material/particle attenuation.
- Smaller camera-following emission volume, larger drops, explicit world-space falling velocity, and startup warm-up make precipitation visible immediately.
- Heavy weather includes gust direction and occasional fading lightning. Rain ambience is quieter.
- Run Tools/run_weather_visual_checks.ps1; wait for the isolated Unity process to finish and inspect .utmp/weather-review/Logs/result.txt and PNGs. This uses the production rain implementation and shader, with silent audio and controllable quality stubs; it is not a full mission or Android-device test.
- Verified locally: rendered rain at .35 and 1.0 density, particle clearing, shader support, storm light activation/deactivation. Runtime/editor compilation passed.
- Android follow-up: inspect rain/storm on ground and rooftops, scoped views, day/night, both graphics presets, weather transitions and mission exit. Check frame time and confirm rain audio/visuals stop together.

## Enemy awareness and cover pass

- Detection now has a visible suspicion threshold, distance/crouch scaling and gradual decay. Vision raycasts run roughly every 120–150 ms; corpse scans run every 750 ms. Burst shots still recheck obstruction immediately.
- Investigations approach the reported location, then scan for 4.5 seconds before returning to patrol. Continued visual contact keeps investigation active until detection completes.
- Radio messages transmit the supplied reported location; cover facing and aim use last-known information when sight is lost.
- Cover requires a solid non-actor occluder, clearance along the direct travel route, continuous supporting ground, and separation from other guards. Arrival is revalidated. No suitable cover falls back to repositioning/search rather than claiming protection in the open.
- Validation: Tools/test_enemy_awareness.ps1; Tools/run_enemy_cover_checks.ps1 (isolated Unity physics). These do not replace full mission playtesting.
- Device scenarios: crouch vs stand at near/far distances; hide after partial detection; relocate silently behind a wall; shoot then relocate; verify radio recipients investigate the old report; disappear while enemies are in cover; block a guard's route; rooftop gaps; hide during a burst. Check bodies, runners, VIP attackers and counter-sniper contracts for regressions.


## Mapped water and awareness feedback — 2026-09-27

Automated: Tools/check_code.ps1 compiles runtime/editor sources. Tools/run_water_visual_checks.ps1 launches an isolated Unity project and checks sector clipping of large water polygons, concavity, island area and triangle containment, ring JSON roundtrip, malformed bounds rejection, and the production water mesh/shader render. Screenshot and result: .utmp/water-review/Logs. This is a synthetic geometry fixture, not a live map screenshot.

Device checks pending (APK build deferred):
- Load fresh lake, river and coast sectors from Overture; verify shoreline placement against the map and island cutouts on both map and world.
- Cross sector boundaries with elevation enabled; inspect water seams and shoreline terrain overlap, especially narrow rivers and slopes.
- Reload the sector from disk and reuse nearby coordinates; confirm water and land stay aligned.
- Check Performance/Balanced/High at 30/60 fps during rain; record frame time and thermal behavior on Android hardware.
- Observe suspicion, investigation, searching, spotted and cover labels. Walk behind walls and verify labels and barks disappear within the 150 ms visibility cache window. Check crouched targets and scope transitions.

Current limits: water levels are estimated from boundary terrain elevations, not measured hydrological heights. Water is an inexpensive opaque animated surface with an approximate sky tint; no planar reflections, depth simulation or swimming. Missing source geometry cannot be reconstructed accurately. OSM fallback still lacks multipolygon relation assembly and linear river width generation. Full scene/device visual acceptance remains pending.


## Water placement regression — 2026-09-27

Screenshot regression: Dummat Lake extraction showed enemy and helicopter on the water surface with the player underneath. Startup now requires a supported dry player spawn; mission enemy, patrol, and extraction placement reject mapped water, including island holes. The player stays at their last supported dry position when walking into a mapped lake. A fully water-covered sector reports deployment failure instead of launching an unreachable mission.

Validation: Tools/check_code.ps1 passed. The isolated Unity water fixture checks that lake points are wet, while islands and shore points remain dry; its result is .utmp/water-review/Logs/result.txt. Android replay of the screenshot's exact map tile and extraction flow remains pending because APK building is deferred.


## Ground and scenery realism — 2026-09-27

Implemented: water levels with matching map IDs share one height in a session; terrain forms shallow submerged edges and raised dry banks, including islands. Shore-adjacent terrain gets finer mesh sampling near the player. Soil, grass and pavement blend on the terrain; asphalt, paved roads and dirt tracks have distinct textures and roughness. OSM fallback now turns mapped river/stream/canal centerlines into width-limited water ribbons; Overture water polygons continue to supply lakes and mapped coastal water. Shrubs and stones are batched with grass and culled by camera distance; tree and grass density scale by Android graphics preset. Placement respects water, parks, roads and buildings.

Verified: Tools/check_code.ps1 compilation and Tools/run_water_visual_checks.ps1 isolated Unity fixture. The fixture checks lake bed and island bank height, equal connected-water heights, river ribbon area, distinct terrain shader surfaces, plant rejection on water and roads, and distance-cullable shrub/stone batches. Review .utmp/water-review/Logs/shoreline-surface.png and terrain-material-blend.png.

Still required on Android: inspect multiple live lake, river, coastline and island locations at sector seams; measure frame time, RAM and thermal behavior on Performance/Balanced/High. Coastline lines from OSM alone are insufficient to infer an ocean polygon. Terrain height is still estimated where mapped water elevation is absent; ID-less or unrelated source polygons cannot be guaranteed to align vertically.
