using System;
using UnityEngine;
namespace GeoSniper
{
    // A pin owns a fixed world destination; camera movement, map panning and zoom never move it.
    public sealed class WaypointPin
    {
        public bool Active { get; private set; }
        public Vector3 Position { get; private set; }
        public string Name { get; private set; }="";
        public void Set(Vector3 position,string name)
        {Position=position;Name=string.IsNullOrWhiteSpace(name)?"Dropped pin":name;Active=true;}
        public void Clear() {Active=false;Name="";Position=Vector3.zero;}
        public float Distance(Vector3 player)
        {float x=Position.x-player.x,z=Position.z-player.z;return (float)Math.Sqrt(x*x+z*z);}
        public float HeightDifference(Vector3 player)=>Position.y-player.y;
        public bool Arrived(Vector3 player)=>Active && Distance(player)<=5 && Math.Abs(HeightDifference(player))<=2;
    }
}
