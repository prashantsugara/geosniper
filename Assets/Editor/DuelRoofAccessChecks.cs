using System;
using System.Collections.Generic;
using System.Reflection;
using GeoSniper;
using GeoSniper.Duel;
using UnityEngine;

public static class DuelRoofAccessChecks
{
    public static void Run()
    {
        var root = new GameObject("Inactive duel roof access checks");
        root.SetActive(false);
        try
        {
            var manager = root.AddComponent<SniperDuelManager>();
            Vector3 offset = new Vector3(10000,0,10000);
            typeof(SniperDuelManager).GetProperty("LocalSpawnPoint").SetValue(manager,offset+new Vector3(0,12,0));
            typeof(SniperDuelManager).GetProperty("RivalSpawnPoint").SetValue(manager,offset+new Vector3(120,12,0));
            var ensure = typeof(SniperDuelManager).GetMethod("EnsureArenaRoofAccess",BindingFlags.Instance|BindingFlags.NonPublic);
            for(int peer=0;peer<2;peer++)
            {
                var child = new GameObject("Peer " + peer + " shared map");
                child.transform.SetParent(root.transform);
                child.transform.position=offset;
                var world=child.AddComponent<SectorWorld>();
                for(int roof=0;roof<2;roof++)
                    world.Features.Add(new MapFeature { Kind="building", Points=new List<Vector2> {
                        new Vector2(roof*120-40,-20),new Vector2(roof*120+40,-20),
                        new Vector2(roof*120+40,20),new Vector2(roof*120-40,20) }});
                SectorWorld.LoadedWorlds.Add(world);
                ensure.Invoke(manager,null);
                int count=SectorWorld.Ladders.FindAll(route=>route.Owner==world).Count;
                if(count!=2) throw new Exception("Both assigned roofs need ladders on peer " + peer);
                ensure.Invoke(manager,null);
                if(SectorWorld.Ladders.FindAll(route=>route.Owner==world).Count!=2)
                    throw new Exception("Repeated rounds duplicated ladders on a large roof");
                if(world.EnsureDuelRooftopLadder(offset+new Vector3(300,12,0))
                    ||world.EnsureDuelRooftopLadder(offset+new Vector3(0,0,0)))
                    throw new Exception("Ground or unrelated sector incorrectly received rooftop access");
                SectorWorld.LoadedWorlds.Remove(world);
            }
            Debug.Log("PASS: both duel roofs have ladders on both peers, large-roof deduplication and ground/sector filtering");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
