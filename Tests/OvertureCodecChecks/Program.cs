using GeoSniper;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

void Check(bool value,string label) { if(!value) throw new Exception(label); }
void Reject(Action action,string label)
{
    try { action(); } catch(FormatException) { return; }
    throw new Exception("Accepted malformed " + label);
}
Check(OvertureTileCodec.TileId(0,0,0)==0,"root ID");
Check(OvertureTileCodec.TileId(1,0,0)==1,"NW ID");
Check(OvertureTileCodec.TileId(1,0,1)==2,"SW ID");
Check(OvertureTileCodec.TileId(1,1,1)==3,"SE ID");
Check(OvertureTileCodec.TileId(1,1,0)==4,"NE ID");
var ids=new HashSet<ulong>();
for(int x=0;x<16;x++) for(int y=0;y<16;y++) Check(ids.Add(OvertureTileCodec.TileId(4,x,y)),"Hilbert uniqueness");
var directory=OvertureTileCodec.Directory(new byte[]{2,1,2,1,1,3,4,1,0});
Check(directory[1].Id==3 && directory[1].Offset==3,"directory delta decoding");
Check(OvertureTileCodec.Find(directory,2)==null,"missing tile");
Reject(()=>OvertureTileCodec.Directory(new byte[]{1,128}),"directory");
Reject(()=>OvertureTileCodec.Decode(new byte[]{26,128}),"protobuf");
Reject(()=>OvertureTileCodec.Inflate(new byte[]{0},4),"compression");
// One point at tile coordinate (1,2), layer 'place', using packed MVT commands.
byte[] feature={24,1,34,3,9,2,4};
var layer=new List<byte>{10,5}; layer.AddRange(Encoding.UTF8.GetBytes("place"));
layer.Add(18); layer.Add((byte)feature.Length); layer.AddRange(feature);
var tile=new List<byte>{26,(byte)layer.Count}; tile.AddRange(layer);
var decoded=OvertureTileCodec.Decode(tile.ToArray());
Check(decoded.Count==1 && decoded[0].Paths[0][0][1]==2,"point decoding");
// Attribute allow-list must retain styling data but reject private contact data.
foreach(string key in new[]{"@name","name","name:en","official_name","brand","class","width_rules","road_surface","num_floors","facade_color","roof_shape","basic_category","phones"})
{
    var styledFeature=new List<byte>{18,2,0,0}; styledFeature.AddRange(feature);
    var styledLayer=new List<byte>{10,5}; styledLayer.AddRange(Encoding.UTF8.GetBytes("place"));
    styledLayer.Add(18); styledLayer.Add((byte)styledFeature.Count); styledLayer.AddRange(styledFeature);
    styledLayer.Add(26); styledLayer.Add((byte)key.Length); styledLayer.AddRange(Encoding.UTF8.GetBytes(key));
    styledLayer.AddRange(new byte[]{34,3,10,1,120});
    var styledTile=new List<byte>{26,(byte)styledLayer.Count}; styledTile.AddRange(styledLayer);
    var tags=OvertureTileCodec.Decode(styledTile.ToArray())[0].Tags;
    Check(tags.ContainsKey(key)==(key!="phones"),"styling attribute "+key);
}
Console.WriteLine("Codec fixtures passed.");
if(!args.Contains("--live")) return;
using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(40)};
http.DefaultRequestHeaders.UserAgent.ParseAdd("GeoSniperPrototype/1.0");
var catalog=JsonDocument.Parse(await http.GetStringAsync("https://stac.overturemaps.org/catalog.json"));
string release=catalog.RootElement.GetProperty("latest").GetString();
async Task<byte[]> Range(string url,ulong start,ulong count)
{
    using var request=new HttpRequestMessage(HttpMethod.Get,url);
    request.Headers.Range=new RangeHeaderValue((long)start,(long)(start+count-1));
    using var response=await http.SendAsync(request);
    Check(response.StatusCode==HttpStatusCode.PartialContent,"range response");
    var bytes=await response.Content.ReadAsByteArrayAsync(); Check((ulong)bytes.Length==count,"range length"); return bytes;
}
int z=14,xTile=(int)Math.Floor((73.934+180)/360*(1<<14));
double lat=18.553*Math.PI/180;
int yTile=(int)Math.Floor((1-Math.Log(Math.Tan(lat)+1/Math.Cos(lat))/Math.PI)/2*(1<<14));
foreach(string theme in new[]{"transportation","buildings","places"})
{
    string url=$"https://overturemaps-extras-us-west-2.s3.us-west-2.amazonaws.com/tiles/{release}/{theme}.pmtiles";
    var header=await Range(url,0,127);
    var bytes=await Range(url,OvertureTileCodec.U64(header,8),OvertureTileCodec.U64(header,16));
    var entries=OvertureTileCodec.Directory(OvertureTileCodec.Inflate(bytes,header[97]));
    bool found=false;
    for(int depth=0;depth<4;depth++)
    {
        var entry=OvertureTileCodec.Find(entries,OvertureTileCodec.TileId(z,xTile,yTile));
        Check(entry!=null,"live tile missing");
        bytes=await Range(url,OvertureTileCodec.U64(header,entry.Run==0?40:56)+entry.Offset,entry.Length);
        if(entry.Run==0) entries=OvertureTileCodec.Directory(OvertureTileCodec.Inflate(bytes,header[97]));
        else
        {
            var rows=OvertureTileCodec.Decode(OvertureTileCodec.Inflate(bytes,header[98]));
            Check(rows.Count>0,"empty live geometry");
            Check(rows.Any(f=>f.Tags.ContainsKey("@name")),"missing live names");
            Console.WriteLine($"{release} {theme}: {rows.Count} decoded features; {bytes.Length} compressed tile bytes");
            found=true; break;
        }
    }
    Check(found,"directory recursion");
}
