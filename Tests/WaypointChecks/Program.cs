using GeoSniper;
using UnityEngine;
int count=0;
void Check(bool pass,string message) {if(!pass) throw new Exception(message);count++;Console.WriteLine("PASS: "+message);}
var pin=new WaypointPin();
Check(!pin.Active && !pin.Arrived(Vector3.zero),"No arrival before placing a pin");
pin.Set(new Vector3(30,12,40),"Roof position A");
Check(pin.Active && pin.Name=="Roof position A","Placed pin retains its location name");
Check(pin.Distance(Vector3.zero)==50 && pin.HeightDifference(Vector3.zero)==12,"Horizontal navigation distance is separate from rooftop height");
Check(!pin.Arrived(new Vector3(30,0,40)),"Standing underneath a rooftop pin is not arrival");
Check(pin.Arrived(new Vector3(30,12,40)),"Reaching the correct roof is arrival");
Check(!pin.Arrived(new Vector3(36,12,40)),"A nearby street position outside arrival radius remains active");
var saved=pin.Position;
foreach(var view in new[]{new StreetMapViewport(new Rect(200,50,900,500),Vector3.zero,450),new StreetMapViewport(new Rect(200,50,500,900),new Vector3(100,0,100),120)})
{
 var projected=view.Project(pin.Position);var unprojected=view.Unproject(projected);
 Check(Math.Abs(unprojected.x-pin.Position.x)<.001 && Math.Abs(unprojected.z-pin.Position.z)<.001,"Pinned horizontal coordinate survives pan/zoom and aspect changes");
 Check(pin.Position==saved && pin.Position.y==12,"Map projection never overwrites pinned elevation");
}
pin.Set(new Vector3(-10,0,20),"Market");Check(pin.Position.x==-10 && pin.Name=="Market","Selecting another place replaces the existing destination");
pin.Clear();Check(!pin.Active && pin.Name=="" && !pin.Arrived(Vector3.zero),"Clear removes marker and arrival state");
Console.WriteLine(count+" waypoint checks passed.");
