"""Release-pinned Overture sectors. Metadata checks do not download map geometry."""
import hashlib
import json
import math
import re
import sqlite3
import subprocess
import sys
import threading
import time
import urllib.request
from contextlib import closing
from pathlib import Path

from server import MapUnavailable, MAX_BYTES, coordinates, normalize

CATALOG = "https://stac.overturemaps.org/catalog.json"
CHECK_SECONDS = 3600


def latest_release():
    with urllib.request.urlopen(CATALOG, timeout=15) as response:
        data = response.read(1024 * 1024 + 1)
    if len(data) > 1024 * 1024:
        raise MapUnavailable("CATALOG_TOO_LARGE")
    release = json.loads(data).get("latest")
    if not isinstance(release, str) or not re.fullmatch(r"\d{4}-\d{2}-\d{2}\.\d+", release):
        raise MapUnavailable("INVALID_RELEASE")
    return release


def fetch_sector(release, lat, lon):
    # Bound the full cloud query, including SDK metadata calls and all three themes.
    try:
        result = subprocess.run(
            [sys.executable, str(Path(__file__).resolve()), release, str(lat), str(lon)],
            capture_output=True, timeout=180, check=True)
        if len(result.stdout) > MAX_BYTES:
            raise MapUnavailable("SECTOR_TOO_LARGE")
        payload = json.loads(result.stdout)
        if payload.get("release") != release or not payload.get("features"):
            raise MapUnavailable("INVALID_SECTOR")
        return payload
    except Exception:
        raise MapUnavailable("OVERTURE_DOWNLOAD_FAILED") from None


class OvertureService:
    def __init__(self, path, fetcher=fetch_sector, catalog=latest_release, now=time.time):
        self.path, self.fetcher, self.catalog, self.now = str(path), fetcher, catalog, now
        self.lock = threading.Lock()
        self.retry_after = 0
        Path(path).parent.mkdir(parents=True, exist_ok=True)
        with closing(sqlite3.connect(self.path)) as db, db:
            db.execute("CREATE TABLE IF NOT EXISTS overture_sectors (key TEXT PRIMARY KEY, release TEXT, accessed REAL, payload TEXT)")
            db.execute("CREATE TABLE IF NOT EXISTS overture_catalog (id INTEGER PRIMARY KEY, release TEXT, checked REAL)")

    def sector(self, body):
        lat, lon = coordinates(body)
        key = hashlib.sha256(f"overture-v1:{lat:.6f}:{lon:.6f}".encode()).hexdigest()
        if not self.lock.acquire(timeout=1):
            raise MapUnavailable("SERVICE_BUSY")
        try:
            with closing(sqlite3.connect(self.path)) as db, db:
                now = self.now()
                row = db.execute("SELECT release,payload FROM overture_sectors WHERE key=?", (key,)).fetchone()
                cached = json.loads(row[1]) if row else None
                meta = db.execute("SELECT release,checked FROM overture_catalog WHERE id=1").fetchone()
                release = meta[0] if meta else None
                verified = bool(meta and now - meta[1] < CHECK_SECONDS)
                if not verified and now >= self.retry_after:
                    try:
                        candidate = self.catalog()
                        if not re.fullmatch(r"\d{4}-\d{2}-\d{2}\.\d+", candidate):
                            raise ValueError("invalid release")
                        # Never roll back a published release because of an older catalog replica.
                        release = max(release or candidate, candidate,
                                      key=lambda value: tuple(map(int, re.split(r"[-.]", value))))
                        db.execute("INSERT OR REPLACE INTO overture_catalog VALUES (1,?,?)", (release, now))
                        verified = True
                    except Exception:
                        self.retry_after = now + 60
                source = "cache"
                if verified and (not cached or row[0] != release) and now >= self.retry_after:
                    try:
                        result = self.fetcher(release, lat, lon)
                        encoded = json.dumps(result, allow_nan=False)
                        if (result.get("version") != 2 or result.get("provider") != "overture" or result.get("release") != release
                                or not any(f.get("kind") == "road" for f in result.get("features", []))
                                or len(encoded.encode()) > MAX_BYTES):
                            raise ValueError("invalid sector")
                        db.execute("INSERT OR REPLACE INTO overture_sectors VALUES (?,?,?,?)", (key, release, now, encoded))
                        cached, source = result, "live"
                    except Exception:
                        self.retry_after = now + 60
                if not cached:
                    raise MapUnavailable("OVERTURE_UNAVAILABLE")
                if not verified or cached["release"] != release:
                    source = "stale_cache"
                db.execute("UPDATE overture_sectors SET accessed=? WHERE key=?", (now, key))
                db.execute("DELETE FROM overture_sectors WHERE key NOT IN (SELECT key FROM overture_sectors ORDER BY accessed DESC LIMIT 128)")
                if body.get("cachedRelease") == cached["release"]:
                    return dict(version=2, provider="overture", release=cached["release"], source=source,
                                notModified=True, attribution=cached.get("attribution", ""))
                return dict(cached, source=source)
        finally:
            self.lock.release()


