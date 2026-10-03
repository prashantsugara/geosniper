"""Small OSM sector service. No request bodies, IPs or coordinates are logged."""
import argparse
import hashlib
import json
import math
import os
import sqlite3
import threading
import time
import urllib.parse
import urllib.request
from contextlib import closing
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

PROVIDERS = ("https://overpass.private.coffee/api/interpreter",
             "https://overpass-api.de/api/interpreter")
MAX_BYTES = 8*1024*1024
FRESH_SECONDS = 15 * 60
STALE_SECONDS = 7 * 86400


class MapUnavailable(Exception):
    pass


def coordinates(body):
    if not isinstance(body, dict):
        raise ValueError("object required")
    lat, lon = body.get("latitude"), body.get("longitude")
    if any(isinstance(v, bool) or not isinstance(v, (int, float)) or not math.isfinite(v)
           for v in (lat, lon)) or abs(lat) > 85 or abs(lon) > 180:
        raise ValueError("invalid coordinates")
    # Preserve the gameplay origin; rounding to 3 decimals shifted buildings by up to 55 m.
    return round(lat, 6), round(lon, 6)


def clip(a, b):
    start, end = 0.0, 1.0
    delta = [b[i] - a[i] for i in range(2)]
    for i in range(2):
        if abs(delta[i]) < 1e-9:
            if abs(a[i]) > 320:
                return None
            continue
        low, high = sorted(((-320-a[i])/delta[i], (320-a[i])/delta[i]))
        start, end = max(start, low), min(end, high)
        if start > end:
            return None
    points = [[round(a[i]+delta[i]*t, 2) for i in range(2)] for t in (start, end)]
    return points if math.dist(*points) > .1 else None


def normalize(raw, lat, lon):
    if not isinstance(raw, dict) or raw.get("remark") or not isinstance(raw.get("elements"), list):
        raise MapUnavailable("INVALID_UPSTREAM_RESPONSE")
    roads, buildings, other = [], [], []
    for element in raw["elements"][:10000]:
        try:
            tags, geometry = element.get("tags", {}), element.get("geometry", [])
            is_node = element.get("type") == "node"
            if "military" in tags:
                continue
            if not is_node and not 2 <= len(geometry) <= 400:
                continue
            amenity = str(tags.get("amenity", "")).lower()
            is_landmark = amenity in ("fuel", "police")
            is_shop = "shop" in tags
            kind = ("place" if is_node else "building" if is_landmark or is_shop or "building" in tags
                    else "road" if "highway" in tags else "water" if tags.get("natural") == "water" else "park")
            if is_node:
                if not tags.get("name") and not is_landmark:
                    continue
                points = [[(float(element["lon"])-lon)*math.cos(math.radians(lat))*111320,
                           (float(element["lat"])-lat)*111320]]
            else:
                points = [[(float(p["lon"])-lon)*math.cos(math.radians(lat))*111320,
                           (float(p["lat"])-lat)*111320] for p in geometry]
            if any(not math.isfinite(v) for p in points for v in p):
                continue
            if kind == "place":
                x, z = points[0]
                points = [[x-2, z-2], [x+2, z-2], [x+2, z+2], [x-2, z+2]]
                if abs(x) >= 320 or abs(z) >= 320:
                    continue
                item = dict(kind=kind, name=feature_name(tags),
                            landmark=amenity if is_landmark else "shop", height=3, points=points)
                other.append(item)
                continue
            if kind == "road":
                run = None
                for a, b in zip(points, points[1:]):
                    segment = clip(a, b)
                    if not segment:
                        run = None
                    elif run is not None and math.dist(run["points"][-1], segment[0]) < .01:
                        run["points"].append(segment[1])
                    else:
                        run = dict(kind=kind, name=feature_name(tags), height=0, points=segment)
                        roads.append(run)
            else:
                if len(points) < 4 or math.dist(points[0], points[-1]) > .1:
                    continue
                if any(abs(v) > 960 for p in points for v in p):
                    continue
                owner = [sum(p[i] for p in points[:-1]) / (len(points)-1) for i in (0, 1)]
                if any(v < -320 or v >= 320 for v in owner):
                    continue
                try:
                    height = float(tags.get("height", float(tags.get("building:levels", 4))*3))
                except (ValueError, TypeError):
                    height = 12
                if not math.isfinite(height):
                    height = 12
                item = dict(kind=kind, name=feature_name(tags),
                            landmark=amenity if is_landmark else "shop" if is_shop else "",
                            height=max(3, min(60, height)),
                            points=[[round(v, 2) for v in p] for p in points[:-1]])
                (buildings if kind == "building" else other).append(item)
        except (ValueError, TypeError, KeyError, AttributeError):
            continue
    if not roads:
        raise MapUnavailable("NO_USABLE_ROADS")
    def distance(item):
        points = item.get("points", [])
        if not points:
            return float("inf")
        return sum(x*x + z*z for x, z in points) / len(points)

    buildings.sort(key=distance)
    roads.sort(key=distance)
    other.sort(key=distance)
    places = [item for item in other if item["kind"] == "place"]
    areas = [item for item in other if item["kind"] != "place"]
    return dict(version=2, features=roads[:600] + buildings[:1200] + places[:240] + areas[:80],
                attribution="OpenStreetMap contributors (ODbL)")


def feature_name(tags):
    for key in ("name", "name:en", "official_name", "short_name"):
        value = tags.get(key)
        if isinstance(value, str) and value.strip():
            return value.strip().replace("\n", " ").replace("\r", " ")[:160]
    return ""


