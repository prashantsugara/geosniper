using GeoSniper;
using Newtonsoft.Json.Linq;
using UnityEngine;

void Check(bool pass,string message) { if(!pass) throw new Exception(message); Console.WriteLine("PASS: "+message); }
var origin=new GameLocation {Latitude=18.5,Longitude=73.9};
var building=new MapFeature {Kind="building",Name="Market \"A\"",Points=new List<Vector2>{new(0,0),new(10,0),new(10,10),new(0,10)}};
var row=StreetMapStyle.Feature(building,new Vector3(640,0,0),origin);
var ring=(JArray)row["geometry"]["coordinates"][0];
Check(ring.Count==5 && JToken.DeepEquals(ring[0],ring[4]),"Polygon nesting and closed ring");
Check(Math.Abs((double)ring[0][0]-(73.9+640/(111320*Math.Cos(18.5*Math.PI/180))))<1e-9,"Streamed sector longitude offset");
Check(Math.Abs((double)ring[0][1]-18.5)<1e-9,"North/south coordinate preserved");
building.Kind="place";
var shop=StreetMapStyle.Feature(building,Vector3.zero,origin);
Check((string)shop["geometry"]["type"]=="Point" && shop["geometry"]["coordinates"].Count()==2,"Shop polygon converted to single marker");
var style=StreetMapStyle.Build(new JArray {row,shop});
var parsed=JObject.Parse(style.ToString());
Check((string)parsed["sources"]["world"]["type"]=="geojson" && !style.ToString().Contains("tile.openstreetmap.org"),"Map uses actual gameplay features, no raster basemap");
Check((string)parsed["sources"]["world"]["data"]["features"][0]["properties"]["name"]=="Market \"A\"","Names survive JSON escaping");
Check(parsed["layers"].Any(x=>(string)x["id"]=="place-names" && !(bool)x["layout"]["text-allow-overlap"]),"Shop labels prevent overlap");
Check(parsed["layers"].Where(x=>(string)x["type"]=="symbol").All(x=>(string)x["layout"]["text-font"][0]=="Open Sans Semibold"),"Labels use the verified font stack");
Check(parsed["layers"].Any(x=>(string)x["id"]=="building-names" && (double)x["minzoom"]<=17.2),"Building names eligible at opening zoom");
