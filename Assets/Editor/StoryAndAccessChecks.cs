using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeoSniper;
using Newtonsoft.Json.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
public static class StoryAndAccessChecks
{
    public static void Run()
    {
        CampaignProgressionChecks.Graph();CampaignProgressionChecks.Profiles();CampaignProgressionChecks.Routes();
        var titles=new HashSet<string>();var types=new HashSet<string>();
        foreach(var node in CampaignNodeGraph.Nodes)
        {
            var operation=CampaignStory.Get(node.id);Check(titles.Add(operation.title),"Repeated story title");types.Add(operation.type);
            Check(operation.briefing.Length>100 && operation.outcome.Length>50 && operation.objective.Length>20,"Missing story content at "+node.id);
            Check(node.id==49?node.nextNodeIds.Length==0:node.nextNodeIds.SequenceEqual(new[]{node.id+1}),"Story can skip a chapter");
            if(operation.type=="EvidenceRecovery" || operation.type=="Sabotage")Check(operation.interactions>=2,"Field objective has no meaningful progression");
        }
        Check(types.Count==9 && CampaignNodeGraph.Nodes.Count(n=>n.isPvPDuel)==5,"Story must contain nine activity types and five rivals");
        InteriorChecks.Run();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject("Expanded building access");var world=root.AddComponent<SectorWorld>();world.BuildNavigation=false;
        var road=new MapFeature{Kind="road",Details=new JObject{["class"]="residential"},Points={new Vector2(-80,0),new Vector2(80,0)}};
        var collinear=new MapFeature{Kind="building",Height=15,Details=new JObject{["id"]="collinear-house"},Points={new Vector2(-7,6),new Vector2(0,6),new Vector2(7,6),new Vector2(7,18),new Vector2(-7,18),new Vector2(-7,6)}};
        var concave=new MapFeature{Kind="building",Height=18,Details=new JObject{["id"]="concave-house"},Points={new Vector2(22,6),new Vector2(38,6),new Vector2(38,18),new Vector2(32,18),new Vector2(32,14),new Vector2(22,14),new Vector2(22,6)}};
        try
        {
            world.Generate(new List<MapFeature>{road,collinear,concave});Physics.SyncTransforms();
            foreach(var feature in new[]{collinear,concave})
            {
                var interior=world.Buildings[feature].GetComponentInChildren<EnterableBuilding>();Check(interior!=null,"Realistic footprint remains sealed");
                Check(interior.Floors>=5,"Taller building missing floors");Check(interior.GetComponentsInChildren<WorldSign>().Any(s=>s.Caption=="OPEN // ENTER"),"Entrance is not marked");
                var walker=new GameObject("Full-height player");var body=walker.AddComponent<CharacterController>();body.height=1.8f;body.center=Vector3.up*.9f;body.radius=.35f;body.stepOffset=.45f;body.skinWidth=.03f;
                Vector3 outward=interior.transform.TransformDirection(interior.EntranceOutward);
                walker.transform.position=interior.transform.TransformPoint(interior.Entrance)+outward*1.2f-Vector3.up*.32f;Physics.SyncTransforms();
                for(int i=0;i<45;i++)body.Move(-outward*.07f+Vector3.down*.04f);
                var local=interior.transform.InverseTransformPoint(walker.transform.position);
                Check(Vector3.Dot(local-interior.Entrance,interior.EntranceOutward)<-.8f,"Player cannot enter mapped footprint");
                int before=SectorWorld.Ladders.Count;Vector3 roof=interior.transform.TransformPoint(interior.StairOrigin+Vector3.up*feature.Height);
                Check(world.EnsureRooftopStairs(roof) && world.EnsureDuelRooftopLadder(roof),"Mission cannot reuse interior stairs");Check(SectorWorld.Ladders.Count==before,"Mission added an obstructing exterior ladder");
                Object.DestroyImmediate(walker);
            }
            Check(Physics.Raycast(new Vector3(25,25,16),Vector3.down,out var notch,30) && !notch.collider.name.Contains("floor"),"Floor spills outside concave footprint");
            var extras=new List<MapFeature>{road};
            for(int i=0;i<24;i++){float x=i*18;extras.Add(new MapFeature{Kind="building",Height=35,Details=new JObject{["id"]="lobby-"+i},Points={new Vector2(x,30),new Vector2(x+10,30),new Vector2(x+10,40),new Vector2(x,40)}});}
            world.Generate(extras);Physics.SyncTransforms();
            foreach(var feature in extras.Skip(1)){var lobby=world.Buildings[feature].GetComponentInChildren<EnterableBuilding>();Check(lobby!=null && lobby.Floors==1,"Ground-floor access still capped or tall building sealed");Check(!lobby.HasRoofAccess,"Lobby incorrectly advertised roof access");Check(lobby.GetComponentsInChildren<BoxCollider>().Any(c=>c.name=="Ground-connected foundation"),"Floating lobby foundation");var door=lobby.transform.TransformPoint(lobby.Entrance)+Vector3.up*.95f;Check(!Physics.CheckCapsule(door-Vector3.up*.5f,door+Vector3.up*.5f,.32f,~0,QueryTriggerInteraction.Ignore),"Tall lobby doorway blocked");}
            for(int z=0;z<SectorElevation.Size;z++)for(int x=0;x<SectorElevation.Size;x++)world.Elevation.Heights[z*SectorElevation.Size+x]=(x*SectorElevation.Step-SectorElevation.Radius)*.08f;
            var sideRoad=new MapFeature{Kind="road",Details=new JObject{["class"]="residential"},Points={new Vector2(18,-60),new Vector2(18,60)}};
            var sloped=new MapFeature{Kind="building",Height=9,Details=new JObject{["id"]="slope-door"},Points={new Vector2(-7,-6),new Vector2(7,-6),new Vector2(7,6),new Vector2(-7,6)}};
            world.Generate(new List<MapFeature>{sideRoad,sloped});Physics.SyncTransforms();
            var slopeInterior=world.Buildings[sloped].GetComponentInChildren<EnterableBuilding>();Check(slopeInterior.HasRoofAccess,"Stair layout lost roof access");
            Vector3 entry=slopeInterior.Entrance;var approach=entry-slopeInterior.EntranceOutward*1.2f;
            for(int step=0;step<16;step++){var foot=slopeInterior.transform.TransformPoint(Vector3.Lerp(entry,approach,step/15f))+Vector3.up*.06f;Check(!Physics.CheckCapsule(foot+Vector3.up*.4f,foot+Vector3.up*1.4f,.32f,~0,QueryTriggerInteraction.Ignore),"Stairs obstruct side doorway");}
            foreach(var plinth in slopeInterior.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Ground-connected foundation")){var bottom=plinth.transform.position-Vector3.up*plinth.transform.lossyScale.y*.5f;var local=world.transform.InverseTransformPoint(bottom);Check(local.y<=world.Ground(local.x,local.z),"Foundation floats above sloped terrain");}
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/StoryAndAccessChecks.txt","PASS: 50 unique connected story missions, nine activity types, five rival duels; saved progression, difficulty curves and routes.\nPASS: original entry/stairs/window shooting; collinear and concave mapped footprints, 15m/18m buildings, marked doors, player entry, stair-route reuse and footprint floors; uncapped 24 tall-building lobbies; sloped foundations and side-door stair clearance.\n");
        }
        finally{EditorSceneManager.CloseScene(scene,true);}
    }
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}
