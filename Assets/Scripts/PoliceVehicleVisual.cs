using System.Collections.Generic;
using UnityEngine;
namespace GeoSniper
{
    public sealed class PoliceVehicleVisual : MonoBehaviour
    {
        readonly List<Material> owned=new List<Material>();
        public static void Apply(GameObject visual)
        {
            if(visual.GetComponent<PoliceVehicleVisual>()!=null)return;
            var owner=visual.AddComponent<PoliceVehicleVisual>();
            var surfaces=new Dictionary<string,Material>();
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++)
                {
                    string name=slots[i]==null?"PoliceBlue":slots[i].name;
                    if(!surfaces.TryGetValue(name,out var material))
                    {
                        Color color=name.Contains("White")?new Color(.9f,.93f,.97f):name.Contains("Glass")?new Color(.055f,.16f,.23f):
                            name.Contains("Rubber")?new Color(.025f,.028f,.035f):name.Contains("LightRed")?new Color(.95f,.015f,.02f):
                            name.Contains("LightBlue")?new Color(.025f,.18f,1):name.Contains("Chrome")?new Color(.55f,.6f,.66f):new Color(.025f,.12f,.44f);
                        material=new Material(Shader.Find("Standard")){name=name,color=color};
                        material.SetFloat("_Glossiness",name.Contains("Rubber")?.12f:.65f);
                        material.SetFloat("_Metallic",name.Contains("Chrome")?.8f:0);
                        if(name.Contains("Light")){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*.55f);}
                        surfaces[name]=material;owner.owned.Add(material);
                    }
                    slots[i]=material;
                }
                renderer.sharedMaterials=slots;
            }
        }
        void OnDestroy(){foreach(var material in owned)if(material!=null)Destroy(material);}
    }
}
