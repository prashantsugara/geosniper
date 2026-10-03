using UnityEngine;

namespace GeoSniper
{
    public static class ImportedVisual
    {
        public static Bounds RendererBounds(Transform space,Renderer renderer)
        {
            var filter=renderer.GetComponent<MeshFilter>();
            var local=filter!=null && filter.sharedMesh!=null;
            var source=local?filter.sharedMesh.bounds:renderer.bounds;
            var matrix=local?space.worldToLocalMatrix*renderer.transform.localToWorldMatrix:space.worldToLocalMatrix;
            var result=new Bounds(matrix.MultiplyPoint3x4(source.min),Vector3.zero);
            for(int i=0;i<8;i++) result.Encapsulate(matrix.MultiplyPoint3x4(source.center+Vector3.Scale(source.extents,
                new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
            return result;
        }
        public static void SanitizeWeaponModel(Transform root)
        {
            if (root == null) return;
            // Render-only weapons must never block player movement, camera rays, or their own shots.
            foreach(var collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                string n = r.gameObject.name.ToLowerInvariant();
                var mf = r.GetComponent<MeshFilter>();
                string mName = (mf != null && mf.sharedMesh != null) ? mf.sharedMesh.name.ToLowerInvariant() : "";
                if (n.Contains("icosphere") || n.Contains("psphere") || n == "sphere" || n.StartsWith("sphere")
                    || mName.Contains("icosphere") || mName.Contains("psphere") || mName == "sphere" || mName.StartsWith("sphere"))
                {
                    r.gameObject.SetActive(false);
                    if (Application.isPlaying) Object.Destroy(r.gameObject);
                    else Object.DestroyImmediate(r.gameObject);
                }
            }
        }

        public static Bounds AlignVehicle(GameObject rootObj, GameObject visual, bool isTank, bool isPolice)
        {
            AssetCalibration.Apply(visual.transform);

            var bounds = LocalBounds(rootObj.transform);
            return bounds;
        }

        public static GameObject CharacterAnimationRoot(Transform visual)
        {
            var placement = visual.Find("Model placement");
            if (placement != null && placement.childCount > 0) return placement.GetChild(0).gameObject;
            var skin = visual.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin == null || skin.rootBone == null) return visual.gameObject;
            Transform root = skin.transform;
            while (root.parent != null && !skin.rootBone.IsChildOf(root)) root = root.parent;
            return root.gameObject;
        }

        public static GameObject CreateEnemy(GameObject asset,Transform parent)
        {
            var wrapper=new GameObject("Character visual"); wrapper.transform.SetParent(parent,false);
            // Keep normalization outside the animated FBX hierarchy. Clips can
            // overwrite the imported root's position and scale when sampled.
            var placement=new GameObject("Model placement");
            placement.transform.SetParent(wrapper.transform,false);
            var visual = Object.Instantiate(asset,placement.transform,false);
            if(AssetCalibration.TryGet(asset.name,out var calibration))
                placement.transform.localRotation=Quaternion.Euler(calibration.rotation);
            foreach(var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.updateWhenOffscreen=true;

            var animation=wrapper.GetComponentInChildren<Animation>();
            if(animation!=null)
            {
                animation.playAutomatically=false; animation.cullingType=AnimationCullingType.AlwaysAnimate;
                foreach(AnimationState state in animation)
                    if(state.name.ToLowerInvariant().Contains("idle"))
                    { animation.Stop(); animation.Play(state.name); state.time=0; animation.Sample(); break; }
            }
            var bounds = PosedBounds(wrapper.transform);
            if (calibration == null && bounds.size.z > bounds.size.y * 1.15f)
            {
                // Blender FBX Z-Up auto-correction: character is lying flat on back
                placement.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f) * placement.transform.localRotation;
                bounds = PosedBounds(wrapper.transform);
            }
            float height = bounds.size.y;
            if (height > 0.001f && float.IsFinite(height))
            {
                placement.transform.localScale=Vector3.one*((calibration!=null && calibration.height>0?calibration.height:1.85f)/height);
                bounds = PosedBounds(wrapper.transform);
                placement.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }
            return wrapper;
        }
        public static Bounds PosedBounds(Transform root)
        {
            var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>();
            if(skins.Length==0) return LocalBounds(root);
            var bounds=new Bounds(); bool initialized=false;
            var baked=new Mesh();
            try
            {
                foreach(var skin in skins)
                {
                    if(skin.sharedMesh==null) continue;
                    // Compensate renderer scale before applying localToWorld below.
                    // Without compensation the TF2 civilian's parent scale is applied twice.
                    skin.BakeMesh(baked,true);
                    var matrix=root.worldToLocalMatrix*skin.transform.localToWorldMatrix;
                    foreach(var vertex in baked.vertices)
                    {
                        Vector3 point=matrix.MultiplyPoint3x4(vertex);
                        if(!initialized) { bounds=new Bounds(point,Vector3.zero); initialized=true; } else bounds.Encapsulate(point);
                    }
                    var culling=baked.bounds;
                    culling.Expand(culling.size.magnitude*.65f);
                    skin.localBounds=culling;
                }
            }
            finally { if(Application.isPlaying) Object.Destroy(baked); else Object.DestroyImmediate(baked); }
            return bounds;
        }
        public static Bounds LocalBounds(Transform root)
        {
            var bounds=new Bounds(); bool initialized=false;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if(!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                // Static renderer world bounds can be stale immediately after
                // spawning/scaling. Mesh bounds plus the current transform are
                // reliable before the first render, even for centimetre assets.
                var filter=renderer.GetComponent<MeshFilter>();
                bool staticMesh=renderer is MeshRenderer && filter!=null && filter.sharedMesh!=null;
                var world=staticMesh?filter.sharedMesh.bounds:renderer.bounds;
                var toRoot=staticMesh?root.worldToLocalMatrix*renderer.transform.localToWorldMatrix:root.worldToLocalMatrix;
                var corners=new[]{
                    new Vector3(world.min.x,world.min.y,world.min.z), new Vector3(world.min.x,world.min.y,world.max.z),
                    new Vector3(world.min.x,world.max.y,world.min.z), new Vector3(world.min.x,world.max.y,world.max.z),
                    new Vector3(world.max.x,world.min.y,world.min.z), new Vector3(world.max.x,world.min.y,world.max.z),
                    new Vector3(world.max.x,world.max.y,world.min.z), new Vector3(world.max.x,world.max.y,world.max.z)};
                foreach(var corner in corners)
                {
                    var point=toRoot.MultiplyPoint3x4(corner);
                    if(!initialized) { bounds=new Bounds(point,Vector3.zero); initialized=true; }
                    else bounds.Encapsulate(point);
                }
            }
            if(initialized) return bounds;
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if(filter.sharedMesh==null) continue;
                var mesh=filter.sharedMesh.bounds;
                var matrix=root.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                for(int i=0;i<8;i++)
                {
                    var corner=mesh.center+Vector3.Scale(mesh.extents,
                        new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var point=matrix.MultiplyPoint3x4(corner);
                    if(!initialized) { bounds=new Bounds(point,Vector3.zero); initialized=true; }
                    else bounds.Encapsulate(point);
                }
            }
            if(!initialized) throw new System.InvalidOperationException("Imported model has no mesh: "+root.name);
            return bounds;
        }

        public static void ConfigureCharacterSurfaces(Transform root)
        {
            // Per-renderer overrides retain atlas textures and shared material ownership.
            var block=new MaterialPropertyBlock();
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    var material=materials[i];
                    if(material==null)continue;
                    string name=(renderer.name+" "+material.name).ToLowerInvariant();
                    if(name.Contains("glass") || name.Contains("lens") || name.Contains("scar") || name.Contains("gun"))continue;
                    bool skin=name.Contains("skin") || name.Contains("head") || name.Contains("face");
                    bool leather=name.Contains("boot") || name.Contains("glove") || name.Contains("leather");
                    float smoothness=skin?.28f:leather?.24f:.14f;
                    block.Clear();renderer.GetPropertyBlock(block,i);
                    if(material.HasProperty("_Metallic"))block.SetFloat("_Metallic",0f);
                    if(material.HasProperty("_Glossiness"))block.SetFloat("_Glossiness",smoothness);
                    if(material.HasProperty("_Smoothness"))block.SetFloat("_Smoothness",smoothness);
                    renderer.SetPropertyBlock(block,i);
                }
            }
        }

        public static void ConfigurePBRModel(GameObject root, bool castShadows = true)
        {
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = true;
                if (r.sharedMaterials != null)
                {
                    foreach (var mat in r.sharedMaterials)
                    {
                        if (mat == null) continue;
                        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.65f);
                        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.35f);
                    }
                }
            }
        }

        public static GameObject CreateUrbanProp(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;
            var instance = Object.Instantiate(prefab, position, rotation, parent);
            instance.name = prefab.name;
            AssetCalibration.Apply(instance.transform);
            ConfigurePBRModel(instance, true);
            return instance;
        }
    }
}
