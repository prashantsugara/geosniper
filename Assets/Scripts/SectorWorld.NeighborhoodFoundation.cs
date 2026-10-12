using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    public sealed partial class SectorWorld
    {
        static Texture2D neighborhoodPavingTexture;
        static Texture2D BuildNeighborhoodPavingTexture()
        {
            if(neighborhoodPavingTexture!=null)return neighborhoodPavingTexture;
            const int size=128;var colors=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                uint hash=unchecked((uint)(x*73856093 ^ y*19349663));hash^=hash>>13;hash*=1274126177;
                float grain=(hash&255)/255f;
                float shade=.88f+(grain-.5f)*.055f;
                colors[y*size+x]=new Color(shade,shade,shade*.98f);
            }
            neighborhoodPavingTexture=new Texture2D(size,size,TextureFormat.RGB24,true){name="Fine concrete paving",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};
            neighborhoodPavingTexture.SetPixels(colors);neighborhoodPavingTexture.Apply(true,true);
            return neighborhoodPavingTexture;
        }
        void BuildNeighborhoodFoundation(Transform site,int kind,int roadside,Material paving,Material stone,Material paint,Material iron)
        {
            // Sample every tile against the terrain instead of floating a flat platform on sloped streets.
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            const int columns=4,rows=8;const float width=5.2f,length=10.4f,gap=.018f;
            for(int x=0;x<columns;x++)for(int z=0;z<rows;z++)
            {
                float left=-width*.5f+x*width/columns+gap,right=left+width/columns-gap*2;
                float near=-length*.5f+z*length/rows+gap,far=near+length/rows-gap*2;
                int first=vertices.Count;
                foreach(var corner in new[]{new Vector2(left,near),new Vector2(left,far),new Vector2(right,far),new Vector2(right,near)})
                {
                    vertices.Add(NeighborhoodGroundPoint(site,corner.x,corner.y,.035f));
                    uv.Add(corner*.65f);
                }
                triangles.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
            }
            var mesh=new Mesh{name="Terrain fitted neighborhood paving"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
            var surface=new GameObject("Neighborhood pavement");surface.transform.SetParent(site,false);
            surface.AddComponent<MeshFilter>().sharedMesh=mesh;surface.AddComponent<MeshRenderer>().sharedMaterial=paving;
            surface.AddComponent<NeighborhoodSurfaceMarker>();
            // Leave the roadside edge open for access. Borders remain inside the validated site footprint.
            for(int z=0;z<8;z++)
                DressingBox(site,"Paving border",NeighborhoodGroundPoint(site,roadside*2.5f,-4.55f+z*1.3f,.1f),new Vector3(.16f,.16f,1.27f),stone,false);
            if(kind==0)
            {
                foreach(float z in new[]{-5.05f,0,5.05f})
                    DressingBox(site,"Parking bay line",NeighborhoodGroundPoint(site,0,z,.055f),new Vector3(4.2f,.018f,.08f),paint,false);
            }
            else if(kind==2)
            {
                for(int z=0;z<6;z++)DressingBox(site,"Bus boarding edge",NeighborhoodGroundPoint(site,-roadside*2.35f,-2.5f+z,.055f),new Vector3(.18f,.018f,.72f),paint,false);
            }
            else if(kind==1 || kind==3)
            {
                DressingBox(site,"Loading drain",NeighborhoodGroundPoint(site,roadside*2.1f,3.5f,.065f),new Vector3(.45f,.045f,.9f),iron,false);
                for(int i=0;i<6;i++)DressingBox(site,"Drain grate",NeighborhoodGroundPoint(site,roadside*2.1f,3.15f+i*.14f,.092f),new Vector3(.4f,.012f,.022f),stone,false);
            }
        }
        Vector3 NeighborhoodGroundPoint(Transform site,float x,float z,float height)
        {
            var local=transform.InverseTransformPoint(site.TransformPoint(new Vector3(x,0,z)));
            return site.InverseTransformPoint(transform.TransformPoint(new Vector3(local.x,Ground(local.x,local.z)+height,local.z)));
        }
    }
    public sealed class NeighborhoodSurfaceMarker : MonoBehaviour { }
}
