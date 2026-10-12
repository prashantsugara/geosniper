using System;
using System.IO;
using System.Collections.Generic;
using GeoSniper;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class ArmoryShowcaseRenderer
{
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/.utmp/review-unity/"))throw new Exception("Use disposable review project");
        var studioMaterials=new List<Material>();
        var scene=EditorSceneManager.NewPreviewScene();
        var ambientMode=RenderSettings.ambientMode;
        var sky=RenderSettings.ambientSkyColor;var equator=RenderSettings.ambientEquatorColor;var ground=RenderSettings.ambientGroundColor;
        string source=Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).Parent.FullName;
        try
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.35f,.4f,.48f);
            RenderSettings.ambientEquatorColor=new Color(.16f,.19f,.23f);
            RenderSettings.ambientGroundColor=new Color(.07f,.08f,.1f);
            var camera=Create(scene,"Armory studio camera").AddComponent<Camera>();
            camera.scene=scene;camera.orthographic=true;camera.aspect=8f/3;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.06f,.075f);
            camera.allowHDR=false;camera.allowMSAA=true;camera.nearClipPlane=.01f;camera.farClipPlane=20;
            Lamp(scene,"Warm key",new Vector3(25,-40,0),new Color(1,.9f,.76f),2.1f);
            Lamp(scene,"Cool fill",new Vector3(10,135,0),new Color(.65f,.8f,1),1.3f);
            Lamp(scene,"Upper rim",new Vector3(65,-160,0),new Color(.85f,.92f,1),1.8f);
            for(int i=0;i<WeaponCatalog.Count;i++)
            {
                var gun=WeaponCatalog.Get(i);
                var holder=Create(scene,"Showcase "+gun.Name);
                var prefab=Resources.Load<GameObject>("Models/Weapons/"+gun.Model);
                if(prefab==null)throw new Exception("Missing armory model: "+gun.Model);
                var model=Object.Instantiate(prefab,holder.transform,false);
                if(!WeaponGeometry.Configure(model.transform,holder.transform,i))throw new Exception("Invalid weapon: "+gun.Model);
                WeaponAuthoredMaterials.Apply(model.transform);WeaponAuthoredMaterials.ApplyFinish(model.transform);
                foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>())
                    {
                        var slots=renderer.sharedMaterials;
                        for(int slot=0;slot<slots.Length;slot++)
                        {
                            var original=slots[slot];
                            if(original==null)continue;
                            var finish=new Material(original);studioMaterials.Add(finish);
                            string surface=(renderer.name+" "+original.name).ToLowerInvariant();
                            bool soft=original.name.EndsWith("/stock") || surface.Contains("wood") || surface.Contains("rust");
                            bool glass=original.name.EndsWith("/glass");
                            if(original.mainTexture!=null)
                            {
                                finish.SetFloat("_BumpScale",.25f);
                                if(soft)
                                {
                                    finish.DisableKeyword("_METALLICGLOSSMAP");finish.SetTexture("_MetallicGlossMap",null);
                                    finish.SetFloat("_Metallic",0);finish.SetFloat("_Glossiness",.22f);
                                    finish.color=new Color(.72f,.72f,.72f);
                                }
                                else finish.SetFloat("_GlossMapScale",.6f);
                            }
                            else if(!glass)
                            {
                                finish.color=soft?new Color(.18f,.22f,.15f):new Color(.11f,.14f,.18f);
                                if(surface.Contains("rubber") || surface.Contains("butt"))finish.color=new Color(.045f,.05f,.055f);
                                finish.SetFloat("_Metallic",soft?0:.78f);
                                finish.SetFloat("_Glossiness",soft?.22f:surface.Contains("bolt")?.55f:.4f);
                            }
                            slots[slot]=finish;
                        }
                        renderer.sharedMaterials=slots;
                    }
                foreach(var renderer in holder.GetComponentsInChildren<SkinnedMeshRenderer>())renderer.enabled=false;
                var bounds=ImportedVisual.LocalBounds(holder.transform);
                camera.transform.position=bounds.center+new Vector3(3,.6f,.9f);
                camera.transform.LookAt(bounds.center);
                float halfHeight=0,halfWidth=0;
                for(int corner=0;corner<8;corner++)
                {
                    var point=bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                    var relative=camera.transform.InverseTransformPoint(point);
                    halfHeight=Mathf.Max(halfHeight,Mathf.Abs(relative.y));halfWidth=Mathf.Max(halfWidth,Mathf.Abs(relative.x));
                }
                camera.orthographicSize=Mathf.Max(halfHeight/.78f,halfWidth/(camera.aspect*.88f));
                Capture(camera,Path.Combine(source,"Assets/Resources/Textures/"+gun.Model+"Showcase.png"));
                Object.DestroyImmediate(holder);
            }
            Debug.Log("PASS: eight high resolution armory studio renders generated from the gameplay models");
        }
        finally
        {
            RenderSettings.ambientMode=ambientMode;RenderSettings.ambientSkyColor=sky;
            RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;
            EditorSceneManager.ClosePreviewScene(scene);
            foreach(var material in studioMaterials)Object.DestroyImmediate(material);
        }
    }
    static GameObject Create(Scene scene,string name){var root=new GameObject(name);SceneManager.MoveGameObjectToScene(root,scene);return root;}
    static void Lamp(Scene scene,string name,Vector3 angle,Color color,float intensity)
    {var light=Create(scene,name).AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(angle);light.color=color;light.intensity=intensity;}
    static void Capture(Camera camera,string path)
    {
        var target=RenderTexture.GetTemporary(2048,768,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);
        var previous=RenderTexture.active;var image=new Texture2D(2048,768,TextureFormat.RGB24,false);
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,2048,768),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(image);}
    }
}
