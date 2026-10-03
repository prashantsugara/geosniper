using UnityEngine;

namespace GeoSniper
{
    public sealed partial class SectorWorld
    {
        static readonly Material[] barrierMaterials = new Material[2];
        void RoadBarrier(Vector3 position, Vector3 direction, bool yellow, Material fallback)
        {
            string name = yellow ? "BarrierYellow" : "BarrierOrange";
            var prefab = Resources.Load<GameObject>("Models/StreetProps/" + name);
            if (prefab == null)
            {
                var box = Box("Concrete Jersey Barrier", position + Vector3.up * .475f, new Vector3(.6f,.95f,2.4f), fallback);
                box.transform.localRotation = Quaternion.LookRotation(direction);
                return;
            }
            var root = new GameObject("Textured Concrete Barrier");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.LookRotation(direction);
            var visual = new GameObject("Barrier visual");
            visual.transform.SetParent(root.transform, false);
            Instantiate(prefab, visual.transform, false);
            var bounds = ImportedVisual.LocalBounds(visual.transform);
            if (bounds.size.x > .001f && bounds.size.y > .001f && bounds.size.z > .001f)
            {
                var scale = new Vector3(.6f/bounds.size.x,.95f/bounds.size.y,2.4f/bounds.size.z);
                visual.transform.localScale = scale;
                visual.transform.localPosition = -Vector3.Scale(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z),scale);
            }
            int index = yellow ? 1 : 0;
            if (barrierMaterials[index] == null)
            {
                var material = new Material(Shader.Find("Standard"));
                material.name = name + " concrete";
                material.mainTexture = Resources.Load<Texture2D>("Models/StreetProps/" + name + "_albedo");
                material.SetTexture("_BumpMap",Resources.Load<Texture2D>("Models/StreetProps/Barrier_normal"));
                material.EnableKeyword("_NORMALMAP");
                material.SetFloat("_Glossiness",.12f);
                material.enableInstancing = true;
                barrierMaterials[index] = material;
            }
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = barrierMaterials[index];
            var collision = root.AddComponent<BoxCollider>();
            collision.center = Vector3.up * .475f;
            collision.size = new Vector3(.6f,.95f,2.4f);
        }
    }
}
