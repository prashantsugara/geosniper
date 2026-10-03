# Geo Sniper Android prototype

## Lobby

The lobby uses a charcoal/amber command-room layout: navigation on the left,
an inspectable 3D operator in the centre, and mission selection/deployment on the
right. Drag only inside the operator showcase to rotate it. MODIFY opens the
armory; DIFFICULTY opens settings. Offline Ops is the existing generated-sector
practice mode; Sniper Duel is against an AI rival, not online matchmaking.
The home layout scales within the device safe area. In Unity's Game view, use
Fit/1x scale to see the complete frame instead of a zoomed crop.

Lobby implementation: `Assets/Scripts/GeoSniperGame.Lobby.cs`. For the
shared campaign, armory, world-map, rewards, settings, mode-selector and loading
theme, see `Assets/Scripts/CommandGUI.cs` and `Assets/Scripts/GeoSniperGame.Pages.cs`. These pages
share the home palette and left navigation; the combat HUD is unchanged.
Add `-AllPages` to the capture command below to export every menu screen, and
combine it with `-Compact` to check the narrower layout.

To reproduce the isolated visual check, run `Tools/run_review_checks.ps1`, wait for that Unity
process to finish, then run `Tools/capture_lobby.ps1` (or add `-Compact`). Captures
are written under `.utmp/review-unity/Logs/`. This does not modify your open scene.

## Playing

The campaign now progresses from untimed two-target training to expert contracts
across 14 authored nodes. Casual / Standard / Hardcore is applied alongside each
node's tier. See [CAMPAIGN_PROGRESSION.md](CAMPAIGN_PROGRESSION.md) for the level
order, balancing rules, fixes, and remaining playtesting work.

Open Assets/Scenes/GeoSniper.unity and enter Play mode. Choose a place by name,
use My Location on a phone, or start Offline Practice. Gameplay uses 640 m square
OSM sectors and streams neighbouring sectors. WASD moves, Shift sprints, Space
jumps, E uses a nearby ladder, left mouse fires, right mouse scopes, and R reloads.
Mobile has touch controls. Tap the radar to open the map. Free roam has fictional
patrol enemies and an invulnerable player. The HUD reports actual horizontal speed:
walking is capped at 5 m/s (18 km/h), sprinting at 9 m/s (32.4 km/h).

Android requests foreground precise and approximate location permissions together,
only after My Location is tapped. GPS requests 5 m accuracy, waits up to 18 seconds,
rejects stale fixes, and shows the reported accuracy. Requested accuracy is not a
guarantee. If no fresh fix within 100 m is available, choose a place instead; the
game never substitutes a capital city for a failed GPS lookup. The editor uses
place search or Offline Practice. Spawn preserves the selected coordinate on the
ground, or the roof directly above it; a blocked edge may require up to 3 m of
clearance. Restart uses a safe surface near the current player, not an old roof.

The requested coordinates are sent to the map provider and saved with up to 32
sector caches (64 MB maximum) in Application.persistentDataPath. Search terms go to Photon. The
optional MapService also caches requests; see MapService/README.md. Coordinates
use six decimal places at the service boundary, not the old roughly 100 m grid.
Fresh cached sectors load first; older caches can be reused for seven days on a
download failure. GPS drift up to 12 m reuses nearby cached geometry with corrected
local coordinates, rather than moving buildings. Failed initial downloads or builds
show the cause and Retry Same Location; they never automatically start an offline
grid. Offline Practice is a separate explicit action. Direct downloads use POST,
30-second server queries and 45-second request timeouts, with provider failover and
rate-limit cooldown. Neighbour requests are serialized, prioritized near the player,
and backed off after failures instead of repeatedly requesting all eight neighbours.

CountryCapitals.json contains 250 REST Countries entries, downloaded 2026-09-06
from https://restcountries.conventus.de/v3.1/all?fields=cca2,name,capital,capitalInfo.
Source project: https://gitlab.com/restcountries/restcountries (MPL-2.0).
This legacy bundled dataset is not used as an automatic location fallback.

