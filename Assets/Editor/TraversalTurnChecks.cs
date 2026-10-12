using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using GeoSniper;
public static partial class TraversalTurnChecks
{
    public static IEnumerator Windows(UrbanPlayer player,Action<string> report)
    {
        foreach(var bot in UnityEngine.Object.FindObjectsByType<EnemyBot>(FindObjectsSortMode.None))bot.Suspended=true;
        player.Health.Invulnerable=true;
        foreach(var building in EnterableBuilding.All)
            foreach(var window in building.OpenWindows)
            {
                var center=building.transform.TransformPoint(window.Center);var normal=building.transform.TransformDirection(window.Outward).normalized;
                if(!Physics.Raycast(center+normal*.75f+Vector3.up*1.5f,Vector3.down,out var floor,2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))continue;
                if(Mathf.Abs(floor.point.y-center.y)>.3f)continue;
                player.Place(floor.point+Vector3.up*.12f);
                if(!player.TryWindowVault(-normal))continue;
                float until=Time.time+1;while(Time.time<until)yield return null;
                if(Vector3.Dot(player.transform.position-center,normal)>-.5f)throw new Exception("Window entry did not cross the wall");
                player.Place(new Vector3(center.x,player.transform.position.y,center.z)-normal*.75f);
                if(!player.TryWindowVault(normal))throw new Exception("Window exit was rejected after entry");
                until=Time.time+1;while(Time.time<until)yield return null;
                if(Vector3.Dot(player.transform.position-center,normal)<.5f)throw new Exception("Window exit did not cross the wall");
                var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    blocker.transform.position=center+Vector3.up*1.5f;blocker.transform.localScale=new Vector3(2,2,.8f);Physics.SyncTransforms();
                    player.Place(floor.point+Vector3.up*.12f);
                    if(player.TryWindowVault(-normal))throw new Exception("Vault accepted an obstructed opening");
                }
                finally{UnityEngine.Object.Destroy(blocker);}
                report("PASS: player vaults in and out through a generated window; obstructed opening is rejected");yield break;
            }
        throw new Exception("No generated window accepted a supported vault");
    }
    public static void Weapons(Action<string> report)
    {
        int count=0;const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        foreach(var anchor in UnityEngine.Object.FindObjectsByType<EnemyWeaponAnchor>(FindObjectsSortMode.None))
        {
            if(anchor.bot==null || anchor.rightHand==null)continue;
            var bot=anchor.bot;bot.currentState=AIState.Combat;
            Renderer stock=null,barrel=null;
            foreach(var renderer in anchor.GetComponentsInChildren<Renderer>())
            {if(renderer.name.StartsWith("Stock",StringComparison.OrdinalIgnoreCase))stock=renderer;if(renderer.name.StartsWith("Barrel",StringComparison.OrdinalIgnoreCase))barrel=renderer;}
            if(stock!=null && barrel!=null && Vector3.Dot((barrel.bounds.center-stock.bounds.center).normalized,anchor.transform.forward)<.85f)throw new Exception("Imported rifle barrel does not follow weapon aim axis");
            var rest=bot.transform.rotation;
            foreach(float yaw in new[]{0f,90f,180f,270f,359f,1f})
            {
                bot.transform.rotation=Quaternion.Euler(0,yaw,0);
                foreach(var animation in anchor.visual.GetComponentsInChildren<Animation>())animation.Sample();
                typeof(EnemyWeaponAnchor).GetMethod("UpdatePose",flags).Invoke(anchor,new object[]{false});
                Vector3 aim=bot.transform.InverseTransformDirection(anchor.transform.rotation*Quaternion.Euler(-anchor.localEulerOffset)*Vector3.forward);
                if(Mathf.Abs(Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg)>35.1f)throw new Exception("Rifle twists behind the torso while turning");
                float gripError=Vector3.Distance(anchor.rightHand.TransformPoint(WeaponGripPose.PalmLocal(anchor.rightHand)),anchor.transform.position);
                if(gripError>.12f || !float.IsFinite(gripError))throw new Exception("Right hand loses rifle grip during turn: "+gripError);
                var support=anchor.transform.Find("WeaponSupport");
                if(anchor.leftHand!=null && support!=null && Vector3.Distance(anchor.leftHand.TransformPoint(WeaponGripPose.PalmLocal(anchor.leftHand)),support.position)>.18f)throw new Exception("Support hand loses fore-end during turn");
                if(yaw==180f)Capture(anchor,count);
            }
            bot.transform.rotation=rest;count++;
        }
        if(count==0)throw new Exception("No armed enemies checked");
        report("PASS: "+count+" enemy rifles retain right-hand grip and bounded torso aim through wraparound and half turns");
    }
    static void Capture(EnemyWeaponAnchor anchor,int index)
    {
        var camera=new GameObject("Turning pose preview").AddComponent<Camera>();
        var rt=new RenderTexture(900,700,24);var image=new Texture2D(900,700,TextureFormat.RGB24,false);var previous=RenderTexture.active;
        try
        {
            var root=anchor.bot.transform;camera.transform.position=root.position+root.TransformDirection(new Vector3(3f,1.5f,-1.2f));camera.transform.LookAt(root.position+Vector3.up*1.1f);camera.fieldOfView=35;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,900,700),0,0);image.Apply();System.IO.Directory.CreateDirectory("Logs");System.IO.File.WriteAllBytes("Logs/EnemyTurn-"+index+".png",image.EncodeToPNG());
        }
        finally{camera.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(camera.gameObject);}
    }
}

