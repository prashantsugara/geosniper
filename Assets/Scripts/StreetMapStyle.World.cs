using Newtonsoft.Json.Linq;
namespace GeoSniper
{
    public static partial class StreetMapStyle
    {
        public static string Loaded(GameLocation origin)
        {
            var rows=new JArray();
            foreach(var sector in SectorWorld.LoadedWorlds)
            {
                if(sector==null) continue;
                foreach(var feature in sector.Features)
                {
                    var row=Feature(feature,sector.transform.position,origin);
                    if(row!=null) rows.Add(row);
                }
            }
            return Build(rows).ToString(Newtonsoft.Json.Formatting.None);
        }
    }
}
