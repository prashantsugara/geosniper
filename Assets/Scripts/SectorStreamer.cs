using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    public sealed class SectorStreamer : MonoBehaviour
    {
        const float Size=640;
        const int KeepRadius=1;
        readonly Dictionary<Vector2Int,SectorWorld> loaded=new Dictionary<Vector2Int,SectorWorld>();
        readonly HashSet<Vector2Int> pending=new HashSet<Vector2Int>();
        readonly HashSet<GameObject> pendingRoots=new HashSet<GameObject>();
        readonly Dictionary<Vector2Int,float> retryAfter=new Dictionary<Vector2Int,float>();
        readonly Dictionary<Vector2Int,int> failures=new Dictionary<Vector2Int,int>();
        float nextWindowCheck;
        UrbanPlayer player;
        GameLocation origin;
        string endpoint;
        Vector2Int current;
        Vector2Int desiredCenter;
        SectorWorld firstSector;
        bool windowLoading;
        bool running;
        bool useElevation;
        int activeLoads;
        bool preserveFirstSector;
        public void SetPlayer(UrbanPlayer target) { player=target; }

        public void StopAndPreserveFirstSector()
        {
            preserveFirstSector=true;
            running=false;
            StopAllCoroutines();
            foreach(var root in pendingRoots)
                if(root!=null){root.SetActive(false);Destroy(root);}
            pendingRoots.Clear();
            foreach(var sector in loaded.Values)
                if(sector!=null && sector!=firstSector)
                {sector.gameObject.SetActive(false);Destroy(sector.gameObject);}
            loaded.Clear();
        }

        public void Begin(UrbanPlayer target,GameLocation location,string mapEndpoint,SectorWorld first,bool elevation=false)
        {
            player=target; origin=location; endpoint=mapEndpoint; useElevation=elevation; firstSector=first; loaded[Vector2Int.zero]=first; running=true;
            desiredCenter=Vector2Int.zero;
            nextWindowCheck=Time.realtimeSinceStartup+5f;
            StartCoroutine(StartupDelayKeepWindow());
        }

        IEnumerator StartupDelayKeepWindow()
        {
            yield return new WaitForSeconds(4f);
            if(running && player!=null && !windowLoading) StartCoroutine(KeepWindow());
        }

        void Update()
        {
            if(!running || player==null) return;
            Vector2Int next=ChunkAt(player.transform.position);
            if(next!=current) { current=next; desiredCenter=next; if(!windowLoading) StartCoroutine(KeepWindow()); }
            if(!windowLoading && Time.realtimeSinceStartup>=nextWindowCheck)
            { nextWindowCheck=Time.realtimeSinceStartup+5; StartCoroutine(KeepWindow()); }
        }

        Vector2Int ChunkAt(Vector3 position)
        {
            return new Vector2Int(Mathf.FloorToInt((position.x+Size*.5f)/Size),Mathf.FloorToInt((position.z+Size*.5f)/Size));
        }

        IEnumerator KeepWindow()
        {
            windowLoading=true;
            Vector2Int center;
            try
            {
            do
            {
                center=desiredCenter;
                var requested=new HashSet<Vector2Int>();
                var targets=new List<Vector2Int>();
                for(int z=-KeepRadius;z<=KeepRadius;z++) for(int x=-KeepRadius;x<=KeepRadius;x++)
                {
                    var key=center+new Vector2Int(x,z);
                    // Preload only when player actually nears the border (<220m) rather than immediately on spawn.
                    float preloadDistance=Application.isMobilePlatform?180:220;
                    if(key==center || DistanceToSectorSquared(key)<preloadDistance*preloadDistance) targets.Add(key);
                }
                targets.Sort((a,b)=>DistanceToSectorSquared(a).CompareTo(DistanceToSectorSquared(b)));
                foreach(var key in targets)
                {
                    if(center!=desiredCenter) break;
                    if(!loaded.ContainsKey(key) && !pending.Contains(key) && CanRetry(key) && activeLoads<1)
                    {
                        requested.Add(key);
                        StartCoroutine(LoadChunkManaged(key));
                    }
                }
                while(center==desiredCenter)
                {
                    bool waiting=false;
                    foreach(var key in targets)
                    {
                        if(!CanRetry(key)) continue;
                        if(loaded.ContainsKey(key) || requested.Contains(key))
                        {
                            if(pending.Contains(key)) waiting=true;
                            continue;
                        }
                        if(activeLoads<1)
                        {
                            requested.Add(key);
                            StartCoroutine(LoadChunkManaged(key));
                        }
                        waiting=true;
                    }
                    if(!waiting) break;
                    yield return null;
                }
                if(center!=desiredCenter) continue;
                var remove=new List<Vector2Int>();
                foreach(var pair in loaded)
                    if(Mathf.Abs(pair.Key.x-center.x)>KeepRadius || Mathf.Abs(pair.Key.y-center.y)>KeepRadius) remove.Add(pair.Key);
                foreach(var key in remove)
                    if(loaded.TryGetValue(key,out var sector)) { loaded.Remove(key); Destroy(sector.gameObject); }
                yield return null;
            } while(desiredCenter!=center);
            }
            finally { windowLoading=false; }
        }

        IEnumerator LoadChunkManaged(Vector2Int key)
        {
            activeLoads++;
            try { yield return LoadChunk(key); }
            finally { activeLoads=Mathf.Max(0,activeLoads-1); }
        }
        bool CanRetry(Vector2Int key) => !retryAfter.TryGetValue(key,out var time) || Time.realtimeSinceStartup>=time;
        float DistanceToSectorSquared(Vector2Int key)
        {
            var p=player.transform.position;
            float x=Mathf.Max(0,Mathf.Abs(p.x-key.x*Size)-Size/2);
            float z=Mathf.Max(0,Mathf.Abs(p.z-key.y*Size)-Size/2);
            return x*x+z*z;
        }
        void ScheduleRetry(Vector2Int key)
        {
            failures.TryGetValue(key,out int count);
            failures[key]=Mathf.Min(count+1,4);
            retryAfter[key]=Time.realtimeSinceStartup+Mathf.Min(300,30*Mathf.Pow(2,count));
        }

        IEnumerator LoadChunk(Vector2Int key)
        {
            pending.Add(key);
            GameObject objectRoot=null;
            bool completed=false;
            try
            {
                double latitude=origin.Latitude+(key.y*Size)/111320.0;
                double longitudeScale=111320*Math.Cos(origin.Latitude*Math.PI/180);
                if(Math.Abs(longitudeScale)<1) yield break;
                double longitude=origin.Longitude+(key.x*Size)/longitudeScale;
                var location=new GameLocation { Latitude=latitude,Longitude=longitude,Source="streamed",Label="STREAMED SECTOR" };
                List<MapFeature> map=null;
                bool synthetic=true;
                string source="";
                yield return SectorMap.Load(location,endpoint,(features,offline)=>{map=features; synthetic=offline; source=SectorMap.LastSource;},null);
                // A failed adjacent download must never put a fictional neighbourhood on the real map.
                if(synthetic || map==null || !map.Exists(feature=>feature.Kind=="road"))
                { ScheduleRetry(key); yield break; }
                objectRoot=new GameObject("Streamed sector "+key.x+","+key.y);
                pendingRoots.Add(objectRoot);
                objectRoot.transform.position=new Vector3(key.x*Size,0,key.y*Size);
                var sector=objectRoot.AddComponent<SectorWorld>();
                sector.SourceLabel=source;
                sector.DetailFocus=player.transform.position;
                // Adjacent chunks add scenery, not new patrols. Baking thousands of
                // colliders into another NavMesh here stalled Android during movement.
                sector.BuildNavigation=false;
                if(useElevation && firstSector!=null) sector.Elevation=firstSector.Elevation;
                var job=sector.GenerateAsync(map);
                while(true)
                {
                    bool more;
                    try { more=job.MoveNext(); }
                    catch(Exception error)
                    {
                        ScheduleRetry(key);
                        Debug.LogWarning("Adjacent sector generation failed: "+error.GetType().Name);
                        yield break;
                    }
                    if(!more) break;
                    yield return job.Current;
                }
                retryAfter.Remove(key); failures.Remove(key);
                loaded[key]=sector;
                completed=true;
            }
            finally { pending.Remove(key); if(objectRoot!=null)pendingRoots.Remove(objectRoot); if(!completed && objectRoot!=null) Destroy(objectRoot); }
        }

        void OnDestroy()
        {
            running=false;
            StopAllCoroutines();
            foreach(var root in pendingRoots)if(root!=null)Destroy(root);
            pendingRoots.Clear();
            foreach(var sector in loaded.Values)
                if(sector!=null && (!preserveFirstSector || sector!=firstSector)) Destroy(sector.gameObject);
            loaded.Clear();
        }
    }
}
