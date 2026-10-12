using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace GeoSniper
{
    // Opt-in material study: preserve authored maps and separate soft fabric from metal.
    public sealed class StreetStudyPresentation : MonoBehaviour
    {
        readonly List<Material> owned=new List<Material>();
        Texture2D metalFinish;
        Texture2D coatedAlbedo,polymerAlbedo;
        Texture2D fabricNormal,coatingNormal;
        Texture2D FinishNormal(bool cloth)
        {
            var found=cloth?fabricNormal:coatingNormal;if(found!=null)return found;
            var texture=new Texture2D(128,128,TextureFormat.RGBA32,true,true){name=cloth?"Fine fabric weave normal":"Coating micrograin normal",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};
            var pixels=new Color[128*128];
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                float nx=cloth?Mathf.Sin(x*Mathf.PI*.5f)*.12f:(Mathf.PerlinNoise(x*.43f+3,y*.43f+17)-.5f)*.075f;
                float ny=cloth?Mathf.Sin(y*Mathf.PI*.5f)*.12f:(Mathf.PerlinNoise(x*.47f+11,y*.47f+7)-.5f)*.075f;
                var normal=new Vector3(nx,ny,1).normalized;pixels[y*128+x]=new Color(normal.x*.5f+.5f,normal.y*.5f+.5f,normal.z*.5f+.5f,1);
            }
            texture.SetPixels(pixels);texture.Apply(true,true);if(cloth)fabricNormal=texture;else coatingNormal=texture;return texture;
        }
        Texture2D SurfaceGrain(bool polymer)
        {
            var existing=polymer?polymerAlbedo:coatedAlbedo;if(existing!=null)return existing;
            var texture=new Texture2D(128,128,TextureFormat.RGBA32,true){name=polymer?"Moulded stock grain":"Phosphate coating grain",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};
            var pixels=new Color[128*128];
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                float grain=Mathf.PerlinNoise(x*.73f+7,y*.73f+19);
                float machining=Mathf.PerlinNoise(x*.07f+13,y*1.1f);
                float tone=polymer?.60f+grain*.30f:.66f+grain*.16f+machining*.10f;
                pixels[y*128+x]=new Color(tone,tone,tone,1);
            }
            texture.SetPixels(pixels);texture.Apply(true,true);
            if(polymer)polymerAlbedo=texture;else coatedAlbedo=texture;return texture;
        }
        Texture2D MetalFinish()
        {
            if(metalFinish!=null)return metalFinish;
            metalFinish=new Texture2D(128,128,TextureFormat.RGBA32,true,true){name="Street study phosphate metal roughness",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};
            var pixels=new Color[128*128];
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                float grain=Mathf.PerlinNoise(x*.61f+3,y*.61f+17);
                float wear=Mathf.PerlinNoise(x*.035f,y*.035f+8);
                float machining=Mathf.PerlinNoise(x*.08f+21,y*.8f);
                pixels[y*128+x]=new Color(.45f+wear*.16f,0,0,.15f+grain*.07f+machining*.04f);
            }
            metalFinish.SetPixels(pixels);metalFinish.Apply(true,true);return metalFinish;
        }
        IEnumerator Start()
        {
            yield return new WaitForSeconds(2);
            var finishes=new Dictionary<Material,Material>();
            foreach(var renderer in GetComponentsInChildren<Renderer>())
            {
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++)
                {
                    var source=slots[i];if(source==null || !source.HasProperty("_Glossiness"))continue;
                    if(!finishes.TryGetValue(source,out var material))
                    {
                        string label=(source.name+" "+renderer.name).ToLowerInvariant();
                        bool fabric=label.Contains("glove") || label.Contains("sleeve") || label.Contains("arms") || label.Contains("hands");
                        bool soft=fabric || label.Contains("stock") || label.Contains("grip") || label.Contains("rubber") || label.Contains("wood");
                        bool glass=label.Contains("glass") || label.Contains("lens") || label.Contains("optic");
                        material=new Material(source){name="Street study / "+source.name};owned.Add(material);
                        if(!glass)
                        {
                            material.SetFloat("_Glossiness",fabric?.14f:soft?.22f:.36f);
                            if(material.HasProperty("_GlossMapScale"))material.SetFloat("_GlossMapScale",fabric?.18f:.55f);
                            if(fabric && material.HasProperty("_Metallic")){material.DisableKeyword("_METALLICGLOSSMAP");material.SetFloat("_Metallic",0);}
                            if(!fabric && source.mainTexture==null)
                            {
                                material.mainTexture=SurfaceGrain(soft);material.mainTextureScale=Vector2.one*4;
                                if(soft && material.HasProperty("_Metallic"))material.SetFloat("_Metallic",.02f);
                            }
                            if(!soft && material.HasProperty("_Metallic") && material.GetFloat("_Metallic")>.2f && material.HasProperty("_MetallicGlossMap") && material.GetTexture("_MetallicGlossMap")==null)
                            {
                                material.SetTexture("_MetallicGlossMap",MetalFinish());material.EnableKeyword("_METALLICGLOSSMAP");
                                material.SetFloat("_GlossMapScale",1);
                            }
                            if(material.HasProperty("_BumpScale"))material.SetFloat("_BumpScale",fabric?1.1f:.8f);
                            bool cloth=label.Contains("glove") || label.Contains("sleeve");
                            if(material.HasProperty("_BumpMap") && material.GetTexture("_BumpMap")==null && (cloth || (!fabric && source.mainTexture==null)))
                            {
                                material.SetTexture("_BumpMap",FinishNormal(cloth));material.SetTextureScale("_BumpMap",Vector2.one*(cloth?6:4));material.EnableKeyword("_NORMALMAP");
                                if(material.HasProperty("_BumpScale"))material.SetFloat("_BumpScale",cloth?.55f:.6f);
                            }
                        }
                        finishes[source]=material;
                    }
                    slots[i]=material;
                }
                renderer.sharedMaterials=slots;
            }
        }
        void OnDestroy(){foreach(var material in owned)if(material!=null)Destroy(material);if(metalFinish!=null)Destroy(metalFinish);if(coatedAlbedo!=null)Destroy(coatedAlbedo);if(polymerAlbedo!=null)Destroy(polymerAlbedo);if(fabricNormal!=null)Destroy(fabricNormal);if(coatingNormal!=null)Destroy(coatingNormal);}
    }
}
