using UnityEngine;
using UnityEngine.Rendering;
namespace GeoSniper
{
    public sealed class TrafficBrakeLights : MonoBehaviour
    {
        static Mesh lamps;
        static Material material;
        MeshRenderer lampRenderer;
        MaterialPropertyBlock properties;
        int lastState=-1;
        public void Configure(Bounds body,string modelName="")
        {
            if(properties==null)properties=new MaterialPropertyBlock();
            if(lampRenderer!=null)return;
            if(lamps==null)
            {
                lamps=new Mesh{name="Paired tail lamps"};
                var vertices=new System.Collections.Generic.List<Vector3>();
                var triangles=new System.Collections.Generic.List<int>();
                foreach(float x in new[]{-.30f,.30f})
                {
                    int first=vertices.Count;
                    foreach(var point in new[]{new Vector3(-.05f,-.5f,-.5f),new Vector3(.05f,-.5f,-.5f),new Vector3(.05f,.5f,-.5f),new Vector3(-.05f,.5f,-.5f),new Vector3(-.05f,-.5f,.5f),new Vector3(.05f,-.5f,.5f),new Vector3(.05f,.5f,.5f),new Vector3(-.05f,.5f,.5f)})vertices.Add(point+Vector3.right*x);
                    foreach(int i in new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5})triangles.Add(first+i);
                }
                lamps.SetVertices(vertices);lamps.SetTriangles(triangles,0);lamps.RecalculateNormals();lamps.RecalculateBounds();
            }
            if(material==null){material=new Material(Shader.Find("Standard")){name="Traffic tail lamp",color=new Color(.3f,.008f,.004f)};material.EnableKeyword("_EMISSION");material.SetFloat("_Glossiness",.4f);}
            var holder=new GameObject("Brake lamps");holder.transform.SetParent(transform,false);
            holder.transform.localPosition=new Vector3(body.center.x,body.min.y+body.size.y*(modelName.ToLowerInvariant().StartsWith("ford")?.54f:.64f),body.min.z+.015f);
            holder.transform.localScale=new Vector3(body.size.x,.075f,.04f);
            holder.AddComponent<MeshFilter>().sharedMesh=lamps;
            lampRenderer=holder.AddComponent<MeshRenderer>();lampRenderer.sharedMaterial=material;
            lampRenderer.shadowCastingMode=ShadowCastingMode.Off;lampRenderer.receiveShadows=false;
            SetState(false,true);
        }
        public void SetState(bool braking,bool powered)
        {
            int state=!powered?0:braking?2:1;if(state==lastState || lampRenderer==null)return;lastState=state;
            properties.SetColor("_Color",state==2?new Color(.9f,.015f,.006f):new Color(.22f,.004f,.002f));
            properties.SetColor("_EmissionColor",new Color(1f,.012f,.003f)*(state==2?2.2f:state==1?.08f:0));
            lampRenderer.SetPropertyBlock(properties);
        }
    }
}
