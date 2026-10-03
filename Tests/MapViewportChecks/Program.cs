using GeoSniper;
using UnityEngine;
void Check(bool pass,string message) { if(!pass) throw new Exception(message);Console.WriteLine("PASS: "+message); }
bool Near(float a,float b)=>Math.Abs(a-b)<.001;
foreach(var rect in new[]{new Rect(296,58,1100,620),new Rect(226,58,420,800),new Rect(20,50,600,600)})
foreach(float span in new[]{60f,450f,2400f})
{
    var center=new Vector3(725,12,-340);
    var view=new StreetMapViewport(rect,center,span);
    var point=center+new Vector3(23,0,17);
    var screen=view.Project(point);
    var restored=view.Unproject(screen);
    Check(Near(point.x,restored.x)&&Near(point.z,restored.z),"Click and marker projection round trip");
    var uv=view.TextureCoordinates;
    float textureX=.5f+(point.x-center.x)/view.RasterSpan;
    float textureY=.5f+(point.z-center.z)/view.RasterSpan;
    float imageX=rect.x+(textureX-uv.x)/uv.width*rect.width;
    float imageY=rect.y+(1-(textureY-uv.y)/uv.height)*rect.height;
    Check(Near(screen.x,imageX)&&Near(screen.y,imageY),"Raster and markers align at every aspect ratio and zoom");
    Check(Near(view.Project(center+Vector3.right*10).x-rect.center.x,rect.center.y-view.Project(center+Vector3.forward*10).y),"Equal metre scale on both axes");
}
