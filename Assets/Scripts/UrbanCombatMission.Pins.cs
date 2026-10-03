using System;
using UnityEngine;
namespace GeoSniper
{
    public sealed partial class UrbanCombatMission
    {
        Texture2D waypointIcon;
        string mapPinNotice="";
        float mapPinNoticeUntil;
        void SetWaypoint(Vector3 point, string name)
        {
            waypointPin.Set(point, name);
            mapPinNotice = "";
        }
        bool SetSurfaceWaypoint(Vector3 point,string name)
        {
            if(!TryMapSurface(point,out var surface))
            {
                mapPinNotice="Choose a street or roof in the loaded area.";
                mapPinNoticeUntil=Time.unscaledTime+4;return false;
            }
            SetWaypoint(surface,name);return true;
        }
        public static bool TryMapSurface(Vector3 point,out Vector3 surface)
        {
            surface=default;float top=point.y+100;
            bool loaded=false;
            foreach(var world in SectorWorld.LoadedWorlds)
            {
                if(world==null) continue;
                var local=world.transform.InverseTransformPoint(point);
                if(Mathf.Abs(local.x)>320 || Mathf.Abs(local.z)>320) continue;
                loaded=true;
                foreach(var building in world.Buildings.Values)
                    if(building!=null)
                        foreach(var collider in building.GetComponentsInChildren<Collider>()) top=Mathf.Max(top,collider.bounds.max.y+10);
                top=Mathf.Max(top,world.transform.position.y+400);
            }
            if(!loaded) return false;
            Physics.SyncTransforms();
            var hits=Physics.RaycastAll(new Vector3(point.x,top,point.z),Vector3.down,top-point.y+1000,~0,QueryTriggerInteraction.Ignore);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                if(hit.collider.GetComponentInParent<SectorWorld>()==null) continue;
                string name=hit.collider.name;
                if(name=="water") return false;
                if(name!="Local terrain" && name!="Sloped road" && name!="park" && name!="building" && name!="Mapped pitched roof") continue;
                if(hit.normal.y<.65f) return false;
                surface=hit.point+Vector3.up*.12f;return true;
            }
            return false;
        }
        void PinMapPoint(StreetMapViewport view,Vector2 mouse)
        {
            float nearest=18*18;Vector3 destination=default;string name=null;bool exact=false;
            void Candidate(Vector3 point,string label,bool preserveHeight)
            {
                float distance=(view.Project(point)-mouse).sqrMagnitude;
                if(distance>nearest) return;
                nearest=distance;destination=point;name=label;exact=preserveHeight;
            }
            foreach(var place in mapPlaces) Candidate(place.Position,place.Name,false);
            if(FirstContactMode && firstContact!=null)
            {
                for(int i=0;i<contractVantages.Count;i++) Candidate(contractVantages[i],"Position "+(char)('A'+i),true);
                if(contractTarget!=null && !firstContact.TargetDown) Candidate(contractTarget.transform.position,"Target position",true);
                Candidate(extractionPoint,"Extraction",true);
            }
            if(name==null) SetSurfaceWaypoint(view.Unproject(mouse),"Dropped pin");
            else if(exact) SetWaypoint(destination,name);
            else SetSurfaceWaypoint(destination,name);
        }
        string PinGuidance()
        {
            if(player==null || !hasWaypointPin) return "";
            if(waypointPin.Arrived(player.transform.position)) return "ARRIVED";
            float up=waypointPin.HeightDifference(player.transform.position);
            string level=Mathf.Abs(up)>2?"  |  "+Mathf.RoundToInt(Mathf.Abs(up))+" m "+(up>0?"ABOVE":"BELOW"):"";
            return Mathf.RoundToInt(waypointPin.Distance(player.transform.position))+" m"+level;
        }
        void DrawWaypointIcon(Vector2 tip,Color color,float height=30)
        {
            if(waypointIcon==null)
            {
                waypointIcon=new Texture2D(24,32,TextureFormat.RGBA32,false){name="Waypoint pin",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                var pixels=new Color32[24*32];
                for(int y=0;y<32;y++) for(int x=0;x<24;x++)
                {
                    float dx=x-11.5f,dy=y-21;
                    bool head=dx*dx+dy*dy<=100;
                    bool stem=y<=21 && Mathf.Abs(dx)<=y*.55f;
                    if(head || stem) pixels[y*24+x]=dx*dx+dy*dy<13?new Color32(35,60,90,255):new Color32(255,255,255,255);
                }
                waypointIcon.SetPixels32(pixels);waypointIcon.Apply(false,true);
            }
            GUI.color=color;GUI.DrawTexture(new Rect(tip.x-height*.375f,tip.y-height,height*.75f,height),waypointIcon);GUI.color=Color.white;
        }
        void DrawPinWorldGuidance(float width,float height)
        {
            if(!hasWaypointPin || cameraView==null || mapOpen) return;
            Vector3 screen=cameraView.WorldToViewportPoint(waypointPinPos+Vector3.up*1.5f);
            Vector2 direction=new Vector2(screen.x-.5f,.5f-screen.y);
            if(screen.z<0) direction=-direction;
            if(direction.sqrMagnitude<.0001f) direction=Vector2.right;
            Vector2 position=new Vector2(screen.x*width,(1-screen.y)*height);
            bool outside=screen.z<=0 || screen.x<.08f || screen.x>.92f || screen.y<.15f || screen.y>.85f;
            if(outside)
            {
                direction.Normalize();
                float reach=Mathf.Min((width*.5f-110)/Mathf.Max(Mathf.Abs(direction.x),.001f),(height*.5f-80)/Mathf.Max(Mathf.Abs(direction.y),.001f));
                position=new Vector2(width*.5f,height*.5f)+direction*Mathf.Max(0,reach);
            }
            // Keep the marker visible while reserving the top objective strip, the
            // left mission buttons, the right fire/scope controls and the bottom
            // speed/health HUD used on touch screens.
            float leftSafe=220f, rightSafe=width-275f, topSafe=112f, bottomSafe=height-155f;
            if(rightSafe<=leftSafe) { leftSafe=105f; rightSafe=width-105f; }
            if(bottomSafe<=topSafe) { topSafe=80f; bottomSafe=height-80f; }
            position.x=Mathf.Clamp(position.x,leftSafe,rightSafe);position.y=Mathf.Clamp(position.y,topSafe,bottomSafe);
            var color=waypointPin.Arrived(player.transform.position)?new Color(.2f,.8f,.4f):new Color(1f,.78f,.16f);
            DrawWaypointIcon(position,color,30);
            // Guidance is a compact, fixed banner so it never lands on a virtual
            // button or the bottom status bars when the pin is off-screen.
            float bannerWidth=Mathf.Min(260f,Mathf.Max(190f,width-40f));
            var rect=new Rect(Mathf.Clamp(width*.5f-bannerWidth*.5f,12f,width-bannerWidth-12f),topSafe-2f,bannerWidth,32f);
            MapFill(rect,new Color(.04f,.07f,.10f,.92f));
            var text=MapText(12,Color.white,true);text.alignment=TextAnchor.MiddleCenter;
            GUI.Label(rect,waypointPin.Name+"  |  "+(outside?"TURN TOWARD PIN  |  ":"")+PinGuidance(),text);
        }
    }
}
