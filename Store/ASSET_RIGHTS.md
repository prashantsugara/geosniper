# Asset rights — publisher checklist

Status: UNVERIFIED. Do not publish until evidence is retained for every distributed asset and required attribution is included. File names alone do not establish infringement or permission.

Inventory starting points: `Assets/Resources/Models`, audio/texture resource folders and `Logs/GameplayGeometryAudit.md` (42 imported models). Review branded vehicle names and third-party-looking character names especially carefully. No comprehensive license register was found alongside those models.

Maintain one row per asset or licensed pack:

| Asset/path | Creator/source URL | License/version | Commercial redistribution allowed | Required attribution | Evidence location | Reviewer/date |
| --- | --- | --- | --- | --- | --- | --- |
| Pending inventory | Unknown | Unvetell me steprified | Not established | Unknown | Missing | Pending |

Include models, embedded textures, animations, weapons, vehicles, music, effects, fonts, icons, datasets and SDK notices. Purchases may permit use inside a game but forbid redistribution of raw source files. Check trademark/publicity restrictions separately where relevant.

For maps, retain OSM contributor attribution and comply with ODbL requirements; separately review Overture source attribution/license obligations and the actual tile/search provider terms. Attribution is not a substitute for service-capacity permission.

Generated branding provenance is in `BRANDING.md`; generation does not constitute trademark clearance. Set `assetRightsVerified` only after review is complete.

## Verified addition — 30 September 2026

| Asset/path | Creator/source URL | License/version | Commercial redistribution allowed | Required attribution | Evidence location | Reviewer/date |
| --- | --- | --- | --- | --- | --- | --- |
| Resources/Models/StreetProps/BarrierOrange, BarrierYellow and textures | YadroGames — https://sketchfab.com/3d-models/concrete-barrier-afaab6285c484c36aab250e06727d471 | CC BY 4.0 | Yes, with attribution | In-game Settings > Privacy & Support > Asset Credits: creator, source, license and modification notice | Store/SketchfabBarrierLicense.json | Codex / 2026-09-30 |

This row verifies the new barrier asset only; the earlier asset inventory remains unverified.

## Verified addition — 6 October 2026

Street Props by syedraza: https://sketchfab.com/3d-models/street-props-741e544242a9481fa54c1f7bca6e92b2 — CC BY 4.0 (https://creativecommons.org/licenses/by/4.0/). Commercial use allowed with attribution. Five grounded, uniformly scaled FBX props and original 1024px atlases are in Resources/Models/NeighborhoodProps. Evidence: Store/SketchfabNeighborhoodLicense.json. Attribution and modification notice appear in game Asset Credits.

Free Street Props by ribrado — https://www.cgtrader.com/free-3d-models/exterior/street-exterior/free-street-props — Royalty Free License (no AI). Used as incorporated game assets; do not redistribute standalone source files. Evidence: Store/CGTraderStreetPropLicense.json. Original PBR textures/materials and LOD prefabs retained in Resources/Models/CGStreetProps. In-game Asset Credits includes the creator.

Vegetation from CGTrader: Realistic Trees-Pack of 2 by styropyro, Plant Bush Asset Pack by Coynese, and Free Basic Grass and Shrubs by connorwilding. All three listing pages show Royalty Free License (no AI). Source links, download checksums and modification details: Store/CGTraderVegetationLicenses.json. Eight grounded, normalized game meshes and reduced atlases live in Resources/Models/Vegetation. Trees are reduced to approximately 16,000 triangles, shrubs to 2,000; ground plants use 52/117 triangles. Tree counts scale with graphics quality, with spatial batching and distance culling. These assets are incorporated in the game, with creator credits in Asset Credits; do not redistribute standalone source downloads.

Buildings: House interior by Katydid — https://sketchfab.com/3d-models/house-interior-e910b8395ddc4b9886d87caffdbacc69 — CC BY 4.0 (https://creativecommons.org/licenses/by/4.0/). Grounded, scaled and exported as HouseInteriorSF.fbx. Evidence: Store/SketchfabHouseLicense.json. Creator, source, license and modification notice are available in in-game Asset Credits.

House floor plan by maxm77353 — https://www.cgtrader.com/free-3d-models/interior/living-room/free-low-poly-house-floor-plan-maya-ma-and-mb-and-fbx-format — Royalty Free License (no AI). Grounded, scaled and exported as HouseLayoutCG.fbx. Incorporated game asset; do not redistribute standalone source files. Evidence and source checksum: Store/CGTraderHouseLicense.json.

## Civilian additions — 7 October 2026

Active game models:
- Carla Rigged 001 - Rigged 3D Business Women by Renderpeople: https://sketchfab.com/3d-models/carla-rigged-001-rigged-3d-business-women-acf520f450d14dd799f98a6fede3edf5
- Eric Rigged 001 - Rigged 3D Business Man by Renderpeople: https://sketchfab.com/3d-models/eric-rigged-001-rigged-3d-business-man-a46bc9f67aaa415bb4f3241eef900e7f
- Doctor by Nasser: https://sketchfab.com/3d-models/doctor-31c09ab8b098489b8c46712650e45bac

