import json
import uuid
import threading
import unittest
import urllib.request
import urllib.error
from pathlib import Path
from http.server import ThreadingHTTPServer
from server import SectorService, MapUnavailable, normalize, clip, coordinates, handler, FRESH_SECONDS, STALE_SECONDS

def sample():
    return {"version": 2, "features": [{"kind": "road", "height": 0, "points": [[-100, 0], [100, 0]]}]}

class Checks(unittest.TestCase):
    def setUp(self):
        # Avoid restrictive mkdtemp ACLs in Windows sandbox test runs.
        directory = Path(__file__).parent / ("test-cache-" + uuid.uuid4().hex)
        directory.mkdir()
        self.addCleanup(directory.rmdir)
        self.path = directory / "cache.sqlite"
        self.addCleanup(lambda: self.path.unlink(missing_ok=True))
        self.clock = [1000000]
        self.calls = []
        def fetch(*args):
            self.calls.append(args)
            return sample()
        self.service = SectorService(self.path, fetch, lambda: self.clock[0])
        self.body = {"latitude": 28.61391, "longitude": 77.20901}

    def test_cache_survives_service_restart(self):
        self.assertEqual(self.service.sector(self.body)["source"], "live")
        service = SectorService(self.path, lambda *a: self.fail("unnecessary upstream call"), lambda: self.clock[0])
        self.assertEqual(service.sector(self.body)["source"], "cache")
        self.assertEqual(self.calls[0][1:], (28.61391, 77.20901))

    def test_failover(self):
        def fetch(provider, *args):
            self.calls.append(provider)
            if len(self.calls) == 1:
                raise MapUnavailable("FAIL")
            return sample()
        self.service.fetcher = fetch
        self.assertEqual(self.service.sector(self.body)["source"], "live")
        self.assertEqual(len(self.calls), 2)

    def test_refresh_after_fifteen_minutes(self):
        self.service.sector(self.body)
        self.clock[0] += 899
        self.assertEqual(self.service.sector(self.body)["source"], "cache")
        self.clock[0] += 1
        self.assertEqual(self.service.sector(self.body)["source"], "live")
        self.assertEqual(len(self.calls), 2)

    def test_stale_response_does_not_renew_cache(self):
        self.service.sector(self.body)
        self.service.fetcher = lambda *a: (_ for _ in ()).throw(MapUnavailable("FAIL"))
        self.clock[0] += STALE_SECONDS - 60
        self.assertEqual(self.service.sector(self.body)["source"], "stale_cache")
        self.clock[0] += 61
        with self.assertRaises(MapUnavailable):
            self.service.sector(self.body)

    def test_stale_then_expired(self):
        self.service.sector(self.body)
        self.service.fetcher = lambda *a: (_ for _ in ()).throw(MapUnavailable("FAIL"))
        self.clock[0] += FRESH_SECONDS+1
        self.assertEqual(self.service.sector(self.body)["source"], "stale_cache")
        self.clock[0] += STALE_SECONDS
        with self.assertRaises(MapUnavailable):
            self.service.sector(self.body)

    def test_input_validation(self):
        for value in (None, "12", True, float("nan"), 91):
            with self.assertRaises(ValueError):
                coordinates({"latitude": value, "longitude": 1})

    def test_geometry_privacy_clipping(self):
        raw = {"elements": [{"tags": {"highway": "residential", "name": "Secret", "addr:street": "Private"},
                             "geometry": [{"lat": 0, "lon": -.01}, {"lat": 0, "lon": .01}]}]}
        result = normalize(raw, 0, 0)
        self.assertEqual(result["features"][0]["points"], [[-320, 0], [320, 0]])
        self.assertEqual(result["features"][0]["name"], "Secret")
        self.assertNotIn("Private", json.dumps(result))
        self.assertIsNone(clip([-400, 400], [400, 400]))

    def test_boundary_building_and_more_than_40_places(self):
        raw = {"elements": [{"tags": {"highway": "residential", "name:en": "Market Road"},
                              "geometry": [{"lat": 0, "lon": 0}, {"lat": 0, "lon": .001}]}]}
        points = [(305, 0), (325, 0), (325, 20), (305, 20), (305, 0)]
        raw["elements"].append({"type": "way", "tags": {"building": "yes", "name": "Boundary House"},
                                "geometry": [{"lat": z/111320, "lon": x/111320} for x, z in points]})
        for i in range(60):
            raw["elements"].append({"type": "node", "lat": .001, "lon": i*.00001,
                                    "tags": {"shop": "convenience", "name": f"Shop {i}"}})
        result = normalize(raw, 0, 0)
        self.assertEqual(result["version"], 2)
        self.assertEqual(result["features"][0]["name"], "Market Road")
        self.assertEqual(sum(f["kind"] == "place" for f in result["features"]), 60)
        self.assertTrue(any(f["name"] == "Boundary House" for f in result["features"]))

    def test_road_polyline_is_not_truncated_by_vertex_count(self):
        road = {"tags": {"highway": "residential", "name": "Continuous Road"},
                "geometry": [{"lat": 0, "lon": i/111320} for i in range(250)]}
        features = normalize({"elements": [road]}, 0, 0)["features"]
        self.assertEqual(len(features), 1)
        self.assertEqual(len(features[0]["points"]), 250)

    def test_upstream_error_is_not_empty_live_map(self):
        for raw in ({"remark": "timeout", "elements": []}, {}, {"elements": []}):
            with self.assertRaises(MapUnavailable):
                normalize(raw, 0, 0)

    def test_http_contract(self):
        server = ThreadingHTTPServer(("127.0.0.1", 0), handler(self.service))
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            url = f"http://127.0.0.1:{server.server_port}"
            with urllib.request.urlopen(url+"/health") as r:
                self.assertEqual(json.load(r)["status"], "ok")
            req = urllib.request.Request(url+"/v1/sector", data=json.dumps(self.body).encode(), headers={"Content-Type": "application/json"})
            with urllib.request.urlopen(req) as r:
                self.assertEqual(json.load(r)["source"], "live")
            bad = urllib.request.Request(url+"/v1/sector", data=b'{"latitude":null}')
            with self.assertRaises(urllib.error.HTTPError) as caught:
                urllib.request.urlopen(bad)
            self.assertEqual(caught.exception.code, 400)
        finally:
            server.shutdown()
            server.server_close()
            thread.join()

if __name__ == "__main__":
    unittest.main()
