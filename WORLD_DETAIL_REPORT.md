# Game-world detail implementation

26 September 2026

The playable world now spends its close-up building, balcony, rooftop, tree, streetlight, road-prop, and sign budgets around the player's entry position instead of the order in which a map provider returned features. Named landmarks get a small priority boost. This makes the first visible streets more complete when a sector contains more features than the detail budget permits.

Overture `building_part` relationships now survive tile decoding and the sector cache. Parts remain separate meshes and colliders but are attached to their parent building when its Overture ID is available. They do not consume the main-building facade budget or appear as separate mission buildings. Buildings with close-up facade geometry use a simpler textured shell at distance while retaining the original footprint collision. Mobile sector preloading begins at 260 metres from the edge to reduce the chance of seeing empty space while moving toward an adjacent sector.

Street props now favor parked cars, benches, and bins on residential roads and near named public places. The existing separate street-lighting pass remains responsible for lamps. The tactical map improvements and Overture-first source selection are described in `MAP_DETAIL_REPORT.md` and `OVERTURE_BUILDING_REPORT.md`.

Validation: `Tools/check_code.ps1` compiles runtime and editor code. Isolated Unity `MapWorldChecks` verifies near-player detail selection, building-part parenting, LOD collision, mesh footprint, spawning, and signs; `OvertureDetailChecks` verifies building-part metadata filtering and cache persistence; `OverturePriorityChecks` verifies dense Overture cache selection. Render preview: `.utmp/review-unity/Logs/WorldDetail.png`.

The rendered test sector confirms the new part and facade geometry, but it is a small synthetic location. A live Overture sector and Android target-device frame time still need visual and performance checks; the change does not manufacture buildings that are absent from source tiles.

## Playable-world color pass

The ground now uses a lighter grass/soil texture with neighborhood-scale patches and softer normal detail, replacing the uniformly dark olive field. Tree canopies use two natural green tones without adding individual tree objects. Unmapped building facades receive stable, coordinate-based warm, neutral, or cool colors; Overture facade colors and materials still override them. Paved roads use a textured asphalt material, while dirt, gravel, concrete, and footpaths retain their mapped surface colors. Sidewalks and roofs have a less glossy finish so terrain, roads, and architecture separate under daylight.

The final synthetic-sector preview is `.utmp/review-unity/Logs/WorldDetail.png`. It validates color balance and geometry in one daylight scene, not every location or time of day. Device readability and any retention effect require a playtest with live sectors.
