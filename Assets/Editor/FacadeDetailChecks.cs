using System;
using System.Collections.Generic;
using GeoSniper;
using UnityEditor;
using UnityEngine;

public static class FacadeDetailChecks
{
    [MenuItem("Geo Sniper/Validate Balcony Geometry")]
    public static void Run()
    {
        var building=new MapFeature {Kind="building",Height=16,Points=new List<Vector2> {
            new Vector2(-10,-8),new Vector2(10,-8),new Vector2(10,8),new Vector2(-10,8)}};
        var world=new List<MapFeature>{building};
        var mesh=BuildingFacadeDetails.Build(building,world);
        try
        {
            if(mesh==null || mesh.vertexCount==0) throw new Exception("Expected balconies on an unobstructed building");
            if(mesh.vertexCount>24*13*24) throw new Exception("Balcony vertex budget exceeded");
            foreach(var vertex in mesh.vertices)
                if(!float.IsFinite(vertex.x) || !float.IsFinite(vertex.y) || !float.IsFinite(vertex.z) || vertex.y<3 || vertex.y>building.Height)
                    throw new Exception("Invalid balcony vertex");
        }
        finally { if(mesh!=null) UnityEngine.Object.DestroyImmediate(mesh); }
        var neighbour=new MapFeature {Kind="building",Height=20,Points=new List<Vector2> {
            new Vector2(-15,-15),new Vector2(15,-15),new Vector2(15,15),new Vector2(-15,15)}};
        world.Add(neighbour);
        mesh=BuildingFacadeDetails.Build(building,world);
        if(mesh!=null) { UnityEngine.Object.DestroyImmediate(mesh); throw new Exception("Overlapping building must suppress balconies"); }
        world.Remove(neighbour);
        // A small non-rectangular footprint was previously excluded by the asset-fit gate.
        building.Points=new List<Vector2>{new Vector2(0,0),new Vector2(8,0),new Vector2(7,8),new Vector2(0,8)};
        mesh=BuildingFacadeDetails.Build(building,world);
        if(mesh==null) throw new Exception("Small irregular buildings should receive balconies");
        UnityEngine.Object.DestroyImmediate(mesh);
        var street=new MapFeature {Kind="road",Points=new List<Vector2>{new Vector2(-5,-6),new Vector2(13,-6)}};
        world.Add(street);
        int entrance=BuildingFacadeDetails.EntranceEdge(building,world);
        if(entrance!=0) throw new Exception("Ground-floor entrance did not face the nearby street");
        mesh=BuildingFacadeDetails.BuildWindows(building,entrance);
        if(mesh==null || mesh.subMeshCount!=3 || mesh.GetTriangles(1).Length==0 || mesh.GetTriangles(2).Length==0)
            throw new Exception("Expected illuminated windows, glass and a closed entrance");
        bool groundDoor=false;
        float residentialBayMin=float.MaxValue;
        foreach(var vertex in mesh.vertices)if(vertex.y<.12f)groundDoor=true;
        if(!groundDoor)throw new Exception("Entrance does not reach the ground floor");
        foreach(var vertex in mesh.vertices)
            if(vertex.x>.5f && vertex.x<4f && Mathf.Abs(vertex.z)<.5f && vertex.y<.4f)
                residentialBayMin=Mathf.Min(residentialBayMin,vertex.x);
        UnityEngine.Object.DestroyImmediate(mesh);
        var shop=new MapFeature {Kind="place",Landmark="shop",Points=new List<Vector2>{new Vector2(2,2)}};
        shop.Details["category"]="grocery_store";
        if(!SectorSignage.IsShop(shop) || SectorSignage.FindHost(shop,world,world)!=building)
            throw new Exception("Tagged shop did not match its building");
        shop.Details["category"]="school";
        if(SectorSignage.IsShop(shop))throw new Exception("Non-retail place became a shop");
        shop.Points[0]=new Vector2(100,100);
        if(SectorSignage.FindHost(shop,world,world)!=null)throw new Exception("Distant place matched a building");
        building.Details["class"]="retail";
        mesh=BuildingFacadeDetails.BuildWindows(building,entrance,true);
        float retailBayMin=float.MaxValue;
        foreach(var vertex in mesh.vertices)
            if(vertex.x>.5f && vertex.x<4f && Mathf.Abs(vertex.z)<.5f && vertex.y<.4f)
                retailBayMin=Mathf.Min(retailBayMin,vertex.x);
        UnityEngine.Object.DestroyImmediate(mesh);
        if(retailBayMin>=residentialBayMin-.25f)throw new Exception("Retail ground-floor glazing did not widen");
        mesh=BuildingFacadeDetails.BuildArchitecture(building,entrance);
        try
        {
            if(mesh==null || mesh.vertexCount==0 || mesh.vertexCount>60000)
                throw new Exception("Architectural detail missing or over vertex budget");
            foreach(var vertex in mesh.vertices)
                if(!float.IsFinite(vertex.x) || !float.IsFinite(vertex.y) || !float.IsFinite(vertex.z) || vertex.y<0 || vertex.y>building.Height+.2f)
                    throw new Exception("Invalid facade surround vertex");
        }
        finally {if(mesh!=null) UnityEngine.Object.DestroyImmediate(mesh);}
        building.Height=4;
        if(BuildingFacadeDetails.Build(building,world)!=null) throw new Exception("Low building must not get balconies");
        Debug.Log("Facade checks passed: balconies, street-facing entrance, retail glazing, geometry budgets and adjacent footprint exclusion.");
    }
}
