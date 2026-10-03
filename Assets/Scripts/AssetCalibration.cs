using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    // Corrections describe the imported root; animation stays below a placement wrapper.
    public static class AssetCalibration
    {
        [Serializable] public sealed class Entry
        {
            public string name;
            public Vector3 rotation;
            public float height=1.85f;
        }
        [Serializable] sealed class Catalog { public Entry[] entries; }
        static Dictionary<string,Entry> entries;
        static readonly HashSet<string> warned=new HashSet<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() {entries=null; warned.Clear();}
        public static bool TryGet(string name,out Entry entry)
        {
            if(entries==null)
            {
                entries=new Dictionary<string,Entry>(StringComparer.OrdinalIgnoreCase);
                var json=Resources.Load<TextAsset>("AssetCalibration");
                if(json!=null) foreach(var item in JsonUtility.FromJson<Catalog>(json.text).entries)
                    entries.Add(item.name,item);
            }
            string key=name.Replace("(Clone)","").Trim();
            if(entries.TryGetValue(key,out entry))return true;
            // Blender exports numbered copies with the same source orientation.
            int suffix=key.LastIndexOf('.');
            if(suffix>0 && int.TryParse(key.Substring(suffix+1),out _))
                return entries.TryGetValue(key.Substring(0,suffix),out entry);
            return false;
        }
        public static void Apply(Transform visual)
        {
            if(TryGet(visual.name,out var profile)) visual.localRotation=Quaternion.Euler(profile.rotation)*visual.localRotation;
            else if(warned.Add(visual.name)) Debug.LogWarning("No orientation calibration for "+visual.name+"; preserving imported transform.");
        }
        public static Transform Marker(Transform root,string name)
        {
            foreach(var child in root.GetComponentsInChildren<Transform>(true))
                if(string.Equals(child.name,name,StringComparison.OrdinalIgnoreCase)) return child;
            return null;
        }
    }
}
