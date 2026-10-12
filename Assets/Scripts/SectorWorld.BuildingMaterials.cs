using System.Collections.Generic;
using UnityEngine;
namespace GeoSniper
{
    public sealed partial class SectorWorld
    {
        readonly Dictionary<string,Material> facadeMaterialCache=new Dictionary<string,Material>();
        Material FacadeStyled(MapFeature feature,Color color,bool windows)
        {
            string style=MapFeatureStyle.ResolveFacadeMaterial(feature);
            bool scenery=MapFeatureStyle.Text(feature,"scenery_only")=="true";
            bool study=MapFeatureStyle.Text(feature,"street_study")=="true" || scenery;
            float storey=MapFeatureStyle.StoreyHeight(feature);
            string key=style+":"+storey.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+":"+ColorUtility.ToHtmlStringRGB(color)+(windows?":window":":plain")+(study?":study":"")+(scenery?":scenery":"");
            if(facadeMaterialCache.TryGetValue(key,out var found))return found;

            var shader=Shader.Find("GeoSniper/RealisticBuilding") ?? Shader.Find("Standard");
            var material=new Material(shader);
            material.color=color;
            material.SetColor("_Color",color);
            float gloss=style=="glass"?.95f:style=="metal"?.55f:.16f;
            float metallic=style=="metal"?.65f:style=="glass"?.85f:0f;
            material.SetFloat("_Glossiness",gloss);
            if(material.HasProperty("_StreetStudy")){material.SetFloat("_StreetStudy",study?1:0);material.SetFloat("_BumpScale",study?.32f:1);}
            material.SetFloat("_Metallic",metallic);
            material.SetFloat("_GlassGlossiness",.98f);
            material.SetFloat("_GlassMetallic",.88f);
            if(scenery)
            {
                material.SetFloat("_Glossiness",.14f);material.SetFloat("_Metallic",.05f);
                material.SetFloat("_GlassGlossiness",.6f);material.SetFloat("_GlassMetallic",.15f);
                material.SetFloat("_WindowLightScale",.45f);material.SetFloat("_BumpScale",.12f);
            }
            material.SetColor("_GlassTint",new Color(.05f,.09f,.14f,1f));
            material.SetColor("_EmissionColor",Color.black);
            if(material.HasProperty("_WindowGrid"))material.SetFloat("_WindowGrid",style=="glass"?8:2);
            material.enableInstancing=true;
            materials.Add(material);

            material.mainTexture=MappedSurfaceTextures.Facade(style,windows);
            if(!windows && style!="glass")
            {
                // A repeat covers physical metres, independently of window/floor UV spacing.
                Vector2 metres=style.Contains("brick")?new Vector2(2f,1.2f):
                    style.Contains("stone")?new Vector2(3f,2f):
                    (style.Contains("wood") || style.Contains("timber"))?new Vector2(2f,3f):new Vector2(4f,4f);
                material.mainTextureScale=new Vector2(8f/metres.x,2f*storey/metres.y);
                material.SetTexture("_BumpMap",BuildWallNormalTexture(style));
            }
            else if(style=="glass")
            {
                material.SetTexture("_BumpMap",BuildWallNormalTexture("glass"));
            }
            else
            {
                material.SetTexture("_BumpMap",BuildFacadeNormalTexture());
            }
            if(study)material.SetTextureScale("_BumpMap",material.mainTextureScale);
            material.EnableKeyword("_NORMALMAP");

            facadeMaterialCache[key]=material;
            return material;
        }
    }
}
