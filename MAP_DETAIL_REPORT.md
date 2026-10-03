# Map detail update

26 September 2026

The playable sector now generates thin road edge lines, dashed center lines, and zebra crossings at angled street junctions. The markings follow sampled terrain and share one mesh per sector. They add no collision objects, and mobile builds use smaller detail budgets. Crossings appear only where loaded road geometry identifies a junction; incomplete map data can still leave an intersection without one.

The 2D tactical map now places street names at the midpoint of each road path rather than an arithmetic center that may sit off a curved street. Road labels are thinned by area and drawn after landmark names, while named sites receive their available category and color in the map directory. Existing north and distance markers remain.

`Tools/check_code.ps1` compiles the updated runtime and editor code. `Tools/run_map_detail_checks.ps1` runs an isolated Unity test for batched geometry, terrain following, angled junctions, and street label position. Its report is `.utmp/review-unity/Logs/MapDetailChecks.txt`, and its preview is `.utmp/review-unity/Logs/MapDetailPreview.png`.

Map detail is limited by the provider's source data. These changes do not create missing street names, building metadata, or points of interest for a location that has none. Target-device performance and a full live-map playthrough remain to be checked.
