using System;
using System.Collections.Generic;
using UnityEngine;
using GeoSniper.Duel;

namespace GeoSniper
{
    public sealed partial class UrbanCombatMission
    {
        Vector3 mapBrowseCenter;
        Vector2 mapPointerStart, mapScroll;
        Vector3 mapDragOrigin;
        bool mapDragging, mapPointerMoved, mapSearchFocused;
        string mapSearch="";
        Texture2D streetMinimap;
        Vector3 minimapOrigin;
        int minimapSignature;
        float minimapRefresh;
        readonly List<MapPlace> mapPlaces=new List<MapPlace>();
        readonly List<MapPlace> matchingPlaces=new List<MapPlace>();
        readonly List<Rect> mapLabelAreas=new List<Rect>();
        int placeSignature=int.MinValue;
        sealed class MapPlace { public string Name, Kind, Category; public Vector3 Position; public Color Color; }
        static Vector2 MapLabelPoint(MapFeature feature)
        {
            if(feature.Kind!="road" || feature.Points.Count<2) return SectorSignage.Center(feature.Points);
            float length=0;
            for(int i=1;i<feature.Points.Count;i++) length+=Vector2.Distance(feature.Points[i-1],feature.Points[i]);
            float middle=length*.5f;
            for(int i=1;i<feature.Points.Count;i++)
            {
                float segment=Vector2.Distance(feature.Points[i-1],feature.Points[i]);
                if(middle<=segment) return Vector2.Lerp(feature.Points[i-1],feature.Points[i],segment>.001f?middle/segment:0);
                middle-=segment;
            }
            return feature.Points[feature.Points.Count-1];
        }
        static GUIStyle MapText(int size,Color color,bool bold=false)
        {
            var style=new GUIStyle(GUI.skin.label){fontSize=size,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,wordWrap=false};
            // The inherited hover colour was white, hiding pin labels on the pale map.
            foreach(var state in new[]{style.normal,style.hover,style.active,style.focused,style.onNormal,style.onHover,style.onActive,style.onFocused})state.textColor=color;
            return style;
        }
        static void MapFill(Rect rect,Color color) { GUI.color=color; GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=Color.white; }
        int MapSignature()
        {
            int result=17;
            foreach(var world in SectorWorld.LoadedWorlds)
                if(world!=null) result=unchecked(result*31+world.GetHashCode()+world.Features.Count);
            return result;
        }
        void RefreshMapPlaces(int signature)
        {
            if(placeSignature==signature) return;
            placeSignature=signature; mapPlaces.Clear();
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var world in SectorWorld.LoadedWorlds)
            {
                if(world==null) continue;
                foreach(var feature in world.Features)
                {
                    if(feature.Points==null || feature.Points.Count==0 || string.IsNullOrWhiteSpace(feature.Name)) continue;
                    var p=MapLabelPoint(feature);
                    var position=world.transform.TransformPoint(new Vector3(p.x,0,p.y));
                    float spacing=feature.Kind=="road"?90f:8f;
                    string key=feature.Kind+":"+feature.Name.Trim()+":"+Mathf.RoundToInt(position.x/spacing)+":"+Mathf.RoundToInt(position.z/spacing);
                    if(!seen.Add(key)) continue;
                    string category=MapFeatureStyle.Category(feature);
                    mapPlaces.Add(new MapPlace {Name=feature.Name.Trim(),Kind=feature.Kind,Category=category,
                        Color=MapFeatureStyle.PlaceColor(feature),Position=position});
                }
            }
            // Keep landmarks legible before trying to place road labels.
            mapPlaces.Sort((a,b)=>{
                int priority=(a.Kind=="road").CompareTo(b.Kind=="road");
                return priority!=0?priority:StringComparer.OrdinalIgnoreCase.Compare(a.Name,b.Name);
            });
        }
        void DrawStreetMinimap(Rect rect,Vector3 center)
        {
            int signature=MapSignature();
            if(streetMinimap==null || signature!=minimapSignature || (minimapOrigin-center).sqrMagnitude>3600 ||
                (Time.unscaledTime>minimapRefresh && (minimapOrigin-center).sqrMagnitude>400))
            {
                if(streetMinimap!=null) Destroy(streetMinimap);
                // A padded raster lets the view follow the player between refreshes.
                minimapOrigin=center; minimapSignature=signature; minimapRefresh=Time.unscaledTime+2;
                streetMinimap=GameplayMapRaster.Render(center,320);
            }
            float span=170f*rect.width/(rect.width-24);
            Vector3 delta=center-minimapOrigin;
            GUI.color=Color.white;
            GUI.DrawTextureWithTexCoords(rect,streetMinimap,new Rect(.5f+delta.x/320-span/640,
                .5f+delta.z/320-span/640,span/320,span/320));
        }
        bool TryGetDuelMapRival(out DuelOpponentAvatar rival)
        {
            rival=SniperDuelManager.ActiveOpponent;
            var duel=SniperDuelManager.ActiveInstance;
            return MultiplayerDuel && duel!=null && duel.MatchState==DuelMatchState.InRound && duel.RivalTrackingVisible &&
                player!=null && player.Health!=null && !player.Health.IsDead && rival!=null && !rival.IsDead && rival.gameObject.activeInHierarchy;
        }
        static string DuelMapHeight(float metres) => metres>3f ? $"ABOVE {metres:F0}m" : metres< -3f ? $"BELOW {-metres:F0}m" : "SAME LEVEL";
        void FocusDuelMap(Vector3 rivalPosition)
        {
            Vector3 position=player.transform.position;
            mapBrowseCenter=(position+rivalPosition)*.5f;
            Vector3 delta=rivalPosition-position;
            mapSpan=Mathf.Clamp(Mathf.Max(Mathf.Abs(delta.x),Mathf.Abs(delta.z))*1.35f+70f,120f,2400f);
        }
        // Preserve the bearing even when the rival is outside the visible map.
        static Vector2 ClampDuelMapPoint(Rect rect,Vector2 point,float inset)
        {
            Vector2 center=rect.center,offset=point-center;
            float halfWidth=Mathf.Max(1f,rect.width*.5f-inset),halfHeight=Mathf.Max(1f,rect.height*.5f-inset);
            float overflow=Mathf.Max(Mathf.Abs(offset.x)/halfWidth,Mathf.Abs(offset.y)/halfHeight);
            return overflow>1f ? center+offset/overflow : point;
        }
        void DrawDuelArenaRing(Rect rect, Vector2 center, float radius)
        {
            var duel = SniperDuelManager.ActiveInstance;
            if (!MultiplayerDuel || duel == null || duel.MatchState != DuelMatchState.InRound) return;
            GUI.BeginGroup(rect);
            center -= rect.position;
            GUI.color = new Color(1f, .65f, .2f, .6f);
            Vector2 previous = center + Vector2.right * radius;
            for (int i = 1; i <= 48; i++)
            {
                float angle = i * Mathf.PI * 2f / 48;
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                DrawMapLine(previous, point, 2f); previous = point;
            }
            GUI.EndGroup(); GUI.color = Color.white;
        }
        void DrawDuelMapMarker(Rect rect,Vector2 projected,Vector2 observer,Vector3 delta,bool compact)
        {
            Vector2 point=ClampDuelMapPoint(rect,projected,compact?14f:20f);
            bool edge=(point-projected).sqrMagnitude>.1f;
            Color red=new Color(1f,.22f,.18f);
            GUI.color=new Color(red.r,red.g,red.b,.6f);
            DrawMapLine(observer,point,compact?1.2f:2f);
            float radius=compact?8f:12f;
            float pulse=1f+Mathf.PingPong(Time.unscaledTime*1.6f,1f)*.25f;
            var top=point+Vector2.up*radius*pulse;
            var right=point+Vector2.right*radius*pulse;
            var bottom=point+Vector2.down*radius*pulse;
            var left=point+Vector2.left*radius*pulse;
            GUI.color=new Color(.04f,.015f,.02f,.95f);
            DrawMapLine(top,right,compact?5f:7f);DrawMapLine(right,bottom,compact?5f:7f);
            DrawMapLine(bottom,left,compact?5f:7f);DrawMapLine(left,top,compact?5f:7f);
            GUI.color=red;
            DrawMapLine(top,right,compact?3f:4f);DrawMapLine(right,bottom,compact?3f:4f);
            DrawMapLine(bottom,left,compact?3f:4f);DrawMapLine(left,top,compact?3f:4f);
            MapFill(new Rect(point.x-3f,point.y-3f,6f,6f),Color.white);
            if(edge)
            {
                Vector2 direction=(projected-point).normalized,rightOfDirection=new Vector2(-direction.y,direction.x);
                Vector2 tip=point+direction*(radius+5f);
                GUI.color=red;
                DrawMapLine(tip,tip-direction*6f+rightOfDirection*4f,3f);
                DrawMapLine(tip,tip-direction*6f-rightOfDirection*4f,3f);
            }
            float labelWidth=Mathf.Min(compact?62f:190f,rect.width-6f),labelHeight=compact?18f:40f;
            float labelX=Mathf.Clamp(point.x-labelWidth*.5f,rect.x+3f,Mathf.Max(rect.x+3f,rect.xMax-labelWidth-3f));
            float labelY=point.y+radius+5f;
            if(labelY+labelHeight>rect.yMax-3f) labelY=point.y-radius-labelHeight-5f;
            var label=new Rect(labelX,Mathf.Max(rect.y+3f,labelY),labelWidth,labelHeight);
            MapFill(label,new Color(.1f,.015f,.025f,.96f));
            GUI.Label(label,compact ? "RIVAL" : $"RIVAL {delta.magnitude:F0}m\n{DuelMapHeight(delta.y)}"+(edge?" / OFF MAP":""),
                GUIStyleCache.Get(compact?9:13,FontStyle.Bold,TextAnchor.MiddleCenter,Color.white));
            GUI.color=Color.white;
        }
        void DrawTacticalMap(float width,float height)
        {
            if(player==null) return;
            var ink=new Color(.83f,.94f,.93f);
            var muted=new Color(.55f,.71f,.72f);
            var blue=new Color(.12f,.90f,.68f);
            MapFill(new Rect(0,0,width,height),new Color(.025f,.055f,.065f));
            var rect=TacticalRect();
            int signature=MapSignature();RefreshMapPlaces(signature);
            var viewport=new StreetMapViewport(rect,mapBrowseCenter,mapSpan);
            var e=Event.current;
            // The toolbar and place list sit outside this hit area.
            if(Input.touchCount>1) {mapDragging=false;mapPointerMoved=true;}
            if(Input.touchCount<2 && e.type==EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                if(e.button==0) { mapDragging=true;mapPointerMoved=false;mapPointerStart=e.mousePosition;mapDragOrigin=mapBrowseCenter;e.Use(); }
                else if(e.button==1) {hasWaypointPin=false;e.Use();}
            }
            if(mapDragging && e.type==EventType.MouseDrag)
            {
                var d=e.mousePosition-mapPointerStart;
                mapPointerMoved |= d.sqrMagnitude>25;
                if(mapPointerMoved) mapBrowseCenter=mapDragOrigin+new Vector3(-d.x/viewport.Scale,0,d.y/viewport.Scale);
                e.Use();
            }
            if(mapDragging && e.type==EventType.MouseUp)
            {
                if(!mapPointerMoved && (e.mousePosition-mapPointerStart).sqrMagnitude<=25 && rect.Contains(e.mousePosition)) PinMapPoint(viewport,e.mousePosition);
                mapDragging=false;e.Use();
            }
            viewport=new StreetMapViewport(rect,mapBrowseCenter,mapSpan);
            // Refresh only on repaint; GUI layout/input events share the cached texture.
            if(Event.current.type==EventType.Repaint && (gameplayOverview==null || overviewSectors!=signature ||
                !Mathf.Approximately(overviewSpan,viewport.RasterSpan) || (overviewOrigin-mapBrowseCenter).sqrMagnitude>1))
            {
                if(gameplayOverview!=null) Destroy(gameplayOverview);
                gameplayOverview=GameplayMapRaster.Render(mapBrowseCenter,viewport.RasterSpan);
                overviewOrigin=mapBrowseCenter;overviewSpan=viewport.RasterSpan;overviewSectors=signature;
            }
            if(gameplayOverview!=null)
            {
                // Compensate for sub-metre movement below the regeneration threshold.
                var uv=viewport.TextureCoordinates;
                uv.x+=(mapBrowseCenter.x-overviewOrigin.x)/overviewSpan;
                uv.y+=(mapBrowseCenter.z-overviewOrigin.z)/overviewSpan;
                GUI.DrawTextureWithTexCoords(rect,gameplayOverview,uv);
            }
            GUI.BeginGroup(rect);
            var local=new StreetMapViewport(new Rect(0,0,rect.width,rect.height),mapBrowseCenter,mapSpan);
            mapLabelAreas.Clear();
            var text=MapText(14,ink);
            foreach(var place in mapPlaces)
            {
                var p=local.Project(place.Position);
                if(!local.Rect.Contains(p)) continue;
                bool isRoad=place.Kind=="road";
                if(isRoad && local.Scale<.9f) continue;
                Color color=place.Kind=="place"?new Color(.78f,.68f,.89f):place.Kind=="park"?new Color(.50f,.78f,.54f):isRoad?new Color(.71f,.81f,.81f):
                    place.Category.Length>0?place.Color:muted;
                if(!isRoad)
                {
                    MapFill(new Rect(p.x-4,p.y-4,8,8),Color.white);
                    MapFill(new Rect(p.x-2.5f,p.y-2.5f,5,5),color);
                }
                Vector2 measure=text.CalcSize(new GUIContent(place.Name));
                float labelWidth=Mathf.Min(measure.x+8,240);
                for(int slot=0;slot<4;slot++)
                {
                    float dx=slot<2?8:-labelWidth-8,dy=slot%2==0?-10:10;
                    var area=new Rect(p.x+dx,p.y+dy,labelWidth,22);
                    if(area.x<4 || area.y<4 || area.xMax>rect.width-4 || area.yMax>rect.height-4 || mapLabelAreas.Exists(a=>a.Overlaps(area))) continue;
                    mapLabelAreas.Add(new Rect(area.x-3,area.y-2,area.width+6,area.height+4));
                    MapFill(area,new Color(.02f,.045f,.06f,isRoad?.78f:.94f));text.normal.textColor=color;
                    GUI.Label(area,new GUIContent(place.Name,place.Name),text);break;
                }
            }
            Vector2 operatorPos=local.Project(player.transform.position);
            if(stageMode && StoryFieldMode)foreach(var objective in storyPoints)
            {
                if(objective.Done)continue;
                var destination=ClampDuelMapPoint(new Rect(0,0,rect.width,rect.height),local.Project(objective.Position),20);
                DrawWaypointPath(operatorPos,destination,blue);DrawWaypointIcon(destination,blue);
                GUI.Label(new Rect(destination.x+12,destination.y-25,230,44),(activeContract.Type==CampaignContractType.Sabotage?"RELAY // INSIDE":"DOCUMENTS // INSIDE")+"\n"+Mathf.RoundToInt(Vector3.Distance(player.transform.position,objective.Position))+"m",MapText(12,blue,true));
            }

            if(hasWaypointPin && (!TryGetExtractionDestination(out var sameExit) || (waypointPinPos-sameExit).sqrMagnitude>1))
            {
                var pin=local.Project(waypointPinPos);
                DrawWaypointPath(operatorPos,pin,blue);
                DrawWaypointIcon(pin,blue);
                float labelOffset=TryGetExtractionDestination(out var labelExit) && (local.Project(labelExit)-pin).sqrMagnitude<160*160?-82:-30;
                GUI.Label(new Rect(Mathf.Clamp(pin.x+12,4,Mathf.Max(4,rect.width-244)),Mathf.Clamp(pin.y+labelOffset,4,Mathf.Max(4,rect.height-48)),240,44),waypointPin.Name+"\n"+PinGuidance(),MapText(13,blue,true));
            }
            foreach(var enemy in enemies)
            {
                if(enemy==null || !enemy.gameObject.activeSelf) continue;
                var p=local.Project(enemy.transform.position);
                MapFill(new Rect(p.x-4,p.y-4,8,8),new Color(.86f,.20f,.16f));
            }
            if(TryGetExtractionDestination(out var mapExit))
            {
                var p=ClampDuelMapPoint(new Rect(0,0,rect.width,rect.height),local.Project(mapExit),20);
                var exitColor=new Color(.12f,.7f,.35f);
                DrawWaypointPath(operatorPos,p,exitColor);DrawWaypointIcon(p,exitColor);
                GUI.Label(new Rect(Mathf.Clamp(p.x+9,4,Mathf.Max(4,rect.width-184)),Mathf.Clamp(p.y+8,4,Mathf.Max(4,rect.height-48)),180,44),"EXTRACTION\n"+Mathf.RoundToInt(Vector3.Distance(player.transform.position,mapExit))+" m",MapText(13,exitColor,true));
            }
            DrawFirstContactMap(local);
            var arena=SniperDuelManager.ActiveInstance;
            if(MultiplayerDuel && arena!=null) DrawDuelArenaRing(local.Rect,local.Project(arena.ArenaCenter),arena.ArenaRadius*local.Scale);
            bool hasDuelRival=TryGetDuelMapRival(out var mapRival);
            if(hasDuelRival) DrawDuelMapMarker(local.Rect,local.Project(mapRival.transform.position),operatorPos,mapRival.transform.position-player.transform.position,false);
            DrawModernPlayerMarker(operatorPos,18);
            GUI.EndGroup();

            // A stable place directory makes every loaded name accessible without overlapping text.
            float panel=rect.x-20;
            int mappedBuildings=0;
            bool hasOverture=false,hasOsm=false,hasSaved=false,hasOffline=false;
            foreach(var loadedWorld in SectorWorld.LoadedWorlds)
            {
                if(loadedWorld==null) continue;
                mappedBuildings+=loadedWorld.Buildings.Count;
                if(!string.IsNullOrWhiteSpace(loadedWorld.SourceLabel))
                {
                    string label=loadedWorld.SourceLabel;
                    hasOverture|=label.StartsWith("OVERTURE",StringComparison.OrdinalIgnoreCase);
                    hasOffline|=label.StartsWith("OFFLINE",StringComparison.OrdinalIgnoreCase);
                    hasSaved|=label.StartsWith("CACHED",StringComparison.OrdinalIgnoreCase);
                    hasOsm|=label.StartsWith("OSM",StringComparison.OrdinalIgnoreCase) || label.StartsWith("LIVE OSM",StringComparison.OrdinalIgnoreCase);
                }
            }
            string mapProvider=hasOverture?(hasOsm?"OVERTURE + OSM":"OVERTURE"):
                hasOsm?"OSM":hasSaved?"SAVED MAP":hasOffline?"OFFLINE PRACTICE":"MAP DATA";
            GUI.Label(new Rect(16,12,panel,26),"🌐 REAL-WORLD SECTOR",MapText(18,ink,true));
            GUI.Label(new Rect(16,38,panel,20),mapProvider+" / "+mappedBuildings+" buildings",MapText(12,new Color(.10f,.50f,.83f),true));
            GUI.Label(new Rect(16,61,panel,24),"Search real places & streets",MapText(12,muted));
            GUI.SetNextControlName("MapPlaceSearch");
            string search=GUI.TextField(new Rect(16,89,panel-8,32),mapSearch,new GUIStyle(GUI.skin.textField){fontSize=15});
            mapSearchFocused=GUI.GetNameOfFocusedControl()=="MapPlaceSearch";
            if(search!=mapSearch) {mapSearch=search;mapScroll=Vector2.zero;}
            matchingPlaces.Clear();
            foreach(var place in mapPlaces) if(string.IsNullOrWhiteSpace(mapSearch) || place.Name.IndexOf(mapSearch,StringComparison.OrdinalIgnoreCase)>=0) matchingPlaces.Add(place);
            float directoryTop=160f;
            if(hasDuelRival)
            {
                Vector3 rivalDelta=mapRival.transform.position-player.transform.position;
                var findRect=new Rect(16,128,panel-8,50);
                if(TacticalGUI.DrawButton(findRect,$"FIND RIVAL / {rivalDelta.magnitude:F0}m\n{DuelMapHeight(rivalDelta.y)}",true,12)) FocusDuelMap(mapRival.transform.position);
                GUI.Label(new Rect(16,184,panel,24),matchingPlaces.Count+" locations in loaded area",MapText(13,muted));
                directoryTop=216f;
            }
            else GUI.Label(new Rect(16,128,panel,24),matchingPlaces.Count+" locations in loaded area",MapText(13,muted));
            var listRect=new Rect(12,directoryTop,panel,Mathf.Max(30,height-directoryTop-44));
            mapScroll=GUI.BeginScrollView(listRect,mapScroll,new Rect(0,0,panel-20,matchingPlaces.Count*62));
            var rowStyle=new GUIStyle(GameButtonChrome.Style(GameButtonTone.Steel,14)){alignment=TextAnchor.MiddleLeft};
            // Only paint visible rows; the full directory remains scrollable and searchable.
            int start=Mathf.Max(0,Mathf.FloorToInt(mapScroll.y/62));
            int end=Mathf.Min(matchingPlaces.Count,Mathf.CeilToInt((mapScroll.y+listRect.height)/62));
            for(int i=start;i<end;i++)
            {
                var place=matchingPlaces[i];
                if(GUI.Button(new Rect(0,i*62,panel-24,56),place.Name+"\n"+
                    (place.Category.Length>0?place.Category:place.Kind.ToUpperInvariant()),rowStyle))
                {
                    mapBrowseCenter=place.Position;mapSpan=Mathf.Min(mapSpan,180);
                    SetSurfaceWaypoint(place.Position,place.Name);
                }
            }
            GUI.EndScrollView();
            if(matchingPlaces.Count==0) GUI.Label(new Rect(16,directoryTop+10,panel-12,64),mapPlaces.Count==0?"Place data is still loading.":"No matching locations.",new GUIStyle(MapText(14,muted)){wordWrap=true});
            var button=GameButtonChrome.Style(GameButtonTone.Steel,14);
            float x=rect.x;
            Rect zoomInRect = new Rect(x,10,44,44);
            if(TacticalGUI.IsClicked(zoomInRect) || GUI.Button(zoomInRect,"+",button)) ZoomMap(1/1.4f);
            Rect zoomOutRect = new Rect(x+50,10,44,44);
            if(TacticalGUI.IsClicked(zoomOutRect) || GUI.Button(zoomOutRect,"-",button)) ZoomMap(1.4f);
            Rect myLocRect = new Rect(x+102,10,110,44);
            if(TacticalGUI.IsClicked(myLocRect) || GUI.Button(myLocRect,"My location",button)) mapBrowseCenter=player.transform.position;
            if(TryGetExtractionDestination(out _))
            {
                var exitRect=new Rect(x+218,10,112,44);
                if(TacticalGUI.DrawButton(exitRect,"EXTRACTION",true,12))RestoreExtractionGuidance();
            }
            Rect clearPinRect = new Rect(x+220,10,100,44);
            if(hasWaypointPin && !TryGetExtractionDestination(out _) && (TacticalGUI.IsClicked(clearPinRect) || GUI.Button(clearPinRect,"Clear pin",button))) hasWaypointPin=false;
            float btnW = 200f, btnH = 46f;
            float topResumeX = Mathf.Max(x + 330, width - btnW - 20f);
            Rect closeMapRect = new Rect(topResumeX, 8, btnW, btnH);
            if (TacticalGUI.DrawButton(closeMapRect, "◀ RESUME GAME", true, 13) || GUI.Button(closeMapRect, "", button))
            {
                CloseMap();
                return;
            }

            // Bottom-right thumb zone RESUME button (ultra-accessible for mobile thumbs)
            Rect btmResumeRect = new Rect(width - 215, height - 60, 200, 48);
            if (TacticalGUI.DrawButton(btmResumeRect, "◀ RESUME GAME", true, 13) || GUI.Button(btmResumeRect, "", button))
            {
                CloseMap();
                return;
            }

            GUI.Label(new Rect(rect.x+12,rect.y+12,28,24),"N",MapText(16,ink,true));
            float metres=mapSpan>=500?100:mapSpan>=150?50:10;
            float length=metres*viewport.Scale;
            MapFill(new Rect(rect.xMax-length-18,rect.yMax-18,length,3),ink);
            GUI.Label(new Rect(rect.xMax-length-18,rect.yMax-43,length+5,24),metres+" m",MapText(13,ink));
            GUI.Label(new Rect(rect.x,height-30,Mathf.Max(100, rect.width - 225),24),"🌐 REAL-WORLD GPS CARTOGRAPHY | Drag / Pinch to Pan & Zoom | Tap to set Tactical Waypoint",MapText(12,muted));
            Rect airstrikeRect = new Rect(16,height-48,panel-12,40);
            if(hasWaypointPin && (TacticalGUI.IsClicked(airstrikeRect) || GUI.Button(airstrikeRect,"Air strike at waypoint",button))) CallAirStrike();
            if(Time.unscaledTime<mapPinNoticeUntil) GUI.Label(new Rect(rect.x+12,rect.y+44,rect.width-24,32),mapPinNotice,MapText(14,new Color(.7f,.2f,.1f),true));
            GUI.color=Color.white;
        }
    }
}

