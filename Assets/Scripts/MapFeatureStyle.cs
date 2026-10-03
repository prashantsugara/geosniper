using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GeoSniper
{
    public static class MapFeatureStyle
    {
        public const float MaxBuildingHeight=220f;
        public static string Text(MapFeature f,string key)=>((string)f.Details?[key]??"").Trim().ToLowerInvariant();
        public static float Number(MapFeature f,string key,float fallback)
        {
            return float.TryParse((string)f.Details?[key],NumberStyles.Float,CultureInfo.InvariantCulture,out var n) && !float.IsNaN(n) && !float.IsInfinity(n)?n:fallback;
        }
        public static float StoreyHeight(MapFeature f)
        {
            float floors=Number(f,"num_floors",0);
            return Mathf.Clamp(floors>=1?f.Height/Mathf.Clamp(floors,1,80):4f,2.8f,4.5f);
        }
        public static Vector2 WallUv(MapFeature f,Vector3 vertex,Vector3 normal)
        {
            Vector3 along=Vector3.Cross(Vector3.up,normal).normalized;
            return new Vector2(Vector3.Dot(vertex,along)/8f,vertex.y/(2f*StoreyHeight(f)));
        }
        public static bool Path(MapFeature f)
        {
            string c=Text(f,"class");
            return c=="footway" || c=="pedestrian" || c=="path" || c=="cycleway" || c=="steps" || c=="bridleway";
        }
        public static bool PavedRoad(MapFeature f)
        {
            if(f.Kind!="road" || Path(f) || Text(f,"class")=="track")return false;
            string surface=Text(f,"surface");
            return surface!="dirt" && surface!="gravel" && surface!="unpaved" && surface!="ground" && surface!="sand" && surface!="mud";
        }
        public enum RoadType
        {
            Asphalt,
            Concrete,
            Cobblestone,
            Gravel,
            MudDirt
        }
        public static RoadType GetRoadType(MapFeature f)
        {
            if (f == null || f.Kind != "road") return RoadType.Asphalt;
            string surface = Text(f, "surface");
            string roadClass = Text(f, "class");
            string trackType = Text(f, "tracktype");

            if (surface == "mud" || surface == "dirt" || surface == "earth" || surface == "ground" || surface == "clay" || surface == "sand" || surface == "unpaved")
                return RoadType.MudDirt;
            if (surface == "gravel" || surface == "fine_gravel" || surface == "pebblestone" || surface == "compacted" || surface == "chipseal")
                return RoadType.Gravel;
            if (surface == "cobblestone" || surface == "sett" || surface == "paving_stones" || surface == "unhewn_cobblestone" || surface == "stone")
                return RoadType.Cobblestone;
            if (surface == "concrete" || surface == "concrete:plates" || surface == "concrete:lanes")
                return RoadType.Concrete;
            if (surface == "asphalt" || surface == "paved" || surface == "bitumen" || surface == "tarmac")
                return RoadType.Asphalt;

            if (roadClass == "track")
            {
                if (trackType == "grade1") return RoadType.Gravel;
                return RoadType.MudDirt;
            }
            if (roadClass == "bridleway" || roadClass == "path")
                return RoadType.MudDirt;
            if (roadClass == "pedestrian" || roadClass == "footway")
                return RoadType.Cobblestone;
            if (roadClass == "unclassified" || roadClass == "service")
            {
                string name = (f.Name ?? "").ToLowerInvariant();
                if (name.Contains("mud") || name.Contains("dirt") || name.Contains("trail") || name.Contains("village") || name.Contains("farm"))
                    return RoadType.MudDirt;
                if (name.Contains("gravel") || name.Contains("lane") || name.Contains("track"))
                    return RoadType.Gravel;
            }
            return RoadType.Asphalt;
        }
        // Direction follows the source geometry; -1 means travel against its point order.
        public static int TrafficDirection(MapFeature f)
        {
            string value=Text(f,"oneway").ToLowerInvariant();
            if(value=="-1" || value=="reverse")return -1;
            if(value=="yes" || value=="true" || value=="1")return 1;
            if(value=="no" || value=="false" || value=="0")return 0;
            return Text(f,"junction")=="roundabout"?1:0;
        }
        public static float TrafficLaneOffset(MapFeature f)
        {
            if(TrafficDirection(f)!=0 || RoadWidth(f)<5f)return 0;
            float offset=Mathf.Clamp(RoadWidth(f)*.25f,1.1f,3f);
            return Text(f,"driving_side")=="left"?-offset:offset;
        }
        public static float RoadWidth(MapFeature f)
        {
            float width=Number(f,"width",0);
            if(width>0) return Mathf.Clamp(width,1,30);
            switch(Text(f,"class"))
            {
                case "motorway": return 16;
                case "trunk": return 14;
                case "primary": return 12;
                case "secondary": return 10;
                case "tertiary": return 8;
                case "residential": case "unclassified": return 6;
                case "service": case "living_street": return 4;
                case "track": return 3;
                default: return Path(f)?2:6;
            }
        }
        public static Color RoadColor(MapFeature f)
        {
            string surface=Text(f,"surface");
            if(surface=="gravel" || surface=="dirt" || surface=="unpaved" || Text(f,"class")=="track") return new Color(.39f,.31f,.22f);
            if(Path(f)) return new Color(.54f,.50f,.40f);
            if(surface=="concrete" || surface=="paving_stones") return new Color(.39f,.41f,.40f);
            return new Color(.045f,.052f,.063f);
        }
        public static string Category(MapFeature f)
        {
            string c=Text(f,"category");
            if(f.Landmark=="fuel" || c.Contains("gas_station") || c.Contains("petrol")) return "PETROL STATION";
            if(f.Landmark=="police" || c.Contains("police")) return "POLICE STATION";
            if(c.Contains("fitness") || c.Contains("gym")) return "GYM";
            if(c.Contains("restaurant")) return "RESTAURANT";
            if(c.Contains("cafe") || c.Contains("coffee")) return "CAFE";
            if(c.Contains("pharmacy")) return "PHARMACY";
            if(c.Contains("hospital") || c.Contains("clinic")) return "HEALTHCARE";
            if(c.Contains("school") || c.Contains("college")) return "EDUCATION";
            if(c.Contains("grocery") || c.Contains("supermarket") || c.Contains("convenience")) return "MARKET";
            return c.Length>0?c.Replace('_',' ').ToUpperInvariant():f.Landmark=="shop"?"SHOP":"";
        }
        public static Color PlaceColor(MapFeature f)
        {
            switch(Category(f))
            {
                case "PETROL STATION": return new Color(.69f,.39f,.06f);
                case "POLICE STATION": return new Color(.08f,.22f,.47f);
                case "GYM": return new Color(.40f,.14f,.08f);
                case "PHARMACY": case "HEALTHCARE": return new Color(.06f,.35f,.23f);
                case "CAFE": case "RESTAURANT": return new Color(.36f,.23f,.12f);
                default: return new Color(.07f,.16f,.20f);
            }
        }
        public static Color BuildingColor(MapFeature f,Color fallback)
        {
            string mapped=(string)f.Details?["facade_color"];
            if(!string.IsNullOrEmpty(mapped) && ColorUtility.TryParseHtmlString(mapped,out var color)) return color;
            switch(Text(f,"class"))
            {
                case "industrial": case "warehouse": fallback=new Color(.58f,.61f,.62f); break;
                case "commercial": case "retail": fallback=new Color(.75f,.69f,.57f); break;
                case "school": case "education": fallback=new Color(.74f,.64f,.50f); break;
            }
            string style=ResolveFacadeMaterial(f);
            switch(style)
            {
                case "brick":
                {
                    uint h=f.Points!=null && f.Points.Count>0?(uint)Mathf.Abs(f.Points[0].x*13+f.Points[0].y*29)%3:0;
                    if(h==0) return new Color(.66f,.35f,.25f);
                    if(h==1) return new Color(.54f,.32f,.26f);
                    return new Color(.48f,.40f,.35f);
                }
                case "stone":
                {
                    uint h=f.Points!=null && f.Points.Count>0?(uint)Mathf.Abs(f.Points[0].x*17+f.Points[0].y*31)%3:0;
                    if(h==0) return new Color(.82f,.80f,.74f);
                    if(h==1) return new Color(.76f,.71f,.62f);
                    return new Color(.70f,.69f,.67f);
                }
                case "glass":
                {
                    uint h=f.Points!=null && f.Points.Count>0?(uint)Mathf.Abs(f.Points[0].x*19+f.Points[0].y*37)%3:0;
                    if(h==0) return new Color(.18f,.24f,.32f);
                    if(h==1) return new Color(.22f,.25f,.28f);
                    return new Color(.16f,.24f,.22f);
                }
                case "metal":
                {
                    uint h=f.Points!=null && f.Points.Count>0?(uint)Mathf.Abs(f.Points[0].x*23+f.Points[0].y*41)%3:0;
                    if(h==0) return new Color(.38f,.40f,.44f);
                    if(h==1) return new Color(.68f,.65f,.60f);
                    return new Color(.84f,.84f,.83f);
                }
                case "wood": return new Color(.48f,.37f,.26f);
                default: return fallback;
            }
        }
        public static string ResolveFacadeMaterial(MapFeature feature)
        {
            string style=Text(feature,"facade_material");
            if(!string.IsNullOrEmpty(style)) return style;
            string bldClass=Text(feature,"class");
            if(bldClass=="commercial" || bldClass=="retail")
                return feature.Height>=18f?"glass":"metal";
            if(bldClass=="industrial" || bldClass=="warehouse")
                return "metal";
            if(feature.Height>=32f)
                return "glass";
            if(feature.Points!=null && feature.Points.Count>0)
            {
                var p=feature.Points[0];
                int bx=Mathf.FloorToInt(p.x/22f);
                int by=Mathf.FloorToInt(p.y/22f);
                uint hash=unchecked((uint)(bx*73856093 ^ by*19349663 ^ (int)feature.Height*83492791));
                uint m=hash%10;
                if(m==0 && feature.Height>=15f) return "glass";
                if(m==1 || m==2) return "brick";
                if(m==3 || m==4) return "stone";
                if(m==5) return "metal";
            }
            return "concrete";
        }
        public static JObject ReadDetails(JToken value)
        {
            var details=new JObject();
            if(!(value is JObject obj)) return details;
            foreach(string key in new[]{"oneway","junction","driving_side","class","surface","width","category","num_floors","facade_color","facade_material","roof_shape","roof_color","roof_material","roof_height","min_height","subtype","level","is_bridge","brand","id","building_id","has_parts","is_part","water_id","water_class","landuse","natural","leisure"})
            {
                var v=obj[key];
                if(v==null || v.Type==JTokenType.Object || v.Type==JTokenType.Array) continue;
                string text=(string)v;
                if(text!=null && text.Length<=120) details[key]=text;
            }
            return details;
        }
    }
}
