using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace GeoSniper
{
    // PMTiles v3 + the subset of MVT needed for Overture geometry; no native plugins.
    public static class OvertureTileCodec
    {
        public const int Limit=8*1024*1024;
        public sealed class Entry { public ulong Id,Offset,Length,Run; }
        public sealed class Feature
        {
            public string Layer;
            public int Type,Extent;
            public Dictionary<string,string> Tags=new Dictionary<string,string>();
            public List<List<double[]>> Paths=new List<List<double[]>>();
        }
        public static ulong TileId(int z,int x,int y)
        {
            if(z<0 || z>24 || x<0 || y<0 || x>=(1<<z) || y>=(1<<z)) throw new FormatException("TILE_COORDINATE");
            ulong id=((1UL<<(2*z))-1)/3;
            for(int s=(1<<z)/2;s>0;s/=2)
            {
                int rx=(x&s)>0?1:0, ry=(y&s)>0?1:0;
                id+=(ulong)s*(ulong)s*(ulong)((3*rx)^ry);
                if(ry==0)
                {
                    if(rx==1) { x=s-1-x; y=s-1-y; }
                    int swap=x; x=y; y=swap;
                }
            }
            return id;
        }
        public static ulong U64(byte[] data,int offset)
        {
            if(offset<0 || offset+8>data.Length) throw new FormatException("HEADER");
            ulong value=0;
            for(int i=0;i<8;i++) value|=(ulong)data[offset+i]<<(8*i);
            return value;
        }
        public static byte[] Inflate(byte[] data,int compression)
        {
            if(compression==1) return data;
            if(compression!=2) throw new FormatException("UNSUPPORTED_TILE_COMPRESSION");
            using(var input=new MemoryStream(data))
            using(var gzip=new GZipStream(input,CompressionMode.Decompress))
            using(var output=new MemoryStream())
            {
                var buffer=new byte[8192]; int count;
                while((count=gzip.Read(buffer,0,buffer.Length))>0)
                {
                    if(output.Length+count>Limit) throw new FormatException("TILE_TOO_LARGE");
                    output.Write(buffer,0,count);
                }
                return output.ToArray();
            }
        }
        public static List<Entry> Directory(byte[] bytes)
        {
            var p=new Proto(bytes); int count=checked((int)p.Varint());
            if(count>100000) throw new FormatException("DIRECTORY_LIMIT");
            var rows=new List<Entry>(count); ulong id=0;
            for(int i=0;i<count;i++) { id=checked(id+p.Varint()); rows.Add(new Entry{Id=id}); }
            foreach(var row in rows) row.Run=p.Varint();
            foreach(var row in rows) row.Length=p.Varint();
            for(int i=0;i<count;i++)
            {
                ulong offset=p.Varint();
                if(offset==0 && i==0) throw new FormatException("DIRECTORY_OFFSET");
                rows[i].Offset=offset==0?checked(rows[i-1].Offset+rows[i-1].Length):offset-1;
            }
            return rows;
        }
        public static Entry Find(List<Entry> entries,ulong id)
        {
            int lo=0,hi=entries.Count-1;
            while(lo<=hi) { int mid=(lo+hi)/2; if(entries[mid].Id<=id) lo=mid+1; else hi=mid-1; }
            if(hi<0) return null;
            var e=entries[hi];
            return e.Run==0 || id-e.Id<e.Run?e:null;
        }
        public static List<Feature> Decode(byte[] bytes)
        {
            if(bytes.Length>Limit) throw new FormatException("TILE_LIMIT");
            var result=new List<Feature>(); var p=new Proto(bytes);
            while(!p.End)
            {
                int tag=(int)p.Varint();
                if(tag==26) Layer(p.Bytes(),result); else p.Skip(tag&7);
            }
            return result;
        }
        static void Layer(byte[] bytes,List<Feature> output)
        {
            var keys=new List<string>(); var values=new List<string>(); var features=new List<byte[]>();
            string name=""; int extent=4096; var p=new Proto(bytes);
            while(!p.End)
            {
                int tag=(int)p.Varint();
                if(tag==10) name=Encoding.UTF8.GetString(p.Bytes());
                else if(tag==18) { if(features.Count>=100000) throw new FormatException("FEATURE_LIMIT"); features.Add(p.Bytes()); }
                else if(tag==26) keys.Add(Encoding.UTF8.GetString(p.Bytes()));
                else if(tag==34) values.Add(Value(p.Bytes()));
                else if(tag==40) extent=checked((int)p.Varint());
                else p.Skip(tag&7);
            }
            if(extent<1 || extent>65536) throw new FormatException("EXTENT");
            if(name!="building" && name!="building_part" && name!="segment" && name!="place" && name!="water" && name!="land_use") return;
            foreach(var raw in features)
            {
                var f=new Feature{Layer=name,Extent=extent}; byte[] geometry=null; p=new Proto(raw);
                while(!p.End)
                {
                    int tag=(int)p.Varint();
                    if(tag==18)
                    {
                        var tags=new Proto(p.Bytes());
                        while(!tags.End)
                        {
                            int key=checked((int)tags.Varint()),value=checked((int)tags.Varint());
                            if(key>=keys.Count || value>=values.Count) throw new FormatException("TAG_INDEX");
                            // Do not retain addresses, phone numbers or other unused metadata.
                            string k=keys[key];
                            if(k=="@name" || k=="name" || k=="name:en" || k=="official_name" || k=="brand" || k=="id" || k=="building_id" || k=="has_parts" || k=="subtype" || k=="height" || k=="min_height" || k=="basic_category" || k=="operating_status"
                                || k=="class" || k=="subclass" || k=="width_rules" || k=="road_surface" || k=="num_floors" || k=="facade_color" || k=="facade_material"
                                || k=="roof_shape" || k=="roof_color" || k=="roof_material" || k=="roof_height" || k=="level" || k=="is_bridge" || k=="is_tunnel" || k=="is_part") f.Tags[k]=values[value];
                        }
                    }
                    else if(tag==24) f.Type=checked((int)p.Varint());
                    else if(tag==34) geometry=p.Bytes();
                    else p.Skip(tag&7);
                }
                if(geometry==null) continue;
                p=new Proto(geometry); int x=0,y=0; List<double[]> path=null; int points=0;
                while(!p.End)
                {
                    ulong command=p.Varint(); int op=(int)(command&7),count=checked((int)(command>>3));
                    if(count<1 || count>100000) throw new FormatException("GEOMETRY_COUNT");
                    if(op==7)
                    {
                        if(count!=1 || path==null || path.Count<3) throw new FormatException("CLOSE_PATH");
                        continue;
                    }
                    if(op!=1 && op!=2) throw new FormatException("GEOMETRY_COMMAND");
                    for(int i=0;i<count;i++)
                    {
                        x=checked(x+p.Signed()); y=checked(y+p.Signed());
                        if(++points>100000) throw new FormatException("GEOMETRY_LIMIT");
                        if(op==1) { path=new List<double[]>(); f.Paths.Add(path); }
                        if(path==null) throw new FormatException("MISSING_MOVE");
                        path.Add(new double[]{x,y});
                    }
                }
                output.Add(f);
            }
        }
        static string Value(byte[] bytes)
        {
            var p=new Proto(bytes); string result="";
            while(!p.End)
            {
                int tag=(int)p.Varint();
                if(tag==10) result=Encoding.UTF8.GetString(p.Bytes());
                else if(tag==21) result=BitConverter.ToSingle(p.Fixed(4),0).ToString(System.Globalization.CultureInfo.InvariantCulture);
                else if(tag==25) result=BitConverter.ToDouble(p.Fixed(8),0).ToString(System.Globalization.CultureInfo.InvariantCulture);
                else if(tag==32 || tag==40 || tag==56) result=p.Varint().ToString();
                else if(tag==48) result=p.Signed().ToString();
                else p.Skip(tag&7);
            }
            return result;
        }
        sealed class Proto
        {
            readonly byte[] data; int pos;
            public Proto(byte[] data) { this.data=data; }
            public bool End=>pos>=data.Length;
            public ulong Varint()
            {
                ulong value=0;
                for(int shift=0;shift<64;shift+=7)
                {
                    if(End) throw new FormatException("TRUNCATED_VARINT");
                    byte b=data[pos++];
                    if(shift==63 && b>1) throw new FormatException("VARINT_OVERFLOW");
                    value|=(ulong)(b&127)<<shift;
                    if(b<128) return value;
                }
                throw new FormatException("INVALID_VARINT");
            }
            public int Signed() { ulong v=Varint(); return checked((int)((long)(v>>1)^-((long)v&1))); }
            public byte[] Bytes()=>Fixed(checked((int)Varint()));
            public byte[] Fixed(int length)
            {
                if(length<0 || length>data.Length-pos) throw new FormatException("TRUNCATED_FIELD");
                var value=new byte[length]; Buffer.BlockCopy(data,pos,value,0,length); pos+=length; return value;
            }
            public void Skip(int wire)
            {
                if(wire==0) Varint();
                else if(wire==1) Fixed(8);
                else if(wire==2) Bytes();
                else if(wire==5) Fixed(4);
                else throw new FormatException("WIRE_TYPE");
            }
        }
    }
}
