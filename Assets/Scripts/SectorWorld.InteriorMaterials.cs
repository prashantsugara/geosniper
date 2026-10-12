using UnityEngine;
namespace GeoSniper
{
    public sealed partial class SectorWorld
    {
        // Reuse one set across the entire sector instead of allocating per room.
        Material InteriorSurface(string name, Color tint, float smoothness=.15f, float metal=0)
        {
            string key="interior:"+name;
            if(styledCache.TryGetValue(key,out var existing))return existing;
            var surface=Material(tint,smoothness,metal);surface.name="Interior "+name;
            surface.mainTexture=Resources.Load<Texture2D>("Textures/Interiors/"+name);
            styledCache[key]=surface;return surface;
        }
        Material InteriorLamp()
        {
            const string key="interior:lamp";
            if(styledCache.TryGetValue(key,out var existing))return existing;
            var surface=Material(new Color(.93f,.91f,.79f),.25f,0);
            surface.name="Warm ceiling diffuser";
            surface.EnableKeyword("_EMISSION");surface.SetColor("_EmissionColor",new Color(.55f,.49f,.32f));
            styledCache[key]=surface;return surface;
        }
        Material StreetStudyAccent(bool magenta)
        {
            string key=magenta?"study:magenta":"study:cyan";
            if(styledCache.TryGetValue(key,out var found))return found;
            var tint=magenta?new Color(.64f,.12f,.36f):new Color(.06f,.45f,.48f);
            var surface=Material(tint,.22f,0);surface.EnableKeyword("_EMISSION");surface.SetColor("_EmissionColor",tint*.9f);
            styledCache[key]=surface;return surface;
        }
        Material StreetStudyWindow(int style,bool shop=false)
        {
            string key="study:window:"+style+(shop?":shop":"");
            if(styledCache.TryGetValue(key,out var found))return found;
            var surface=new Material(Shader.Find(shop?"GeoSniper/StreetStudyShopGlass":"GeoSniper/RealisticBuilding") ?? Shader.Find("Standard"));
            surface.name="Recessed room glazing "+style;
            if(shop){surface.enableInstancing=true;materials.Add(surface);styledCache[key]=surface;return surface;}
            surface.mainTexture=MappedSurfaceTextures.WindowInteriors();
            surface.mainTextureScale=new Vector2(.25f,1);
            surface.mainTextureOffset=new Vector2((style%4)*.25f,0);
            surface.SetColor("_GlassTint",new Color(.10f,.13f,.16f));
            surface.SetFloat("_GlassGlossiness",.70f);
            surface.SetFloat("_GlassMetallic",.20f);
            surface.SetFloat("_WindowGrid",1);
            surface.SetFloat("_StreetStudy",1);
            surface.SetFloat("_WindowLightScale",style<3?.015f:style<5?.3f:style==5?.58f:style==6?.8f:1);
            surface.enableInstancing=true;
            materials.Add(surface);styledCache[key]=surface;return surface;
        }
    }
}