def fetch(provider, lat, lon):
    area = f"(around:455,{lat:.6f},{lon:.6f})"
    query = '[out:json][timeout:30];(' + ''.join(
        f'way[{tag}]{area};' for tag in ('highway', 'building', 'shop', 'amenity~"^(fuel|police)$"', 'leisure=park', 'natural=water')) + ''.join(
        f'node[{tag}]{area};' for tag in ('shop', 'amenity~"^(fuel|police)$"')) + ');out geom;'
    data = urllib.parse.urlencode({"data": query}).encode()
    request = urllib.request.Request(provider, data=data, headers={
        "Content-Type": "application/x-www-form-urlencoded", "Accept": "application/json",
        "User-Agent": "GeoSniperPrototype/1.0"})
    try:
        with urllib.request.urlopen(request, timeout=45) as response:
            payload = response.read(MAX_BYTES+1)
            if len(payload) > MAX_BYTES:
                raise MapUnavailable("UPSTREAM_TOO_LARGE")
            return normalize(json.loads(payload), lat, lon)
    except MapUnavailable:
        raise
    except Exception:
        # Never return upstream URLs, response bodies or coordinate-bearing exceptions.
        raise MapUnavailable("UPSTREAM_UNAVAILABLE") from None


class SectorService:
    def __init__(self, path, fetcher=fetch, now=time.time):
        self.path, self.fetcher, self.now = str(path), fetcher, now
        self.lock = threading.Lock()
        self.retry_after = 0
        Path(path).parent.mkdir(parents=True, exist_ok=True)
        with closing(sqlite3.connect(self.path)) as db, db:
            db.execute("CREATE TABLE IF NOT EXISTS sectors (key TEXT PRIMARY KEY, created REAL, payload TEXT)")

    def sector(self, body):
        lat, lon = coordinates(body)
        key = hashlib.sha256(f"v2:{lat:.6f}:{lon:.6f}".encode()).hexdigest()
        # Single flight bounds upstream load and coalesces duplicate requests.
        if not self.lock.acquire(timeout=1):
            raise MapUnavailable("SERVICE_BUSY")
        try:
            now = self.now()
            with closing(sqlite3.connect(self.path)) as db, db:
                db.execute("DELETE FROM sectors WHERE created < ?", (now-STALE_SECONDS,))
                row = db.execute("SELECT created,payload FROM sectors WHERE key=?", (key,)).fetchone()
                cached = None
                if row:
                    try:
                        cached = json.loads(row[1])
                        if cached.get("version") != 2 or not cached.get("features"):
                            cached = None
                    except (ValueError, AttributeError):
                        pass
                if cached and now-row[0] < FRESH_SECONDS:
                    return dict(cached, source="cache")
                if now >= self.retry_after:
                    for provider in PROVIDERS:
                        try:
                            result = self.fetcher(provider, lat, lon)
                        except MapUnavailable:
                            continue
                        db.execute("INSERT OR REPLACE INTO sectors VALUES (?,?,?)",
                                   (key, now, json.dumps(result, allow_nan=False)))
                        db.execute("DELETE FROM sectors WHERE key NOT IN (SELECT key FROM sectors ORDER BY created DESC LIMIT 128)")
                        return dict(result, source="live")
                    self.retry_after = self.now()+30
                if cached:
                    return dict(cached, source="stale_cache")
                raise MapUnavailable("MAP_UNAVAILABLE")
        finally:
            self.lock.release()


def handler(service):
    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *args):
            pass

        def setup(self):
            super().setup()
            self.connection.settimeout(35)

        def reply(self, code, payload):
            data = json.dumps(payload, allow_nan=False, separators=(",", ":")).encode()
            self.send_response(code)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(data)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(data)

        def do_GET(self):
            self.reply(200 if self.path == "/health" else 404,
                       {"status": "ok", "version": 1} if self.path == "/health" else {"error": "NOT_FOUND"})

        def do_POST(self):
            if self.path != "/v1/sector":
                self.reply(404, {"error": "NOT_FOUND"})
                return
            try:
                length = int(self.headers.get("Content-Length", "0"))
                if not 0 < length <= 1024:
                    self.reply(413, {"error": "REQUEST_TOO_LARGE"})
                    return
                body = json.loads(self.rfile.read(length))
                self.reply(200, service.sector(body))
            except (ValueError, TypeError):
                self.reply(400, {"error": "INVALID_COORDINATES"})
            except MapUnavailable as error:
                self.reply(503, {"error": str(error)})
            except (BrokenPipeError, ConnectionResetError, TimeoutError):
                pass
            except Exception:
                self.reply(500, {"error": "SERVICE_ERROR"})
    return Handler


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=int(os.getenv("PORT", "8787")))
    parser.add_argument("--cache", default=str(Path(__file__).parent / "data" / "sectors.sqlite"))
    parser.add_argument("--provider", choices=("overture", "osm"), default="overture")
    args = parser.parse_args()
    if args.provider == "overture":
        import sys
        sys.modules.setdefault("server", sys.modules[__name__])
        from overture_service import OvertureService
        service = OvertureService(args.cache)
    else:
        service = SectorService(args.cache)
    server = ThreadingHTTPServer((args.host, args.port), handler(service))
    print(f"Geo Sniper map service listening on port {args.port}", flush=True)
    server.serve_forever()