def convert(rows, lat, lon):
    """Reuse the game's bounded local-coordinate contract, not OSM IDs or names."""
    elements = []
    skipped = 0
    for kind, props, geometry in rows:
        name = (props.get("names") or {}).get("primary") or ""
        tags = {"name": name}
        if kind == "segment":
            if props.get("subtype") != "road":
                continue
            tags["highway"] = props.get("class") or "residential"
            parts = [geometry] if geometry.geom_type == "LineString" else list(getattr(geometry, "geoms", []))
        elif kind == "building":
            tags["building"] = "yes"
            tags["height"] = props.get("height") or 12
            parts = [geometry] if geometry.geom_type == "Polygon" else list(getattr(geometry, "geoms", []))
        else:
            if props.get("operating_status") == "permanently_closed" or geometry.geom_type != "Point":
                continue
            category = (props.get("categories") or {}).get("primary", "")
            if category in ("gas_station", "fuel_station", "petrol_station"):
                tags["amenity"] = "fuel"
            elif category in ("police_station", "police_department"):
                tags["amenity"] = "police"
            else:
                tags["shop"] = category or "yes"
            elements.append(dict(type="node", tags=tags, lon=geometry.x, lat=geometry.y))
            continue
        for part in parts:
            if part.geom_type == "Polygon":
                if part.interiors:
                    skipped += 1
                    continue  # Current Unity mesh contract cannot represent courtyards safely.
                points = part.exterior.coords
            elif part.geom_type == "LineString":
                points = part.coords
            else:
                continue
            elements.append(dict(type="way", tags=tags, geometry=[dict(lon=p[0], lat=p[1]) for p in points]))
    result = normalize(dict(elements=elements), lat, lon)
    result.update(provider="overture", skippedCourtyards=skipped,
                  attribution="OpenStreetMap contributors, Overture Maps Foundation | https://docs.overturemaps.org/attribution/")
    return result


def download(release, lat, lon):
    vendor = Path(__file__).parent / "vendor"
    if vendor.is_dir():
        sys.path.insert(0, str(vendor))
    from overturemaps.core import record_batch_reader
    from shapely import from_wkb
    dx, dy = 455 / (111320 * math.cos(math.radians(lat))), 455 / 111320
    bbox = (max(-180, lon-dx), max(-85, lat-dy), min(180, lon+dx), min(85, lat+dy))

    def rows():
        count = 0
        for kind in ("segment", "building", "place"):
            reader = record_batch_reader(kind, bbox=bbox, release=release, stac=True,
                                         connect_timeout=10, request_timeout=30)
            if reader is None:
                continue
            for batch in reader:
                for row in batch.to_pylist():
                    count += 1
                    if count > 10000:
                        raise MapUnavailable("SECTOR_TOO_DENSE")
                    yield kind, row, from_wkb(row["geometry"])
    result = convert(rows(), lat, lon)
    result["release"] = release
    return result


if __name__ == "__main__":
    from contextlib import redirect_stdout
    # SDK diagnostic output must not contaminate the JSON protocol.
    with redirect_stdout(sys.stderr):
        payload = download(sys.argv[1], float(sys.argv[2]), float(sys.argv[3]))
    print(json.dumps(payload, allow_nan=False))
