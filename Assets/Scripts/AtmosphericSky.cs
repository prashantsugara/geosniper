using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    public sealed class AtmosphericSky : MonoBehaviour
    {
        public static AtmosphericSky Instance { get; private set; }

        Camera cam;
        MeshFilter skyFilter;
        MeshRenderer skyRenderer;
        Transform sunDiscTransform;
        Light mainSun;

        public Color zenithColor = new Color(0.09f, 0.24f, 0.44f);
        public Color midSkyColor = new Color(0.32f, 0.50f, 0.72f);
        public Color horizonColor = new Color(0.76f, 0.82f, 0.88f);
        public Color groundHazeColor = new Color(0.38f, 0.42f, 0.45f);
        public Color sunGlowColor = new Color(1.0f, 0.95f, 0.82f);

        public static AtmosphericSky Create(Camera targetCam = null)
        {
            if (Instance != null)
            {
                if (targetCam != null) Instance.cam = targetCam;
                return Instance;
            }
            var obj = new GameObject("Atmospheric Sky Dome");
            Instance = obj.AddComponent<AtmosphericSky>();
            if (targetCam != null) Instance.cam = targetCam;
            return Instance;
        }

        void Awake()
        {
            if (Instance == null) Instance = this;
            BuildSkyDome();
            BuildSunDisc();
        }

        void Start()
        {
            FindSunLight();
        }

        public void SyncCamera(Camera target = null)
        {
            if (target != null) cam = target;
            if (cam == null) cam = Camera.main;
            if (cam != null)
            {
                transform.position = cam.transform.position;
            }

            if (mainSun == null) FindSunLight();
            if (sunDiscTransform != null && mainSun != null)
            {
                Vector3 sunDir = -mainSun.transform.forward;
                sunDiscTransform.position = transform.position + sunDir * 1350f;
                sunDiscTransform.LookAt(transform.position);
            }
        }

        void LateUpdate()
        {
            SyncCamera();
        }

        void FindSunLight()
        {
            var sunObj = GameObject.Find("Directional Light") ?? GameObject.Find("Sun") ?? GameObject.Find("Sun Direct Light") ?? GameObject.Find("Sector Sun");
            if (sunObj != null) mainSun = sunObj.GetComponent<Light>();
            if (mainSun == null)
            {
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (l.type == LightType.Directional) { mainSun = l; break; }
                }
            }
        }

        public void SetWeatherAtmosphere(int weatherCode, bool isDay)
        {
            if (!isDay)
            {
                // Night atmosphere
                zenithColor = new Color(0.02f, 0.04f, 0.08f);
                midSkyColor = new Color(0.05f, 0.08f, 0.14f);
                horizonColor = new Color(0.08f, 0.12f, 0.20f);
                groundHazeColor = new Color(0.04f, 0.06f, 0.10f);
                sunGlowColor = new Color(0.45f, 0.55f, 0.75f) * 0.4f; // Moonlight
            }
            else if (weatherCode >= 51 && weatherCode <= 67)
            {
                // Rainy / overcast
                zenithColor = new Color(0.32f, 0.38f, 0.44f);
                midSkyColor = new Color(0.45f, 0.50f, 0.55f);
                horizonColor = new Color(0.55f, 0.60f, 0.65f);
                groundHazeColor = new Color(0.35f, 0.38f, 0.42f);
                sunGlowColor = new Color(0.85f, 0.85f, 0.80f) * 0.5f;
            }
            else
            {
                // Crisp daylight
                zenithColor = new Color(0.09f, 0.24f, 0.44f);
                midSkyColor = new Color(0.32f, 0.50f, 0.72f);
                horizonColor = new Color(0.76f, 0.82f, 0.88f);
                groundHazeColor = new Color(0.38f, 0.42f, 0.45f);
                sunGlowColor = new Color(1.0f, 0.95f, 0.82f);
            }
            RebuildDomeColors();
            RenderSettings.fogColor = horizonColor;
        }

        void BuildSkyDome()
        {
            var mesh = new Mesh { name = "Procedural Sky Dome" };
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var indices = new List<int>();

            int rings = 18;
            int segments = 28;
            float radius = 1400f;

            for (int r = 0; r <= rings; r++)
            {
                float t = (float)r / rings;
                // Latitude from -15 deg (below horizon) to 90 deg (zenith)
                float lat = Mathf.Lerp(-15f * Mathf.Deg2Rad, 90f * Mathf.Deg2Rad, t);
                float sinLat = Mathf.Sin(lat);
                float cosLat = Mathf.Cos(lat);

                Color ringColor;
                if (sinLat < 0f)
                {
                    float factor = Mathf.Clamp01(-sinLat / Mathf.Sin(15f * Mathf.Deg2Rad));
                    ringColor = Color.Lerp(horizonColor, groundHazeColor, factor);
                }
                else if (sinLat < 0.25f)
                {
                    float factor = sinLat / 0.25f;
                    ringColor = Color.Lerp(horizonColor, midSkyColor, Mathf.Pow(factor, 0.75f));
                }
                else
                {
                    float factor = (sinLat - 0.25f) / 0.75f;
                    ringColor = Color.Lerp(midSkyColor, zenithColor, Mathf.Pow(factor, 0.85f));
                }

                for (int s = 0; s <= segments; s++)
                {
                    float lon = (float)s / segments * Mathf.PI * 2f;
                    Vector3 pos = new Vector3(Mathf.Cos(lon) * cosLat, sinLat, Mathf.Sin(lon) * cosLat) * radius;
                    vertices.Add(pos);
                    colors.Add(ringColor);
                }
            }

            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int curr = r * (segments + 1) + s;
                    int next = curr + segments + 1;
                    // Inward facing triangles
                    indices.Add(curr);
                    indices.Add(curr + 1);
                    indices.Add(next);

                    indices.Add(next);
                    indices.Add(curr + 1);
                    indices.Add(next + 1);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();

            skyFilter = gameObject.AddComponent<MeshFilter>();
            skyFilter.sharedMesh = mesh;

            skyRenderer = gameObject.AddComponent<MeshRenderer>();
            var shader = Shader.Find("GeoSniper/AtmosphericSky") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            var skyMat = new Material(shader) { name = "AtmosphericSky_Material" };
            skyMat.renderQueue = 1000; // Background queue
            skyRenderer.sharedMaterial = skyMat;
            skyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            skyRenderer.receiveShadows = false;
        }

        void RebuildDomeColors()
        {
            if (skyFilter == null || skyFilter.sharedMesh == null) return;
            var mesh = skyFilter.sharedMesh;
            var vertices = mesh.vertices;
            var colors = new Color[vertices.Length];

            for (int i = 0; i < vertices.Length; i++)
            {
                float sinLat = vertices[i].normalized.y;
                if (sinLat < 0f)
                {
                    float factor = Mathf.Clamp01(-sinLat / Mathf.Sin(15f * Mathf.Deg2Rad));
                    colors[i] = Color.Lerp(horizonColor, groundHazeColor, factor);
                }
                else if (sinLat < 0.25f)
                {
                    float factor = sinLat / 0.25f;
                    colors[i] = Color.Lerp(horizonColor, midSkyColor, Mathf.Pow(factor, 0.75f));
                }
                else
                {
                    float factor = (sinLat - 0.25f) / 0.75f;
                    colors[i] = Color.Lerp(midSkyColor, zenithColor, Mathf.Pow(factor, 0.85f));
                }
            }
            mesh.colors = colors;
        }

        void BuildSunDisc()
        {
            var sunObj = new GameObject("Sun Disc");
            sunObj.transform.SetParent(transform, false);
            sunDiscTransform = sunObj.transform;

            var mesh = new Mesh { name = "SunDisc_Mesh" };
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();

            int segments = 24;
            float radius = 55f;

            verts.Add(Vector3.zero);
            colors.Add(sunGlowColor * 1.5f);

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
                colors.Add(new Color(sunGlowColor.r, sunGlowColor.g, sunGlowColor.b, 0f));
            }

            for (int i = 1; i <= segments; i++)
            {
                tris.Add(0);
                tris.Add(i);
                tris.Add(i + 1);
            }

            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            var mf = sunObj.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = sunObj.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Mobile/Particles/Alpha Blended");
            var mat = new Material(shader) { name = "SunDisc_Material" };
            mat.renderQueue = 1001;
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
    }
}
