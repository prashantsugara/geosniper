using UnityEngine;

namespace GeoSniper
{
    // One projection drives raster cropping, labels, actors, panning and waypoint input.
    public readonly struct StreetMapViewport
    {
        public readonly Rect Rect;
        public readonly Vector3 Center;
        public readonly float Scale;
        public float RasterSpan => Mathf.Max(Rect.width, Rect.height) / Scale;
        public Rect TextureCoordinates => new Rect((1-Rect.width/Mathf.Max(Rect.width,Rect.height))*.5f,
            (1-Rect.height/Mathf.Max(Rect.width,Rect.height))*.5f,
            Rect.width/Mathf.Max(Rect.width,Rect.height),Rect.height/Mathf.Max(Rect.width,Rect.height));
        public StreetMapViewport(Rect rect, Vector3 center, float span)
        { Rect=rect; Center=center; Scale=Mathf.Min(rect.width,rect.height)/span; }
        public Vector2 Project(Vector3 point) => Rect.center+new Vector2(point.x-Center.x,Center.z-point.z)*Scale;
        public Vector3 Unproject(Vector2 point) => Center+new Vector3((point.x-Rect.center.x)/Scale,0,(Rect.center.y-point.y)/Scale);
    }
}
