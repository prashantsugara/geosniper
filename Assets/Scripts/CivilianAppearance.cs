using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    public enum CivilianRole { Resident, Witness, Doctor, Reporter, Shopkeeper }

    public static class CivilianAppearance
    {
        [Serializable] sealed class Surface { public string material,texture; }
        [Serializable] sealed class SurfaceFile { public Surface[] surfaces; }
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        static bool loaded;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){materials.Clear();loaded=false;}

        public static CivilianRole Role(int index, bool storyWitness, string briefing)
        {
            if(storyWitness)
                return (briefing??"").IndexOf("doctor",StringComparison.OrdinalIgnoreCase)>=0
                    ?CivilianRole.Doctor:CivilianRole.Witness;
            switch(index%8){case 3:return CivilianRole.Shopkeeper;case 5:return CivilianRole.Reporter;case 7:return CivilianRole.Doctor;default:return CivilianRole.Resident;}
        }
        public static GameObject Model(CivilianRole role,int index,string briefing=null)
        {
            string name=role==CivilianRole.Doctor?"civilian_doctor"
                :role==CivilianRole.Reporter?"civilian_carla"
                :role==CivilianRole.Shopkeeper?"civilian_eric"
                :role==CivilianRole.Witness && ((briefing??"").Contains("Mina") || (briefing??"").Contains("Nia"))?"civilian_carla"
                :role==CivilianRole.Witness?"civilian_eric"
                :index%2==0?"civilian_eric":"civilian_carla";
            return Resources.Load<GameObject>("Models/Civilians/"+name);
        }
        public static bool ApplySurfaces(Transform visual)
        {
            if(!loaded)
            {
                loaded=true;
                var data=Resources.Load<TextAsset>("CivilianSurfaces");
                var file=data!=null?JsonUtility.FromJson<SurfaceFile>(data.text):null;
                if(file?.surfaces!=null)foreach(var surface in file.surfaces)
                {
                    var texture=Resources.Load<Texture2D>(surface.texture);
                    var shader=Shader.Find("Standard")??Shader.Find("Unlit/Texture");
                    if(texture==null || shader==null)continue;
                    var material=new Material(shader){name=surface.material,color=Color.white,mainTexture=texture,enableInstancing=true};
                    if(material.HasProperty("_Glossiness"))material.SetFloat("_Glossiness",.18f);
                    materials[surface.material]=material;
                }
            }
            bool applied=false;
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++)if(slots[i]!=null)
                    foreach(var entry in materials)if(slots[i].name.StartsWith(entry.Key,StringComparison.Ordinal))
                    {slots[i]=entry.Value;applied=true;break;}
                renderer.sharedMaterials=slots;
            }
            return applied;
        }

        public static void AddRoleDetails(Transform actor,Transform visual,CivilianRole role)
        {
            if(role!=CivilianRole.Reporter && role!=CivilianRole.Shopkeeper)return;
            Transform chest=null;
            foreach(var bone in visual.GetComponentsInChildren<Transform>(true))
                if(bone.name.ToLowerInvariant().Replace("_","").Contains("spine2")){chest=bone;break;}
            var holder=new GameObject(role+" details");holder.transform.SetParent(actor,false);
            var shader=Shader.Find("Standard")??Shader.Find("Unlit/Color");
            if(shader==null)return;
            string key="role_"+role;
            if(!materials.TryGetValue(key,out var paint))
            {paint=new Material(shader){color=role==CivilianRole.Reporter?new Color(.12f,.32f,.65f):new Color(.35f,.20f,.10f)};materials[key]=paint;}
            Part(holder.transform,"Identification badge",new Vector3(.13f,1.42f,.24f),new Vector3(.14f,.10f,.018f),paint);
            if(role==CivilianRole.Reporter)
            {
                if(!materials.TryGetValue("camera_black",out var dark))
                {dark=new Material(shader){color=new Color(.035f,.04f,.045f)};materials["camera_black"]=dark;}
                Part(holder.transform,"Camera strap left",new Vector3(-.075f,1.37f,.255f),new Vector3(.012f,.25f,.012f),dark);
                Part(holder.transform,"Camera strap right",new Vector3(.075f,1.37f,.255f),new Vector3(.012f,.25f,.012f),dark);
                Part(holder.transform,"Press camera body",new Vector3(0,1.22f,.28f),new Vector3(.20f,.12f,.075f),dark);
                var lens=GameObject.CreatePrimitive(PrimitiveType.Cylinder);lens.name="Press camera lens";lens.transform.SetParent(holder.transform,false);
                lens.transform.localPosition=new Vector3(0,1.22f,.35f);lens.transform.localScale=new Vector3(.07f,.045f,.07f);lens.transform.localRotation=Quaternion.Euler(90,0,0);
                UnityEngine.Object.Destroy(lens.GetComponent<Collider>());lens.GetComponent<Renderer>().sharedMaterial=dark;
            }
            else Part(holder.transform,"Shop work apron",new Vector3(0,1.06f,.225f),new Vector3(.38f,.53f,.018f),paint);
            if(chest!=null)holder.transform.SetParent(chest,true);
        }
        static void Part(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;part.transform.SetParent(parent,false);
            part.transform.localPosition=position;part.transform.localScale=size;
            UnityEngine.Object.Destroy(part.GetComponent<Collider>());part.GetComponent<Renderer>().sharedMaterial=material;
        }
    }
}
