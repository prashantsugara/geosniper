using System;
using System.IO;
using System.Linq;
using System.Text;
using GeoSniper;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class CombatHitChecks
{
//  [InitializeOnLoadMethod]
    static void QueueDiagnostic()
    {
        if(SessionState.GetBool("GeoSniper.HitDiagnostic.5",false)) return;
        SessionState.SetBool("GeoSniper.HitDiagnostic.5",true);
        EditorApplication.delayCall+=()=> { if(!EditorApplication.isPlayingOrWillChangePlaymode) Run(); };
    }
    [MenuItem("Geo Sniper/Validate Sniper Hits")]
    public static void Run()
    {
        var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        var report=new StringBuilder();
        try
        {
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            var player=new GameObject("Test player");
            player.transform.position=new Vector3(10000,0,9980);
            var root=new GameObject("Test enemy"); root.transform.position=new Vector3(10000,0,10000);
            var asset=Resources.Load<GameObject>("Models/Enemies/army_character_1");
            if(asset==null) throw new Exception("Missing contractor asset");
            var visual=ImportedVisual.CreateEnemy(asset,root.transform);
            var enemy=root.AddComponent<EnemyBot>(); enemy.Initialize(player.transform,0);
            var volumes=root.AddComponent<EnemyHitboxes>(); volumes.Initialize(visual.transform);
            foreach(float height in new[]{1.1f,1.55f,1.8f})
            {
                volumes.Refresh(); Physics.SyncTransforms();
                foreach(var collider in root.GetComponentsInChildren<Collider>())
                    report.AppendLine(collider.name+" enabled="+collider.enabled+" layer="+collider.gameObject.layer+" bounds="+collider.bounds+" root="+root.transform.position);
                var ray=new Ray(player.transform.position+Vector3.up*height,Vector3.forward);
                report.AppendLine("Ray "+ray);
                var hits=Physics.RaycastAll(ray,50,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Collide);
                volumes.Disable();
                foreach(var hit in hits.OrderBy(h=>h.distance))
                    report.AppendLine(height+"m: "+hit.collider.name+" owner="+(hit.collider.GetComponentInParent<EnemyBot>()==enemy)+" distance="+hit.distance);
                if(!hits.Any(h=>h.collider.GetComponentInParent<EnemyBot>()==enemy)) throw new Exception("No enemy hit at height "+height);
            }
            report.AppendLine("PASS: Imported enemy central hit rays");
            var chest=new Ray(player.transform.position+Vector3.up*1.1f,Vector3.forward);
            var bots=new[]{enemy};
            bool IsEnemy(Collider c)=>c!=null && c.GetComponentInParent<EnemyBot>()==enemy;
            var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.position=player.transform.position+new Vector3(0,1.1f,10);
            if(SniperHitQuery.Cast(chest,bots,player.transform,null)!=obstacle.GetComponent<Collider>()) throw new Exception("Shot went through solid cover");
            report.AppendLine("PASS: Solid wall stops shot");
            obstacle.GetComponent<Collider>().isTrigger=true;
            if(!IsEnemy(SniperHitQuery.Cast(chest,bots,player.transform,null))) throw new Exception("Unrelated trigger blocked shot");
            report.AppendLine("PASS: Unrelated trigger ignored");
            obstacle.GetComponent<Collider>().isTrigger=false;
            obstacle.transform.SetParent(player.transform,true);
            if(!IsEnemy(SniperHitQuery.Cast(chest,bots,player.transform,null))) throw new Exception("Own weapon blocked shot");
            report.AppendLine("PASS: Own weapon ignored");
            CheckVisibleSkin(report);
            CheckRooftopShots(report);
            enemy.Damage(100);
            if(!enemy.GetComponent<CombatActor>().IsDead) throw new Exception("Sniper damage not lethal");
            report.AppendLine("PASS: Confirmed sniper damage kills enemy");
            if(root.GetComponentsInChildren<Collider>().Any(c=>c.isTrigger && c.enabled)) throw new Exception("Shot volumes left in movement physics");
            report.AppendLine("PASS: Temporary hitboxes disabled after query");
        }
        catch(Exception error) { report.AppendLine("FAIL: "+error); Debug.LogException(error); throw; }
        finally
        {
            EditorSceneManager.CloseScene(scene,true);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/CombatHitChecks.txt",report.ToString());
        }
    }
    static void CheckRooftopShots(StringBuilder report)
    {
        var root=new GameObject("Rooftop hit regression");
        var shooter=new GameObject("Street shooter");
        var roof=GameObject.CreatePrimitive(PrimitiveType.Cube);
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            root.transform.position=new Vector3(12000,12,12000);
            shooter.transform.position=new Vector3(12000,0,11960);
            var enemy=root.AddComponent<EnemyBot>();enemy.Initialize(shooter.transform,0);
            root.AddComponent<EnemyHitboxes>().Initialize(root.transform);
            var bots=new[]{enemy};
            roof.transform.position=new Vector3(12000,6,12004);roof.transform.localScale=new Vector3(12,12,8);
            wall.SetActive(false);
            bool IsEnemy(Collider c)=>c!=null && c.GetComponentInParent<EnemyBot>()==enemy;
            foreach(float height in new[]{1.2f,1.8f})
            {
                var from=shooter.transform.position+Vector3.up*1.55f;
                var ray=new Ray(from,(root.transform.position+Vector3.up*height-from).normalized);
                if(!IsEnemy(SniperHitQuery.Cast(ray,bots,shooter.transform,null))) throw new Exception("Street-to-rooftop ray missed exposed enemy at "+height);
                if(SniperHitQuery.TryCast(ray,bots,shooter.transform,null,out _,20)) throw new Exception("Rooftop shot exceeded max range");
                wall.SetActive(true);wall.transform.localScale=Vector3.one*.5f;wall.transform.position=ray.GetPoint(1);
                if(SniperHitQuery.Cast(ray,bots,shooter.transform,null)!=wall.GetComponent<Collider>()) throw new Exception("Nearby street wall was ignored");
                float targetDistance=Vector3.Distance(from,root.transform.position+Vector3.up*height);
                wall.transform.position=ray.GetPoint(targetDistance-1.5f);
                if(SniperHitQuery.Cast(ray,bots,shooter.transform,null)!=wall.GetComponent<Collider>()) throw new Exception("Shot penetrated cover just in front of rooftop target");
                wall.SetActive(false);
            }
            var above=root.transform.position+new Vector3(0,18,-30);
            var down=new Ray(above,(root.transform.position+Vector3.up*1.6f-above).normalized);
            if(!IsEnemy(SniperHitQuery.Cast(down,bots,shooter.transform,null))) throw new Exception("Downward rooftop shot failed");
            if(root.GetComponentsInChildren<Collider>().Any(c=>c.isTrigger && c.enabled)) throw new Exception("Rooftop query leaked active shot volumes");
            report.AppendLine("PASS: elevated body/head, downward angle, near cover, roof-edge cover, range and cleanup");
        }
        finally {Object.DestroyImmediate(root);Object.DestroyImmediate(shooter);Object.DestroyImmediate(roof);Object.DestroyImmediate(wall);}
    }
    static void CheckVisibleSkin(StringBuilder report)
    {
        var root=new GameObject("Visible skin regression");
        var mesh=new Mesh();
        GameObject wall=null;
        try
        {
            root.transform.position=new Vector3(11000,0,11000);
            var visual=new GameObject("Skin"); visual.transform.SetParent(root.transform,false);
            var bone=new GameObject("Test bone"); bone.transform.SetParent(visual.transform,false);
            // Geometry deliberately outside the controller and fallback torso.
            mesh.vertices=new[]{new Vector3(.9f,.7f,0),new Vector3(1.3f,.7f,0),new Vector3(1.1f,1.4f,0)};
            mesh.triangles=new[]{0,1,2};
            mesh.boneWeights=Enumerable.Repeat(new BoneWeight {boneIndex0=0,weight0=1},3).ToArray();
            mesh.bindposes=new[]{Matrix4x4.identity}; mesh.RecalculateBounds();
            var skin=visual.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh=mesh; skin.bones=new[]{bone.transform}; skin.rootBone=bone.transform;
            skin.updateWhenOffscreen=true; skin.localBounds=new Bounds(new Vector3(1.1f,1,0),new Vector3(3,3,1));
            var enemy=root.AddComponent<EnemyBot>(); enemy.Initialize(null,0);
            root.transform.rotation=Quaternion.identity;
            var volumes=root.AddComponent<EnemyHitboxes>(); volumes.Initialize(visual.transform);
            var bots=new[]{enemy};
            var ray=new Ray(root.transform.position+new Vector3(1.1f,1,-20),Vector3.forward);
            if(!SniperHitQuery.TryCast(ray,bots,null,null,out var hit,50) || hit.Collider.GetComponentInParent<EnemyBot>()!=enemy)
                throw new Exception("Visible skin outside capsules reported a miss");
            if(Vector3.Distance(hit.Point,ray.GetPoint(20))>.01f) throw new Exception("Impact point differs from visible skin: point="+hit.Point+" expected="+ray.GetPoint(20)+" collider="+hit.Collider.name);
            if(SniperHitQuery.TryCast(ray,bots,null,null,out _,10)) throw new Exception("Hit beyond maximum range");
            bone.transform.localPosition=Vector3.right*.5f;
            ray.origin+=Vector3.right*.5f;
            if(!SniperHitQuery.TryCast(ray,bots,null,null,out hit,50)) throw new Exception("Hit did not follow posed skin");
            // Verify pose movement before damage reactions are allowed to rotate this fixture.
            float health=enemy.GetComponent<CombatActor>().Health;
            DamageSystem.ProcessHit(hit.Collider,10,hit.Point);
            if(enemy.GetComponent<CombatActor>().Health>=health) throw new Exception("Visible skin hit did not apply damage");
            wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position=ray.GetPoint(10);
            if(!SniperHitQuery.TryCast(ray,bots,null,null,out hit,50) || hit.Collider!=wall.GetComponent<Collider>())
                throw new Exception("Skin fallback shot through cover");
            if(root.GetComponentsInChildren<Collider>().Any(c=>c.isTrigger && c.enabled))
                throw new Exception("Visual query left hitboxes enabled");
            report.AppendLine("PASS: visible/posed skin, saved impact point, damage, range, cover and hitbox cleanup");
        }
        finally { if(wall!=null) Object.DestroyImmediate(wall); Object.DestroyImmediate(root); Object.DestroyImmediate(mesh); }
    }
}
