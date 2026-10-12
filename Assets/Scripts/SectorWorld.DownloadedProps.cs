using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    public sealed partial class SectorWorld
    {
        readonly Dictionary<string,Material> streetPropSurfaces=new Dictionary<string,Material>();
        void AddDownloadedStreetProps(Transform site,int kind)
        {
            DownloadedStreetProp(site,"StreetBin",new Vector3(-1.7f,0,3.2f),0);
            if(kind==1 || kind==3)DownloadedStreetProp(site,"DeliveryCrate",new Vector3(-1.4f,0,-2.6f),20);
            if(kind==4)
            {
                DownloadedStreetProp(site,"UtilityDrum",new Vector3(-1.4f,0,2.6f),0);
                for(int i=0;i<3;i++)DownloadedStreetProp(site,"TrafficCone",new Vector3(-1.8f,0,-2+i*1.2f),0);
            }
            if(kind==5)DownloadedStreetProp(site,"StreetBench",new Vector3(-1.2f,0,-3.7f),90);
            if(kind==0)CGStreetProp(site,"bollard_01_c_06",new Vector3(-1.8f,0,0),0);
            if(kind==1)CGStreetProp(site,"garbage_bin_d_05",new Vector3(1.7f,0,-2.4f),0);
            if(kind==2)CGStreetProp(site,"firehydrant_01_a_02",new Vector3(-1.7f,0,3.9f),0);
            if(kind==3)CGStreetProp(site,"bollard_d_05",new Vector3(-1.8f,0,1.6f),0);
            if(kind==4)CGStreetProp(site,"barricade_01_a_01",new Vector3(0,0,4),0);
        }
        void CGStreetProp(Transform site,string name,Vector3 position,float yaw)
        {
            var prefab=Resources.Load<GameObject>("Models/CGStreetProps/PBR_Prefabs/"+name);
            if(prefab==null)return;
            var prop=Instantiate(prefab,site,false);prop.name="CGTrader "+name;
            prop.transform.localPosition=position;prop.transform.localRotation=Quaternion.Euler(0,yaw,0);
            prop.AddComponent<DownloadedStreetPropMarker>();
            foreach(var collider in prop.GetComponentsInChildren<Collider>())collider.enabled=false;
        }
        void DownloadedStreetProp(Transform site,string name,Vector3 position,float yaw)
        {
            var prefab=Resources.Load<GameObject>("Models/NeighborhoodProps/"+name);
            if(prefab==null)return;
            var prop=Instantiate(prefab,site,false);prop.name="Downloaded "+name;
            prop.transform.localPosition=position;prop.transform.localRotation=Quaternion.Euler(0,yaw,0);
            prop.AddComponent<DownloadedStreetPropMarker>();
            foreach(var renderer in prop.GetComponentsInChildren<Renderer>())
            {
                var surfaces=renderer.sharedMaterials;
                for(int i=0;i<surfaces.Length;i++)
                {
                    // FBX import can rename material slots; bind the source pack's known atlases explicitly.
                    string atlas=name=="StreetBin"?"material":name=="StreetBench" || name=="DeliveryCrate"?"Mat.2":"Mat.1";
                    if(!streetPropSurfaces.TryGetValue(atlas,out var surface))
                    {
                        var texture=Resources.Load<Texture2D>("Models/NeighborhoodProps/"+atlas+"_baseColor");
                        surface=new Material(Shader.Find("Standard")){name="Street prop "+atlas,mainTexture=texture,color=Color.white};
                        surface.SetFloat("_Glossiness",.22f);materials.Add(surface);streetPropSurfaces[atlas]=surface;
                    }
                    surfaces[i]=surface;
                }
                renderer.sharedMaterials=surfaces;
            }
        }
    }
    public sealed class DownloadedStreetPropMarker : MonoBehaviour { }
}
