using System;
using System.IO;
using System.Reflection;
using System.Text;
using GeoSniper;
using GeoSniper.Duel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class DuelVisualChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly StringBuilder report = new StringBuilder();

    public static void RunAll()
    {
        Run();
        DuelGameplayChecks.Run();
        Debug.Log("PASS: duel visual and gameplay checks");
    }

    [MenuItem("Geo Sniper/Validate Duel Visuals")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Run in Edit mode.");
        report.Clear();
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var camera = Create(scene, "Duel visual camera").AddComponent<Camera>();
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.22f, .29f, .36f);
            camera.fieldOfView = 65;
            camera.nearClipPlane = .05f;
            var light = Create(scene, "Duel visual light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.transform.rotation = Quaternion.Euler(35, -35, 0);

            string[] weapons = { "Barrett50", "M24Tactical", "MK12SPR" };
            for (int i = 0; i < weapons.Length; i++)
            {
                var holder = Create(scene, "Weapon diagnostic");
                var model = Object.Instantiate(Resources.Load<GameObject>("Models/Weapons/" + weapons[i]), holder.transform, false);
                Require(WeaponGeometry.Configure(model.transform, holder.transform, i), "Invalid weapon " + weapons[i]);
                var dimensions = ImportedVisual.LocalBounds(holder.transform).size;
                Require(Mathf.Abs(dimensions.z - WeaponGeometry.Length(i)) < .02f && dimensions.z > dimensions.y * 2 && dimensions.z > dimensions.x * 2,
                    "Weapon scale or longitudinal axis changed: " + weapons[i]);
                Object.DestroyImmediate(holder);

                var presentation = Create(scene, "First person check").AddComponent<SniperPresentation>();
                typeof(SniperPresentation).GetField("cam", Private).SetValue(presentation, camera);
                presentation.EquipWeaponModel(i);
                var rifle = (Transform)typeof(SniperPresentation).GetField("rifle", Private).GetValue(presentation);
                Require(rifle.localPosition.z > .3f && rifle.localPosition.y < -.1f, "Weapon starts inside the camera: " + weapons[i]);
                report.AppendLine("FPS " + weapons[i] + " " + ImportedVisual.LocalBounds(rifle));
                Capture(camera, weapons[i] + "-first-person");
                report.AppendLine("PASS: " + weapons[i] + " metre-scale geometry and initial first-person pose");
                Object.DestroyImmediate(rifle.gameObject);
                Object.DestroyImmediate(presentation.gameObject);
            }

            var rival = Create(scene, "Remote duel player").AddComponent<DuelOpponentAvatar>();
            rival.Initialize("VISUAL CHECK", Vector3.zero, Quaternion.identity);
            var visual = (GameObject)typeof(DuelOpponentAvatar).GetField("visualRoot", Private).GetValue(rival);
            var bounds = ImportedVisual.PosedBounds(visual.transform);
            Require(bounds.size.y > 1.7f && bounds.size.y < 2f, "Remote character height: " + bounds);
            Require(Mathf.Abs(bounds.min.y) < .05f, "Remote character feet are not grounded: " + bounds);
            foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                foreach (var material in renderer.sharedMaterials)
                    Require(material != null && material.mainTexture != null, "Remote character is missing a texture: " + renderer.name);
            var remoteRifle = (GameObject)typeof(DuelOpponentAvatar).GetField("rifleObject", Private).GetValue(rival);
            var gunBounds = ImportedVisual.LocalBounds(remoteRifle.transform);
            Require(Mathf.Abs(gunBounds.size.z - WeaponGeometry.Length(1)) < .02f, "Remote rifle size: " + gunBounds);
            foreach (var renderer in remoteRifle.GetComponentsInChildren<Renderer>())
            {
                Require(!(renderer is SkinnedMeshRenderer), "Remote rifle includes FPS arms");
                Require(!renderer.name.ToLowerInvariant().Contains("sphere"), "Remote rifle includes helper sphere");
            }
            Require(rival.GetComponentInChildren<EnemyBot>() == null, "Remote player has AI");
            camera.transform.position = new Vector3(0, 1.25f, 4);
            camera.transform.LookAt(new Vector3(0, 1.05f, 0));
            Capture(camera, "remote-player");
            report.AppendLine("PASS: remote player height, grounded feet, textures, rifle dimensions and no AI/helpers");
            rival.ApplyNetworkState(Vector3.zero, 0, 0, false, true, 100);
            typeof(DuelOpponentAvatar).GetMethod("Update", Private).Invoke(rival, null);
            Require((string)typeof(DuelOpponentAvatar).GetField("currentAnimation", Private).GetValue(rival) == "crouch_idle",
                "Network crouch did not select authored stance");
            var characterAnimation = (Animation)typeof(DuelOpponentAvatar).GetField("characterAnimation", Private).GetValue(rival);
            characterAnimation.Stop(); characterAnimation.Play("crouch_idle"); characterAnimation["crouch_idle"].time = .25f; characterAnimation.Sample();
            typeof(DuelOpponentAvatar).GetMethod("LateUpdate", Private).Invoke(rival, null);
            var crouched = ImportedVisual.PosedBounds(visual.transform);
            Require(crouched.size.y < bounds.size.y * .85f, "Network crouch does not lower visible character: " + crouched);
            Capture(camera, "remote-crouched");
            rival.PlayRemoteFire(Vector3.zero,Vector3.forward,1);
            Require(characterAnimation["fire_upper"]!=null && characterAnimation["fire_upper"].layer==2
                && (string)typeof(DuelOpponentAvatar).GetField("currentAnimation",Private).GetValue(rival)=="crouch_idle",
                "Firing loses crouched locomotion stance");
            rival.TakeLocalDamage(1,20,rival.transform.position);
            Require((string)typeof(DuelOpponentAvatar).GetField("actionAnimation",Private).GetValue(rival)=="hit_upper","Nonlethal damage fails to animate hit reaction");
            report.AppendLine("PASS: remote fire and hit reactions preserve crouched stance");

            rival.Die();
            Require(!remoteRifle.activeSelf, "Dead remote player still holds rifle");
            rival.Respawn(Vector3.zero, Quaternion.identity);
            var standing = ImportedVisual.PosedBounds(visual.transform);
            Require(remoteRifle.activeSelf && Mathf.Abs(standing.size.y - bounds.size.y) < .05f && Mathf.Abs(standing.min.y) < .05f,
                "Respawn lost standing calibration: " + standing);
            report.AppendLine("PASS: network crouch, death and calibrated respawn");
            camera.transform.SetPositionAndRotation(new Vector3(0, 1.55f, -7), Quaternion.identity);
            rival.transform.rotation = Quaternion.Euler(0, 180, 0);
            typeof(DuelOpponentAvatar).GetMethod("LateUpdate", Private).Invoke(rival, null);
            var localPresentation = Create(scene, "Duel camera weapon").AddComponent<SniperPresentation>();
            typeof(SniperPresentation).GetField("cam", Private).SetValue(localPresentation, camera);
            localPresentation.EquipWeaponModel(0);
            Capture(camera, "combined-view");
            var localRifle = (Transform)typeof(SniperPresentation).GetField("rifle", Private).GetValue(localPresentation);
            localRifle.gameObject.SetActive(false);
            rival.Respawn(Vector3.zero, Quaternion.identity);
            camera.transform.position = new Vector3(0, 1.1f, 216);
            camera.transform.LookAt(new Vector3(0, 1.1f, 0));
            camera.fieldOfView = 65f / 8.5f;
            camera.backgroundColor = new Color(.72f, .80f, .88f);
            var post = camera.gameObject.AddComponent<MobilePostProcess>();
            post.scopeBlend = 1f;
            Require(Shader.Find("GeoSniper/MobilePostProcess") != null, "Scope bloom shader missing from visual fixture");
            rival.ApplyNetworkState(Vector3.zero, 0, 0, true, false, 100);
            var updateGlint = typeof(DuelOpponentAvatar).GetMethod("UpdateOpticGlint", Private);
            updateGlint.Invoke(rival, new object[] { camera });
            var glint = (LineRenderer)typeof(DuelOpponentAvatar).GetField("opticGlint", Private).GetValue(rival);
            Require(glint.enabled && rival.IsAimingAtPlayer && glint.startWidth < .08f
                && Vector3.Distance(glint.GetPosition(0), glint.GetPosition(1)) < .08f,
                "Aiming glint obscures remote player");
            Capture(camera, "scoped-216m-aiming");
            rival.ApplyNetworkState(Vector3.zero, 0, 0, false, false, 100);
            updateGlint.Invoke(rival, new object[] { camera });
            Require(!glint.enabled && !rival.IsAimingAtPlayer, "Glint remains after opponent stops aiming");
            Capture(camera, "scoped-216m-idle");
            report.AppendLine("PASS: small aiming glint and clear scoped character at 216m with bloom");
            var thermal=ThermalVisionOverlay.Ensure(camera);
            thermal.SetScopeState(false,0,null);
            int baselineWarm=CountWarm(Capture(camera,"thermal-off"));
            thermal.SetScopeState(true,1,rival);
            int visibleWarm=CountWarm(Capture(camera,"thermal-visible"));
            Require(visibleWarm>baselineWarm+50,"Actual rival thermal silhouette did not appear: "+baselineWarm+" / "+visibleWarm);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(wall,scene);
            wall.name="Thermal occlusion wall";wall.transform.position=new Vector3(0,1,100);wall.transform.localScale=new Vector3(20,20,2);
            thermal.SetScopeState(false,0,null);
            int wallBaseline=CountWarm(Capture(camera,"thermal-wall-off"));
            thermal.SetScopeState(true,1,rival);
            int wallWarm=CountWarm(Capture(camera,"thermal-wall-on"));
            Require(wallWarm<=wallBaseline+10,"Thermal silhouette leaks through opaque cover: "+wallBaseline+" / "+wallWarm);
            Object.DestroyImmediate(wall);
            rival.transform.position=new Vector3(20,0,0);
            thermal.SetScopeState(false,0,null);
            int outsideBaseline=CountWarm(Capture(camera,"thermal-outside-off"));
            thermal.SetScopeState(true,1,rival);
            int outsideWarm=CountWarm(Capture(camera,"thermal-outside-on"));
            Require(outsideWarm<=outsideBaseline+10,"Thermal silhouette leaks outside circular optic: "+outsideBaseline+" / "+outsideWarm);
            thermal.SetScopeState(false,0,null);
            Require(camera.GetCommandBuffers(UnityEngine.Rendering.CameraEvent.AfterForwardOpaque).Length==0,"FLIR remains attached after switching off");
            report.AppendLine("PASS: real rival FLIR silhouette, wall occlusion, circular lens clipping and cleanup");

        }
        catch (Exception error)
        {
            report.AppendLine("FAIL: " + error);
            throw;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/DuelVisualChecks.txt", report.ToString());
            Debug.Log(report.ToString());
        }
    }

    static GameObject Create(Scene scene, string name)
    {
        var result = new GameObject(name);
        SceneManager.MoveGameObjectToScene(result, scene);
        return result;
    }

    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

    static int CountWarm(Color32[] pixels)
    {
        int count=0;
        foreach(var color in pixels) if(color.r>100 && color.r>color.g+30 && color.g>color.b+20)count++;
        return count;
    }

    static Color32[] Capture(Camera camera, string name)
    {
        var target = new RenderTexture(1280, 720, 24);
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/duel-" + name + ".png", image.EncodeToPNG());
            return image.GetPixels32();
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }
    }
}
