using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GeoSniper
{
    public static partial class StreetMapStyle
    {
        static Vector2 Center(System.Collections.Generic.List<Vector2> points)
        {
            Vector2 center=Vector2.zero;
            foreach(var point in points) center+=point;
            return center/Math.Max(1,points.Count);
        }

        public static JObject Feature(MapFeature feature,Vector3 offset,GameLocation origin)
        {
            bool road=feature.Kind=="road", place=feature.Kind=="place";
            if(feature.Points==null || feature.Points.Count<(road?2:place?1:3)) return null;
            JArray Coordinate(Vector2 point) => new JArray(
                origin.Longitude+(point.x+offset.x)/(111320*Math.Cos(origin.Latitude*Math.PI/180)),
                origin.Latitude+(point.y+offset.z)/111320.0);
            var coordinates=new JArray();
            foreach(var point in feature.Points) coordinates.Add(Coordinate(point));
            if(!road && !place && !JToken.DeepEquals(coordinates[0],coordinates[coordinates.Count-1]))
                coordinates.Add(coordinates[0].DeepClone());
            var geometry=new JObject { ["type"]=road?"LineString":place?"Point":"Polygon",
                ["coordinates"]=road?coordinates:place?Coordinate(Center(feature.Points)):new JArray {coordinates} };
            return new JObject { ["type"]="Feature",["geometry"]=geometry,
                ["properties"]=new JObject { ["kind"]=feature.Kind,["name"]=feature.Name??"",
                    ["class"]=MapFeatureStyle.Text(feature,"class"),["category"]=MapFeatureStyle.Category(feature),
                    ["width"]=MapFeatureStyle.RoadWidth(feature) } };
        }

        public static JObject Build(JArray features)
        {
            var layers=new JArray(new JObject { ["id"]="background",["type"]="background",
                ["paint"]=new JObject {["background-color"]="#f2efe9"} });
            JObject Layer(string id,string type,string kind) => new JObject {
                ["id"]=id,["type"]=type,["source"]="world",["filter"]=new JArray("==","kind",kind) };
            foreach(var surface in new[]{new[]{"park","#dff0db"},new[]{"water","#acd5e4"},new[]{"building","#d8d1c9"}})
            {
                var fill=Layer(surface[0],"fill",surface[0]);
                fill["paint"]=new JObject {["fill-color"]=surface[1], ["fill-opacity"]=.94};
                layers.Add(fill);
            }
            var roads=Layer("streets","line","road");
            roads["layout"]=new JObject {["line-cap"]="round",["line-join"]="round"};
            roads["paint"]=new JObject {
                ["line-color"]=new JArray("match",new JArray("get","class"),
                    new JArray("motorway","trunk"),"#f5aa94",new JArray("primary","secondary"),"#f4edaf","#ffffff"),
                ["line-width"]=new JArray("interpolate",new JArray("exponential",2),new JArray("zoom"),
                    14,1,17,new JArray("*",new JArray("get","width"),.8),20,new JArray("*",new JArray("get","width"),6.4)) };
            layers.Add(roads);
            var dots=Layer("shops","circle","place");
            dots["minzoom"]=16;
            dots["paint"]=new JObject {["circle-radius"]=3,["circle-color"]="#98709d",
                ["circle-stroke-color"]="#ffffff",["circle-stroke-width"]=1};
            layers.Add(dots);
            foreach(string kind in new[]{"place","road","building"})
            {
                var label=Layer(kind+"-names","symbol",kind);
                label["minzoom"]=kind=="building"?17:15;
                label["filter"]=new JArray("all",new JArray("==","kind",kind),new JArray("!=","name",""));
                var layout=new JObject {["text-field"]="{name}",["text-font"]=new JArray("Open Sans Semibold"),
                    ["text-size"]=kind=="building"?11:13,["text-max-width"]=10,["text-padding"]=8,
                    ["text-allow-overlap"]=false,["text-ignore-placement"]=false};
                if(kind=="road") { layout["symbol-placement"]="line"; layout["symbol-spacing"]=250; }
                else { layout["text-offset"]=new JArray(0,kind=="place"?1.1:0); }
                label["layout"]=layout;
                label["paint"]=new JObject {["text-color"]=kind=="place"?"#78577c":"#62605e",
                    ["text-halo-color"]="#f7f4ef",["text-halo-width"]=1.5};
                layers.Add(label);
            }
            return new JObject {["version"]=8,["name"]="Geo Sniper neighborhood",
                ["glyphs"]="https://demotiles.maplibre.org/font/{fontstack}/{range}.pbf",
                ["sources"]=new JObject {["world"]=new JObject {["type"]="geojson",
                    ["data"]=new JObject {["type"]="FeatureCollection",["features"]=features}}},["layers"]=layers};
        }
    }
}
