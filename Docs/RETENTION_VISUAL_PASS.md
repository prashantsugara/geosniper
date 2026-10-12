# First-impression environment pass — 8 October 2026

The landscape game now has a designed skyline around the fictional offline starter sector. Sixteen background buildings vary in height and facade finish. They use simple building meshes, have no playable interiors and are excluded from rooftop objective/enemy spawn positions. The centre of the teaching arena stays open. The skyline is not added to live GPS maps.

The atmospheric sky adds layered drifting clouds, warm sun-facing cloud highlights, storm coverage and subtle night stars. It reuses the existing dome and adds no texture downloads or cloud objects. Performance graphics uses the simpler sky gradient. Existing day/night and rain selection drives the sky appearance.

Building window frames now project further from the masonry, creating deeper reveals. Rooftop parapets remain low enough to preserve opening target sightlines while providing physical edge detail. The previous reference pass supplies facade courses, pilasters, rooftop HVAC equipment, warm daylight/cool ambient fill, night window lighting and procedural weapon handling.

Validation: source compilation, shader import/render checks, eight weapon handling checks, visible objectives in the first three missions, all 50 campaign setups, and nearby security plus pickup/shutdown checks for field objectives. Captures use actual Unity rendering in landscape. Cached real-map rendering uses flat elevation; these tests do not replace Android frame-time/memory profiling.

Retention improvement is a goal, not a verified result. Compare first-mission starts/completions, loading failures/duration and returning players by released app version. Higher fidelity building assets, fuller interiors and richer sound remain further production work.

Global chromatic aberration has been removed to keep architecture readable; slight optical fringing is limited to the scope. Current captures and reports are in `output/retention-visuals`. The mesh/triangle totals include generated children and inactive LOD geometry, not just visible draw calls.

Night polish: a stable moon disc with procedural surface shading and a soft halo is visible only at night and fades behind clouds/storms. Facade window bays now select stable warm, neutral or cool-white room lighting, with modest brightness variation and some unoccupied dark rooms. This uses the existing shaders/materials without adding room lights or draw calls. Source compilation and Unity day/night render checks passed; the night capture shows both the moon and varied window tones.

Curtain variation uses muted beige, cream, grey-blue and sage, differing opening widths, and gentle folds. It changes visible facade glass shading and transmitted room light without adding window geometry.

Civilian motion: stationary authored rigs receive non-accumulating breathing and head movement. Patrol destinations require complete navigation paths, sparse street points have bounded dry-ground fallback searches, and blocked patrols reselect routes instead of immediately waiting for several seconds.

Enemy death: physics disables authored animation, resolves deforming skeleton bones instead of mesh names, connects limbs in a second pass, excludes internal body collision, and uses restrained impulses with joint projection and damping. Bone collision radii compensate for imported model scale. Verification samples all three civilian idle rigs, checks connected/disconnected navigation, and simulates both enemy rig collapses. It does not cover every terrain, staircase or rooftop death in gameplay.

One randomized opening setup failed its second-mission sightline check before the final passing run. Diagnostic ray logging remains enabled on failures; repeatability across random layouts still needs investigation before treating the opening sightline guarantee as complete.
