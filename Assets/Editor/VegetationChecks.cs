using System;
using System.IO;
using System.Linq;
using GeoSniper;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
public static class VegetationChecks
{
    public static void Run()
    {
        var scene=EditorSceneManager.NewPreviewScene();
        var root=new GameObject("Vegetation showcase");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
        try
        {
            var plants=root.AddComponent<RealisticVegetation>();
            string[] names={"BroadleafTree","SpreadingTree","RoundedShrub","ConeShrub","LowShrub","TallShrub","GrassClump","LeafyGroundPlant"};
            var diagnostics=new System.Text.StringBuilder();
            for(int i=0;i<names.Length;i++)
            {
                var source=Resources.Load<GameObject>("Models/Vegetation/"+names[i]);
                if(source==null)throw new Exception("Missing vegetation "+names[i]);
                foreach(var r in source.GetComponentsInChildren<Renderer>())diagnostics.AppendLine(names[i]+" "+r.bounds+" "+string.Join(",",r.sharedMaterials.Select(m=>m==null?"NULL":m.name)));
                int triangles=source.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);
                if(triangles>17000 || triangles==0)throw new Exception("Vegetation triangle budget: "+names[i]+" "+triangles);
                if(!plants.Add(names[i],new Vector3(i<2?-4+i*7:-4+(i-2)*1.7f,0,i<2?3:-2),i<2?6:i<6?1.2f:.6f,i*31))throw new Exception("Placement failed");
            }
            plants.Flush();
            foreach(var r in root.GetComponentsInChildren<Renderer>())diagnostics.AppendLine("BATCH "+r.bounds+" "+r.sharedMaterial.name+" texture="+(r.sharedMaterial.mainTexture==null?"NULL":r.sharedMaterial.mainTexture.name));
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/VegetationMaterials.txt",diagnostics.ToString());
            if(root.GetComponentsInChildren<Collider>().Length>0)throw new Exception("Vegetation blocks navigation");
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
                if(renderer.sharedMaterial.shader.name!="GeoSniper/Vegetation")throw new Exception("Vegetation material missing");
            var camera=new GameObject("Vegetation camera").AddComponent<Camera>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject,scene);camera.scene=scene;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.64f,.74f,.81f);
            camera.transform.position=new Vector3(9,5,-12);camera.transform.LookAt(new Vector3(0,2.3f,1));
            var light=new GameObject("Daylight").AddComponent<Light>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject,scene);light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(45,-30,0);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ground,scene);ground.transform.localScale=Vector3.one*3;
            var groundMat=new Material(Shader.Find("Standard")){color=new Color(.34f,.37f,.29f)};ground.GetComponent<Renderer>().sharedMaterial=groundMat;
            var target=RenderTexture.GetTemporary(1280,720,24);var previous=RenderTexture.active;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/Vegetation.png",image.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(image);Object.DestroyImmediate(groundMat);}
            File.WriteAllText("Logs/VegetationChecks.txt","PASS: eight imported vegetation varieties, bounded triangle counts, cutout shader and no navigation colliders.");
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
        StreetBlockChecks.Run();
    }
}
