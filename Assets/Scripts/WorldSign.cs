using UnityEngine;
using UnityEngine.Rendering;

namespace GeoSniper
{
    public sealed class WorldSign : MonoBehaviour
    {
        public string Caption { get; private set; }
        public Vector2 PanelSize { get; private set; }
        public const float Thickness = .16f;
        Font font;
        Material ink;
        TextMesh[] faces;
        bool needsFit;

        public static WorldSign Create(Transform parent, string caption, Vector3 position, Quaternion rotation,
            Vector2 size, Material panel, bool twoSided = true)
        {
            var root = new GameObject("Sign - " + caption.Replace('\n', ' '));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = rotation;
            var sign = root.AddComponent<WorldSign>();
            sign.Caption = caption;
            sign.PanelSize = size;
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Sign panel";
            board.transform.SetParent(root.transform, false);
            board.transform.localScale = new Vector3(size.x, size.y, Thickness);
            board.GetComponent<Collider>().enabled = false;
            board.GetComponent<Renderer>().sharedMaterial = panel;
            sign.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var shader = Resources.Load<Shader>("Shaders/RoadSignText");
            sign.ink = new Material(shader != null ? shader : sign.font.material.shader);
            sign.ink.color = new Color(1f, .96f, .79f);
            sign.faces = new TextMesh[twoSided ? 2 : 1];
            for (int i = 0; i < sign.faces.Length; i++)
            {
                var label = new GameObject(i == 0 ? "Front text" : "Back text");
                label.transform.SetParent(root.transform, false);
                // An unscaled root keeps both faces outside panels of any width or thickness.
                label.transform.localPosition = Vector3.forward * (i == 0 ? 1 : -1) * (Thickness / 2 + .025f);
                label.transform.localRotation = Quaternion.Euler(0, i == 0 ? 180 : 0, 0);
                var text = label.AddComponent<TextMesh>();
                text.font = sign.font;
                text.fontSize = 64;
                text.fontStyle = FontStyle.Bold;
                text.characterSize = .2f;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.richText = false;
                text.text = caption;
                text.color = sign.ink.color;
                var renderer = text.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = sign.ink;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                sign.faces[i] = text;
            }
            Font.textureRebuilt += sign.RefreshAtlas;
            sign.font.RequestCharactersInTexture(caption, 64, FontStyle.Bold);
            sign.RefreshAtlas(sign.font);
            sign.FitText();
            return sign;
        }

        void RefreshAtlas(Font changed)
        {
            if (changed != font || ink == null) return;
            ink.mainTexture = font.material.mainTexture;
            needsFit = true;
        }

        public void FitText()
        {
            foreach (var text in faces)
            {
                Vector3 size = text.GetComponent<MeshRenderer>().localBounds.size;
                if (size.x < .001f || size.y < .001f) { needsFit = true; return; }
                float fit = Mathf.Min((PanelSize.x - .3f) / size.x, (PanelSize.y - .22f) / size.y);
                text.transform.localScale = Vector3.one * fit;
            }
            needsFit = false;
        }

        void LateUpdate() { if (needsFit) FitText(); }
        void OnDestroy()
        {
            Font.textureRebuilt -= RefreshAtlas;
            if (ink != null) { if (Application.isPlaying) Destroy(ink); else DestroyImmediate(ink); }
        }
    }
}