Map attribution: OpenStreetMap contributors, https://www.openstreetmap.org/copyright
(ODbL). Mapped road/building names and named shops, fuel stations and police
stations are retained. Road boards use the name of their own OSM way. Building
and tenant names share an outward-facing wall panel; unmatched POIs have supported
freestanding boards. Missing OSM names and missing building footprints are not
invented. Names in unsupported font scripts may need additional font assets.
The requested bounding area is transmitted to the configured map provider.
Simple closed building ways keep their original footprint vertices and placement,
including concave outlines and sector-crossing buildings. Heights and facades are
generic where source attributes are absent. Building interior holes and OSM
relation multipolygons are not implemented yet. Overture water polygons preserve
islands and concave shorelines, clip to sector bounds, and use an animated mobile
water shader. Terrain now forms dry banks and shallow edges; mapped roads,
pavement, soil, grass and park vegetation receive distinct treatment. Mapped
river centerlines have bounded fallback widths. Small scenery is culled with
distance on Android. Water heights are approximate; actual Android visual and
performance checks remain pending. Gameplay elevation is disabled by default for
stable roads, foundations and traversal. Sector feature budgets are documented
in MapService/README.md; the map cannot promise full detail beyond those limits.

The public Overpass endpoint is for limited prototype testing. Configure a hosted
map backend before publishing to many players. Review provider usage policies
and the dataset/OSM license obligations before distribution.

Validation: Geo Sniper > Validate Map Signs And Buildings writes Logs/MapWorldChecks.txt
and front/back road-sign and facade screenshots. It checks geometry, spawn
coordinates, GPS freshness, cache density, sign attachment, text fit and rendered
pixels. Run Python map tests with `py -3.14 -m unittest discover -s MapService`.
Geo Sniper > Build Android Test APK writes Builds/GeoSniper-tested.apk and
Logs/AndroidBuildResult.txt. Android Build Support, SDK, NDK and JDK
must be installed through Unity Hub before building an APK. Device permission,
pause/resume, network failure and performance testing remain required on Android.

## Documentation

Ads use the Google Mobile Ads SDK with official test app/ad-unit IDs. `AdManager`
preloads rewarded and interstitial ads at startup; rewards require the SDK reward
callback. Test mode overrides custom ad-unit IDs. Android requires internet access
and a rebuilt APK after code changes. Unity Editor shows the SDK's preview ad,
not a network video. Loading errors appear on screen and detailed SDK failures
are logged with `[AdManager]`. Test via Tools > GeoSniper > AdMob Configuration
while in Play mode, or WATCH AD in the vault.

See [GAME_MODES_AND_DIFFICULTY_GUIDE.md](GAME_MODES_AND_DIFFICULTY_GUIDE.md) for comprehensive documentation covering:
- All 7 combat modes, enemy AI mechanics, ballistics, and difficulty tuning
- Real-world OpenStreetMap GIS engine integration (Satellite Recon briefing, live telemetry, and tactical cartography)
- AdMob monetization architecture (store reviews are optional and never rewarded)

## Play Store release

See [Store/RELEASE_READINESS.md](Store/RELEASE_READINESS.md) for release blockers,
verification results and publisher setup. Development builds retain official test
ads. Non-development Android builds are blocked until production configuration,
signing and required review confirmations are supplied. Public support:
bittruth1solutions@gmail.com. The privacy-policy URL is not configured yet.

## Organic promotion workspace

[Growth Studio](Growth/README.md) is a separate, free local workflow for YouTube
Shorts and Instagram Reels. It includes a pre-launch content plan, vertical-video
renderer, reviewed delivery queue, upload packs, optional official API adapters,
and manually recorded performance reports. Run `node Growth/server.mjs`, then
open http://127.0.0.1:4318. Publishing is disabled by default; account setup and
current-build footage review are required before any public promotion.
