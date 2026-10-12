using System;
using System.IO;
using System.Linq;
using GeoSniper;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
public static class SceneryPoliceChecks
{
    public static void Run()
    {
        var scene=EditorSceneManager.NewPreviewScene();
        var root=new GameObject("Police car validation");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
        try
        {
            var prefab=Resources.Load<GameObject>("Models/PoliceCar");
            var car=Object.Instantiate(prefab,root.transform,false);
            var bounds=ImportedVisual.AlignVehicle(root,car,false,true);
            car.transform.localScale*=4.4f/Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
            bounds=ImportedVisual.LocalBounds(root.transform);car.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            PoliceVehicleVisual.Apply(car);PoliceVehicleVisual.Apply(car);
            var materials=car.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).ToArray();
            foreach(string key in new[]{"White","Blue","Glass","Rubber","LightRed","LightBlue"})
                if(!materials.Any(m=>m.name.Contains(key)))throw new Exception("Missing police car region "+key);
            if(materials.Select(m=>m.color).Distinct().Count()<6)throw new Exception("Police car lost its colors");
            if(car.GetComponents<PoliceVehicleVisual>().Length!=1)throw new Exception("Police paint reapplied twice");
            bounds=ImportedVisual.LocalBounds(root.transform);
            if(Mathf.Abs(Mathf.Max(bounds.size.x,bounds.size.z)-4.4f)>.1f)throw new Exception("Police car import changed scale");
            var trees=new GameObject("Large trees");trees.transform.SetParent(root.transform,false);
            var plants=trees.AddComponent<RealisticVegetation>();
            plants.Add("BroadleafTree",new Vector3(-6,0,7),9,20);plants.Add("SpreadingTree",new Vector3(6,0,10),12,100);plants.Flush();
            if(!trees.GetComponentsInChildren<Renderer>().Any(r=>r.bounds.size.y>10))throw new Exception("Large tree height missing");
            var camera=new GameObject("Police camera").AddComponent<Camera>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject,scene);camera.scene=scene;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.64f,.74f,.81f);
            camera.transform.position=new Vector3(5,3,-6);camera.transform.LookAt(new Vector3(0,.6f,0));
            var light=new GameObject("Daylight").AddComponent<Light>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject,scene);light.type=LightType.Directional;light.intensity=1.1f;light.transform.rotation=Quaternion.Euler(45,-30,0);
            var target=RenderTexture.GetTemporary(1280,720,24);var previous=RenderTexture.active;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/PoliceCar.png",image.EncodeToPNG());
                camera.transform.position=new Vector3(15,8,-16);camera.transform.LookAt(new Vector3(0,4,6));camera.Render();image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/LargeTrees.png",image.EncodeToPNG());
            }
            finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(image);}
            File.WriteAllText("Logs/SceneryPoliceChecks.txt","PASS: distinct police paint, glass, rubber and red/blue lights; repeat application; unchanged vehicle scale; large tree height.");
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
        StreetBlockChecks.Run();
    }
}
