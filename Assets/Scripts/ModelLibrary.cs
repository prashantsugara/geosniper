using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    public static class ModelLibrary
    {
        static readonly Dictionary<string, GameObject[]> cache=new Dictionary<string, GameObject[]>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => cache.Clear();
        public static GameObject[] Load(string category)
        {
            if(cache.TryGetValue(category,out var models)) return models;
            // Load validated individual vehicle prefabs with proper atlas textures
            if(category=="Cars")
            {
                var selected=new List<GameObject>();
                foreach(var name in new[]{"audi", "Ford", "Bmw"})
                {
                    var model=Resources.Load<GameObject>("Models/Cars/"+name);
                    if(model!=null) selected.Add(model);
                }
                var police=Resources.Load<GameObject>("Models/PoliceCar");
                if(police!=null) selected.Add(police);
                models=selected.ToArray();
            }
            else if(category=="Enemies" || category=="Civilians")
            {
                var selected=new List<GameObject>();
                var names=category=="Enemies"?new[]{"swat","ch35"}
                    :new[]{"civilian_modern_men","civilian_tf2c"};
                foreach(var name in names)
                {
                    var model=Resources.Load<GameObject>("Models/"+category+"/"+name) ?? Resources.Load<GameObject>("Models/"+name);
                    if(model!=null) selected.Add(model);
                }
                models=selected.ToArray();
            }
            else models=Resources.LoadAll<GameObject>("Models/"+category);
            if(category=="Enemies" || category=="Civilians")
            {
                var valid=new List<GameObject>();
                foreach(var model in models)
                    if(model!=null && (model.GetComponentInChildren<SkinnedMeshRenderer>(true)!=null || model.GetComponentInChildren<MeshRenderer>(true)!=null))
                        valid.Add(model);
                models=valid.ToArray();
            }
            cache[category]=models;
            return models;
        }
    }
}
