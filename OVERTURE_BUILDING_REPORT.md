# Overture building coverage diagnosis

26 September 2026

The project config selects Overture public PMTiles. The direct loader requests transportation, buildings, places, and base themes, converts building polygons into map features, and passes them to the playable sector. So the game is capable of using Overture building data.

The sparse-map defect was in source selection. `SectorMap.Load` returned the general saved-sector cache before calling the Overture loader. That cache mixes OSM fallback sectors with Overture-derived sectors, and the old status text labeled any cached sector as Overture. Inspection of local saved files found Overture sectors containing hundreds of buildings, while some general saved sectors contain zero. Those counts cannot establish which exact location was visible to the player, but they show why a generic-cache hit could conceal richer Overture data.

The loader now tries configured Overture tiles and their dedicated cache first. It uses the general cache only if Overture is unavailable, and attempts a live source before accepting a general cache with fewer than five buildings. Dense Overture sectors skip an unnecessary OSM supplement request. The source status includes the building count, and the generic cache is no longer mislabeled as Overture. Parsed feature selection is capped to the decoder's 2,400-feature limit so dense cached sectors remain readable.

The tactical map previously displayed "Live OpenStreetMap 3D GIS" regardless of its provider. It now shows the provider used by loaded sectors and the count of buildings actually generated, so the source and density can be checked in-game.

Validation: runtime and editor code compile; the isolated `OverturePriorityChecks` test saved a one-building generic sector and a 32-building Overture sector for the same test location. `SectorMap.Load` chose Overture and reported 32 buildings. Test output: `.utmp/review-unity/Logs/OverturePriorityChecks.txt`. Run with `Tools/run_overture_priority_checks.ps1` after isolated review-project setup.

I did not fetch a new live sector at the player's exact location or run an Android build. If the loading status still reports few buildings from Overture itself, inspect that location's Overture tile coverage and the game's polygon conversion separately.
