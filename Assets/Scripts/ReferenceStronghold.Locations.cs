using System.Collections.Generic;
using UnityEngine;
namespace GeoSniper
{
    public sealed partial class ReferenceStronghold
    {
        readonly List<Vector3> supplySites=new List<Vector3>();
        readonly List<Mesh> locationMeshes=new List<Mesh>();
        readonly Dictionary<Material,List<CombineInstance>> pieces=new Dictionary<Material,List<CombineInstance>>();
        readonly List<Vector3> siteFloors=new List<Vector3>(),siteDoors=new List<Vector3>(),siteFiring=new List<Vector3>(),siteStairs=new List<Vector3>();
        readonly List<int> stairStarts=new List<int>();
        Mesh unitBox;
        Material masonry,timber,steel,concrete,canvas,glass,accent;
        void BuildLocation()
        {
            var prototype=GameObject.CreatePrimitive(PrimitiveType.Cube);prototype.SetActive(false);unitBox=prototype.GetComponent<MeshFilter>().sharedMesh;Destroy(prototype);
            masonry=Surface("Location plaster","Plaster",new Color(.72f,.69f,.60f));
            timber=Surface("Location timber","Timber",new Color(.55f,.39f,.25f));
            concrete=Surface("Location concrete","Stone",new Color(.57f,.57f,.53f));
            steel=Surface("Location painted steel","",new Color(.23f,.28f,.29f),.66f);steel.SetFloat("_Metallic",.35f);
            canvas=Surface("Tent canvas","Sandbag_surface_0",new Color(1.5f,1.6f,1.05f));
            glass=Surface("Recessed shop display glass","",new Color(.15f,.24f,.26f),.2f);
            glass.EnableKeyword("_EMISSION");glass.SetColor("_EmissionColor",new Color(.10f,.075f,.04f));
            accent=Surface("Wayfinding sign","",new Color(.025f,.16f,.18f));accent.EnableKeyword("_EMISSION");accent.SetColor("_EmissionColor",new Color(.02f,.22f,.25f));
            switch(Profile.Kind)
            {
                case CampaignLocationKind.FieldCamp:BuildCamp();break;
                case CampaignLocationKind.ShoppingGallery:BuildMall();break;
                case CampaignLocationKind.FreightDepot:BuildDepot();break;
                default:BuildApartments();break;
            }
            foreach(var group in pieces)
            {
                var mesh=new Mesh{name=Profile.Family+" batched surface"};mesh.CombineMeshes(group.Value.ToArray(),true,true);locationMeshes.Add(mesh);
                var obj=new GameObject(mesh.name);obj.transform.SetParent(transform,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
            }
            pieces.Clear();
            Layout=new GalleryLayout{floors=siteFloors.ToArray(),doors=siteDoors.ToArray(),firing=siteFiring.ToArray(),stairs=siteStairs.ToArray(),stairStarts=stairStarts.ToArray()};
        }
        void Piece(string name,Vector3 at,Vector3 size,Material material,bool solid=true,Quaternion? rotation=null)
        {
            if(!pieces.TryGetValue(material,out var list)){list=new List<CombineInstance>();pieces[material]=list;}
            var turn=rotation??Quaternion.identity;
            list.Add(new CombineInstance{mesh=unitBox,transform=Matrix4x4.TRS(at,turn,size)});
            if(solid){var obj=new GameObject(name);obj.transform.SetParent(transform,false);obj.transform.localPosition=at;obj.transform.localRotation=turn;var box=obj.AddComponent<BoxCollider>();box.size=size;}
        }
        void Deck(Vector3 center,Vector3 size){Piece("Walkable floor",center,size,concrete);}
        void Rail(Vector3 at,float width){Piece("Low balcony rail",at,new Vector3(width,.10f,.10f),steel);for(float x=-width/2;x<=width/2;x+=1.2f)Piece("Rail post",at+new Vector3(x,-.36f,0),new Vector3(.065f,.8f,.065f),steel);}
        void Stairs(float x,float startZ,float baseY,float rise,int count,float runDirection=1)
        {
            stairStarts.Add(siteStairs.Count);
            float tread=.42f,step=rise/count;
            siteStairs.Add(new Vector3(x,baseY+.07f,startZ-runDirection*.2f));
            for(int i=0;i<count;i++)
            {
                float h=(i+1)*step,z=startZ+runDirection*i*tread;
                Piece("Accessible stair tread",new Vector3(x,baseY+h/2,z),new Vector3(1.55f,h,.45f),concrete);
                siteStairs.Add(new Vector3(x,baseY+h+.06f,z));
            }
            float end=startZ+runDirection*(count-1)*tread;
            Deck(new Vector3(x,baseY+rise-.07f,end+runDirection*.6f),new Vector3(2,.14f,1.35f));
            siteStairs.Add(new Vector3(x,baseY+rise+.06f,end+runDirection*.6f));
            for(int side=-1;side<=1;side+=2)
            {
                var a=new Vector3(x+side*.8f,baseY+.9f,startZ);var b=new Vector3(x+side*.8f,baseY+rise+.9f,end);
                Piece("Stair handrail",(a+b)/2,new Vector3(.065f,(b-a).magnitude,.065f),steel,false,Quaternion.FromToRotation(Vector3.up,b-a));
            }
        }
        void Sign(Vector3 at,string text,float width)
        {
            Piece("Site sign",at,new Vector3(width,.65f,.14f),accent,false);
            var obj=new GameObject("Location sign text");obj.transform.SetParent(transform,false);obj.transform.localPosition=at+Vector3.back*.08f;
            var label=obj.AddComponent<TextMesh>();label.text=text;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.characterSize=.12f;label.fontSize=40;label.color=new Color(.80f,.92f,.91f);
        }
        void BuildApartments()
        {
            LicensedSiteAsset("BrickApartments",new Vector3(0,.1f,0),true);
            LicensedStreetProp("garbage_bin_d_05",new Vector3(-7.8f,.1f,-4),.95f);
            siteDoors.Add(new Vector3(0,1.2f,-2.0f));
            siteFloors.Add(new Vector3(0,.17f,-1.3f));
            int stories=2+(Profile.Stage/10)%2;float pitch=3.1f;
            for(int level=0;level<=stories;level++)
            {
                float y=level*pitch;
                Deck(new Vector3(0,y,0),new Vector3(14,.2f,6.2f));
                Deck(new Vector3(0,y,-4.1f),new Vector3(14,.2f,1.8f));
                siteFloors.Add(new Vector3(0,y+.17f,-4.1f));supplySites.Add(new Vector3(6.1f,y+.17f,-4.1f));
                if(level==stories)
                {
                    Rail(new Vector3(0,y+.7f,-4.95f),14);siteFiring.Add(new Vector3(-4.8f,y+.18f,-4.2f));break;
                }
                // The licensed brick wing supplies the varied windows and masonry.
                // Open front galleries retain unobstructed mission firing positions.
                for(int bay=0;bay<4;bay++)
                {
                    float x=-5.1f+bay*3.4f;
                    if(level==0)siteDoors.Add(new Vector3(x,y+1.4f,-4.1f));
                    if(level>0)siteFiring.Add(new Vector3(x,y+.18f,-4.2f));
                }
                if(level>0)Rail(new Vector3(0,y+.8f,-4.95f),14);
                float start=level%2==0?-.6f:-7.0f,direction=level%2==0?-1:1;
                // Side-by-side return flights prevent the next staircase from becoming
                // a low ceiling above the player's head halfway up the current flight.
                Stairs(level%2==0?7.9f:9.55f,start,y+.1f,pitch,16,direction);
                Deck(new Vector3(6.9f,y+pitch,-4.1f),new Vector3(1.3f,.2f,8));
                Deck(new Vector3(8.4f,y+pitch,level%2==0?-7.25f:-.45f),new Vector3(3.7f,.2f,.9f));
            }
            Sign(new Vector3(0,2.7f,-3.3f),"RESIDENCES / "+(Profile.Stage+1),4);
        }
        void Tent(Vector3 at,float length)
        {
            Piece("Tent deck",at+Vector3.up*.05f,new Vector3(4.6f,.1f,5.9f),timber);
            var tent=LicensedSiteAsset("MilitaryTent",at+Vector3.up*.10f,true);
            if(tent!=null)tent.transform.localScale=Vector3.Scale(tent.transform.localScale,new Vector3(1,1.45f,1));
            siteDoors.Add(at+new Vector3(0,1.3f,-2.8f));
        }
        void BuildCamp()
        {
            LicensedStreetProp("barricade_01_a_01",new Vector3(3,.1f,-11),.85f,15);
            Deck(new Vector3(0,-.015f,0),new Vector3(20,.1f,25));siteFloors.Add(new Vector3(0,.09f,0));
            int tents=3+Profile.Stage%3;
            for(int i=0;i<tents;i++)Tent(new Vector3(i%2==0?-4.8f:4.8f,.04f,-5.1f+(i/2)*7.2f),4.3f+(Profile.Stage%2)*.5f);
            for(int side=-1;side<=1;side+=2)
            {
                float h=1.9f+(Profile.Stage/10)*.12f;
                Deck(new Vector3(side*6.8f,h,-9),new Vector3(3,.16f,3));
                for(int corner=-1;corner<=1;corner+=2)Piece("Watch platform support",new Vector3(side*6.8f+corner*1.3f,h/2,-8),new Vector3(.18f,h,.18f),timber);
                Rail(new Vector3(side*6.8f,h+.7f,-10.4f),3);siteFiring.Add(new Vector3(side*6.8f,h+.14f,-9.8f));
                siteFloors.Add(new Vector3(side*6.8f,h+.14f,-9));supplySites.Add(new Vector3(side*6.8f+.6f,h+.14f,-8.3f));
                Stairs(side*8.5f,-3.6f,.04f,h,12,-1);
            }
            siteFiring.Add(new Vector3(0,.12f,-9));supplySites.Add(new Vector3(1.8f,.12f,8));
            Sign(new Vector3(0,2.2f,-10.5f),"FIELD STATION "+(Profile.Stage+1),5);
            for(int side=-1;side<=1;side+=2)Piece("Field station sign post",new Vector3(side*2,1.0f,-10.5f),new Vector3(.12f,2,.12f),timber);
        }
        void BuildMall()
        {
            LicensedStreetProp("garbage_bin_d_05",new Vector3(8,.1f,-10.6f),.95f);
            float span=3.0f+(Profile.Stage/10)*.2f;int bays=4+Profile.Stage%3;
            Deck(new Vector3(0,0,0),new Vector3(18,.18f,23));
            siteFloors.Add(new Vector3(0,.16f,-9));supplySites.Add(new Vector3(7.5f,.16f,-8.8f));
            for(int side=-1;side<=1;side+=2)
            {
                Deck(new Vector3(side*(span+9)/2,3.3f,0),new Vector3(9-span,.18f,22));
                Piece("Mall outer wall",new Vector3(side*8.85f,3.3f,0),new Vector3(.3f,6.4f,22),masonry);
                for(int level=0;level<2;level++)for(int bay=0;bay<bays;bay++)
                {
                    float z=-6.8f+bay*(15f/(bays-1)),y=level*3.3f;
                    Piece("Shop divider",new Vector3(side*6.8f,y+1.55f,z-1.4f),new Vector3(4.1f,2.9f,.16f),masonry);
                    Piece("Shop portal lintel",new Vector3(side*4.8f,y+2.75f,z),new Vector3(.16f,.3f,2.6f),timber);
                    Piece("Shop display",new Vector3(side*8.5f,y+1.4f,z),new Vector3(.06f,1.8f,2.1f),glass,false);
                    Piece("Display counter",new Vector3(side*7.4f,y+.4f,z),new Vector3(.6f,.7f,1.7f),timber);
                    if(bay%2==0)LicensedSiteAsset("ShopShelf",new Vector3(side*6.7f,y+.1f,z),true,side*90);
                    else if(bay==1)LicensedSiteAsset("ShopFridge",new Vector3(side*7.8f,y+.1f,z),true,side*90);
                }
            }
            for(int end=-1;end<=1;end+=2)Deck(new Vector3(0,3.3f,end*9.5f),new Vector3(18,.18f,3));
            Piece("Mall rear wall",new Vector3(0,3.3f,11.35f),new Vector3(18,6.4f,.3f),masonry);
            Piece("Recessed mall canopy",new Vector3(0,6.6f,4),new Vector3(18,.18f,14),steel);
            Rail(new Vector3(0,4.0f,-10.9f),18);
            siteFloors.Add(new Vector3(0,3.46f,-9));supplySites.Add(new Vector3(7.5f,3.46f,-9));
            foreach(float x in new[]{-5f,0f,5f}){siteFiring.Add(new Vector3(x,3.46f,-9.7f));siteDoors.Add(new Vector3(x,1.4f,-11.4f));}
            Stairs(-1.1f,0,.09f,3.3f,18,-1);Deck(new Vector3(-1.1f,3.3f,-8),new Vector3(2.2f,.18f,2));
            Sign(new Vector3(0,6.5f,-11),"GALLERY / "+(Profile.Stage+1),8);
            Piece("Mall entrance fascia",new Vector3(0,6.5f,-10.8f),new Vector3(18,.65f,.3f),steel);
        }
        void BuildDepot()
        {
            var warehouse=LicensedSiteAsset("Warehouse",new Vector3(-2,.1f,0),true);
            if(warehouse!=null)warehouse.transform.localScale=new Vector3(1,1.8f,1);
            LicensedStreetProp("barricade_01_a_01",new Vector3(3,.1f,-11.8f),.85f);
            Deck(new Vector3(0,0,0),new Vector3(20,.18f,28));siteFloors.Add(new Vector3(0,.16f,-12));
            Deck(new Vector3(-1,1.15f,-7.8f),new Vector3(14,.2f,6));siteFloors.Add(new Vector3(-1,1.32f,-8.7f));supplySites.Add(new Vector3(-6.8f,1.32f,-7.7f));
            for(int bay=0;bay<3;bay++)
            {
                float x=-6+bay*4;
                Piece("Loading bay pillar",new Vector3(x-1.8f,2.6f,-5.2f),new Vector3(.35f,5,.35f),concrete);
                Piece("Loading door header",new Vector3(x,4.75f,-5.2f),new Vector3(3.6f,.5f,.3f),steel);
                Piece("Raised roller shutter",new Vector3(x,4.2f,-5.35f),new Vector3(3.4f,.25f,.25f),steel);
                siteDoors.Add(new Vector3(x,2.6f,-5.2f));
            }
            Deck(new Vector3(6.8f,3.15f,-8.05f),new Vector3(3.2f,.2f,3.1f));
            Piece("Depot office rear pier",new Vector3(5.3f,1.65f,-6.6f),new Vector3(.2f,3,.2f),masonry);
            Piece("Office side",new Vector3(8.3f,1.65f,-7.6f),new Vector3(.2f,3,4),masonry);
            Rail(new Vector3(6.8f,3.85f,-9.5f),3.2f);
            Stairs(6.8f,-.1f,.1f,3.15f,17,-1);
            Stairs(-4.5f,-13.5f,.1f,1.15f,7,1);
            siteFiring.Add(new Vector3(-4.6f,1.32f,-9.7f));siteFiring.Add(new Vector3(1.5f,1.32f,-9.7f));siteFiring.Add(new Vector3(6.8f,3.32f,-8.8f));
            siteFloors.Add(new Vector3(6.8f,3.32f,-7.6f));supplySites.Add(new Vector3(5.85f,3.32f,-7.1f));
            Sign(new Vector3(-2,4.0f,-5.45f),"FREIGHT / "+(Profile.Stage+1),7);
        }
    }
}
