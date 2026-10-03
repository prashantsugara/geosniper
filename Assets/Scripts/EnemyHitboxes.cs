using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    // Separate animated shot volumes from the narrow controller used for navigation.
    public sealed class EnemyHitboxes : MonoBehaviour
    {
        sealed class Segment
        {
            public Transform From, To;
            public CapsuleCollider Shape;
            public Vector3 StartOffset, EndOffset;
            public string BodyPart;
        }
        readonly List<Segment> segments = new List<Segment>();
        SkinnedMeshRenderer[] skins;
        MeshFilter[] staticMeshes;
        sealed class PosedMesh { public Mesh Mesh; public int Version; }
        readonly Dictionary<SkinnedMeshRenderer,PosedMesh> posedMeshes = new Dictionary<SkinnedMeshRenderer,PosedMesh>();
        readonly List<Vector3> shotVertices = new List<Vector3>();
        readonly List<int> shotTriangles = new List<int>();

        public bool HasRaycastGeometry
        {
            get
            {
                if (skins != null) foreach (var skin in skins)
                        if (skin != null && skin.enabled && skin.gameObject.activeInHierarchy && skin.sharedMesh != null) return true;
                if (staticMeshes != null) foreach (var mesh in staticMeshes)
                        if (mesh != null && mesh.gameObject.activeInHierarchy && mesh.sharedMesh != null && mesh.sharedMesh.isReadable
                            && mesh.GetComponent<MeshRenderer>() is MeshRenderer renderer && renderer.enabled) return true;
                return false;
            }
        }

        public void Initialize(Transform visual)
        {
            skins = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            staticMeshes = visual.GetComponentsInChildren<MeshFilter>(true);
            var bones = visual.GetComponentsInChildren<Transform>(true);

            // Head bone pivot is located at the neck/chin joint. Offset upward (+0.12m) to center over skull.
            Add(bones, "head", null, .14f, "head", new Vector3(0, 0.12f, 0));
            Add(bones, "spine", "neck", .28f, "torso");
            foreach (string side in new[] { "left", "right" })
            {
                Add(bones, side + "arm", side + "forearm", .15f, side + "arm");
                Add(bones, side + "forearm", side + "hand", .13f, side + "arm");
                Add(bones, side + "upleg", side + "leg", .18f, side + "leg");
                Add(bones, side + "leg", side + "foot", .16f, side + "leg");
            }

            // Missing bones get proportional volumes only; never overlay an animated head.
            var bounds = new Bounds(Vector3.up * .925f, new Vector3(.6f, 1.85f, .4f));
            if (skins.Length > 0 || staticMeshes.Length > 0) bounds = ImportedVisual.PosedBounds(transform);
            float height = Mathf.Max(.1f, bounds.size.y);
            Vector3 axis = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            if (Bone(bones, "spine") == null || Bone(bones, "neck") == null)
                AddFixed("torso", axis + Vector3.up * (height * .25f), axis + Vector3.up * (height * .64f), height * .15f, "torso");
            if (Bone(bones, "head") == null)
                AddFixed("head", axis + Vector3.up * (height * .88f), axis + Vector3.up * (height * .98f), height * .09f, "head");
        }

        static Transform Bone(Transform[] bones, string name)
        {
            foreach (var bone in bones)
            {
                string key = bone.name.ToLowerInvariant().Replace(":", "").Replace("_", "").Replace(".", "");
                if (key.EndsWith(name, System.StringComparison.Ordinal)) return bone;
            }
            return null;
        }

        void Add(Transform[] bones, string from, string to, float radius, string bodyPart, Vector3 offset = default)
        {
            var a = Bone(bones, from); var b = to == null ? a : Bone(bones, to);
            if (a == null || b == null) return;
            var obj = new GameObject("Shot volume " + from);
            obj.transform.SetParent(transform, false);
            var shape = obj.AddComponent<CapsuleCollider>();
            shape.isTrigger = true; shape.enabled = false; shape.radius = radius; shape.direction = 1;
            segments.Add(new Segment { From = a, To = b, StartOffset = offset, EndOffset = offset, Shape = shape, BodyPart = bodyPart });
        }

        void AddFixed(string name, Vector3 start, Vector3 end, float radius, string bodyPart)
        {
            var obj = new GameObject("Shot volume " + name); obj.transform.SetParent(transform, false);
            var shape = obj.AddComponent<CapsuleCollider>();
            shape.isTrigger = true; shape.enabled = false; shape.radius = radius; shape.direction = 1;
            segments.Add(new Segment { From = transform, To = transform, StartOffset = start, EndOffset = end, Shape = shape, BodyPart = bodyPart });
        }

        public string GetBodyPart(Collider c, Vector3 hitPoint = default)
        {
            if (c != null)
            {
                foreach (var segment in segments)
                {
                    if (segment.Shape == c) return segment.BodyPart;
                }
            }

            // Test hitPoint against rigged bone segments
            if (hitPoint != default && segments.Count > 0)
            {
                float closestDist = float.MaxValue;
                string closestPart = null;
                for (int i = 0; i < segments.Count; i++)
                {
                    var seg = segments[i];
                    if (seg.From == null) continue;
                    Vector3 start = seg.From.TransformPoint(seg.StartOffset);
                    Vector3 end = (seg.To != null) ? seg.To.TransformPoint(seg.EndOffset) : start;
                    Vector3 line = end - start;
                    float lenSq = line.sqrMagnitude;
                    float t = lenSq > 0.0001f ? Mathf.Clamp01(Vector3.Dot(hitPoint - start, line) / lenSq) : 0f;
                    Vector3 nearestOnSegment = start + line * t;
                    float d = Vector3.Distance(hitPoint, nearestOnSegment);
                    if (d < closestDist && d < (seg.Shape != null ? seg.Shape.radius * 2.8f : 0.45f) + 0.15f)
                    {
                        closestDist = d;
                        closestPart = seg.BodyPart;
                    }
                }
                if (!string.IsNullOrEmpty(closestPart)) return closestPart;
            }

            // Anatomical coordinate fallback relative to enemy root
            if (hitPoint != default)
            {
                Vector3 local = transform.InverseTransformPoint(hitPoint);
                float y = local.y;
                float x = local.x;
                if (y >= 1.45f) return "head";
                if (y < 0.85f) return (x < 0) ? "leftleg" : "rightleg";
                if (Mathf.Abs(x) > 0.20f) return (x < 0) ? "leftarm" : "rightarm";
                return "torso";
            }

            return "body";
        }

        public void Refresh()
        {
            foreach (var segment in segments)
            {
                segment.Shape.enabled = true;
                Vector3 start = segment.From.TransformPoint(segment.StartOffset), end = segment.To.TransformPoint(segment.EndOffset);
                Vector3 delta = end - start;
                segment.Shape.transform.position = (start + end) * .5f;
                segment.Shape.transform.rotation = delta.sqrMagnitude > .0001f
                    ? Quaternion.FromToRotation(Vector3.up, delta.normalized) : Quaternion.identity;
                segment.Shape.height = Mathf.Max(segment.Shape.radius * 2, delta.magnitude + segment.Shape.radius * 2);
            }
        }

        public void Disable()
        {
            foreach (var segment in segments) segment.Shape.enabled = false;
        }

        public bool TryRaycastVisual(Ray ray, float maxDistance, out Vector3 point, out float distance, out Collider collider, int queryVersion = 0)
        {
            point = default; distance = maxDistance; collider = null;
            bool found = false;
            if (skins != null) foreach (var skin in skins)
                {
                    if (skin == null || !skin.enabled || !skin.gameObject.activeInHierarchy || skin.sharedMesh == null) continue;
                    if (!posedMeshes.TryGetValue(skin, out var pose))
                    {
                        pose = new PosedMesh { Mesh = new Mesh { name = "Enemy shot geometry" } };
                        posedMeshes.Add(skin, pose);
                    }
                    if (queryVersion == 0 || pose.Version != queryVersion)
                    {
                        skin.BakeMesh(pose.Mesh, true);
                        pose.Mesh.RecalculateBounds();
                        pose.Version = queryVersion;
                    }
                    // Renderer bounds can lag a pose or a rooftop spawn. Test the freshly baked
                    // mesh in its own coordinates, retaining world-distance parameterisation.
                    if (RaycastMesh(pose.Mesh, skin.transform, ray, ref distance)) found = true;
                }
            if (staticMeshes != null) foreach (var filter in staticMeshes)
                {
                    if (filter == null || !filter.gameObject.activeInHierarchy || filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer == null || !renderer.enabled) continue;
                    if (RaycastMesh(filter.sharedMesh, filter.transform, ray, ref distance)) found = true;
                }
            if (!found) return false;
            point = ray.GetPoint(distance);
            float closest = float.PositiveInfinity;
            foreach (var segment in segments)
            {
                float gap = (segment.Shape.ClosestPoint(point) - point).sqrMagnitude;
                if (gap < closest) { closest = gap; collider = segment.Shape; }
            }
            return collider != null;
        }

        bool RaycastMesh(Mesh mesh, Transform transform, Ray ray, ref float distance)
        {
            var inverse = transform.worldToLocalMatrix;
            var origin = inverse.MultiplyPoint3x4(ray.origin);
            var direction = inverse.MultiplyVector(ray.direction);
            var bounds = mesh.bounds;
            bounds.Expand(0.2f);
            if (!bounds.IntersectRay(new Ray(origin, direction.normalized), out _)) return false;
            mesh.GetVertices(shotVertices); bool found = false;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles) continue;
                mesh.GetTriangles(shotTriangles, submesh);
                for (int i = 0; i < shotTriangles.Count; i += 3)
                {
                    var a = shotVertices[shotTriangles[i]];
                    var edge1 = shotVertices[shotTriangles[i + 1]] - a;
                    var edge2 = shotVertices[shotTriangles[i + 2]] - a;
                    var cross = Vector3.Cross(direction, edge2);
                    float det = Vector3.Dot(edge1, cross);
                    if (Mathf.Abs(det) < 1e-10f) continue;
                    var offset = origin - a;
                    float u = Vector3.Dot(offset, cross) / det;
                    if (u < 0 || u > 1) continue;
                    var q = Vector3.Cross(offset, edge1);
                    float v = Vector3.Dot(direction, q) / det;
                    if (v < 0 || u + v > 1) continue;
                    float t = Vector3.Dot(edge2, q) / det;
                    if (t < 0 || t >= distance) continue;
                    distance = t; found = true;
                }
            }
            return found;
        }

        void OnDestroy()
        {
            foreach (var pose in posedMeshes.Values) if (pose.Mesh != null) { if (Application.isPlaying) Destroy(pose.Mesh); else DestroyImmediate(pose.Mesh); }
            posedMeshes.Clear();
        }
    }
}
