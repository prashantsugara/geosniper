# Enemy reactions, extraction navigation and scope

## Enemy behaviour

Observation guards leave the scripted patrol when they see a nearby exposed player, hear a threat, receive a squad alert or begin an extraction assault. Firing-range targets explicitly retain passive behaviour. Close detection checks visibility each frame and accepts a clear chest or head sightline; nearby players behind solid walls remain concealed. At a few metres, crouching no longer makes recognition unusually slow. Fresh combat contact replaces the long initial firing cooldown with a bounded aiming delay.

Combat locomotion has a minimum responsive speed for ordinary sentries, while counter-sniper posts, fugitives and leg wounds preserve their special movement behaviour. Healthy exposed guards flinch, change angle and return fire; serious wounds, loss of sight, disarming and existing cover behaviour prioritise shelter. Combat destination choices consider clear firing lanes, shelter, spacing, a persistent side preference and recent position instead of alternating left/right by index. All candidates still require supported dry terrain, reachable paths and separation from squadmates. This is contextual variation, not a claim of unrestricted or human-level AI.

## Extraction

Active extraction destinations are derived from mission state independently of the manual waypoint. The opening mission, document/relay objectives, released witnesses and other extraction phases retain green map and radar guidance after selecting an open building, dropping another pin or clearing a manual pin. The map's EXTRACTION button recentres and restores the manual route to the exit. World guidance can show the extraction and a manual building route together.

## Scope

The optic uses a shaded machined rim, bevel highlights, a transparent aim centre and a subtle edge vignette. The lens texture stays at 512 pixels on the performance preset and uses 1024 otherwise. Reticle graduations use camera projection; magnification uses the tangent ratio of camera FOVs. The camera, zoom rail and scope readout share the same limits and calculation. Slider travel adjusts magnification rather than raw degrees, FOV transitions use exponential settling, and higher magnification reduces touch/mouse/gyro aim sensitivity. User sensitivity preferences still apply.

Visual and behaviour quality require Android playtesting. These changes do not establish an AAA quality level or a measured retention improvement.

## Validation

Runtime and editor compilation passed. Scene checks passed close detection, healthy/wounded reactions, passive firing-range behaviour, wall occlusion, remembered-position limits, actual enemy bullet collision, squad spacing and disabled-burst cancellation. Optical zoom endpoints, monotonicity, readout equivalence, aiming scale and 30/60/120 fps settling checks passed. The campaign checks passed all 50 setups, guarded indoor objectives, window traversal, enemy rifle turns and extraction route persistence/restoration. Full report: output/enemy-extraction-scope/FullMissionChecks.txt. These are setup and regression checks, not a full 50-level playthrough or an Android performance test.
