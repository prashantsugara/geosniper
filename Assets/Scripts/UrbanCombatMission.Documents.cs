using System.Collections.Generic;
using UnityEngine;
namespace GeoSniper
{
    public sealed partial class UrbanCombatMission
    {
        sealed class ArchiveSite {public EnterableBuilding Building;public Vector3 Standing,Desktop;}
        bool FindIndoorOperationStart(List<Vector3> ground,int required,out Vector3 start)
        {
            foreach(var candidate in ground)
            {
                int count=0;
                foreach(var building in EnterableBuilding.All)foreach(var station in building.EvidenceStations)
                {
                    var position=building.transform.TransformPoint(station.Standing);float distance=FlatDistance(candidate,position);
                    if(distance>=10 && distance<=180 && SupportedStandingPoint(position) && StoryReachable(candidate,position))count++;
                }
                if(count>=required){start=candidate;return true;}
            }
            start=Vector3.zero;return false;
        }
        void PlaceIndoorEvidence(Vector3 start,int required)
        {
            var sites=new List<ArchiveSite>();
            foreach(var building in EnterableBuilding.All)
                foreach(var station in building.EvidenceStations)
                {
                    var standing=building.transform.TransformPoint(station.Standing);
                    if(!SupportedStandingPoint(standing) || !StoryReachable(start,standing))continue;
                    sites.Add(new ArchiveSite{Building=building,Standing=standing,Desktop=building.transform.TransformPoint(station.Desktop)});
                }
            sites.Sort((a,b)=>
            {
                float delta=FlatDistance(a.Standing,start)-FlatDistance(b.Standing,start);
                if(a.Building!=b.Building || Mathf.Abs(delta)>.5f)return delta.CompareTo(0);
                int preferred=a.Building.Floors>1?1+stageIndex%(a.Building.Floors-1):0;
                return Mathf.Abs(a.Building.FloorAt(a.Standing)-preferred).CompareTo(Mathf.Abs(b.Building.FloorAt(b.Standing)-preferred));
            });
            var used=new HashSet<EnterableBuilding>();
            for(int pass=0;pass<2 && storyPoints.Count<required;pass++)foreach(var site in sites)
            {
                float d=FlatDistance(start,site.Standing);if(d<10 || d>180 || (pass==0 && LeakOperation && d<35) || (pass==0 && used.Contains(site.Building)))continue;
                bool duplicate=false;foreach(var point in storyPoints)
                    if(Vector3.Distance(site.Standing,point.Position)<(pass==0 && LeakOperation?30:4))duplicate=true;
                if(duplicate)continue;
                var visual=activeContract.Type==CampaignContractType.Sabotage?CreateRelayPanel(site.Desktop,site.Building.transform.rotation):CreateDocumentFolder(site.Desktop,site.Building.transform.rotation);
                visual.transform.SetParent(storyObjects.transform,true);
                // The room identity remains after the objective is collected.
                bool controlRoom=activeContract.Type==CampaignContractType.Sabotage;
                var roomMark=new GameObject(controlRoom?"Relay control station":"Dispatch archive station");
                roomMark.transform.SetParent(storyObjects.transform,false);
                roomMark.transform.SetPositionAndRotation(site.Desktop,site.Building.transform.rotation);
                var signMaterial=new Material(Shader.Find("Standard")){color=controlRoom?new Color(.24f,.19f,.08f):new Color(.08f,.19f,.24f)};
                ownedMaterials.Add(signMaterial);
                WorldSign.Create(roomMark.transform,controlRoom?"ELECTRICAL CONTROL\nAUTHORIZED PERSONNEL":"DISPATCH ARCHIVE\nCONFIDENTIAL RECORDS",
                    new Vector3(0,.29f,-.3f),Quaternion.identity,new Vector2(.85f,.26f),signMaterial,true);
                foreach(float post in new[]{-.29f,.29f})
                    RelayPart(roomMark,"Desktop placard support",new Vector3(post,.09f,-.3f),new Vector3(.018f,.18f,.018f),new Color(.18f,.21f,.23f),true);
                if(controlRoom)
                {
                    for(int circuit=0;circuit<3;circuit++)
                        RelayPart(roomMark,"Circuit conduit",new Vector3(-.57f+circuit*.045f,.03f,-.12f),new Vector3(.018f,.018f,.38f),new Color(.13f,.15f,.16f),true);
                }
                else
                {
                    for(int file=0;file<3;file++)
                        RelayPart(roomMark,"Indexed archive file",new Vector3(.48f,.025f+file*.035f,0),new Vector3(.17f,.025f,.3f),new Color(.35f+file*.08f,.28f,.17f));
                }
                storyPoints.Add(new StoryPoint{Position=site.Standing,Visual=visual,Building=site.Building,RestPosition=visual.transform.position,RestRotation=visual.transform.rotation});used.Add(site.Building);
                if(storyPoints.Count>=required)break;
            }
        }
        bool StoryPointInReach(StoryPoint point)=>StoryInReach(point.RestPosition-Vector3.up*.8f);
        GameObject CreateExtractionBeacon(Vector3 ground)
        {
            var root=new GameObject("Portable extraction signal");root.transform.position=ground;
            // Keep the exact extraction position clear. The case is a readable physical landmark beside it.
            RelayPart(root,"Weatherproof signal case",new Vector3(.9f,.15f,0),new Vector3(.48f,.28f,.34f),new Color(.16f,.22f,.18f),true);
            foreach(float x in new[]{.71f,1.09f})
                RelayPart(root,"Case latch",new Vector3(x,.19f,.18f),new Vector3(.045f,.09f,.025f),new Color(.52f,.55f,.53f),true);
            RelayPart(root,"Radio antenna",new Vector3(1.05f,.61f,-.08f),new Vector3(.018f,.7f,.018f),new Color(.08f,.10f,.09f),true);
            RelayPart(root,"Signal lamp base",new Vector3(.82f,.34f,0),new Vector3(.14f,.09f,.14f),new Color(.08f,.10f,.09f),true);
            var lens=RelayPart(root,"Green extraction lamp",new Vector3(.82f,.44f,0),new Vector3(.10f,.12f,.10f),new Color(.16f,.8f,.43f));
            var surface=lens.GetComponent<Renderer>().sharedMaterial;surface.EnableKeyword("_EMISSION");surface.SetColor("_EmissionColor",new Color(.12f,.65f,.3f));
            var ring=new GameObject("Extraction perimeter").AddComponent<LineRenderer>();ring.transform.SetParent(root.transform,false);ring.useWorldSpace=false;ring.loop=true;ring.positionCount=48;ring.widthMultiplier=.035f;
            for(int i=0;i<48;i++){float angle=i*Mathf.PI*2/48;ring.SetPosition(i,new Vector3(Mathf.Cos(angle)*1.5f,.035f,Mathf.Sin(angle)*1.5f));}
            ring.sharedMaterial=surface;return root;
        }
        GameObject CreateRelayPanel(Vector3 desktop,Quaternion rotation)
        {
            var panel=new GameObject("Meridian electrical relay panel");panel.transform.SetPositionAndRotation(desktop+Vector3.up*.25f,rotation);
            RelayPart(panel,"Steel enclosure",Vector3.zero,new Vector3(.48f,.5f,.22f),new Color(.32f,.36f,.39f),true);
            RelayPart(panel,"Recessed control face",new Vector3(0,0,.117f),new Vector3(.42f,.44f,.02f),new Color(.07f,.09f,.10f));
            var placard=GameObject.CreatePrimitive(PrimitiveType.Quad);placard.name="Relay identification placard";placard.transform.SetParent(panel.transform,false);placard.transform.localPosition=new Vector3(-.045f,.15f,.145f);placard.transform.localRotation=Quaternion.Euler(0,180,0);placard.transform.localScale=new Vector3(.25f,.125f,1);Destroy(placard.GetComponent<Collider>());
            var label=new Material(Shader.Find("Standard")){color=Color.white,mainTexture=Resources.Load<Texture2D>("Textures/RelayPlacard")};ownedMaterials.Add(label);placard.GetComponent<Renderer>().sharedMaterial=label;
            for(int i=0;i<4;i++)RelayPart(panel,"Vent",new Vector3(-.10f,.06f-i*.025f,.135f),new Vector3(.13f,.009f,.008f),Color.black);
            RelayPart(panel,"Power indicator",new Vector3(.12f,.15f,.14f),new Vector3(.04f,.04f,.015f),Color.green);
            RelayPart(panel,"Breaker base",new Vector3(.10f,-.05f,.14f),new Vector3(.10f,.15f,.025f),new Color(.3f,.3f,.3f));
            var lever=RelayPart(panel,"Disconnect lever",new Vector3(.10f,-.015f,.19f),new Vector3(.06f,.10f,.07f),new Color(.85f,.12f,.06f));lever.transform.localRotation=Quaternion.Euler(-25,0,0);
            for(int i=0;i<4;i++)RelayPart(panel,"Enclosure screw",new Vector3(i%2==0?-.19f:.19f,i<2?-.19f:.19f,.14f),new Vector3(.014f,.014f,.012f),new Color(.6f,.6f,.6f),true);
            var cable=new GameObject("Relay power cable").AddComponent<LineRenderer>();cable.transform.SetParent(panel.transform,false);cable.useWorldSpace=false;cable.widthMultiplier=.018f;cable.positionCount=5;cable.SetPositions(new[]{new Vector3(-.12f,-.25f,0),new Vector3(-.12f,-.3f,0),new Vector3(-.35f,-.3f,0),new Vector3(-.38f,-.3f,-.35f),new Vector3(-.1f,-.3f,-.38f)});cable.numCornerVertices=3;cable.sharedMaterial=panel.transform.Find("Vent").GetComponent<Renderer>().sharedMaterial;
            return panel;
        }
        GameObject RelayPart(GameObject root,string name,Vector3 position,Vector3 size,Color color,bool metal=false)
        {
            var part=CreateStoryBox(name,root.transform.position,size,color);part.transform.SetParent(root.transform,false);part.transform.localPosition=position;part.transform.localRotation=Quaternion.identity;
            var material=part.GetComponent<Renderer>().sharedMaterial;material.SetFloat("_Metallic",metal?.75f:.15f);material.SetFloat("_Glossiness",metal?.55f:.25f);return part;
        }
        void SwitchRelayOff(GameObject panel)
        {
            var lever=panel.transform.Find("Disconnect lever");if(lever!=null)lever.localRotation=Quaternion.Euler(25,0,0);
            var indicator=panel.transform.Find("Power indicator");if(indicator!=null)indicator.GetComponent<Renderer>().sharedMaterial.color=new Color(.12f,.13f,.12f);
        }
        GameObject CreateDocumentFolder(Vector3 desktop,Quaternion rotation)
        {
            var folder=new GameObject("Recoverable dispatch documents");folder.transform.SetPositionAndRotation(desktop,rotation);
            var cover=CreateStoryBox("Archive folder cover",desktop,new Vector3(.34f,.008f,.46f),new Color(.16f,.23f,.31f));cover.transform.SetParent(folder.transform,true);cover.transform.localRotation=Quaternion.identity;
            var paper=GameObject.CreatePrimitive(PrimitiveType.Quad);paper.name="Printed dispatch document";paper.transform.SetParent(folder.transform,false);paper.transform.localPosition=Vector3.up*.007f;paper.transform.localRotation=Quaternion.Euler(90,0,180);paper.transform.localScale=new Vector3(.28f,.40f,1);
            Destroy(paper.GetComponent<Collider>());
            var material=new Material(Shader.Find("Standard")){color=Color.white,mainTexture=Resources.Load<Texture2D>("Textures/StoryDocument")};ownedMaterials.Add(material);paper.GetComponent<Renderer>().sharedMaterial=material;
            var tab=CreateStoryBox("Archive index tab",desktop,new Vector3(.12f,.009f,.035f),new Color(.65f,.56f,.30f));tab.transform.SetParent(folder.transform,true);tab.transform.localPosition=new Vector3(-.08f,0,-.23f);tab.transform.localRotation=Quaternion.identity;
            return folder;
        }
    }
}