The original Sketchfab listings show CC Attribution 4.0, permitting commercial use with attribution. Evidence: Store/SketchfabCivilianLicenses.json. Downloaded through the configured Blender Sketchfab API. Converted to FBX, texture resolution reduced to 1024px, identical texture atlases shared, rig aliases and rest poses adapted for the game's procedural gait. Reporter camera, badges and shop apron are original role accessories. Respect Renderpeople's NoAI tags; do not use these assets in generative AI workflows. In-game Settings > Privacy & Support > Asset Credits > Civilian Credits gives authors, titles, source links, license link and modification notice.

A fourth downloaded candidate, Casual Man Character by Bogdan Strielecki (https://sketchfab.com/3d-models/casual-man-character-05d9dd5bdddd4157bd46dc179781ee6e), is retained in Assets/ThirdParty/CivilianAlternates under CC BY 4.0. Its stylized appearance did not match the active lineup, so it is not used or included through Resources.

## Stronghold additions — 10 October 2026

Sketchfab CC Attribution 4.0 source records are retained in Store/SketchfabStrongholdLicenses.json:

- Low Poly Afghanistan House Obj by omar.takwa0865: https://sketchfab.com/3d-models/low-poly-afghanistan-house-obj-9f97af2ec2fd4379a65c0683b65d08c3
- Desert house 01 FREE by filthycent: https://sketchfab.com/3d-models/desert-house-01-free-2f7b88bbcf1b454cad2c3e9dbc9dd18e
- Sandbag [Low Poly Realist] by Islide: https://sketchfab.com/3d-models/sandbag-low-poly-realist-7d52600a15c747749d845d9f906045cf
- Arab Building by Usmanix3D (review candidate; closed-panel wing removed from current runtime export): https://sketchfab.com/3d-models/arab-building-a00d262125c245058f8ddd0e887bb833

The 6,018-triangle Afghan shell retains its exterior stairs and is joined to an original 10,088-triangle open-gallery extension, authored by the project. The old Arab arcade wing is no longer part of the export. The combined mesh was grounded, scaled to 10 metres, fitted with physical planar UVs and exported to FBX; Afghan source window inserts were removed. The desert-house creator's plaster, stone and timber maps are used on the shell and extension. Sandbags retain their 712-triangle source geometry. Texture copies are capped at 1024px; ORM channels are converted to Unity Standard metallic/smoothness and occlusion layouts. Source meshes and textures are adapted through ordinary modelling and channel conversion. In-game Settings > Privacy & Support > Asset Credits > Stronghold Credits provides authors, source links, license link and modification notices.

## Campaign location assets � 10 October 2026

- Military Tent by Reinaw13: https://sketchfab.com/3d-models/military-tent-d95296298b2a4caca8a5c3ce4450bb15
- Old Apartment Building by jimbogies: https://sketchfab.com/3d-models/old-apartment-building-a2a27bec2e364496b34ae399ee93cb67
- Pack - Realistic - 3 Warehouse by Islide: https://sketchfab.com/3d-models/pack-realistic-3-warehouse-320c272513a94c0482dbb3c4e79d61f5
- Abandoned Shopping Mall by Miguel Ad�o: https://sketchfab.com/3d-models/abandoned-shopping-mall-5ede6f812941428dae4b31a3487623fd

All four sources were downloaded through the configured Sketchfab connection under CC BY 4.0. Evidence and modifications: `Store/SketchfabLevelLocationLicenses.json`. Runtime exports are in `Assets/Resources/Models/LevelSites`. Meshes were scaled and grounded; warehouse centre geometry extracted and door/window inserts omitted; apartment ground and glass removed; shelves and fridge extracted from the mall interior vignette. The mall architecture and accessible site floors/stairs are authored by the project. Maps capped at 1024px; ORM metallic/roughness converted to Unity channels. No source asset is used for generative AI.

Existing CGTrader Free Street Props by ribrado (`Store/CGTraderStreetPropLicense.json`, Royalty Free/no AI) supply bins and barricades. Source PBR and LOD references are preserved; these are decorative, without added collision across mission routes. Source: https://www.cgtrader.com/free-3d-models/exterior/street-exterior/free-street-props . The reviewed C4D-only CGTrader mall was not imported. In-game Asset Credits > Stronghold Credits > Level Location Credits provides all four authors, source links, license link and change notices.

Gameplay refinements: the apartment central closed door/frame (40 triangles) and warehouse front panels (82 triangles) were removed to preserve walk-in access. Source fa�ades/roofs and UV maps remain. Source root axis conversions are preserved under Unity placement roots. Controller checks cover connected return stairs rather than teleporting between apartment flights.
