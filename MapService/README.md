# Release-aware Overture map service

## Current default: direct mobile downloads (no personal server)

Unity now selects `provider: overture`, `transport: publicTiles`. It uses HTTPS
Range requests against Overture's public PMTiles archives, decodes nearby zoom-14
vector tiles in managed C#, and stores sectors in the phone's persistent app data.
Neither `androidUrl` nor this Python service is needed for that mode.

The small release catalog is checked at most hourly while playing. Unchanged
sectors are reused, new/missing areas are downloaded at runtime, and saved sectors
remain playable offline if updates fail. Downloaded archive chunks are also reused
for neighbouring sectors. Limits: 32 sectors / 64 MB plus 128 MB of tile chunks;
evicted areas require another download. Removing app data removes saved maps.

The public tiles are Overture inspection data, not a production availability
guarantee. They may simplify footprints and split polygons at tile boundaries.
Courtyards are skipped rather than filled, and the renderer's existing feature
limits still apply. Full-resolution GeoParquet is available through the optional
backend below; direct mode does not silently mix the two caches. The existing
Overture gameplay mode now uses a filled overview drawn directly from the loaded
game geometry, rather than the unrelated OSM MapLibre background. Legacy OSM mode
retains its native MapLibre view.

Direct-mode styling preserves road class, whole-segment width/surface rules,
place category, floor count and available facade/roof properties through caching.
Older saved sectors are reprocessed from cached tile chunks when online; the old
sector remains the offline fallback. Missing attributes use documented class-based
defaults. Signs and grass clearance follow road width. Gabled and pyramidal roofs
are supported on approximately rectangular footprints; other roofs remain flat.
Pitched roofs do not receive flat-roof enemy spawns or ladders terminating inside
the roof mesh. The overview only shows loaded sectors, not undownloaded geography.

Validation: `dotnet run --project Tests/OvertureCodecChecks` runs codec fixtures;
add `-- --live` to fetch and decode three public Pune tiles. Unity compilation is
also checked separately. A rebuilt APK and real Android testing are still needed.

Public archive documentation:
https://docs.overturemaps.org/getting-data/cloud-sources/#pmtiles

## Optional backend (not required by direct mode)

The rest of this document applies to `transport: backend`. Keep that setting only
when a hosted service and full-resolution GeoParquet conversion are desired.

Overture is now the default gameplay provider. Install and start:

```powershell
python -m pip install -r MapService/requirements.txt
python MapService/server.py
```

A project-local `--target MapService/vendor` install is also supported.
Docker installs the dependencies automatically.

## Overture release policy

- Check the small official STAC catalog on demand, at most once per hour.
  This is metadata traffic, not a map download. No requests means no checks.
- Persist release IDs and geometry across service restarts. Reuse unchanged
  sectors without daily or weekly expiry.
- Download only missing sectors or sectors requested after a new release.
  Buildings, roads and places use the same explicitly pinned release.
- Commit a new sector only after successful validation. Failed updates retain
  the old sector and label it as saved data, with 60-second retry backoff.
- Phones send `cachedRelease` for the exact requested origin. The service returns
  `notModified: true` without geometry when that copy is unchanged.
- Server storage is capped at 128 sectors; phone storage at 32 sectors / 64 MB.
  Evicted sectors need downloading again. These are capacity limits, not daily expiry.
- Already loaded game sectors do not rebuild underneath the player. Updates apply
  when a sector is loaded again, not as mid-game building replacements.

Cloud queries have a 180-second total worker timeout. Overture and OSM caches are
separate. The provider is selected in `Assets/Resources/MapServiceConfig.json`.

**Backend mode requires a deployed HTTPS service URL.** `androidUrl` is currently empty.
Configure it and rebuild before testing Overture on a phone. No hosting is deployed
or purchased here. Overture mode does not silently fall back to OSM or offline grids.

The existing MapLibre OSM raster overview and Photon search are unchanged. This
replaces gameplay geometry, not the background map tiles; outlines can still differ.
MultiPolygon components are supported, but components with holes are skipped and
reported in `skippedCourtyards` until Unity supports courtyard meshes.

Tests cover reuse after 40 days, restarts, new releases, unchanged responses,
failed updates, catalog outages, and the legacy geometry/HTTP cases:

```powershell
python -m unittest discover -s MapService -v
```

Attribution: OpenStreetMap contributors, Overture Maps Foundation. Retain the
in-game credits link and applicable source licenses:
https://docs.overturemaps.org/attribution/

Catalog: https://stac.overturemaps.org/catalog.json
Reader: https://docs.overturemaps.org/getting-data/overturemaps-py/

## Legacy OSM mode (explicit opt-in only)

The following notes apply only when starting with `--provider osm` and selecting
`provider: osm` in the Unity config; these time-based caches do not apply to Overture.

Uses free OpenStreetMap data. No paid SDK or account is required. Public upstream
servers are shared services and do not guarantee availability.

Start from the Unity project folder:

```powershell
py -3.14 MapService/server.py --provider osm
```

Check `http://127.0.0.1:8787/health`. A healthy process does NOT prove upstream OSM
access. POST `/v1/sector` with JSON `latitude` and `longitude` to test data fetching.
Unity Editor uses this address via `Assets/Resources/MapServiceConfig.json`.

The service preserves the requested center to six decimal places, fetches a 455 m
radius, clips road polylines, includes named shops and fuel/police POIs, strips
addresses and returns local X/Z geometry.
It stores up to 128 sectors in SQLite: fresh for 15 minutes, usable during upstream
failure for seven days. Requests are serialized and upstream failures impose a
30-second cooldown. Do not use it as an unrestricted public high-volume API.

The Unity client keeps up to 32 sectors (64 MB), fresh for 15 minutes and available
as a fallback for seven days. Each cache includes its map center. Small GPS drift
up to 12 m is accounted for by reprojecting the cached coordinates. A different
neighbourhood is never substituted. Schema version 2 invalidates older incomplete
sectors. Failed downloads/builds show Retry Same Location instead of automatically
starting the offline grid. Only the explicit Offline Practice button uses that grid.
Neighbour downloads are serialized and retried with backoff near sector boundaries.
No real downloaded OSM asset is bundled yet.

## Android deployment

Set `androidUrl` in `Assets/Resources/MapServiceConfig.json` to the HTTPS base
address of your service and rebuild. An empty value retains the direct public
Overpass prototype loader. Do not set it to localhost: on a phone that means the
phone itself. LAN HTTP is intentionally not enabled in the Android release.

Dockerfile is supplied for hosting. Put TLS and rate limiting in front of it,
mount `/data` for the cache, and disable request body logging at the proxy. The
Python HTTP server is a small development service, not a hardened internet edge.
This repository does not deploy or purchase hosting automatically.

## Validation and limitations

```powershell
py -3.14 -m unittest discover -s MapService -v
```

Tests cover HTTP schema, invalid input, provider failover, disk cache across process
instances, expired/stale cache, road clipping and removal of metadata. Upstream
access and Android performance require separate checks on the deployment network.
Simple polygon ways only: multipolygons, holes and terrain elevation are not yet
supported. Per sector, budgets are 1,200 buildings, 600 road polylines,
240 named places and 80 land-use areas. Boundary-crossing footprints are retained
in the sector containing their center. Source data still determines which buildings
and names are available; standalone POI signs do not invent missing buildings.

Attribution: OpenStreetMap contributors, https://www.openstreetmap.org/copyright
(ODbL). Keep attribution visible in the game and with exported map data.
