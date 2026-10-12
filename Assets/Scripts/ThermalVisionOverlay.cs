using System.Collections.Generic;
using GeoSniper.Duel;
using UnityEngine;
using UnityEngine.Rendering;

namespace GeoSniper
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ThermalVisionOverlay : MonoBehaviour
    {
        static readonly int ScopeBlendId = Shader.PropertyToID("_ScopeBlend");
        static readonly int AspectId = Shader.PropertyToID("_Aspect");
        const CameraEvent RenderEvent = CameraEvent.AfterForwardOpaque;
        readonly List<Renderer> renderers = new List<Renderer>(32);
        readonly Plane[] frustum = new Plane[6];
        Camera targetCamera;
        Material heatMaterial;
        CommandBuffer commands;
        DuelOpponentAvatar rival;
        bool attached;
        bool rivalWasDead;
        float refreshAt;

        public static ThermalVisionOverlay Ensure(Camera camera)
        {
            if (camera == null) return null;
            return camera.GetComponent<ThermalVisionOverlay>() ?? camera.gameObject.AddComponent<ThermalVisionOverlay>();
        }

        public void SetScopeState(bool thermal, float scopeBlend, DuelOpponentAvatar realRival)
        {
            if (targetCamera == null) targetCamera = GetComponent<Camera>();
            // This game uses the built-in forward renderer. An unsupported shader
            // or a future render pipeline should leave the ordinary view intact.
            if (!thermal || scopeBlend <= .05f || targetCamera == null || GraphicsSettings.currentRenderPipeline != null)
            {
                Detach();
                return;
            }
            if (heatMaterial == null)
            {
                var shader = Resources.Load<Shader>("Shaders/ThermalSignature") ?? Shader.Find("GeoSniper/ThermalSignature");
                if (shader == null || !shader.isSupported) return;
                heatMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            }
            heatMaterial.SetFloat(ScopeBlendId, Mathf.Clamp01(scopeBlend));
            heatMaterial.SetFloat(AspectId, (float)targetCamera.pixelWidth / Mathf.Max(1, targetCamera.pixelHeight));
            bool rivalDead = realRival == null || realRival.IsDead;
            if (rival != realRival || rivalWasDead != rivalDead)
            {
                rival = realRival;
                rivalWasDead = rivalDead;
                refreshAt = 0;
            }
            if (commands == null) commands = new CommandBuffer { name = "Visible human thermal signatures" };
            if (!attached)
            {
                targetCamera.AddCommandBuffer(RenderEvent, commands);
                attached = true;
                refreshAt = 0;
            }
            if (Time.unscaledTime >= refreshAt) RefreshTargets();
        }

        void OnPreCull()
        {
            if (!attached || heatMaterial == null || targetCamera == null) return;
            // Resolve the actual render dimensions after targetTexture or a
            // camera viewport changes, including standalone visual captures.
            heatMaterial.SetFloat(AspectId, (float)targetCamera.pixelWidth / Mathf.Max(1, targetCamera.pixelHeight));
            if (Time.unscaledTime >= refreshAt) RefreshTargets();
        }

        void RefreshTargets()
        {
            refreshAt = Time.unscaledTime + .1f;
            commands.Clear();
            // Preserve the current camera color AND depth attachment. Drawing
            // the animated meshes with ZTest LEqual hides heat behind cover.
            // The shader clips fragments to the circular optic, not a GUI box.
            GeometryUtility.CalculateFrustumPlanes(targetCamera, frustum);
            int drawCount = 0;
            if (rival != null && !rival.IsDead && rival.gameObject.activeInHierarchy)
                AddHuman(rival.transform, ref drawCount);
            var bots = EnemyBot.AllBots;
            if (bots == null) return;
            for (int i = 0; i < bots.Count && drawCount < 64; i++)
            {
                var bot = bots[i];
                if (bot == null || bot.Actor == null || bot.Actor.IsDead || !bot.gameObject.activeInHierarchy) continue;
                AddHuman(bot.transform, ref drawCount);
            }
        }

        void AddHuman(Transform human, ref int drawCount)
        {
            var visual = human.Find("Character visual") ?? human.Find("Visual") ?? human.Find("Fallback duel operative") ?? human;
            renderers.Clear();
            visual.GetComponentsInChildren(false, renderers);
            for (int i = 0; i < renderers.Count && drawCount < 64; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy
                    || !GeometryUtility.TestPlanesAABB(frustum, renderer.bounds)) continue;
                if ((targetCamera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh
                    : renderer is MeshRenderer ? renderer.GetComponent<MeshFilter>()?.sharedMesh : null;
                if (mesh == null) continue;
                for (int submesh = 0; submesh < mesh.subMeshCount && drawCount < 64; submesh++)
                {
                    commands.DrawRenderer(renderer, heatMaterial, submesh, 0);
                    drawCount++;
                }
            }
        }

        void Detach()
        {
            if (attached && targetCamera != null && commands != null) targetCamera.RemoveCommandBuffer(RenderEvent, commands);
            attached = false;
            commands?.Clear();
        }

        void OnDisable() => Detach();

        void OnDestroy()
        {
            Detach();
            commands?.Release();
            commands = null;
            if (heatMaterial == null) return;
            if (Application.isPlaying) Destroy(heatMaterial);
            else DestroyImmediate(heatMaterial);
        }
    }
}
