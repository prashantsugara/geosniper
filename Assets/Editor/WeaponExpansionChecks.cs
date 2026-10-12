using System;
using System.IO;
using System.Reflection;
using GeoSniper;
using GeoSniper.Duel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
public static class WeaponExpansionChecks
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run()
    {
        var scene=EditorSceneManager.NewPreviewScene();
        var ballistic=ScriptableObject.CreateInstance<BallisticsConfig>();var config=ScriptableObject.CreateInstance<WeaponConfig>();
        try
        {
            var camera=Create(scene,"Weapon card camera").AddComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.backgroundColor=new Color(.035f,.055f,.08f);camera.clearFlags=CameraClearFlags.SolidColor;
            var light=Create(scene,"Key light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.6f;light.transform.rotation=Quaternion.Euler(30,-35,0);
            var avatarRoot=Create(scene,"Expanded rival");var avatar=avatarRoot.AddComponent<DuelOpponentAvatar>();avatar.Initialize("TEST",new Vector3(100,0,0),Quaternion.identity);
            Directory.CreateDirectory("Logs");var report=new System.Text.StringBuilder();
            for(int i=0;i<WeaponCatalog.Count;i++)
            {
                avatar.EquipWeapon(i);
                Check(avatarRoot.transform.Find("Duel rival sniper rifle")!=null,"Opponent model missing "+i);
                var gun=WeaponCatalog.Get(i);Check(gun.Damage>0&&gun.Cadence>0&&gun.Range>0,"Invalid weapon stats");
                Check(WeaponProgression.Key(i,WeaponUpgrade.Damage).EndsWith("_"+i),"Upgrade IDs collide");
                WeaponProgression.Apply(i,ballistic,config,true);
                Check(ballistic.baseDamage==DuelShotValidator.DamageFor(1,i)&&config.magazineSize==gun.Magazine&&Mathf.Abs(ballistic.maxRange-DuelShotValidator.RangeFor(i))<.01f,"Host and client base stats differ: "+gun.Name);
                var validator=new DuelShotValidator();validator.ResetRound(10);
                var pose=new DuelHistoricalPose(0,Vector3.zero,0,0,false,true);
                Check(validator.TryAccept(10,1,i,new Vector3(0,1.65f,0),Vector3.forward,pose,0,out var shot,out _),"Host rejects gun "+i);
                Check(validator.GetRemainingAmmo(i,0)==gun.Magazine-1,"Host did not consume ammo "+i);
                Check(!validator.TryAccept(10,2,i,new Vector3(0,1.65f,0),Vector3.forward,pose,.001,out _,out _),"Host permits rapid duplicate fire "+i);
                Check(validator.TryBeginReload(10,i,2),"Host reload denied "+i);
                Check(validator.GetRemainingAmmo(i,2+gun.Reload+.01)==gun.Magazine,"Host reload wrong "+i);
                var far=new DuelHistoricalPose(0,new Vector3(0,0,gun.Range+20),0,0,false,true);
                Check(!DuelShotValidator.TryIntersect(shot,far,out _),"Gun hits beyond own range "+i);
                var prefab=Resources.Load<GameObject>("Models/Weapons/"+gun.Model);Check(prefab!=null,"Missing model "+gun.Model);
                var holder=Create(scene,"Weapon "+gun.Name);var model=Object.Instantiate(prefab,holder.transform,false);
                Check(WeaponGeometry.Configure(model.transform,holder.transform,i),"Model calibration failed "+gun.Name);
                if(i>=3)
                {
                    foreach(var renderer in model.GetComponentsInChildren<Renderer>())
                        foreach(var material in renderer.sharedMaterials)
                        {
                            if(i==3 && (material.name.Contains("M40CG_Glass") || material.name.Contains("M40CG_Glossy"))) continue;
                            Check(material!=null && material.mainTexture!=null && material.GetTexture("_BumpMap")!=null && material.GetTexture("_MetallicGlossMap")!=null,"Authored PBR textures missing: "+gun.Name+" "+renderer.name);
                        }
                }
                var bounds=ImportedVisual.LocalBounds(holder.transform);
                Check(Mathf.Abs(bounds.size.z-gun.Length)<.02f,"Weapon length wrong "+gun.Name);
                Check(bounds.size.y<gun.Length*1.1f&&bounds.size.x<gun.Length*.8f,"Weapon axis wrong "+gun.Name+" "+bounds.size);
                camera.orthographicSize=Mathf.Max(bounds.size.y*.75f,bounds.size.z*.36f);camera.transform.position=bounds.center+new Vector3(3,.15f,.15f);camera.transform.LookAt(bounds.center);
                string source=Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).Parent.FullName;
                Capture(camera,Path.Combine(source,"Assets/Resources/Textures/"+gun.Model+(i==0?"ModelCard":"Card")+".png"));
                Capture(camera,"Logs/"+gun.Model+"-model.png");
                if(i>=3)
                {
                    holder.SetActive(false);
                    var presentation=Create(scene,"FPS "+gun.Name).AddComponent<SniperPresentation>();
                    typeof(SniperPresentation).GetField("cam",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(presentation,camera);
                    camera.orthographic=false;camera.fieldOfView=65;camera.transform.position=Vector3.zero;camera.transform.rotation=Quaternion.identity;
                    presentation.EquipWeaponModel(i);Capture(camera,"Logs/"+gun.Model+"-first-person.png");
                    var rifle=(Transform)typeof(SniperPresentation).GetField("rifle",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(presentation);
                    Check(rifle!=null&&rifle.localPosition.z>.3f,"FPS gun missing "+gun.Name);
                    Check(presentation.UsesOptic==(i<5),"Wrong sight type "+gun.Name);
                    if(i>=5)
                    {
                        presentation.Config=config;
                        typeof(SniperPresentation).GetField("currentFOV",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(presentation,config.zoomLevels[0]);
                        typeof(SniperPresentation).GetField("scopeBlend",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(presentation,1f);
                        presentation.Animate(true,0,false,false,false,0);
                        Check(rifle.gameObject.activeSelf,"Iron sight ADS hides gun "+gun.Name);
                        Check(Mathf.Abs(rifle.localPosition.x)<.03f,"Iron sights are not centered "+gun.Name);
                        Capture(camera,"Logs/"+gun.Model+"-iron-sights.png");
                    }
                    Object.DestroyImmediate(rifle.gameObject);Object.DestroyImmediate(presentation.gameObject);camera.orthographic=true;
                }
                report.AppendLine("PASS: "+gun.Name+" model, independent upgrades, host ammo/cadence/reload/range");Object.DestroyImmediate(holder);
            }
            using(var reader=new BinaryReader(new MemoryStream(DuelPacketCodec.SerializeResumeClock(10,30,1,5,20,30,7,.4f,.3f,new[]{2,3,4,5,6}))))
            {reader.ReadByte();reader.ReadByte();reader.ReadInt32();reader.ReadSingle();reader.ReadSingle();for(int n=0;n<4;n++)reader.ReadByte();reader.ReadSingle();reader.ReadSingle();for(int i=0;i<5;i++)Check(reader.ReadByte()==i+2,"Expanded resume loses ammo");}
            var target=Create(scene,"Headshot balance target");var health=target.AddComponent<CombatActor>();target.AddComponent<EnemyBot>();var hitbox=target.AddComponent<BoxCollider>();health.Initialize(100);
            DamageSystem.ProcessHit(hitbox,22,Vector3.up*1.65f,2,.6f,null,false);
            Check(!health.IsDead&&health.Health==56,"Automatic headshots still execute full-health enemies");
            health.Initialize(100);DamageSystem.ProcessHit(hitbox,90,Vector3.up*1.65f,4,.6f,null,true);Check(health.IsDead,"Precision headshot execution regressed");
            report.AppendLine("PASS: precision versus automatic headshot pipeline");
            CheckUnlockAndDock();
            report.AppendLine("PASS: new weapon purchase, independent upgrade and mobile dock cycling");
            File.WriteAllText("Logs/WeaponExpansionChecks.txt",report.ToString());
        }
        finally{Object.DestroyImmediate(ballistic);Object.DestroyImmediate(config);EditorSceneManager.ClosePreviewScene(scene);}
    }
    static void CheckUnlockAndDock()
    {
        var keys=new System.Collections.Generic.List<string>{"GeoSniper.DebugMode","GeoSniper.Credits","GeoSniper.Gold","GeoSniper.SelectedWeapon","GeoSniper.XP","GeoSniper.PlayerRank","GeoSniper.RankUpFrom","GeoSniper.RankUpTo",WeaponProgression.Key(7,WeaponUpgrade.Stability),WeaponProgression.Key(1,WeaponUpgrade.Damage)};
        for(int i=0;i<WeaponCatalog.Count;i++)keys.Add("GeoSniper.WeaponUnlocked_"+i);
        var backup=new System.Collections.Generic.Dictionary<string,int>();var existed=new System.Collections.Generic.HashSet<string>();
        foreach(string key in keys){if(PlayerPrefs.HasKey(key))existed.Add(key);backup[key]=PlayerPrefs.GetInt(key);}
        var root=new GameObject("Dock purchase fixture");root.SetActive(false);
        try
        {
            PlayerPrefs.SetInt("GeoSniper.DebugMode",0);for(int i=0;i<WeaponCatalog.Count;i++)PlayerPrefs.SetInt("GeoSniper.WeaponUnlocked_"+i,0);
            PlayerPrefs.SetInt("GeoSniper.XP",0);PlayerPrefs.SetInt("GeoSniper.PlayerRank",1);PlayerPrefs.DeleteKey("GeoSniper.RankUpFrom");PlayerPrefs.DeleteKey("GeoSniper.RankUpTo");
            PlayerPrefs.SetInt("GeoSniper.SelectedWeapon",0);WeaponCatalog.EnsureStarterLoadout();
            int owned=0;for(int i=0;i<WeaponCatalog.Count;i++)if(WeaponCatalog.Owned(i))owned++;
            Check(owned==2&&WeaponCatalog.Owned(1)&&WeaponCatalog.Owned(7)&&WeaponCatalog.SelectedOwnedWeapon==1,"Fresh account must receive exactly primary and pistol");
            PlayerPrefs.SetInt("GeoSniper.SelectedWeapon",7);
            Check(WeaponCatalog.StartingSniper==1,"Pistol selection must still start a sniper mission with M24");
            PlayerPrefs.SetInt("GeoSniper.SelectedWeapon",1);
            for(int i=0;i<WeaponCatalog.Count-1;i++)if(i!=1)Check(WeaponCatalog.Get(1).Damage<WeaponCatalog.Get(i).Damage,"Starter primary is not weakest");
            var input=root.AddComponent<MobileCombatInput>();input.SelectedWeaponIndex=1;
            var dock=typeof(MobileCombatInput).GetMethod("DockWeapon",BindingFlags.Instance|BindingFlags.NonPublic);
            Check((int)dock.Invoke(input,new object[]{0})==1&&(int)dock.Invoke(input,new object[]{1})==7&&!input.CanSelectWeaponSlot(2),"Starter dock repeats pistol or exposes locked guns");
            PlayerPrefs.SetInt("GeoSniper.DebugMode",1);Check(!WeaponCatalog.Owned(5),"Development build bypasses weapon purchase");
            PlayerPrefs.SetInt("GeoSniper.Credits",4500);
            Check(!WeaponCatalog.TryUnlock(5)&&PlayerPrefs.GetInt("GeoSniper.Credits")==4500,"Money or debug mode bypasses player-rank gate");
            PlayerProgression.AddXP(1999);
            Check(PlayerProgression.Level==2 && WeaponCatalog.RankEligible(6) && !WeaponCatalog.RankEligible(5),"Rank boundary or MP5 unlock incorrect");
            PlayerProgression.AddXP(1);
            Check(PlayerProgression.Level==3 && WeaponCatalog.RankEligible(5) && !WeaponCatalog.Owned(5),"Leveling must enable purchase without granting gun for free");
            Check(PlayerPrefs.GetInt("GeoSniper.RankUpFrom")==1 && PlayerPrefs.GetInt("GeoSniper.RankUpTo")==3,"Multiple rank-ups lose unlock notification");
            PlayerPrefs.SetInt("GeoSniper.Credits",4499);Check(!WeaponCatalog.TryUnlock(5)&&PlayerPrefs.GetInt("GeoSniper.Credits")==4499,"Insufficient unlock consumed credits");
            PlayerPrefs.SetInt("GeoSniper.Credits",4500);Check(WeaponCatalog.TryUnlock(5)&&PlayerPrefs.GetInt("GeoSniper.Credits")==0,"New weapon unlock failed");
            Check(!WeaponCatalog.TryUnlock(5),"Owned weapon bought twice");
            PlayerPrefs.SetInt("GeoSniper.SelectedWeapon",5);WeaponCatalog.EnsureStarterLoadout();Check(WeaponCatalog.Owned(5)&&WeaponCatalog.SelectedOwnedWeapon==5,"Starter initialization erases purchased loadout");
            Check(WeaponCatalog.StartingSniper==1,"Automatic selection must start with an owned sniper");
            PlayerPrefs.SetInt("GeoSniper.WeaponUnlocked_3",1);PlayerPrefs.SetInt("GeoSniper.SelectedWeapon",3);
            Check(WeaponCatalog.StartingSniper==3,"Purchased sniper must remain the starting weapon");
            PlayerPrefs.SetInt("GeoSniper.SelectedWeapon",5);
            PlayerPrefs.SetInt("GeoSniper.XP",0);
            Check(PlayerProgression.Level==3,"XP penalty removes earned player rank");
            PlayerPrefs.SetInt("GeoSniper.PlayerRank",1);
            Check(WeaponCatalog.RankEligible(5)&&WeaponCatalog.SelectedOwnedWeapon==5,"Existing owned gun lost access below new rank gate");
            Check(WeaponProgression.Damage(1,0)==20&&WeaponProgression.Damage(1,6)>=100,"Starter damage upgrades do not progress to a one-shot torso kill");
            PlayerPrefs.SetInt("GeoSniper.WeaponUnlocked_5",0);
            PlayerPrefs.SetInt("GeoSniper.Gold",25);PlayerPrefs.SetInt(WeaponProgression.Key(7,WeaponUpgrade.Stability),0);
            int previous=WeaponProgression.Tier(2,WeaponUpgrade.Stability);
            Check(WeaponProgression.TryUpgrade(7,WeaponUpgrade.Stability)&&PlayerPrefs.GetInt("GeoSniper.Gold")==0&&WeaponProgression.Tier(7,WeaponUpgrade.Stability)==1&&WeaponProgression.Tier(2,WeaponUpgrade.Stability)==previous,"New upgrades reuse old IDs or currency");
            PlayerPrefs.SetInt("GeoSniper.WeaponUnlocked_1",1);PlayerPrefs.SetInt("GeoSniper.WeaponUnlocked_3",1);
            input.SelectedWeaponIndex=7;
            Check((int)dock.Invoke(input,new object[]{0})==3&&(int)dock.Invoke(input,new object[]{1})==7&&(int)dock.Invoke(input,new object[]{2})==1,"Expanded dock cannot reach new weapons");
            PlayerProgression.AddXP(int.MaxValue);
            Check(PlayerProgression.Level==50 && PlayerProgression.Progress==1,"Player rank cap incorrect");
            PlayerProgression.AddXP(1000);Check(PlayerProgression.XP==int.MaxValue,"XP overflow wrapped progression");
        }
        finally{foreach(string key in keys){if(existed.Contains(key))PlayerPrefs.SetInt(key,backup[key]);else PlayerPrefs.DeleteKey(key);}PlayerPrefs.Save();Object.DestroyImmediate(root);}
    }
    static GameObject Create(Scene scene,string name){var obj=new GameObject(name);SceneManager.MoveGameObjectToScene(obj,scene);return obj;}
    static void Capture(Camera camera,string path)
    {
        var target=RenderTexture.GetTemporary(512,288,24);var previous=RenderTexture.active;var old=camera.targetTexture;
        var texture=new Texture2D(512,288,TextureFormat.RGB24,false);
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,512,288),0,0);texture.Apply();Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));File.WriteAllBytes(path,texture.EncodeToPNG());}
        finally{camera.targetTexture=old;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(texture);}
    }
}
