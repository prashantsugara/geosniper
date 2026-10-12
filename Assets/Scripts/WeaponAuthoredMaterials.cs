using System.Collections.Generic;
using UnityEngine;
namespace GeoSniper
{
    public static class WeaponAuthoredMaterials
    {
        static readonly Dictionary<string,Material> cache=new Dictionary<string,Material>();
        public static void Apply(Transform model)
        {
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++)
                {
                    var source=slots[i];
                    if(source==null || (!source.name.StartsWith("EagleCG_") && !source.name.StartsWith("M4CG_") && !source.name.StartsWith("MP5CG_") && !source.name.StartsWith("M40CG_") && !source.name.StartsWith("SVDCG_"))) continue;
                    string key=source.name.Replace(" (Instance)","");
                    if(!cache.TryGetValue(key,out var material))
                    {
                        string path=key.StartsWith("EagleCG_")?"Models/Weapons/Authored/Eagle/"+key.Substring(8)
                            :key.StartsWith("MP5CG_")?"Models/Weapons/Authored/MP5/"+key.Substring(6)
                            :key.StartsWith("M40CG_")?"Models/Weapons/Authored/M40/"+key.Substring(6)
                            :key.StartsWith("SVDCG_")?"Models/Weapons/Authored/M40/"+(key.EndsWith("Wood")?"Rust":"Scope"):"Models/Weapons/Authored/M4A1/T_M4A1";
                        var albedo=Resources.Load<Texture2D>(path+"_BaseColor");
                        if(albedo==null) continue;
                        material=new Material(Shader.Find("Standard")){name=key,mainTexture=albedo,color=Color.white};
                        material.SetTexture("_MetallicGlossMap",Resources.Load<Texture2D>(path+"_MetallicSmoothness"));
                        if(material.GetTexture("_MetallicGlossMap")!=null) material.EnableKeyword("_METALLICGLOSSMAP");
                        material.SetFloat("_GlossMapScale",.95f);
                        material.SetTexture("_BumpMap",Resources.Load<Texture2D>(path+"_Normal"));material.EnableKeyword("_NORMALMAP");
                        cache.Add(key,material);
                    }
                    slots[i]=material;
                }
                renderer.sharedMaterials=slots;
            }
        }
        static readonly Dictionary<string,Material> finishes=new Dictionary<string,Material>();
        public static void ApplyFinish(Transform model)
        {
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer is SkinnedMeshRenderer) continue; // Imported first-person arms keep their skin/cloth.
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++)
                {
                    var source=slots[i];
                    if(source==null || source.name.StartsWith("WeaponFinish/")) continue;
                    string surface=(renderer.name+" "+source.name).ToLowerInvariant();
                    bool glass=surface.Contains("glass") || surface.Contains("lens");
                    bool soft=surface.Contains("wood") || surface.Contains("rubber") || surface.Contains("stock") || surface.Contains("grip") || surface.Contains("cheek") || surface.Contains("sniper body") || surface.Contains("sniper_body") || surface.Contains("second color") || surface.Contains("second_color");
                    string kind=glass?"glass":soft?"stock":"metal";
                    string key=source.GetEntityId()+"/"+kind;
                    if(!finishes.TryGetValue(key,out var material))
                    {
                        material=new Material(source){name="WeaponFinish/"+source.name+"/"+kind};
                        // Imported diffuse shaders have no specular response. Keep the texture while enabling PBR.
                        if(!material.HasProperty("_Metallic")) material.shader=Shader.Find("Standard");
                        bool mapped=material.HasProperty("_MetallicGlossMap") && material.GetTexture("_MetallicGlossMap")!=null;
                        if(mapped)
                        {
                            material.EnableKeyword("_METALLICGLOSSMAP");
                            material.SetFloat("_GlossMapScale",.95f);
                        }
                        else
                        {
                            material.DisableKeyword("_METALLICGLOSSMAP");
                            material.SetFloat("_Metallic",soft?0f:glass?.9f:.88f);
                            material.SetFloat("_Glossiness",soft?.24f:glass?.95f:.68f);
                            if(source.mainTexture==null)
                                material.color=glass?new Color(.035f,.14f,.22f):soft?(surface.Contains("wood")?new Color(.28f,.16f,.08f):new Color(.16f,.18f,.13f)):new Color(.22f,.25f,.28f);
                        }
                        finishes.Add(key,material);
                    }
                    slots[i]=material;
                }
                renderer.sharedMaterials=slots;
                renderer.reflectionProbeUsage=UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
            }
        }
    }
}
