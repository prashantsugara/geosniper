using UnityEngine;

namespace GeoSniper
{
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class MobilePostProcess : MonoBehaviour
    {
        public static MobilePostProcess Instance { get; private set; }

        [Header("Bloom")]
        [Range(0.4f, 1.5f)] public float bloomThreshold = 0.82f;
        [Range(0.1f, 1.0f)] public float bloomSoftKnee = 0.5f;
        [Range(0f, 3f)] public float bloomIntensity = 1.15f;
        [Range(0.5f, 3f)] public float bloomSpread = 1.4f;

        [Header("Tonemapping & Color")]
        [Range(0.5f, 2.0f)] public float exposure = 1.04f;
        [Range(0.8f, 1.4f)] public float contrast = 1.06f;
        [Range(0.5f, 1.6f)] public float saturation = 1.10f;
        [Range(0f, 1f)] public float vignetteIntensity = 0.40f;

        [Header("Optic Scope")]
        [Range(0f, 1f)] public float scopeBlend = 0f;
        public float scopeRadius = 0.44f;

        Material postMaterial;
        Camera targetCam;

        public static MobilePostProcess Ensure(Camera cam)
        {
            if (cam == null) return null;
            var pp = cam.GetComponent<MobilePostProcess>() ?? cam.gameObject.AddComponent<MobilePostProcess>();
            return pp;
        }

        void OnEnable()
        {
            Instance = this;
            targetCam = GetComponent<Camera>();
            if (targetCam != null)
            {
                targetCam.allowHDR = true;
            }
            EnsureMaterial();
        }

        void EnsureMaterial()
        {
            if (postMaterial == null)
            {
                var shader = Shader.Find("GeoSniper/MobilePostProcess");
                if (shader != null)
                {
                    postMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                }
            }
        }

        void OnDisable()
        {
            if (postMaterial != null)
            {
                if (Application.isPlaying) Destroy(postMaterial);
                else DestroyImmediate(postMaterial);
                postMaterial = null;
            }
            if (Instance == this) Instance = null;
        }

        void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            EnsureMaterial();
            if (postMaterial == null)
            {
                Graphics.Blit(src, dest);
                return;
            }

            var preset = MobileGraphics.Selected;
            bool enableBloom = preset != MobileGraphicsPreset.Performance;
            int blurIterations = preset == MobileGraphicsPreset.High ? 2 : 1;

            float aspect = (float)src.width / Mathf.Max(1, src.height);
            postMaterial.SetVector("_ColorGrading", new Vector4(contrast, saturation, exposure, vignetteIntensity));
            postMaterial.SetVector("_ScopeParams", new Vector4(scopeBlend, scopeRadius, aspect, 0.06f));

            if (!enableBloom || bloomIntensity <= 0.01f)
            {
                postMaterial.SetVector("_BloomParams", new Vector4(bloomThreshold, bloomSoftKnee, 0f, 0f));
                Graphics.Blit(src, dest, postMaterial, 2);
                return;
            }

            // Downsampled bloom buffer (1/4 screen resolution)
            int bw = Mathf.Max(64, src.width / 4);
            int bh = Mathf.Max(64, src.height / 4);
            RenderTexture rt1 = RenderTexture.GetTemporary(bw, bh, 0, src.format);
            RenderTexture rt2 = RenderTexture.GetTemporary(bw, bh, 0, src.format);

            postMaterial.SetVector("_BloomParams", new Vector4(bloomThreshold, bloomSoftKnee, bloomIntensity, bloomSpread));

            // Pass 0: Threshold extract
            Graphics.Blit(src, rt1, postMaterial, 0);

            // Pass 1: Kawase blur
            for (int i = 0; i < blurIterations; i++)
            {
                postMaterial.SetVector("_BloomParams", new Vector4(bloomThreshold, bloomSoftKnee, bloomIntensity, bloomSpread * (1f + i * 0.75f)));
                Graphics.Blit(rt1, rt2, postMaterial, 1);
                var temp = rt1; rt1 = rt2; rt2 = temp;
            }

            // Pass 2: Final composite
            postMaterial.SetTexture("_BloomTex", rt1);
            Graphics.Blit(src, dest, postMaterial, 2);

            RenderTexture.ReleaseTemporary(rt1);
            RenderTexture.ReleaseTemporary(rt2);
        }
    }
}
