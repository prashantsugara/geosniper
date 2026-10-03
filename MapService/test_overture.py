import json
import unittest
import uuid
from pathlib import Path
from types import SimpleNamespace
from overture_service import OvertureService, CHECK_SECONDS, convert
from server import MapUnavailable


class ReleaseChecks(unittest.TestCase):
    def setUp(self):
        directory = Path(__file__).parent / ("test-cache-" + uuid.uuid4().hex)
        directory.mkdir()
        self.addCleanup(directory.rmdir)
        self.path = directory / "cache.sqlite"
        self.addCleanup(lambda: self.path.unlink(missing_ok=True))
        self.clock = 1000000
        self.release = "2026-08-19.0"
        self.calls = []
        self.checks = 0
        self.body = dict(latitude=0, longitude=0)
        self.service = self.restart()

    def catalog(self):
        self.checks += 1
        return self.release

    def fetch(self, release, lat, lon):
        self.calls.append((release, lat, lon))
        return dict(version=2, provider="overture", release=release, features=[dict(kind="road", points=[[0, 0], [1, 1]])])

    def restart(self):
        return OvertureService(self.path, self.fetch, self.catalog, lambda: self.clock)

    def test_same_release_no_daily_download(self):
        self.service.sector(self.body)
        self.clock += 40 * 86400
        self.assertEqual(self.service.sector(self.body)["source"], "cache")
        self.assertEqual(len(self.calls), 1)
        self.assertEqual(self.checks, 2)

    def test_restart_keeps_release_and_sector(self):
        self.service.sector(self.body)
        self.service = self.restart()
        response = self.service.sector(dict(self.body, cachedRelease=self.release))
        self.assertTrue(response["notModified"])
        self.assertNotIn("features", response)
        self.assertEqual(len(self.calls), 1)
        self.assertEqual(self.checks, 1)

    def test_new_release_download_once(self):
        self.service.sector(self.body)
        self.release = "2026-09-23.0"
        self.clock += CHECK_SECONDS
        response = self.service.sector(dict(self.body, cachedRelease="2026-08-19.0"))
        self.assertEqual(response["release"], self.release)
        self.assertIn("features", response)
        self.service.sector(self.body)
        self.assertEqual(len(self.calls), 2)

    def test_failed_update_retains_old_sector(self):
        self.service.sector(self.body)
        self.release = "2026-09-23.0"
        self.clock += CHECK_SECONDS
        self.service.fetcher = lambda *a: (_ for _ in ()).throw(MapUnavailable("FAIL"))
        response = self.service.sector(self.body)
        self.assertEqual(response["source"], "stale_cache")
        self.assertEqual(response["release"], "2026-08-19.0")
        self.clock += 61
        self.service.fetcher = self.fetch
        self.assertEqual(self.service.sector(self.body)["release"], self.release)

    def test_catalog_failure_does_not_trigger_geometry_download(self):
        self.service.sector(self.body)
        self.clock += CHECK_SECONDS
        self.service.catalog = lambda: (_ for _ in ()).throw(OSError())
        self.assertEqual(self.service.sector(self.body)["source"], "stale_cache")
        self.assertEqual(len(self.calls), 1)
        with self.assertRaises(MapUnavailable):
            self.service.sector(dict(latitude=1, longitude=1))

    def test_new_sector_same_release(self):
        self.service.sector(self.body)
        self.service.sector(dict(latitude=1, longitude=1))
        self.assertEqual(len(self.calls), 2)
        self.assertEqual(self.checks, 1)

    def test_bad_update_not_committed(self):
        self.service.sector(self.body)
        self.release = "2026-09-23.0"
        self.clock += CHECK_SECONDS
        self.service.fetcher = lambda *a: dict(version=2, release=self.release, features=[])
        self.assertEqual(self.service.sector(self.body)["release"], "2026-08-19.0")

    def test_older_catalog_does_not_roll_back(self):
        self.service.sector(self.body)
        self.clock += CHECK_SECONDS
        self.release = "2026-07-22.0"
        self.assertEqual(self.service.sector(self.body)["release"], "2026-08-19.0")
        self.assertEqual(len(self.calls), 1)


class GeometryChecks(unittest.TestCase):
    def test_roads_names_multipolygons_and_courtyards(self):
        road = SimpleNamespace(geom_type="LineString", coords=[(-.001, 0), (.001, 0)])
        def polygon(x, holes=False):
            points = [(x, .0001), (x+.0001, .0001), (x+.0001, .0002), (x, .0002), (x, .0001)]
            return SimpleNamespace(geom_type="Polygon", interiors=[1] if holes else [], exterior=SimpleNamespace(coords=points))
        multi = SimpleNamespace(geom_type="MultiPolygon", geoms=[polygon(0), polygon(.0003), polygon(.0006, True)])
        poi = SimpleNamespace(geom_type="Point", x=.0004, y=-.0004)
        rows = [
            ("segment", dict(subtype="road", names=dict(primary="Market Road")), road),
            ("segment", dict(subtype="rail"), road),
            ("building", dict(names=dict(primary="Market Hall"), height=9), multi),
            ("place", dict(names=dict(primary="Fuel Stop"), categories=dict(primary="gas_station")), poi),
        ]
        result = convert(rows, 0, 0)
        features = result["features"]
        self.assertEqual([f["kind"] for f in features], ["road", "building", "building", "place"])
        self.assertEqual(features[0]["name"], "Market Road")
        self.assertEqual(features[0]["points"][0], [-111.32, 0])
        self.assertEqual(features[1]["name"], "Market Hall")
        self.assertEqual(features[-1]["landmark"], "fuel")
        self.assertEqual(result["skippedCourtyards"], 1)


if __name__ == "__main__":
    unittest.main()
