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
            float storey=MapFeatureStyle.StoreyHeight(feature);
            string key=style+":"+storey.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+":"+ColorUtility.ToHtmlStringRGB(color)+(windows?":window":":plain");
            if(facadeMaterialCache.TryGetValue(key,out var found))return found;

            var shader=Shader.Find("GeoSniper/RealisticBuilding") ?? Shader.Find("Standard");
            var material=new Material(shader);
            material.color=color;
            material.SetColor("_Color",color);
            float gloss=style=="glass"?.95f:style=="metal"?.55f:.16f;
            float metallic=style=="metal"?.65f:style=="glass"?.85f:0f;
            material.SetFloat("_Glossiness",gloss);
            material.SetFloat("_Metallic",metallic);
            material.SetFloat("_GlassGlossiness",.98f);
            material.SetFloat("_GlassMetallic",.88f);
            material.SetColor("_GlassTint",new Color(.05f,.09f,.14f,1f));
            material.SetColor("_EmissionColor",Color.black);
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
            material.EnableKeyword("_NORMALMAP");

            facadeMaterialCache[key]=material;
            return material;
        }
    }
}
