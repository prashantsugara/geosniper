using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace GeoSniper
{
    public sealed partial class SectorWorld
    {
        void BuildEnterable(MapFeature feature,GameObject building,float height,List<MapFeature> features,Material wall,Material roof,bool detailed=true)
        {
            EnterableBuilding.FitLobby(feature,out var center,out var along,out var size);
            var root=new GameObject("Playable interior");root.transform.SetParent(building.transform,false);
            root.transform.localPosition=new Vector3(center.x,0,center.y);
            root.transform.localRotation=Quaternion.LookRotation(new Vector3(-along.y,0,along.x));
            var polygon=EnterableBuilding.Polygon(feature,center,along);
            int doorEdge=BuildingFacadeDetails.EntranceEdge(feature,features);
            if(doorEdge<0 || doorEdge>=polygon.Count)doorEdge=0;
            var doorway=(polygon[doorEdge]+polygon[(doorEdge+1)%polygon.Count])*.5f;
            Vector2 stairOrigin=Vector2.zero;float stairRun=0;
            bool stairs=detailed && EnterableBuilding.TryStairLayout(polygon,size,height,out stairOrigin,out stairRun,doorway);
            var interior=root.AddComponent<EnterableBuilding>();
            interior.StairOrigin=new Vector3(stairOrigin.x,0,stairOrigin.y);
            int floors=stairs?Mathf.Clamp(Mathf.FloorToInt(height/3),1,6):1;interior.Floors=floors;interior.HasRoofAccess=stairs;
            float levelHeight=stairs?height/floors:Mathf.Min(3.2f,height),w=size.x,d=size.y;
            interior.FloorHeight=levelHeight;interior.Footprint.AddRange(polygon);
            var floor=InteriorSurface("StoneTile",Color.white,.23f);
            var trim=InteriorSurface("Powder coat",new Color(.17f,.22f,.24f),.35f,.45f);
            var room=InteriorSurface("Plaster",Color.white);
            var furniture=InteriorSurface("Oak",Color.white,.28f);
            var lamp=InteriorLamp();
            bool streetStudy=MapFeatureStyle.Text(feature,"street_study")=="true";
            int architecture=Mathf.Abs(Mathf.RoundToInt(center.x*.73f+center.y*.31f))%3;
            bool shop=SectorSignage.IsShop(feature);
            if(streetStudy && string.IsNullOrEmpty(MapFeatureStyle.Text(feature,"roof_material")))
            {
                // Fine bitumen aggregate replaces the oversized, soft relief on the pilot roof.
                roof=InteriorSurface("Street study bitumen",new Color(.82f,.83f,.84f),.07f);
                roof.mainTexture=streetStudyRoadTexture;
                var roofShader=Shader.Find("GeoSniper/StreetStudySurface");if(roofShader!=null){roof.shader=roofShader;roof.SetFloat("_WetResponse",.45f);}
            }
            var stoneTrim=InteriorSurface(streetStudy?"Street study limestone":"Architectural stone",streetStudy?new Color(.39f,.35f,.29f):new Color(.56f,.58f,.57f),.12f);
            var upperFacade=FacadeStyled(feature,wall.color,true);
            // Locate the entrance on the mapped edge nearest a road, rather than choosing an arbitrary side.
            var pieces=new List<GameObject>();
            System.Action<string,Vector3,Vector3,Material> box=(label,pos,scale,surface)=>
            {
                if(scale.x<.001f || scale.y<.001f || scale.z<.001f)return;
                var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=label;part.transform.SetParent(root.transform,false);
                part.transform.localPosition=pos;part.transform.localScale=scale;part.GetComponent<Renderer>().sharedMaterial=surface;pieces.Add(part);
            };
            // Cosmetic details never change traversal or the objective interaction ray.
            System.Action<string,Vector3,Vector3,Material> decor=(label,pos,scale,surface)=>
            {
                int before=pieces.Count;box(label,pos,scale,surface);
                if(pieces.Count>before){var collider=pieces[pieces.Count-1].GetComponent<Collider>();collider.enabled=false;Destroy(collider);}
            };
            float stairWidth=1.5f,run=stairRun,stairX=stairOrigin.x;
            for(int level=0;level<=floors;level++)
            {
                float y=level*levelHeight;
                var surface=level==floors?roof:floor;
                BuildInteriorFloor(root.transform,polygon,level==0 || !stairs?(Rect?)null:new Rect(stairX-stairWidth*.5f,stairOrigin.y-run*.5f,stairWidth,run),y,surface,room);
                if(level==floors)break;
                // Mount wayfinding above head height on a real wall, never across a doorway.
                int signEdge=(doorEdge+1)%polygon.Count;
                Vector2 signA=polygon[signEdge],signB=polygon[(signEdge+1)%polygon.Count];
                Vector2 signMid=(signA+signB)*.5f;
                Vector3 signInward=new Vector3(-signMid.x,0,-signMid.y).normalized;
                if(Vector2.Distance(signA,signB)>2 && signInward.sqrMagnitude>.1f)
                    WorldSign.Create(root.transform,(level==0?"GROUND FLOOR":"FLOOR "+(level+1))+(stairs?"\nSTAIRS / ROOF ACCESS":"\nSTREET EXIT"),
                        new Vector3(signMid.x,y+2.25f,signMid.y)+signInward*.15f,
                        Quaternion.LookRotation(signInward),new Vector2(1.7f,.65f),trim,true);
                if(detailed && EnterableBuilding.Inside(Vector2.zero,polygon))
                {
                    decor("Ceiling fixture housing",new Vector3(0,y+levelHeight-.23f,0),new Vector3(1.25f,.09f,.3f),trim);
                    decor("Warm ceiling diffuser",new Vector3(0,y+levelHeight-.29f,0),new Vector3(1.12f,.035f,.23f),lamp);
                }
                // Partition rooms with a 1.6m passage, leaving the entire stair circulation strip clear.
                float partitionLength=Mathf.Max(0,w-3.8f),doorWidth=1.6f;
                bool rectangular=true;
                foreach(var corner in new[]{new Vector2(-w/2+.3f,-d/2+.3f),new Vector2(w/2-.3f,-d/2+.3f),new Vector2(w/2-.3f,d/2-.3f),new Vector2(-w/2+.3f,d/2-.3f)})
                    if(!EnterableBuilding.Inside(corner,polygon))rectangular=false;
                if(level>0 && rectangular && stairX>w/2-1.2f && Mathf.Abs(stairOrigin.y)<.1f && partitionLength>doorWidth+2)
                {
                    float half=(partitionLength-doorWidth)*.5f;
                    foreach(float sign in new[]{-1f,1f})box("Room partition",new Vector3(-1.7f+sign*(doorWidth+half)*.5f,y+levelHeight*.5f,0),new Vector3(half,levelHeight,.18f),room);
                    box("Room doorway lintel",new Vector3(-1.7f,y+(2.35f+levelHeight)*.5f,0),new Vector3(doorWidth,levelHeight-2.35f,.18f),room);
                }
                int steps=stairs?Mathf.CeilToInt(levelHeight/.18f):0;
                for(int step=0;step<steps;step++)
                {
                    float stepHeight=(step+1)*levelHeight/steps;
                    // Thin treads leave headroom below the next flight instead of filling the stairwell solid.
                    box("Interior stairs",new Vector3(stairX,y+stepHeight-.09f,stairOrigin.y-run*.5f+(step+.5f)*run/steps),new Vector3(stairWidth,.18f,run/steps+.01f),floor);
                }
                // Furnish the window rooms without obstructing the door or the stairs.
                if(!rectangular)continue;
                var desk=new Vector2(-w*.22f,-d*.25f);
                if((stairs && new Rect(desk.x-.9f,desk.y-.6f,1.8f,2f).Overlaps(new Rect(stairX-1.1f,stairOrigin.y-run*.5f-.5f,2.2f,run+1))) ||
                    (level==0 && Vector2.Distance(desk,doorway)<2.4f))continue;
                if(level>0)
                {
                    var cabinet=new Vector3(-w*.5f+.6f,y+.5f,d*.5f-.6f);
                    box("Interior cabinet",cabinet,new Vector3(.75f,1,1),furniture);
                    for(int drawer=0;drawer<3;drawer++)
                    {
                        decor("Cabinet drawer face",cabinet+new Vector3(0,-.31f+drawer*.3f,-.51f),new Vector3(.68f,.27f,.025f),room);
                        decor("Cabinet drawer handle",cabinet+new Vector3(0,-.31f+drawer*.3f,-.54f),new Vector3(.16f,.035f,.035f),trim);
                    }
                }
                box("Interior table top",new Vector3(-w*.22f,y+.78f,-d*.25f),new Vector3(1.4f,.12f,.8f),furniture);
                foreach(float x in new[]{-.55f,.55f})foreach(float z in new[]{-.28f,.28f})
                    box("Table leg",new Vector3(-w*.22f+x,y+.36f,-d*.25f+z),new Vector3(.08f,.72f,.08f),furniture);
                // A small desktop set is also present in simpler ground-floor interiors.
                {
                    // Keep the centre of the desktop reserved for mission documents and switches.
                    decor("Desk blotter",new Vector3(desk.x,y+.846f,desk.y),new Vector3(1.1f,.008f,.6f),trim);
                    for(int book=0;book<3;book++)
                    {
                        var ledger=new Vector3(desk.x-.49f,y+.87f+book*.045f,desk.y-.13f);
                        decor("Ledger pages",ledger,new Vector3(.21f,.03f,.27f),room);
                        foreach(float cover in new[]{-.018f,.018f})
                            decor("Archive ledger cover",ledger+Vector3.up*cover,new Vector3(.22f,.005f,.28f),trim);
                        decor("Ledger spine",ledger+Vector3.left*.106f,new Vector3(.008f,.04f,.28f),trim);
                    }
                    decor("Desk drawer",new Vector3(desk.x+.43f,y+.61f,desk.y),new Vector3(.38f,.2f,.65f),furniture);
                    decor("Desk drawer pull",new Vector3(desk.x+.43f,y+.61f,desk.y+.34f),new Vector3(.12f,.035f,.03f),trim);
                }
                interior.EvidenceStations.Add(new EnterableBuilding.EvidenceStation{Standing=new Vector3(desk.x,y+.08f,desk.y+1.15f),Desktop=new Vector3(desk.x,y+.85f,desk.y)});
            }
            var entryColor=InteriorSurface("Entrance paint",new Color(.08f,.38f,.28f),.3f);
            for(int side=0;side<polygon.Count;side++)
            {
                var start=polygon[side];var end=polygon[(side+1)%polygon.Count];float length=(end-start).magnitude;if(length<.01f)continue;
                var tangent=(end-start)/length;var normal=new Vector2(tangent.y,-tangent.x);var mid=(start+end)*.5f;
                if(EnterableBuilding.Inside(mid+normal*.15f,polygon))normal=-normal;
                Vector3 midpoint=new Vector3(mid.x,0,mid.y),direction=new Vector3(tangent.x,0,tangent.y);
                var rotation=Quaternion.Euler(0,-Mathf.Atan2(tangent.y,tangent.x)*Mathf.Rad2Deg,0);
                if(streetStudy)
                {
                    // Grounded plinth excludes the door, corner pipes have no collision.
                    for(int flank=0;flank<2;flank++)
                    {
                        float a=flank==0?-length*.5f:1.25f,b=flank==0?-1.25f:length*.5f;
                        if(side!=doorEdge){if(flank==1)continue;a=-length*.5f;b=length*.5f;}
                        if(b<=a)continue;
                        int before=pieces.Count;
                        decor("Weathered masonry plinth",midpoint+direction*((a+b)*.5f)+new Vector3(normal.x,0,normal.y)*.15f+Vector3.up*.22f,new Vector3(b-a,.44f,.09f),stoneTrim);
                        if(pieces.Count>before)pieces[pieces.Count-1].transform.localRotation=rotation;
                    }
                    if(length>7)
                    {
                        int before=pieces.Count;
                        decor("Corner rainwater downpipe",midpoint+direction*(length*.5f-.28f)+new Vector3(normal.x,0,normal.y)*.2f+Vector3.up*(height*.5f),new Vector3(.09f,height,.09f),trim);
                        if(pieces.Count>before)pieces[pieces.Count-1].transform.localRotation=rotation;
                    }
                }
                // Shallow projecting stone courses make each storey read from a sniper vantage.
                if(detailed)
                {
                    float course=MapFeatureStyle.StoreyHeight(feature);
                    for(float elevation=course;elevation<=height+.01f;elevation+=course)
                    {
                        int before=pieces.Count;
                        decor("Exterior cornice course",midpoint+new Vector3(normal.x,0,normal.y)*.16f+Vector3.up*(elevation-.08f),new Vector3(length+.08f,streetStudy?.07f:.16f,streetStudy?.12f:.28f),stoneTrim);
                        if(pieces.Count>before)pieces[pieces.Count-1].transform.localRotation=rotation;
                    }
                    if(stairs)
                    {
                        int before=pieces.Count;
                        box("Rooftop masonry parapet",midpoint+Vector3.up*(height+.18f),new Vector3(length,.36f,.18f),stoneTrim);
                        if(pieces.Count>before)pieces[pieces.Count-1].transform.localRotation=rotation;
                        before=pieces.Count;
                        decor("Parapet stone coping",midpoint+Vector3.up*(height+.38f),new Vector3(length,.08f,.27f),stoneTrim);
                        if(pieces.Count>before)pieces[pieces.Count-1].transform.localRotation=rotation;
                    }
                    int pilasters=Mathf.Max(1,Mathf.FloorToInt(length/3));if(pilasters%2==0)pilasters++;
                    for(int column=0;column<=pilasters;column+=(streetStudy?pilasters:(pilasters>12?2:1)))
                    {
                        float alongWall=-length*.5f+column*length/pilasters;
                        // Avoid the ground-floor entrance and its approach.
                        if(side==doorEdge && Mathf.Abs(alongWall)<1.3f)continue;
                        int before=pieces.Count;
                        decor("Facade stone pilaster",midpoint+direction*alongWall+new Vector3(normal.x,0,normal.y)*.18f+Vector3.up*(height*.5f),new Vector3(streetStudy?.09f:.18f,height,streetStudy?.09f:.18f),stoneTrim);
                        if(pieces.Count>before)pieces[pieces.Count-1].transform.localRotation=rotation;
                    }
                }
                System.Action<float,float,float,float,string,Material> panel=(a,b,bottom,top,label,surface)=>
                {
                    int before=pieces.Count;box(label,midpoint+direction*((a+b)*.5f)+Vector3.up*((bottom+top)*.5f),new Vector3(b-a,top-bottom,surface==trim?(streetStudy?.07f:.36f):.24f),surface);
                    if(pieces.Count>before)pieces[pieces.Count-1].transform.localRotation=rotation;
                    if((surface==upperFacade || (streetStudy && surface==wall)) && pieces.Count>before)
                    {
                        // Tall upper shells retain real-world floor/window scale rather than stretching one row.
                        var filter=pieces[pieces.Count-1].GetComponent<MeshFilter>();
                        var mapped=Instantiate(filter.sharedMesh);var coordinates=mapped.uv;
                        for(int vertex=0;vertex<coordinates.Length;vertex++)coordinates[vertex]=Vector2.Scale(coordinates[vertex],new Vector2((b-a)/8f,(top-bottom)/(2*MapFeatureStyle.StoreyHeight(feature))));
                        mapped.uv=coordinates;meshes.Add(mapped);filter.sharedMesh=mapped;
                    }
                    // An inward plaster skin gives the room its own finish; the mapped facade stays outside.
                    if(surface==wall && bottom>=0 && top>bottom)
                    {
                        Vector3 inward=new Vector3(-normal.x,0,-normal.y);
                        before=pieces.Count;
                        decor("Interior plaster lining",midpoint+direction*((a+b)*.5f)+inward*.135f+Vector3.up*((bottom+top)*.5f),new Vector3(b-a,top-bottom,.025f),room);
                        if(pieces.Count>before)pieces[pieces.Count-1].transform.localRotation=rotation;
                        if(Mathf.Abs(bottom/levelHeight-Mathf.Round(bottom/levelHeight))<.01f)
                        {
                            before=pieces.Count;
                            decor("Interior skirting",midpoint+direction*((a+b)*.5f)+inward*.155f+Vector3.up*(bottom+.07f),new Vector3(b-a,.14f,.035f),trim);
                            if(pieces.Count>before)pieces[pieces.Count-1].transform.localRotation=rotation;
                        }
                    }
                };
                float foundationBottom=0;
                foreach(var sample in new[]{start,mid,end}){var gp=transform.InverseTransformPoint(root.transform.TransformPoint(new Vector3(sample.x,0,sample.y)));foundationBottom=Mathf.Min(foundationBottom,Ground(gp.x,gp.z)-gp.y-.3f);}
                panel(-length*.5f,length*.5f,foundationBottom,0,"Ground-connected foundation",wall);
                if(!stairs && height>levelHeight)panel(-length*.5f,length*.5f,levelHeight,height,"Upper building shell",upperFacade);
                for(int level=0;level<floors;level++)
                {
                    float baseY=level*levelHeight;
                    float spacing=streetStudy?(architecture==0?2.8f:architecture==1?3.7f:3.2f):3;
                    int bays=Mathf.Max(1,Mathf.FloorToInt(length/spacing));if(bays%2==0)bays++;
                    float bay=length/bays;
                    for(int b=0;b<bays;b++)
                    {
                        float left=-length*.5f+b*bay,right=left+bay;
                        bool door=level==0 && side==doorEdge && b==bays/2;
                        if(!door && bay<1.6f){panel(left,right,baseY,baseY+levelHeight,"Exterior wall",wall);continue;}
                        // Keep a real usable opening on every edge/floor. The other windows are
                        // smaller, recessed glazing rather than identical floor-to-ceiling holes.
                        bool openWindow=!streetStudy || b==bays/2 || (level==0 && side==doorEdge && b==0);
                        float closedWidth=shop && level==0?2.6f:architecture==0?1.15f:architecture==1?2.2f:1.55f;
                        float width=door?2f:Mathf.Min(streetStudy && !openWindow?closedWidth:1.8f,bay-.6f),centerX=(left+right)*.5f;
                        // Open windows must clear the standing capsule above the sill during a jump.
                        float bottom=door?0:streetStudy && !openWindow?(shop && level==0?.45f:architecture==0?.8f:1.05f):.65f,top=door?2.4f:streetStudy && !openWindow?(architecture==0?2.5f:2.35f):2.8f;
                        panel(left,centerX-width*.5f,baseY,baseY+levelHeight,"Exterior wall",wall);
                        panel(centerX+width*.5f,right,baseY,baseY+levelHeight,"Exterior wall",wall);
                        if(!door)panel(centerX-width*.5f,centerX+width*.5f,baseY,baseY+bottom,"Masonry below opening",wall);
                        panel(centerX-width*.5f,centerX+width*.5f,baseY+top,baseY+Mathf.Min(levelHeight,3.2f),"Opening lintel",wall);
                        if(levelHeight>3.2f)panel(centerX-width*.5f,centerX+width*.5f,baseY+3.2f,baseY+levelHeight,"Upper facade window bays",upperFacade);
                        if(door)
                        {
                            if(streetStudy)
                            {
                                Vector3 outVector=new Vector3(normal.x,0,normal.y);
                                if(shop && length>9)
                                {
                                    // Reuse the already imported PBR street pack beside closed bays.
                                    CGStreetProp(root.transform,"garbage_bin_d_05",midpoint+direction*Mathf.Max(3.3f,length*.4f)+outVector*.55f,rotation.eulerAngles.y);
                                }
                                int before=pieces.Count;
                                decor("Street study entrance canopy",midpoint+outVector*.48f+Vector3.up*2.95f,new Vector3(3.1f,.09f,1.1f),trim);
                                if(pieces.Count>before)pieces[pieces.Count-1].transform.localRotation=rotation;
                                before=pieces.Count;
                                decor("Street study warm canopy strip",midpoint+outVector*.75f+Vector3.up*2.88f,new Vector3(2.6f,.035f,.06f),lamp);
                                if(pieces.Count>before)pieces[pieces.Count-1].transform.localRotation=rotation;
                                if(!string.IsNullOrWhiteSpace(feature.Name))
                                {
                                    var accent=SectorSignage.IsShop(feature)?StreetStudyAccent(MapFeatureStyle.ResolveFacadeMaterial(feature)=="brick"):trim;
                                    before=pieces.Count;
                                    decor("Street study sign edge",midpoint+outVector*.19f+Vector3.up*3.35f,new Vector3(2.9f,.045f,.06f),accent);
                                    if(pieces.Count>before)pieces[pieces.Count-1].transform.localRotation=rotation;
                                    WorldSign.Create(root.transform,feature.Name,midpoint+outVector*.18f+Vector3.up*3.55f,Quaternion.LookRotation(outVector),new Vector2(2.9f,.38f),trim,true);
                                }
                            }
                            interior.Entrance=midpoint;
                            Vector3 outward=new Vector3(normal.x,0,normal.y);interior.EntranceOutward=outward;
                            Vector3 sectorDoor=transform.InverseTransformPoint(root.transform.TransformPoint(midpoint));
                            float drop=root.transform.position.y-transform.position.y-Ground(sectorDoor.x,sectorDoor.z);
                            int entrySteps=Mathf.Clamp(Mathf.CeilToInt(drop/.2f),1,24);
                            for(int step=0;step<entrySteps;step++)
                            {
                                box("Entrance step",midpoint+outward*(.25f+step*.35f)+Vector3.down*(.1f+step*.2f),new Vector3(2.2f,.18f,.5f),floor);
                                pieces[pieces.Count-1].transform.localRotation=rotation;
                            }
                            panel(-1.15f,-1f,0,2.4f,"Green entrance frame",entryColor);panel(1f,1.15f,0,2.4f,"Green entrance frame",entryColor);
                            WorldSign.Create(root.transform,"OPEN // ENTER",midpoint+outward*.18f+Vector3.up*2.65f,Quaternion.LookRotation(outward),new Vector2(2.3f,.35f),entryColor,true);
                        }
                        else
                        {
                            if(streetStudy && level>0 && (b+level+side)%3==0 && bay-width>.75f)
                            {
                                // Shutters sit on masonry beside the aperture, preserving vault and firing width.
                                foreach(float flank in new[]{-1f,1f})
                                {
                                    var shutter=midpoint+direction*(centerX+flank*(width*.5f+.18f))+new Vector3(normal.x,0,normal.y)*.17f+Vector3.up*(baseY+1.7f);
                                    int before=pieces.Count;
                                    decor("Street study folded shutter",shutter,new Vector3(.27f,1.8f,.055f),trim);
                                    if(pieces.Count>before)pieces[pieces.Count-1].transform.localRotation=rotation;
                                    for(int slat=0;slat<7;slat++)
                                    {
                                        before=pieces.Count;
                                        decor("Street study shutter louvre",shutter+new Vector3(normal.x,0,normal.y)*.04f+Vector3.up*(-.65f+slat*.21f),new Vector3(.25f,.035f,.035f),stoneTrim);
                                        if(pieces.Count>before)pieces[pieces.Count-1].transform.localRotation=rotation;
                                    }
                                }
                            }
                            if(openWindow)
                            {
                                interior.FiringWindows.Add(midpoint+direction*centerX+Vector3.up*(baseY+1.7f));
                                interior.OpenWindows.Add(new EnterableBuilding.OpenWindow{Center=midpoint+direction*centerX+Vector3.up*baseY,Outward=new Vector3(normal.x,0,normal.y),Width=width,Sill=bottom+.02f});
                            }
                            else
                            {
                                // Opaque glass collides with bullets and players. It is deliberately
                                // absent from the mission/vault opening lists; no invisible wall in an open bay.
                                int before=pieces.Count;
                                int roomStyle=(b/3+side*7+level/2*5+Mathf.RoundToInt(center.x+center.y))&7;
                                if(shop && level==0)roomStyle=6;
                                box("Closed recessed window",midpoint+direction*centerX-new Vector3(normal.x,0,normal.y)*.055f+Vector3.up*(baseY+(bottom+top)*.5f),new Vector3(width,top-bottom,.045f),StreetStudyWindow(roomStyle,shop && level==0 && side==doorEdge));
                                pieces[pieces.Count-1].transform.localRotation=rotation;
                                before=pieces.Count;
                                decor("Window centre mullion",midpoint+direction*centerX+new Vector3(normal.x,0,normal.y)*.015f+Vector3.up*(baseY+(bottom+top)*.5f),new Vector3(.045f,top-bottom,.07f),trim);
                                if(pieces.Count>before)pieces[pieces.Count-1].transform.localRotation=rotation;
                                before=pieces.Count;
                                decor("Window transom",midpoint+direction*centerX+new Vector3(normal.x,0,normal.y)*.015f+Vector3.up*(baseY+bottom+(top-bottom)*.67f),new Vector3(width,.04f,.07f),trim);
                                if(pieces.Count>before)pieces[pieces.Count-1].transform.localRotation=rotation;
                            }
                            if(streetStudy && shop && level==0 && !openWindow)
                            {
                                var display=midpoint+direction*centerX-new Vector3(normal.x,0,normal.y)*.42f;
                                int before=pieces.Count;decor("Shop display counter",display+Vector3.up*.7f,new Vector3(width*.8f,.35f,.35f),furniture);
                                if(pieces.Count>before)pieces[pieces.Count-1].transform.localRotation=rotation;
                                for(int item=0;item<3;item++)
                                {
                                    before=pieces.Count;decor("Shop display packages",display+direction*((item-1)*width*.22f)+Vector3.up*(1+item*.09f),new Vector3(.22f,.25f+item*.12f,.2f),item==1?room:stoneTrim);
                                    if(pieces.Count>before)pieces[pieces.Count-1].transform.localRotation=rotation;
                                }
                            }
                            panel(centerX-width*.5f-.05f,centerX+width*.5f+.05f,baseY+bottom-.06f,baseY+bottom+.02f,"Window sill",trim);
                            panel(centerX-width*.5f-.04f,centerX-width*.5f+.025f,baseY+bottom+.02f,baseY+top,"Window jamb",trim);
                            panel(centerX+width*.5f-.025f,centerX+width*.5f+.04f,baseY+bottom+.02f,baseY+top,"Window jamb",trim);
                            panel(centerX-width*.5f,centerX+width*.5f,baseY+top-.045f,baseY+top+.02f,"Window header",trim);
                        }
                    }
                }
            }
            if(!stairs && height>levelHeight)BuildInteriorFloor(root.transform,polygon,null,height,roof);
            if(detailed && stairs)
            {
                // A compact rooftop utility kit, placed away from the stair landing.
                var utility=new Vector2(-w*.25f,d*.2f);
                var clearance=new Rect(stairX-1.5f,stairOrigin.y-run*.5f-1.5f,3,run+3);
                bool fits=!clearance.Contains(utility);
                foreach(var offset in new[]{new Vector2(-1,-.7f),new Vector2(1,-.7f),new Vector2(1,.7f),new Vector2(-1,.7f)})
                    if(!EnterableBuilding.Inside(utility+offset,polygon))fits=false;
                if(fits)
                {
                    var at=new Vector3(utility.x,height+.35f,utility.y);
                    box("Rooftop HVAC enclosure",at,new Vector3(1.6f,.7f,1.05f),stoneTrim);
                    foreach(float x in new[]{-.4f,.4f})
                    {
                        decor("HVAC fan housing",at+new Vector3(x,.36f,0),new Vector3(.55f,.04f,.7f),trim);
                        for(int slat=0;slat<7;slat++)decor("HVAC grille",at+new Vector3(x,.39f,-.27f+slat*.09f),new Vector3(.5f,.018f,.025f),stoneTrim);
                    }
                    decor("Rooftop service conduit",at+new Vector3(.86f,-.18f,0),new Vector3(.14f,.32f,.14f),trim);
                    if(streetStudy)
                    {
                        decor("HVAC contact pad",at+Vector3.down*.33f,new Vector3(2.1f,.025f,1.5f),trim);
                        decor("Insulated roof duct",at+new Vector3(0,.06f,.86f),new Vector3(.55f,.38f,.55f),trim);
                        decor("Duct weather hood",at+new Vector3(0,.27f,.86f),new Vector3(.66f,.045f,.64f),stoneTrim);
                    }
                }
                if(streetStudy)
                {
                    foreach(var candidate in new[]{new Vector2(-w*.5f+1.8f,-d*.5f+1.8f),new Vector2(w*.5f-1.8f,d*.5f-1.8f)})
                    {
                        if(clearance.Overlaps(new Rect(candidate.x-1.1f,candidate.y-1.1f,2.2f,2.2f)))continue;
                        bool inside=true;foreach(var offset in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)})if(!EnterableBuilding.Inside(candidate+offset,polygon))inside=false;
                        if(!inside)continue;
                        var at=new Vector3(candidate.x,height,candidate.y);
                        decor("Roof repair membrane",at+Vector3.up*.016f,new Vector3(2.1f,.015f,2.1f),trim);
                        box("Roof ventilation plinth",at+Vector3.up*.32f,new Vector3(.65f,.64f,.65f),stoneTrim);
                        decor("Roof vent rain cap",at+Vector3.up*.69f,new Vector3(.85f,.08f,.85f),trim);
                        for(int seam=0;seam<4;seam++)decor("Roof vent louvre",at+new Vector3(0,.23f+seam*.08f,-.335f),new Vector3(.55f,.03f,.025f),trim);
                    }
                }
            }
            if(streetStudy)
            {
                BuildStudyForecourt(feature,features);
                if(MapFeatureStyle.Text(feature,"training_frontage")!="true")BuildStudyApproach(interior,features);
            }
            // Batch only render geometry. Segment colliders remain in place around every genuine opening.
            var groups=new Dictionary<Material,List<CombineInstance>>();
            foreach(var part in pieces)
            {
                var renderer=part.GetComponent<MeshRenderer>();var filter=part.GetComponent<MeshFilter>();
                if(!groups.TryGetValue(renderer.sharedMaterial,out var group))groups[renderer.sharedMaterial]=group=new List<CombineInstance>();
                group.Add(new CombineInstance{mesh=filter.sharedMesh,transform=root.transform.worldToLocalMatrix*part.transform.localToWorldMatrix});
                renderer.enabled=false;
            }
            foreach(var pair in groups)
            {
                var mesh=new Mesh{name="Enterable architecture",indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray());meshes.Add(mesh);
                var batch=new GameObject("Interior architecture");batch.transform.SetParent(root.transform,false);batch.AddComponent<MeshFilter>().sharedMesh=mesh;batch.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;
            }
        }
    }
}
