using UnityEngine;
namespace GeoSniper
{
    public sealed partial class ReferenceStronghold
    {
        readonly System.Collections.Generic.Dictionary<string,Material> licensedSiteMaterials=new System.Collections.Generic.Dictionary<string,Material>();
        // Source mesh keys are retained by the export, independent of FBX material names.
        GameObject LicensedSiteAsset(string model,Vector3 at,bool collision=false,float yaw=0)
        {
            var source=Resources.Load<GameObject>("Models/LevelSites/"+model);
            if(source==null){Debug.LogError("Missing licensed location asset: "+model);return null;}
            var host=new GameObject("Sketchfab "+model);host.transform.SetParent(transform,false);
            host.transform.localPosition=at;host.transform.localRotation=Quaternion.Euler(0,yaw,0);
            // Keep the FBX import's axis conversion below a Unity-space placement root.
            var obj=Instantiate(source,host.transform,false);
            foreach(var renderer in obj.GetComponentsInChildren<MeshRenderer>())
            {
                var match=System.Text.RegularExpressions.Regex.Match(renderer.name,System.Text.RegularExpressions.Regex.Escape(model)+"_[0-9]+");
                // Unity renames a single-mesh FBX renderer to its file name.
                string key=match.Success?match.Value:model+"_0";
                if(!licensedSiteMaterials.TryGetValue(key,out var material))
                {
                material=new Material(Shader.Find("Standard")){name="Licensed "+key,color=Color.white,enableInstancing=true};surfaces.Add(material);licensedSiteMaterials.Add(key,material);
                material.mainTexture=Resources.Load<Texture2D>("Models/LevelSites/"+key+"_albedo");material.SetFloat("_Glossiness",.15f);
                var normal=Resources.Load<Texture2D>("Models/LevelSites/"+key+"_normal");
                if(normal!=null){material.SetTexture("_BumpMap",normal);material.SetFloat("_BumpScale",.55f);material.EnableKeyword("_NORMALMAP");}
                var packed=Resources.Load<Texture2D>("Models/LevelSites/"+key+"_metallic");
                if(packed!=null){material.SetTexture("_MetallicGlossMap",packed);material.SetFloat("_GlossMapScale",.7f);material.EnableKeyword("_METALLICGLOSSMAP");}
                }
                renderer.sharedMaterial=material;
                // Guy lines and small stakes are visual detail, rather than snagging touch movement.
                if(collision && !(model=="MilitaryTent" && (key=="MilitaryTent_0" || key=="MilitaryTent_1"))){var collider=renderer.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=renderer.GetComponent<MeshFilter>().sharedMesh;}
            }
            return host;
        }
        void LicensedStreetProp(string model,Vector3 at,float height,float yaw=0)
        {
            var source=Resources.Load<GameObject>("Models/CGStreetProps/PBR_Prefabs/"+model);if(source==null)return;
            var obj=Instantiate(source,transform,false);obj.name="CGTrader "+model;obj.transform.localPosition=Vector3.zero;
            foreach(var collider in obj.GetComponentsInChildren<Collider>())collider.enabled=false;
            obj.transform.Rotate(Vector3.up,yaw,Space.Self);
            var renderers=obj.GetComponentsInChildren<Renderer>();if(renderers.Length==0){Destroy(obj);return;}
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            float ratio=height/Mathf.Max(.01f,bounds.size.y);obj.transform.localScale*=ratio;
            // Fit using world bounds, including imported root scale. Keep source PBR/LOD.
            bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            obj.transform.position+=transform.TransformPoint(at)-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        }
    }
}
