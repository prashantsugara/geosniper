using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    public sealed class SniperPresentation : MonoBehaviour
    {
        const string ImportedRiflePath="Models/GeoSniperRifleProvided";
        const string LegacyRiflePath="Models/GeoSniperRifle";
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        Transform rifle;
        Vector3 muzzleLocal;
        AudioSource speaker;
        AudioClip shot, bolt, hit, empty;
        Texture2D scope;
        float kick;
        public bool Muted { get; private set; }
        // Muzzle flash
        Light muzzleLight;
        GameObject muzzleFlash;
        float flashTimer;
        // Bullet tracer
        LineRenderer tracerLine;
        float tracerTimer;
        // Camera shake
        Camera cam;
        float shakeIntensity;
        Vector3 shakeOffset;
        Vector3 weaponViewPosition=new Vector3(.22f,-.24f,.62f);
        Quaternion weaponViewRotation=Quaternion.Euler(0,-5,0);
        
        
        // Phase 1 Additions
        WeaponSway sway;
        public WeaponConfig Config;
        
        float currentFOV;
        int currentZoomIndex = 0;
        
        float hitMarkerTimer;
        
        float killTimer;
        float targetDistance;
        float targetElevation;
        Vector3 lastMissImpactPos;
        float lastMissImpactTimer;
        float lastMissDistance;
        CombatActor currentTargetActor;
        string currentTargetClassification = "";
        float reloadDuration=1.4f;
        float scopeBlend;
        float breathPhase;
        float shotKick;
        Transform combatKnife;
        float knifeSlashTimer;
        bool knifeIsLethal;
        bool knifeHitEffectPlayed;
        float knifeBloodTimer;
        Transform leftArmBone;
        
        // Call of Duty Locomotion & Weapon Kinematics
        float tacticalSprintBlend;
        float crouchBlend;
        float stridePhase;
        Vector3 inertiaPos;
        Vector3 inertiaRot;
        Quaternion lastCamRot = Quaternion.identity;
        bool hasLastCamRot;
        
        public void ConfirmHit() { hitMarkerTimer = 0.2f; lastMissImpactTimer = 0f; HitSound(); }
        public void ConfirmKill() { killTimer = 2.0f; lastMissImpactTimer = 0f; HitSound(); }
        public void RecordMissImpact(Vector3 impactWorldPos)
        {
            lastMissImpactPos = impactWorldPos;
            lastMissImpactTimer = 8.5f;
            lastMissDistance = cam != null ? Vector3.Distance(cam.transform.position, impactWorldPos) : 0f;
            var speaker = cam != null ? cam.GetComponent<AudioSource>() : null;
            if (speaker != null) speaker.PlayOneShot(ProceduralAudio.CreateRicochetMiss(), 0.85f);
        }
        public void TriggerXRayHitCam(Transform target, Vector3 impactPos)
        {
            if (target == null) return;
            var renderers = target.GetComponentsInChildren<Renderer>();
            var originalMats = new System.Collections.Generic.Dictionary<Renderer, Material[]>();
            var xrayShader = Shader.Find("GeoSniper/XRayAnatomical");
            if (xrayShader != null)
            {
                var xrayMat = new Material(xrayShader);
                xrayMat.SetVector("_HitPoint", impactPos);
                xrayMat.SetFloat("_HitRadius", 0.55f);
                foreach (var r in renderers)
                {
                    if (r == null || r.gameObject.name.Contains("XRay")) continue;
                    originalMats[r] = r.sharedMaterials;
                    var newMats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < newMats.Length; i++) newMats[i] = xrayMat;
                    r.sharedMaterials = newMats;
                }
            }

            GameObject xrayOverlay = BuildXRaySkullSpineOverlay(target, impactPos);

            if (speaker != null) speaker.PlayOneShot(ProceduralAudio.CreateHeadshotPing(), 0.9f);
            TimeScaleController.SetHitCam(0.08f);
            // TimeScaleController sets fixedDeltaTime automatically
            StartCoroutine(RestoreXRayTimeScale(2.2f, renderers, originalMats, xrayOverlay));
        }

        static GameObject BuildXRaySkullSpineOverlay(Transform target, Vector3 impactPos)
        {
            var root = new GameObject("XRayAnatomicalOverlay");
            root.transform.SetParent(target, false);

            Vector3 headPos = impactPos;
            var bones = target.GetComponentsInChildren<Transform>();
            Transform headBone = null;
            foreach (var b in bones)
            {
                string n = b.name.ToLowerInvariant();
                if (n.Contains("head") || n.Contains("skull")) { headBone = b; break; }
            }
            if (headBone != null) headPos = headBone.position;
            else headPos = target.position + Vector3.up * 1.62f;

            var boneMat = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
            boneMat.color = new Color(0.98f, 0.96f, 0.88f, 0.95f);

            // 3D Skull Mesh
            var skull = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            skull.name = "XRaySkullMesh";
            skull.transform.SetParent(root.transform, false);
            skull.transform.position = headPos;
            skull.transform.localScale = new Vector3(0.20f, 0.24f, 0.22f);
            var skullCol = skull.GetComponent<Collider>();
            if (skullCol != null) UnityEngine.Object.Destroy(skullCol);
            skull.GetComponent<Renderer>().sharedMaterial = boneMat;

            // 3D Spine Vertebrae
            Vector3 spineBase = target.position + Vector3.up * 0.9f;
            int count = 6;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);
                Vector3 vertPos = Vector3.Lerp(spineBase, headPos - Vector3.up * 0.12f, t);
                var vert = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                vert.name = "XRaySpineMesh";
                vert.transform.SetParent(root.transform, false);
                vert.transform.position = vertPos;
                vert.transform.localScale = new Vector3(0.08f, 0.04f, 0.08f);
                var c = vert.GetComponent<Collider>();
                if (c != null) UnityEngine.Object.Destroy(c);
                vert.GetComponent<Renderer>().sharedMaterial = boneMat;
            }

            return root;
        }

        System.Collections.IEnumerator RestoreXRayTimeScale(float duration, Renderer[] renderers, System.Collections.Generic.Dictionary<Renderer, Material[]> originalMats, GameObject overlay)
        {
            yield return new WaitForSecondsRealtime(duration);
            TimeScaleController.ClearHitCam();
            Time.fixedDeltaTime = 0.02f;

            if (renderers != null && originalMats != null)
            {
                foreach (var r in renderers)
                {
                    if (r != null && originalMats.TryGetValue(r, out var mats))
                    {
                        r.sharedMaterials = mats;
                    }
                }
            }
            if (overlay != null) UnityEngine.Object.Destroy(overlay);
        }
        public void SetTargetDistance(float distance) { targetDistance = distance; }
        public void OnShotFired()
        {
            // Different rifles communicate their weight without requiring a second
            // animation rig: the heavy Barrett kicks hardest, the MK12 settles fastest.
            shotKick = CurrentWeaponIndex==0 ? 1.35f : CurrentWeaponIndex==1 ? 1f : .72f;
            kick = Mathf.Max(kick,shotKick);
            shakeIntensity = Mathf.Max(shakeIntensity,shotKick*.12f);
        }
        public void Initialize(Camera camera)
        {
            if(rifle!=null) return;
            cam=camera;
            cam.nearClipPlane = 0.05f;
            currentFOV = Config != null ? Config.hipFOV : 65f;
            cam.fieldOfView = currentFOV;
            if(FindAnyObjectByType<AudioListener>()==null) camera.gameObject.AddComponent<AudioListener>();
            speaker=gameObject.AddComponent<AudioSource>(); speaker.spatialBlend=0; speaker.volume=.55f;
            shot=Sound("Rifle report",.65f,0); bolt=Sound("Bolt and magazine",1.4f,1);
            hit=Sound("Target confirmation",.18f,2); empty=Sound("Empty chamber",.09f,3);
            BuildMuzzleFlash(camera);
            BuildTracerLine();
            BuildCombatKnife(camera);
            
            EquipWeaponModel(CurrentWeaponIndex);
            
            scope=new Texture2D(512,512,TextureFormat.RGBA32,false); owned.Add(scope);
            var pixels=new Color32[512*512];
            for(int y=0;y<512;y++) for(int x=0;x<512;x++)
            {
                float r=Vector2.Distance(new Vector2(x,y),new Vector2(255.5f,255.5f))/256;
                pixels[y*512+x]=r>.98f?new Color32(5,8,10,255):r>.92f?new Color32(38,44,45,255):r>.90f?new Color32(110,117,108,255):new Color32(0,0,0,0);
            }
            scope.SetPixels32(pixels); scope.Apply();
        }
        Material Mat(Color color,float metallic)
        {
            var shader=Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse") ?? Shader.Find("Diffuse");
            var m=new Material(shader!=null?shader:Shader.Find("Sprites/Default")); m.color=color; m.SetFloat("_Metallic",metallic); m.SetFloat("_Glossiness",.3f); owned.Add(m); return m;
        }
        void ApplyRifleMaterials(Transform root)
        {
            var metal=Mat(new Color(.09f,.105f,.12f),.82f);
            metal.SetFloat("_Glossiness",.42f);
            var polymer=Mat(new Color(.13f,.145f,.115f),.05f);
            polymer.SetFloat("_Glossiness",.22f);
            var coatedShader = Shader.Find("GeoSniper/CoatedOptic") ?? Shader.Find("Standard");
            var glass = new Material(coatedShader) { color = new Color(.035f, .14f, .22f) };
            if (glass.HasProperty("_Metallic")) glass.SetFloat("_Metallic", 0.90f);
            if (glass.HasProperty("_Glossiness")) glass.SetFloat("_Glossiness", 0.98f);
            var steel=Mat(new Color(.32f,.35f,.37f),.9f);steel.SetFloat("_Glossiness",.75f);
            var rubber=Mat(new Color(.025f,.03f,.032f),0);rubber.SetFloat("_Glossiness",.22f);
            if(CurrentWeaponIndex==1)
            {
                polymer.color=new Color(.24f,.22f,.16f);
            }
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            {
                // Preserve imported textured submeshes, rather than collapsing the entire
                // renderer to one material when only its first slot has no texture.
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++)
                {
                    if(slots[i]!=null && slots[i].mainTexture!=null) continue;
                    string name=(renderer.name+" "+(slots[i]!=null?slots[i].name:"")).ToLowerInvariant();
                    if(name.Contains("glass") || name.Contains("lens") || name.Contains("optic")) slots[i]=glass;
                    else if(CurrentWeaponIndex==1 && (name.Contains("bolt silver") || name.Contains("bolt_silver") || name.Contains("screw"))) slots[i]=steel;
                    else if(CurrentWeaponIndex==1 && (name.Contains("second color") || name.Contains("second_color"))) slots[i]=rubber;
                    else slots[i]=name.Contains("stock") || name.Contains("grip") || name.Contains("cheek") || (CurrentWeaponIndex==1 && (name.Contains("sniper body") || name.Contains("sniper_body"))) ? polymer : metal;
                }
                renderer.sharedMaterials=slots;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        void NormalizeImportedRifle(Transform root)
        {
            var holder=root.parent;
            var bounds=ImportedVisual.LocalBounds(holder);
            if(bounds.size.y>bounds.size.z*1.5f) root.localRotation=Quaternion.Euler(90,0,0)*root.localRotation;
            else if(bounds.size.x>bounds.size.z*1.5f) root.localRotation=Quaternion.Euler(0,-90,0)*root.localRotation;
        }
        

        static Mesh quadMeshShared;
        static Mesh GetSharedQuadMesh()
        {
            if (quadMeshShared != null) return quadMeshShared;
            var m = new Mesh { name = "ProceduralFlashQuad" };
            m.vertices = new Vector3[] {
                new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
                new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0)
            };
            m.uv = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateNormals();
            quadMeshShared = m;
            return m;
        }

        AudioClip suppressedShot;

        AudioClip Sound(string name,float seconds,int kind)
        {
            if (kind == 0) return ProceduralAudio.CreateHeavyRifleShot();
            if (kind == 1) return ProceduralAudio.CreateBoltCycle();
            const int rate=44100; var samples=new float[(int)(seconds*rate)]; var random=new System.Random(41+kind);
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate, noise=(float)random.NextDouble()*2-1;
                if(kind==2) samples[i]=Mathf.Sin(t*2*Mathf.PI*880)*.25f*Mathf.Exp(-t*35);
                else samples[i]=noise*.25f*Mathf.Exp(-t*35);
            }
            var clip=AudioClip.Create(name,samples.Length,1,rate,false); clip.SetData(samples,0); owned.Add(clip); return clip;
        }

        void BuildCombatKnife(Camera camera)
        {
            if (combatKnife != null) Destroy(combatKnife.gameObject);
            var knifeObj = new GameObject("TacticalCombatKnife");
            combatKnife = knifeObj.transform;
            combatKnife.SetParent(camera.transform, false);

            EnsureFPSArmsMaterials();

            var steelMat = Mat(new Color(0.86f, 0.89f, 0.93f), 0.96f);
            steelMat.SetFloat("_Glossiness", 0.92f);
            var darkSteelMat = Mat(new Color(0.18f, 0.20f, 0.22f), 0.90f);
            darkSteelMat.SetFloat("_Glossiness", 0.75f);
            var handleMat = Mat(new Color(0.08f, 0.09f, 0.10f), 0.15f);
            handleMat.SetFloat("_Glossiness", 0.30f);

            // Container for knife + hand assembly
            var rig = new GameObject("KnifeHandRig").transform;
            rig.SetParent(combatKnife, false);

            // --- 1. TACTICAL COMBAT TANTO KNIFE (Blade pointing FORWARD +Z into attack) ---
            var knifeRoot = new GameObject("CombatKnife").transform;
            knifeRoot.SetParent(rig, false);
            knifeRoot.localPosition = new Vector3(0.015f, 0f, 0f);
            knifeRoot.localRotation = Quaternion.Euler(-8f, 4f, 0f);

            // Primary Tanto Blade (Lower sharp edge + Upper tactical carbon spine)
            var bladeEdge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bladeEdge.name = "TantoBladeEdge";
            bladeEdge.transform.SetParent(knifeRoot, false);
            bladeEdge.transform.localPosition = new Vector3(0, -0.008f, 0.11f);
            bladeEdge.transform.localScale = new Vector3(0.007f, 0.022f, 0.17f);
            bladeEdge.GetComponent<Renderer>().sharedMaterial = steelMat;
            var cE = bladeEdge.GetComponent<Collider>(); if (cE != null) Destroy(cE);

            var bladeSpine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bladeSpine.name = "TantoBladeSpine";
            bladeSpine.transform.SetParent(knifeRoot, false);
            bladeSpine.transform.localPosition = new Vector3(0, 0.007f, 0.10f);
            bladeSpine.transform.localScale = new Vector3(0.009f, 0.018f, 0.15f);
            bladeSpine.GetComponent<Renderer>().sharedMaterial = darkSteelMat;
            var cS = bladeSpine.GetComponent<Collider>(); if (cS != null) Destroy(cS);

            // Angled Tanto Tip (sharp geometric point angled up at 40 degrees)
            var tantoTip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tantoTip.name = "TantoPoint";
            tantoTip.transform.SetParent(knifeRoot, false);
            tantoTip.transform.localPosition = new Vector3(0, -0.003f, 0.20f);
            tantoTip.transform.localRotation = Quaternion.Euler(38f, 0, 0);
            tantoTip.transform.localScale = new Vector3(0.007f, 0.024f, 0.035f);
            tantoTip.GetComponent<Renderer>().sharedMaterial = steelMat;
            var cT = tantoTip.GetComponent<Collider>(); if (cT != null) Destroy(cT);

            // Blood groove / Fuller
            var fuller = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fuller.name = "BladeFuller";
            fuller.transform.SetParent(knifeRoot, false);
            fuller.transform.localPosition = new Vector3(0, 0.002f, 0.10f);
            fuller.transform.localScale = new Vector3(0.010f, 0.005f, 0.11f);
            fuller.GetComponent<Renderer>().sharedMaterial = darkSteelMat;
            var cF = fuller.GetComponent<Collider>(); if (cF != null) Destroy(cF);

            // Spine jimping / thumb ramp
            var jimping = GameObject.CreatePrimitive(PrimitiveType.Cube);
            jimping.name = "ThumbJimping";
            jimping.transform.SetParent(knifeRoot, false);
            jimping.transform.localPosition = new Vector3(0, 0.018f, 0.026f);
            jimping.transform.localRotation = Quaternion.Euler(20f, 0, 0);
            jimping.transform.localScale = new Vector3(0.012f, 0.008f, 0.025f);
            jimping.GetComponent<Renderer>().sharedMaterial = darkSteelMat;
            var cJ = jimping.GetComponent<Collider>(); if (cJ != null) Destroy(cJ);

            // Crossguard (Tactical steel finger guard)
            var guard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guard.name = "KnifeGuard";
            guard.transform.SetParent(knifeRoot, false);
            guard.transform.localPosition = new Vector3(0, -0.008f, 0.020f);
            guard.transform.localScale = new Vector3(0.020f, 0.052f, 0.012f);
            guard.GetComponent<Renderer>().sharedMaterial = darkSteelMat;
            var c2 = guard.GetComponent<Collider>(); if (c2 != null) Destroy(c2);

            // Handle (Ergonomic combat ribbed grip)
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "KnifeHandle";
            handle.transform.SetParent(knifeRoot, false);
            handle.transform.localPosition = new Vector3(0, -0.004f, -0.055f);
            handle.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            handle.transform.localScale = new Vector3(0.022f, 0.065f, 0.018f);
            handle.GetComponent<Renderer>().sharedMaterial = handleMat;
            var c3 = handle.GetComponent<Collider>(); if (c3 != null) Destroy(c3);

            // Pommel (Heavy steel skull-crusher)
            var pommel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pommel.name = "KnifePommel";
            pommel.transform.SetParent(knifeRoot, false);
            pommel.transform.localPosition = new Vector3(0, -0.004f, -0.125f);
            pommel.transform.localRotation = Quaternion.Euler(0, 45f, 45f);
            pommel.transform.localScale = new Vector3(0.020f, 0.020f, 0.018f);
            pommel.GetComponent<Renderer>().sharedMaterial = darkSteelMat;
            var cP = pommel.GetComponent<Collider>(); if (cP != null) Destroy(cP);

            // --- 2. TACTICAL GLOVED HAND HOLDING THE KNIFE ---
            var handRoot = new GameObject("GlovedHand").transform;
            handRoot.SetParent(rig, false);
            handRoot.localPosition = new Vector3(0.015f, 0f, 0f);

            // Palm & fist body
            var fist = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fist.name = "GloveFist";
            fist.transform.SetParent(handRoot, false);
            fist.transform.localPosition = new Vector3(0.008f, -0.002f, -0.055f);
            fist.transform.localScale = new Vector3(0.046f, 0.062f, 0.082f);
            fist.GetComponent<Renderer>().sharedMaterial = armsGloveMat;
            var cFist = fist.GetComponent<Collider>(); if (cFist != null) Destroy(cFist);

            // Carbon knuckle armor plate
            var knuckles = GameObject.CreatePrimitive(PrimitiveType.Cube);
            knuckles.name = "CarbonKnuckles";
            knuckles.transform.SetParent(handRoot, false);
            knuckles.transform.localPosition = new Vector3(0.028f, 0.005f, -0.055f);
            knuckles.transform.localRotation = Quaternion.Euler(0, 10f, 0);
            knuckles.transform.localScale = new Vector3(0.012f, 0.050f, 0.072f);
            knuckles.GetComponent<Renderer>().sharedMaterial = darkSteelMat;
            var cKnuck = knuckles.GetComponent<Collider>(); if (cKnuck != null) Destroy(cKnuck);

            // 4 Curled gloved fingers wrapped firmly around the hilt
            for (int fIdx = 0; fIdx < 4; fIdx++)
            {
                float zOffset = -0.022f - fIdx * 0.020f;
                var finger = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                finger.name = $"GloveFinger_{fIdx}";
                finger.transform.SetParent(handRoot, false);
                finger.transform.localPosition = new Vector3(-0.012f, -0.005f, zOffset);
                finger.transform.localRotation = Quaternion.Euler(90f, 0, 15f);
                finger.transform.localScale = new Vector3(0.014f, 0.022f, 0.014f);
                finger.GetComponent<Renderer>().sharedMaterial = armsGloveMat;
                var cFin = finger.GetComponent<Collider>(); if (cFin != null) Destroy(cFin);
            }

            // Gloved thumb locked across thumb ramp
            var thumb = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            thumb.name = "GloveThumb";
            thumb.transform.SetParent(handRoot, false);
            thumb.transform.localPosition = new Vector3(0.002f, 0.025f, -0.015f);
            thumb.transform.localRotation = Quaternion.Euler(30f, 35f, 65f);
            thumb.transform.localScale = new Vector3(0.015f, 0.026f, 0.015f);
            thumb.GetComponent<Renderer>().sharedMaterial = armsGloveMat;
            var cThm = thumb.GetComponent<Collider>(); if (cThm != null) Destroy(cThm);

            // Tactical forearm sleeve entering from bottom-right off-screen
            var forearm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            forearm.name = "TacticalSleeve";
            forearm.transform.SetParent(handRoot, false);
            forearm.transform.localPosition = new Vector3(0.065f, -0.095f, -0.165f);
            forearm.transform.localRotation = Quaternion.Euler(60f, -25f, 15f);
            forearm.transform.localScale = new Vector3(0.062f, 0.125f, 0.062f);
            forearm.GetComponent<Renderer>().sharedMaterial = armsSleeveMat;
            var cFore = forearm.GetComponent<Collider>(); if (cFore != null) Destroy(cFore);

            // Forearm extension to guarantee smooth off-screen taper
            var sleeveExt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sleeveExt.name = "SleeveExtension";
            sleeveExt.transform.SetParent(handRoot, false);
            sleeveExt.transform.localPosition = new Vector3(0.120f, -0.175f, -0.260f);
            sleeveExt.transform.localRotation = Quaternion.Euler(60f, -25f, 15f);
            sleeveExt.transform.localScale = new Vector3(0.072f, 0.135f, 0.072f);
            sleeveExt.GetComponent<Renderer>().sharedMaterial = armsSleeveMat;
            var cExt = sleeveExt.GetComponent<Collider>(); if (cExt != null) Destroy(cExt);

            // Turn off shadow casting on FPS knife rig to prevent self-shadow glitches
            foreach (var r in combatKnife.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            combatKnife.gameObject.SetActive(false);
        }

        public void TriggerKnifeSlash(bool isLethal = false)
        {
            knifeSlashTimer = 0.52f;
            knifeIsLethal = isLethal;
            knifeHitEffectPlayed = false;
            if (isLethal) knifeBloodTimer = 1.3f;
            if (combatKnife != null) combatKnife.gameObject.SetActive(true);
            if (rifle != null) rifle.gameObject.SetActive(false);
        }


        void BuildMuzzleFlash(Camera camera)
        {
            muzzleFlash=new GameObject("Muzzle flash");
            muzzleFlash.transform.SetParent(camera.transform,false);
            muzzleFlash.transform.localPosition=new Vector3(.20f,-.22f,1.5f);
            muzzleLight=muzzleFlash.AddComponent<Light>();
            muzzleLight.type=LightType.Point; muzzleLight.range=35; muzzleLight.intensity=0;
            muzzleLight.color=new Color(1f,.72f,.22f);
            
            suppressedShot = ProceduralAudio.CreateSuppressedShot();

            var flashShader=Shader.Find("Unlit/Color") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            if(flashShader!=null)
            {
                var coreMat=new Material(flashShader);
                coreMat.color=new Color(1f,.96f,.75f);
                owned.Add(coreMat);

                var flameMat=new Material(flashShader);
                flameMat.color=new Color(1f,.52f,.08f);
                owned.Add(flameMat);
                
                var mesh = GetSharedQuadMesh();
                for(int i=0; i<4; i++) 
                {
                    var flash=new GameObject("Flash quad "+i);
                    flash.transform.SetParent(muzzleFlash.transform,false);
                    flash.transform.localScale=new Vector3(0.7f, 0.7f, 0.7f);
                    flash.transform.localRotation = Quaternion.Euler(0, 0, i * 45);
                    var mf = flash.AddComponent<MeshFilter>();
                    mf.sharedMesh = mesh;
                    var mr = flash.AddComponent<MeshRenderer>();
                    mr.sharedMaterial=i%2==0?coreMat:flameMat;
                    mr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                var cone = new GameObject("Flash cone");
                cone.transform.SetParent(muzzleFlash.transform, false);
                cone.transform.localScale = new Vector3(0.5f, 1.4f, 1f);
                cone.transform.localPosition = new Vector3(0, 0, 0.45f);
                cone.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var cmf = cone.AddComponent<MeshFilter>();
                cmf.sharedMesh = mesh;
                var cmr = cone.AddComponent<MeshRenderer>();
                cmr.sharedMaterial = flameMat;
                cmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            muzzleFlash.SetActive(false);
        }
        void BuildTracerLine()
        {
            var tracerObj=new GameObject("Bullet tracer");
            tracerObj.transform.SetParent(transform,false);
            tracerLine=tracerObj.AddComponent<LineRenderer>();
            tracerLine.positionCount=2;
            tracerLine.startWidth=.025f; tracerLine.endWidth=.008f;
            tracerLine.useWorldSpace=true;
            var shader=Shader.Find("Unlit/Color") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            if(shader!=null)
            {
                var mat=new Material(shader);
                mat.color=new Color(1f,.92f,.5f);
                tracerLine.sharedMaterial=mat;
                owned.Add(mat);
            }
            tracerLine.enabled=false;
        }
        public void Shot() 
        { 
            OnShotFired();
            AudioClip activeReport = CurrentWeaponIndex == 2 ? (suppressedShot ?? shot) : shot;
            speaker.PlayOneShot(activeReport, CurrentWeaponIndex == 2 ? 0.7f : 1.0f); 
            ShowMuzzleFlash();
            EjectCasing();
        }
        public void ShotWithTracer(Vector3 from,Vector3 to,bool hitEnemy)
        {
            OnShotFired();
            AudioClip activeReport = CurrentWeaponIndex == 2 ? (suppressedShot ?? shot) : shot;
            speaker.PlayOneShot(activeReport, CurrentWeaponIndex == 2 ? 0.7f : 1.0f); 
            ShowMuzzleFlash();
            EjectCasing();
            ResolveProjectileVisual(from,to,hitEnemy);
        }

        void EjectCasing()
        {
            if (cam == null) return;
            var casing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            casing.name = "SpentBrassCasing";
            casing.transform.localScale = new Vector3(0.016f, 0.040f, 0.016f);
            
            Vector3 spawnPos = cam.transform.position + cam.transform.right * 0.22f - cam.transform.up * 0.12f + cam.transform.forward * 0.45f;
            casing.transform.position = spawnPos;
            casing.transform.rotation = cam.transform.rotation * Quaternion.Euler(UnityEngine.Random.Range(-20f, 20f), 90f, UnityEngine.Random.Range(-30f, 30f));

            var rend = casing.GetComponent<Renderer>();
            if (rend != null)
            {
                var brassMat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
                brassMat.color = new Color(0.95f, 0.78f, 0.28f);
                brassMat.SetFloat("_Metallic", 0.90f);
                brassMat.SetFloat("_Glossiness", 0.75f);
                rend.sharedMaterial = brassMat;
            }

            var rb = casing.AddComponent<Rigidbody>();
            rb.mass = 0.04f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            Vector3 ejectDir = (cam.transform.right * UnityEngine.Random.Range(1.8f, 2.6f) + cam.transform.up * UnityEngine.Random.Range(1.2f, 2.0f) - cam.transform.forward * UnityEngine.Random.Range(0.4f, 0.9f)).normalized;
            rb.linearVelocity = ejectDir * UnityEngine.Random.Range(3.2f, 4.8f);
            rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 40f;

            StartCoroutine(PlayCasingClinkDelayed(0.35f));
            Destroy(casing, 2.5f);
        }

        System.Collections.IEnumerator PlayCasingClinkDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (speaker != null) speaker.PlayOneShot(ProceduralAudio.CreateShellClink(), 0.65f);
        }
        public void ResolveProjectileVisual(Vector3 from,Vector3 to,bool hitEnemy)
        {
            if (!BallisticsSystem.isBulletCamActive) ShowTracer(from, to);
        }
        public void HideTracer()
        {
            if (tracerLine != null) tracerLine.enabled = false;
            tracerTimer = 0;
        }
        public void ReloadSound(float duration=1.4f) { reloadDuration=Mathf.Clamp(duration,.5f,3f); if(speaker!=null && bolt!=null) speaker.PlayOneShot(bolt); }
        public void HitSound() { speaker.PlayOneShot(hit); }
        public void EmptySound() { speaker.PlayOneShot(empty); }
        public void ToggleMute() { Muted=!Muted; speaker.mute=Muted; }
        void ShowMuzzleFlash()
        {
            if(muzzleFlash==null || BallisticsSystem.isBulletCamActive) return;
            flashTimer=.035f;
            muzzleFlash.SetActive(true);
            muzzleLight.intensity=MobileGraphics.Selected==MobileGraphicsPreset.Performance?0f:2.2f;

            // Position at active weapon barrel tip
            Vector3 muzzleOffset = rifle!=null ? cam.transform.InverseTransformPoint(rifle.TransformPoint(muzzleLocal)) : new Vector3(.19f,-.25f,1.35f);
            muzzleFlash.transform.localPosition = muzzleOffset;
            muzzleFlash.transform.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 360f));
            muzzleFlash.transform.localScale = Vector3.one * UnityEngine.Random.Range(.12f,.2f);

            if (cam != null)
            {
                Vector3 worldMuzzle = cam.transform.TransformPoint(muzzleOffset);
                SpawnMuzzleSparks(worldMuzzle, cam.transform.forward);
            }
        }

        public static void SpawnMuzzleSparks(Vector3 origin, Vector3 forward)
        {
            var fx = new GameObject("MuzzleSparksFX");
            fx.transform.position = origin;
            fx.transform.rotation = Quaternion.LookRotation(forward);

            var ps = fx.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.15f;
            main.loop = false;
            main.startLifetime = 0.08f;
            main.startSpeed = 38f;
            main.startSize = 0.04f;
            main.startColor = new Color(1f, 0.88f, 0.35f, 0.95f);

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 18) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.02f;

            var psr = fx.GetComponent<ParticleSystemRenderer>();
            var pMat = GetParticleMaterial();
            if (pMat != null) psr.sharedMaterial = pMat;

            ps.Play();
            Destroy(fx, 0.3f);
        }
        void ShowTracer(Vector3 from,Vector3 to)
        {
            if(tracerLine==null) return;
            tracerLine.SetPosition(0,from);
            tracerLine.SetPosition(1,to);
            tracerLine.enabled=true;
            tracerTimer=.05f;
        }

        static Material cachedParticleMat;
        static Material GetParticleMaterial()
        {
            if (cachedParticleMat == null)
            {
                var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Sprites/Default");
                if (shader != null) cachedParticleMat = new Material(shader);
            }
            return cachedParticleMat;
        }

        public static void SpawnImpactSparks(Vector3 position)
        {
            EmitSurfaceImpact(position,true,false);
        }

        static readonly List<ParticleSystem> impactPool=new List<ParticleSystem>(12);
        static Material dustMaterial;
        public static void SpawnSurfaceImpact(Vector3 position,Collider surface)
        {
            if(surface==null || surface.GetComponentInParent<CombatActor>()!=null)return;
            string name=surface.name.ToLowerInvariant();
            bool metal=name.Contains("metal") || name.Contains("steel") || name.Contains("rail")
                || name.Contains("vehicle") || name.Contains("car") || name.Contains("tank");
            EmitSurfaceImpact(position,metal,name.Contains("water"));
        }

        static void EmitSurfaceImpact(Vector3 position,bool metal,bool water)
        {
            ParticleSystem ps=null;
            for(int i=impactPool.Count-1;i>=0;i--)
            {
                if(impactPool[i]==null){impactPool.RemoveAt(i);continue;}
                if(!impactPool[i].IsAlive())ps=impactPool[i];
            }
            int cap=MobileGraphics.Selected==MobileGraphicsPreset.Performance?6:12;
            if(ps==null)
            {
                if(impactPool.Count>=cap)return;
                ps=new GameObject("Pooled surface impact").AddComponent<ParticleSystem>();
                ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var settings=ps.main;settings.loop=false;settings.playOnAwake=false;settings.duration=.8f;
                settings.maxParticles=12;settings.simulationSpace=ParticleSystemSimulationSpace.World;
                var emission=ps.emission;emission.enabled=false;
                var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Hemisphere;shape.radius=.035f;
                var fade=ps.colorOverLifetime;fade.enabled=true;
                var gradient=new Gradient();
                gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                    new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});
                fade.color=gradient;
                var renderer=ps.GetComponent<ParticleSystemRenderer>();
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                impactPool.Add(ps);
            }
            if(dustMaterial==null)
            {
                var texture=new Texture2D(32,32,TextureFormat.RGBA32,false);
                var pixels=new Color[1024];
                for(int y=0;y<32;y++)for(int x=0;x<32;x++)
                {
                    float a=Mathf.Clamp01(1f-new Vector2((x-15.5f)/16f,(y-15.5f)/16f).sqrMagnitude);
                    pixels[y*32+x]=new Color(1,1,1,a*a);
                }
                texture.SetPixels(pixels);texture.Apply(false,true);
                dustMaterial=new Material(Shader.Find("Sprites/Default")){mainTexture=texture};
            }
            ps.transform.position=position;ps.transform.rotation=Quaternion.Euler(-90,0,0);
            var main=ps.main;
            main.startLifetime=metal?.18f:.65f;main.startSpeed=metal?5f:water?2.4f:1.1f;
            main.startSize=metal?.035f:water?.08f:.18f;main.gravityModifier=metal?1f:water?.8f:-.03f;
            main.startColor=metal?new Color(1f,.7f,.25f):water?new Color(.6f,.8f,.85f,.65f):new Color(.57f,.53f,.46f,.6f);
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=dustMaterial;
            ps.Play();
            ps.Emit(MobileGraphics.Selected==MobileGraphicsPreset.Performance?4:8);
        }

        public static void SpawnMissDustPlume(Vector3 position)
        {
            var fx = new GameObject("MissDustPlumeFX");
            fx.transform.position = position;

            var ps = fx.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 2.8f;
            main.loop = false;
            main.startLifetime = 2.2f;
            main.startSpeed = 2.5f;
            main.startSize = 0.40f;
            main.gravityModifier = -0.06f;
            main.startColor = new Color(0.72f, 0.68f, 0.60f, 0.55f);

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { 
                new ParticleSystem.Burst(0f, 16),
                new ParticleSystem.Burst(0.08f, 10)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.1f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.4f);
            curve.AddKey(1f, 2.2f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.78f, 0.74f, 0.68f), 0f), new GradientColorKey(new Color(0.55f, 0.52f, 0.48f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0.35f, 0.4f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            var psr = fx.GetComponent<ParticleSystemRenderer>();
            var pMat = GetParticleMaterial();
            if (pMat != null) psr.sharedMaterial = pMat;

            ps.Play();
            Destroy(fx, 3.0f);
        }
        void UpdateEffects()
        {
            if (BallisticsSystem.isBulletCamActive)
            {
                if (tracerLine != null) { tracerLine.enabled = false; tracerTimer = 0; }
                if (muzzleFlash != null && muzzleFlash.activeSelf)
                {
                    muzzleFlash.SetActive(false);
                    flashTimer = 0f;
                    if (muzzleLight != null) muzzleLight.intensity = 0f;
                }
            }
            if(flashTimer>0)
            {
                flashTimer-=Time.deltaTime;
                muzzleLight.intensity=MobileGraphics.Selected==MobileGraphicsPreset.Performance?0f:2.2f*Mathf.Clamp01(flashTimer/.035f);
                if(flashTimer<=0) muzzleFlash.SetActive(false);
            }
            if(tracerTimer>0)
            {
                tracerTimer-=Time.deltaTime;
                if(tracerTimer<=0 && tracerLine!=null) tracerLine.enabled=false;
            }
            if(cam!=null && shakeIntensity>0)
            {
                shakeIntensity=Mathf.MoveTowards(shakeIntensity,0,Time.deltaTime*8);
                cam.transform.localPosition=cam.transform.localPosition-shakeOffset;
                shakeOffset=UnityEngine.Random.insideUnitSphere*shakeIntensity*.012f;
                cam.transform.localPosition=cam.transform.localPosition+shakeOffset;
            }
            else if(shakeOffset.sqrMagnitude>.0001f)
            {
                cam.transform.localPosition=cam.transform.localPosition-shakeOffset;
                shakeOffset=Vector3.zero;
            }
            if(hitMarkerTimer>0) hitMarkerTimer-=Time.deltaTime;
            if(killTimer>0) killTimer-=Time.deltaTime;
            if (knifeBloodTimer > 0f) knifeBloodTimer -= Time.deltaTime;
            if (knifeSlashTimer > 0f)
            {
                knifeSlashTimer -= Time.deltaTime;
                const float totalDur = 0.52f;
                float progress = 1f - Mathf.Clamp01(knifeSlashTimer / totalDur);
                if (combatKnife != null)
                {
                    Vector3 pos;
                    Quaternion rot;

                    if (progress < 0.16f)
                    {
                        float p = progress / 0.16f;
                        Vector3 p0 = new Vector3(0.18f, -0.24f, 0.32f);
                        Vector3 p1 = new Vector3(0.22f, -0.13f, 0.29f);
                        pos = Vector3.Lerp(p0, p1, p);
                        rot = Quaternion.Euler(Mathf.Lerp(18f, 28f, p), Mathf.Lerp(-15f, -24f, p), Mathf.Lerp(12f, 22f, p));
                    }
                    else if (progress < 0.30f)
                    {
                        float p = (progress - 0.16f) / 0.14f;
                        float ease = p * p;
                        Vector3 p1 = new Vector3(0.22f, -0.13f, 0.29f);
                        Vector3 p2 = new Vector3(-0.08f, -0.04f, 0.36f);
                        pos = Vector3.Lerp(p1, p2, ease);
                        rot = Quaternion.Euler(Mathf.Lerp(28f, -18f, ease), Mathf.Lerp(-24f, 32f, ease), Mathf.Lerp(22f, -40f, ease));
                    }
                    else if (progress < 0.38f)
                    {
                        float p = (progress - 0.30f) / 0.08f;
                        Vector3 p2 = new Vector3(-0.08f, -0.04f, 0.36f);
                        Vector3 p3 = new Vector3(-0.24f, -0.16f, 0.33f);
                        pos = Vector3.Lerp(p2, p3, p);
                        rot = Quaternion.Euler(Mathf.Lerp(-18f, -25f, p), Mathf.Lerp(32f, 40f, p), Mathf.Lerp(-40f, -50f, p));
                    }
                    else
                    {
                        float p = (progress - 0.38f) / 0.14f;
                        Vector3 p3 = new Vector3(-0.24f, -0.16f, 0.33f);
                        Vector3 pEnd = new Vector3(0.20f, -0.34f, 0.28f);
                        pos = Vector3.Lerp(p3, pEnd, p * p);
                        rot = Quaternion.Euler(Mathf.Lerp(-25f, 22f, p), Mathf.Lerp(40f, -12f, p), Mathf.Lerp(-50f, 15f, p));
                    }

                    combatKnife.localPosition = pos;
                    combatKnife.localRotation = rot;

                    // Trigger visceral impact effects at strike apex
                    if (progress >= 0.28f && !knifeHitEffectPlayed)
                    {
                        knifeHitEffectPlayed = true;
                        if (cam != null)
                        {
                            cam.transform.localRotation *= Quaternion.Euler(2.8f, -1.8f, 0.6f);
                        }
                        inertiaPos += new Vector3(-0.025f, 0.02f, -0.05f);
                    }
                }
                if (knifeSlashTimer <= 0f)
                {
                    if (combatKnife != null) combatKnife.gameObject.SetActive(false);
                    if (rifle != null) rifle.gameObject.SetActive(scopeBlend < 0.60f && !BallisticsSystem.isBulletCamActive);
                }
            }

            // Lens Condensation & Breath Wipe (condenses in rain/night, wiped clean when holding breath)
            bool isColdOrRain = RainEffect || SectorStreetLighting.IsNight;
            bool isHoldingBreath = (sway != null && sway.breathStamina < 0.98f);
            float targetCond = (isColdOrRain && scopeBlend > 0.4f) ? 0.20f : 0f;
            if (isHoldingBreath) targetCond = 0f;
            CondensationAlpha = Mathf.MoveTowards(CondensationAlpha, targetCond, Time.deltaTime * (isHoldingBreath ? 3.0f : 0.45f));

            // FLIR Thermal toggle hotkey (V / T)
            if (Input.GetKeyDown(KeyCode.V) || Input.GetKeyDown(KeyCode.T))
            {
                ThermalMode = !ThermalMode;
            }
        }
        
        public void CycleZoom()
        {
            if (Config == null || Config.zoomLevels == null || Config.zoomLevels.Length == 0) return;
            currentZoomIndex = (currentZoomIndex + 1) % Config.zoomLevels.Length;
        }

        public void SetSway(WeaponSway newSway) { sway = newSway; }

        public void SetVisible(bool visible)
        {
            if (rifle != null) rifle.gameObject.SetActive(visible);
            if (!visible && muzzleFlash != null)
            {
                muzzleFlash.SetActive(false);
                flashTimer = 0f;
                if (muzzleLight != null) muzzleLight.intensity = 0f;
            }
        }

        public void Animate(bool zoom, float reload, bool isMoving, bool isSprinting = false, bool isCrouching = false, float speed = 0f)
        {
            if(rifle==null) return;
            if (BallisticsSystem.isBulletCamActive)
            {
                rifle.gameObject.SetActive(false);
                return;
            }
            scopeBlend=Mathf.MoveTowards(scopeBlend,zoom?1f:0f,Time.deltaTime*8f);
            if (MobilePostProcess.Instance != null) MobilePostProcess.Instance.scopeBlend = scopeBlend;
            bool showRifle = scopeBlend < .60f && knifeSlashTimer <= 0f && (UrbanPlayer.Instance == null || !UrbanPlayer.Instance.BinocularsMode);
            rifle.gameObject.SetActive(showRifle);
            kick=Mathf.MoveTowards(kick,0,Time.deltaTime*(reload>0?9f:7f));
            shotKick=Mathf.MoveTowards(shotKick,0,Time.deltaTime*5f);
            float tilt=reload>0?Mathf.Sin((reloadDuration-reload)/Mathf.Max(.1f,reloadDuration)*Mathf.PI):0;
            
            Vector2 swayOffset = Vector2.zero;
            if (sway != null) swayOffset = sway.GetSwayOffset(isMoving, zoom);
            
            // Call of Duty Tactical Sprint & Crouch Stance Transitions
            bool tacticalSprint = isSprinting && !zoom && isMoving && reload <= 0;
            tacticalSprintBlend = Mathf.MoveTowards(tacticalSprintBlend, tacticalSprint ? 1f : 0f, Time.deltaTime * (tacticalSprint ? 6.5f : 8.5f));
            crouchBlend = Mathf.MoveTowards(crouchBlend, (isCrouching && !zoom) ? 1f : 0f, Time.deltaTime * 6f);
            
            // Call of Duty Stride Cadence & Dual-Harmonic Figure-8 Weapon Bob
            float cadence = tacticalSprint ? 9.8f : (isMoving ? (isCrouching ? 4.8f : 6.4f) : 2.1f);
            stridePhase += Time.deltaTime * cadence;
            
            float bobAmpX = tacticalSprint ? 0.034f : (isMoving ? (isCrouching ? 0.009f : 0.016f) : 0.0016f);
            float bobAmpY = tacticalSprint ? 0.028f : (isMoving ? (isCrouching ? 0.007f : 0.013f) : 0.0014f);
            float bobAmpRoll = tacticalSprint ? 4.8f : (isMoving ? (isCrouching ? 1.0f : 1.9f) : 0.35f);
            
            if (zoom)
            {
                bobAmpX *= 0.12f;
                bobAmpY *= 0.12f;
                bobAmpRoll *= 0.08f;
            }
            
            float bobX = Mathf.Cos(stridePhase) * bobAmpX;
            float bobY = (Mathf.Cos(stridePhase * 2f) - 1f) * 0.5f * bobAmpY;
            float bobRoll = Mathf.Sin(stridePhase) * bobAmpRoll;
            
            // Call of Duty Look Inertia & Weapon Drag / Rotational Lag
            if (cam != null)
            {
                if (!hasLastCamRot)
                {
                    lastCamRot = cam.transform.rotation;
                    hasLastCamRot = true;
                }
                Quaternion camRot = cam.transform.rotation;
                Vector3 deltaEuler = (Quaternion.Inverse(lastCamRot) * camRot).eulerAngles;
                float deltaYaw = Mathf.DeltaAngle(0, deltaEuler.y);
                float deltaPitch = Mathf.DeltaAngle(0, deltaEuler.x);
                lastCamRot = camRot;
                
                float zoomLagMult = zoom ? 0.22f : 1.0f;
                Vector3 targetLagPos = new Vector3(-deltaYaw * 0.0026f, deltaPitch * 0.0022f, 0f) * zoomLagMult;
                targetLagPos = Vector3.ClampMagnitude(targetLagPos, 0.032f);
                inertiaPos = Vector3.Lerp(inertiaPos, targetLagPos, Time.deltaTime * 18f);
                
                Vector3 targetLagRot = new Vector3(deltaPitch * 0.85f, -deltaYaw * 0.75f, deltaYaw * 0.65f) * zoomLagMult;
                inertiaRot = Vector3.Lerp(inertiaRot, targetLagRot, Time.deltaTime * 18f);
            }
            inertiaPos = Vector3.Lerp(inertiaPos, Vector3.zero, Time.deltaTime * 8.5f);
            inertiaRot = Vector3.Lerp(inertiaRot, Vector3.zero, Time.deltaTime * 8.5f);
            
            // Signature Call of Duty Tactical Sprint High-Ready Carry Pose
            Vector3 sprintPosOffset = new Vector3(-0.038f, -0.055f, -0.045f) * tacticalSprintBlend;
            Quaternion sprintRotOffset = Quaternion.Euler(-26f * tacticalSprintBlend, 20f * tacticalSprintBlend, -14f * tacticalSprintBlend);
            
            // Crouch tuck offset
            Vector3 crouchPosOffset = new Vector3(-0.015f, 0.012f, -0.025f) * crouchBlend;
            Quaternion crouchRotOffset = Quaternion.Euler(2.2f * crouchBlend, -2.5f * crouchBlend, 1.2f * crouchBlend);
            
            Vector3 targetPos = weaponViewPosition + sprintPosOffset + crouchPosOffset + new Vector3(
                bobX + swayOffset.x * 0.02f + inertiaPos.x,
                -tilt * 0.12f + bobY + swayOffset.y * 0.02f + inertiaPos.y,
                -kick * 0.08f + inertiaPos.z
            );
            rifle.localPosition = targetPos;
            
            Quaternion targetRot = weaponViewRotation * sprintRotOffset * crouchRotOffset * Quaternion.Euler(
                -kick * (CurrentWeaponIndex == 0 ? 11f : 8f) + tilt * 22f - swayOffset.y * 1.5f + inertiaRot.x,
                swayOffset.x * 1.5f + bobRoll * 0.45f + inertiaRot.y,
                tilt * -18f + bobRoll + inertiaRot.z
            );
            rifle.localRotation = targetRot;
            
            float targetFOV = 65f;
            if (UrbanPlayer.Instance != null && UrbanPlayer.Instance.BinocularsMode)
            {
                targetFOV = 65f / UrbanPlayer.Instance.BinocularZoom;
                currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.deltaTime * 14f);
            }
            else if (Config != null) 
            {
                if (zoom && Config.zoomLevels.Length > 0)
                {
                    float minZoom = Config.zoomLevels[0];
                    float maxZoom = Config.zoomLevels.Length > 1 ? Config.zoomLevels[Config.zoomLevels.Length - 1] : minZoom * 0.5f;
                    targetFOV = Mathf.Lerp(minZoom, maxZoom, MobileCombatInput.ZoomSliderValue);
                }
                else
                {
                    targetFOV = Config.hipFOV;
                }
                currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.deltaTime * Config.scopeTransitionSpeed);
            }
            else 
            {
                targetFOV = zoom ? 12f : 65f;
                currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.deltaTime * 10f);
            }
            
            if (cam != null)
            {
                cam.fieldOfView = currentFOV;
                if(zoom)
                {
                    float settle=Mathf.Sin(breathPhase*2f)*(.045f+(.025f*(1f-scopeBlend)));
                    cam.transform.localRotation*=Quaternion.Euler(settle,-settle*.65f,0);
                }

                // Optical Target Acquisition & Ballistic Telemetry Raycast
                if (scopeBlend > 0.05f)
                {
                    Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                    if (Physics.Raycast(ray, out RaycastHit hitInfo, 1200f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        targetDistance = hitInfo.distance;
                        targetElevation = hitInfo.point.y - cam.transform.position.y;
                        var actor = hitInfo.collider.GetComponentInParent<CombatActor>();
                        if (actor != null && !actor.IsDead)
                        {
                            currentTargetActor = actor;
                            var enemy = hitInfo.collider.GetComponentInParent<EnemyBot>();
                            var civ = hitInfo.collider.GetComponentInParent<CivilianBot>();
                            if (enemy != null) currentTargetClassification = "HOSTILE / TARGET";
                            else if (civ != null) currentTargetClassification = "CIVILIAN / NO-FIRE";
                            else currentTargetClassification = "OCCUPANT / UNKNOWN";
                        }
                        else
                        {
                            currentTargetActor = null;
                            currentTargetClassification = "";
                        }
                    }
                    else
                    {
                        targetDistance = -1f;
                        targetElevation = 0f;
                        currentTargetActor = null;
                        currentTargetClassification = "";
                    }
                }
                else
                {
                    targetDistance = -1f;
                    targetElevation = 0f;
                    currentTargetActor = null;
                    currentTargetClassification = "";
                }
            }
            
            UpdateEffects();
        }
        public bool ThermalMode;
        public bool RainEffect;
        public float CondensationAlpha = 0f;
        public int CurrentWeaponIndex = 0; // 0: BARRETT .50 CAL, 1: M24 TACTICAL, 2: MK12 DMR
        public string WeaponName => CurrentWeaponIndex == 0 ? "BARRETT .50 CAL" : CurrentWeaponIndex == 1 ? "M24 TACTICAL" : "MK12 SPR DMR";

        public int MaxAmmo
        {
            get
            {
                int baseAmmo = CurrentWeaponIndex == 0 ? 5 : CurrentWeaponIndex == 1 ? 20 : 30;
                int magLvl = PlayerPrefs.GetInt("GeoSniper.WpnMagLvl_" + CurrentWeaponIndex, 0);
                return baseAmmo + magLvl * (CurrentWeaponIndex == 0 ? 2 : 5);
            }
        }

        public void EquipWeaponModel(int index)
        {
            CurrentWeaponIndex=Mathf.Clamp(index,0,2);
            if(cam==null) return;
            if(rifle!=null) { Destroy(rifle.gameObject); rifle=null; }
            string[] primaryPaths = { "Models/Weapons/Barrett50", "Models/Weapons/M24Tactical", "Models/Weapons/MK12SPR" };
            string[] fallbackPaths = { "Models/GeoSniperRifleProvided", "Models/Weapons/M24Tactical", "Models/SniperRifle" };
            var imported = Resources.Load<GameObject>(primaryPaths[CurrentWeaponIndex]) ?? Resources.Load<GameObject>(fallbackPaths[CurrentWeaponIndex]);
            
            rifle = new GameObject("First person " + WeaponName).transform;
            rifle.SetParent(cam.transform, false);
            
            if (imported != null)
            {
                var model = Instantiate(imported, rifle, false);
                ImportedVisual.SanitizeWeaponModel(model.transform);
                if(!WeaponGeometry.Configure(model.transform, rifle, CurrentWeaponIndex)) return;
                var calibrated=ImportedVisual.LocalBounds(rifle);
                muzzleLocal=new Vector3(calibrated.center.x,calibrated.center.y,calibrated.max.z);
                cam.nearClipPlane = .05f;
                weaponViewPosition = CurrentWeaponIndex == 0 ? new Vector3(.16f, -.225f, .38f) : new Vector3(.15f, -.225f, .37f);
                weaponViewRotation = Quaternion.Euler(0, 1, -2);
                
                ApplyRifleMaterials(model.transform);
                AttachFPSArms(rifle, CurrentWeaponIndex);
            }
            if (speaker != null && bolt != null) speaker.PlayOneShot(bolt, 0.75f);
        }

        static GameObject fpsArmsRigPrefab;
        static Material armsSleeveMat;
        static Material armsGloveMat;

        static void EnsureFPSArmsMaterials()
        {
            if (armsSleeveMat != null && armsGloveMat != null) return;
            var shader = Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse") ?? Shader.Find("Diffuse");
            armsSleeveMat = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { name = "FPSArmsSleeveMat" };
            armsGloveMat = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { name = "FPSArmsGloveMat" };
            
            var sleeveTex = Resources.Load<Texture2D>("Models/FPSArms/Textures/arms_baseColor") ?? Resources.Load<Texture2D>("Models/FPSArms/Textures/Arms_diffuse");
            var gloveTex = Resources.Load<Texture2D>("Models/FPSArms/Textures/arms_baseColor") ?? Resources.Load<Texture2D>("Models/FPSArms/Textures/Gloves_diffuse");
            var sleeveNorm = Resources.Load<Texture2D>("Models/FPSArms/Textures/arms_normal") ?? Resources.Load<Texture2D>("Models/FPSArms/Textures/Arms_normal");
            var gloveNorm = Resources.Load<Texture2D>("Models/FPSArms/Textures/arms_normal") ?? Resources.Load<Texture2D>("Models/FPSArms/Textures/Gloves_normal");

            if (sleeveTex != null) armsSleeveMat.mainTexture = sleeveTex;
            else armsSleeveMat.color = new Color(0.24f, 0.28f, 0.22f);
            if (sleeveNorm != null && armsSleeveMat.HasProperty("_BumpMap")) { armsSleeveMat.EnableKeyword("_NORMALMAP"); armsSleeveMat.SetTexture("_BumpMap", sleeveNorm); }
            if (armsSleeveMat.HasProperty("_Glossiness")) armsSleeveMat.SetFloat("_Glossiness", 0.25f);

            if (gloveTex != null) armsGloveMat.mainTexture = gloveTex;
            else armsGloveMat.color = new Color(0.12f, 0.12f, 0.13f);
            if (gloveNorm != null && armsGloveMat.HasProperty("_BumpMap")) { armsGloveMat.EnableKeyword("_NORMALMAP"); armsGloveMat.SetTexture("_BumpMap", gloveNorm); }
            if (armsGloveMat.HasProperty("_Glossiness")) armsGloveMat.SetFloat("_Glossiness", 0.45f);
            if (armsGloveMat.HasProperty("_Metallic")) armsGloveMat.SetFloat("_Metallic", 0.35f);
        }

        void AttachFPSArms(Transform weaponRoot, int weaponIndex)
        {
            if (weaponRoot != null && weaponRoot.GetComponentInChildren<SkinnedMeshRenderer>() != null)
            {
                // Weapon already has rigged arms integrated (e.g. M24 Tactical)
                CalibrateTacticalHands(weaponRoot, weaponIndex);
                return;
            }
            if (fpsArmsRigPrefab == null)
            {
                fpsArmsRigPrefab = Resources.Load<GameObject>("Models/FPSArms/FPSSniperRifle");
            }
            if (fpsArmsRigPrefab == null) return;

            // Instantiate rigged FPS arms on a temporary holder to calibrate exact transform
            var dummyHolder = new GameObject("DummyArmsHolder").transform;
            var armsInstance = Instantiate(fpsArmsRigPrefab, dummyHolder, false);
            WeaponGeometry.Configure(armsInstance.transform, dummyHolder, 1);

            // Re-parent to weaponRoot
            armsInstance.transform.SetParent(weaponRoot, false);
            Destroy(dummyHolder.gameObject);
            armsInstance.name = "FirstPersonTacticalArmsRig";

            // Immediately disable and strip all M24 rifle meshes so ONLY the authentic rigged arms and hands remain
            foreach (var mr in armsInstance.GetComponentsInChildren<MeshRenderer>(true))
            {
                mr.enabled = false;
                mr.gameObject.SetActive(false);
                Destroy(mr.gameObject);
            }
            foreach (var mf in armsInstance.GetComponentsInChildren<MeshFilter>(true))
            {
                Destroy(mf);
            }

            // Remove any colliders
            foreach (var col in armsInstance.GetComponentsInChildren<Collider>(true))
            {
                Destroy(col);
            }

            // Ensure proper arm materials and shadow settings
            EnsureFPSArmsMaterials();
            foreach (var smr in armsInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                smr.receiveShadows = false;
                if (smr.sharedMaterial == null || smr.sharedMaterial.mainTexture == null)
                {
                    smr.sharedMaterial = armsSleeveMat;
                }
            }

            if (weaponIndex == 0) // Barrett .50 Cal
            {
                armsInstance.transform.localPosition += new Vector3(-0.005f, 0.008f, 0.015f);
            }
            else if (weaponIndex == 2) // MK12 SPR DMR
            {
                armsInstance.transform.localPosition += new Vector3(0.002f, 0.005f, -0.010f);
            }

            CalibrateTacticalHands(armsInstance.transform, weaponIndex);
        }

        void CalibrateTacticalHands(Transform weaponOrArmsRoot, int weaponIndex)
        {
            if (weaponOrArmsRoot == null) return;
            leftArmBone = null;
            Transform lElbow = null, lWrist = null;
            foreach (var t in weaponOrArmsRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "L_arm_01") leftArmBone = t;
                else if (t.name == "L_elbow_02") lElbow = t;
                else if (t.name == "L_wrist_03") lWrist = t;
            }

            if (leftArmBone != null) leftArmBone.localScale = Vector3.one;
            if (lElbow != null) lElbow.localScale = Vector3.one;
            if (lWrist != null) lWrist.localScale = Vector3.one;
            var pose=weaponOrArmsRoot.GetComponent<FirstPersonGripPose>() ?? weaponOrArmsRoot.gameObject.AddComponent<FirstPersonGripPose>();
            pose.Initialize(rifle,weaponOrArmsRoot);

        }

        public void DrawScope(float width, float height, int currentAmmo = -1, bool isReloading = false)
        {
            float d = Mathf.Min(width, height) * 0.92f, x = (width - d) / 2, y = (height - d) / 2;
            
            // Scope Mask Outer Blackout
            GUI.color = new Color(.02f, .03f, .04f, Mathf.Clamp01(scopeBlend));
            GUI.DrawTexture(new Rect(0, 0, x, height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x + d, 0, width - x - d, height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, 0, d, y), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y + d, d, height - y - d), Texture2D.whiteTexture);
            
            // Thermal Mode Lens Overlay
            if (ThermalMode)
            {
                GUI.color = new Color(0f, 0.15f, 0.32f, 0.65f); // Deep FLIR Thermal Blue
                GUI.DrawTexture(new Rect(x, y, d, d), Texture2D.whiteTexture);
            }

            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(scopeBlend));
            GUI.DrawTexture(new Rect(x, y, d, d), scope);

            // Procedural Lens Condensation (subtle outer ring)
            if (CondensationAlpha > 0.01f)
            {
                GUI.color = new Color(0.82f, 0.90f, 0.98f, CondensationAlpha * 0.12f * Mathf.Clamp01(scopeBlend));
                GUI.DrawTexture(new Rect(x + 12, y + 12, d - 24, d - 24), scope);
            }

            // FLIR Thermal Hot Human Signatures (enemies highlighted against cold background)
            if (ThermalMode && cam != null)
            {
                var allBots = EnemyBot.AllBots;
                if (allBots != null)
                {
                    for (int bi = 0; bi < allBots.Count; bi++)
                    {
                        var bot = allBots[bi];
                        if (bot == null || bot.Actor == null || bot.Actor.IsDead || !bot.gameObject.activeInHierarchy) continue;
                        Vector3 scr = cam.WorldToScreenPoint(bot.transform.position + Vector3.up * 1.0f);
                        if (scr.z > 1f && scr.z < 450f)
                        {
                            float bx = scr.x; float by = height - scr.y;
                            if (Vector2.Distance(new Vector2(bx, by), new Vector2(width / 2, height / 2)) < d * 0.44f)
                            {
                                float hSize = Mathf.Clamp(130f / scr.z * 18f, 10f, 48f);
                                float wSize = hSize * 0.42f;
                                GUI.color = new Color(1f, 0.6f, 0.1f, 0.35f * Mathf.Clamp01(scopeBlend));
                                GUI.DrawTexture(new Rect(bx - wSize * 0.7f, by - hSize * 0.6f, wSize * 1.4f, hSize * 1.2f), Texture2D.whiteTexture);
                                GUI.color = new Color(1f, 0.98f, 0.85f, 0.85f * Mathf.Clamp01(scopeBlend));
                                GUI.DrawTexture(new Rect(bx - wSize * 0.5f, by - hSize * 0.5f, wSize, hSize), Texture2D.whiteTexture);
                            }
                        }
                    }
                }
                GUI.color = Color.white;
            }

            // Authentic Mil-Dot Crosshair (Clean, razor-sharp optic etched reticle)
            float milStep = 22f * (d / 600f);
            GUI.color = (ThermalMode ? new Color(0f, 1f, 0.8f, 0.9f) : new Color(0, 0, 0, .85f)) * Mathf.Clamp01(scopeBlend);
            GUI.DrawTexture(new Rect(width / 2 - d * 0.43f, height / 2, d * 0.86f, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(width / 2, height / 2 - d * 0.43f, 1, d * 0.86f), Texture2D.whiteTexture);

            for (int i = -8; i <= 8; i++)
            {
                if (i == 0) continue;
                bool isMajor = (i % 2 == 0);
                float tickLen = isMajor ? 10f : 5f;
                float offset = i * (milStep * 0.5f);

                GUI.DrawTexture(new Rect(width / 2 - tickLen * 0.5f, height / 2 + offset, tickLen, 1), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(width / 2 + offset, height / 2 - tickLen * 0.5f, 1, tickLen), Texture2D.whiteTexture);

                // Subtle BDC holdover dots (no numbers cluttering the optic)
                if (i > 0 && isMajor)
                {
                    float dotMil = milStep * 0.5f;
                    int dotCount = Mathf.Min(i / 2, 3);
                    for (int wDot = 1; wDot <= dotCount; wDot++)
                    {
                        GUI.DrawTexture(new Rect(width / 2 - wDot * dotMil - 1f, height / 2 + offset - 1f, 2, 2), Texture2D.whiteTexture);
                        GUI.DrawTexture(new Rect(width / 2 + wDot * dotMil - 1f, height / 2 + offset - 1f, 2, 2), Texture2D.whiteTexture);
                    }
                }
            }

            // Windage Ballistics Calculation
            float crosswind = cam != null ? Vector3.Dot(cam.transform.right, BallisticsSystem.Wind) : BallisticsSystem.Wind.x;
            float windHoldMils = targetDistance > 30f ? (crosswind * (targetDistance / 800f) * 1.5f) : 0f;
            float windPipPx = windHoldMils * milStep;

            // Subtle Dynamic Windage Holdover Diamond (clean aim point without text clutter)
            if (Mathf.Abs(windPipPx) > 1.5f && targetDistance > 30f)
            {
                float pipX = width / 2 - windPipPx;
                float pipY = height / 2;
                Matrix4x4 savedM = GUI.matrix;
                GUI.color = new Color(1.0f, 0.65f, 0.15f, 0.9f * Mathf.Clamp01(scopeBlend));
                GUIUtility.RotateAroundPivot(45f, new Vector2(pipX, pipY));
                GUI.DrawTexture(new Rect(pipX - 3f, pipY - 3f, 6, 6), Texture2D.whiteTexture);
                GUI.matrix = savedM;
            }

            // Subtle Miss Impact Indicator (small 0.5s pip, no large obstructive HUD cards)
            if (lastMissImpactTimer > 0f && cam != null)
            {
                Vector3 p = cam.WorldToScreenPoint(lastMissImpactPos);
                if (p.z > 0.1f)
                {
                    float ix = p.x;
                    float iy = height - p.y;
                    if (Vector2.Distance(new Vector2(ix, iy), new Vector2(width / 2, height / 2)) < d * 0.43f)
                    {
                        float alpha = Mathf.Clamp01(lastMissImpactTimer / 1.0f);
                        GUI.color = new Color(1f, 0.35f, 0.15f, 0.85f * alpha);
                        GUI.DrawTexture(new Rect(ix - 3, iy - 3, 6, 6), Texture2D.whiteTexture);
                        GUI.color = Color.white;
                    }
                }
            }

            // Center crosshair dot
            GUI.color = ThermalMode ? new Color(1f, 0.9f, 0.1f, 0.95f) : new Color(.9f, .15f, .1f, .9f);
            float cx = width / 2f, cy = height / 2f;
            float bSize = 8f * (d / 600f), bDist = 28f * (d / 600f);
            GUI.color = (ThermalMode ? new Color(0f, 1f, 0.8f, 0.85f) : new Color(0.05f, 0.08f, 0.1f, 0.85f)) * Mathf.Clamp01(scopeBlend);
            GUI.DrawTexture(new Rect(cx - bDist, cy - bDist, bSize, 1.2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - bDist, cy - bDist, 1.2f, bSize), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + bDist - bSize, cy - bDist, bSize, 1.2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + bDist, cy - bDist, 1.2f, bSize), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - bDist, cy + bDist, bSize, 1.2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - bDist, cy + bDist - bSize, 1.2f, bSize), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + bDist - bSize, cy + bDist, bSize, 1.2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + bDist, cy + bDist - bSize, 1.2f, bSize), Texture2D.whiteTexture);
            Color illColor = ThermalMode ? new Color(1f, 0.95f, 0.2f, 0.95f) : new Color(1f, 0.2f, 0.12f, 0.95f);
            GUI.color = new Color(illColor.r, illColor.g, illColor.b, 0.35f * Mathf.Clamp01(scopeBlend));
            GUI.DrawTexture(new Rect(cx - 3.5f, cy - 3.5f, 7f, 7f), Texture2D.whiteTexture);
            GUI.color = illColor * Mathf.Clamp01(scopeBlend);
            GUI.DrawTexture(new Rect(cx - 1.5f, cy - 1.5f, 3f, 3f), Texture2D.whiteTexture);

            // Digital In-Reticle Rangefinder & Target Classification HUD
            if (targetDistance > 5f)
            {
                bool hasLock = currentTargetActor != null && !currentTargetActor.IsDead;
                Color hudColor = hasLock 
                    ? (ThermalMode ? new Color(1f, 0.85f, 0.2f, 0.95f) : new Color(1f, 0.28f, 0.22f, 0.95f))
                    : (ThermalMode ? new Color(0.2f, 1f, 0.85f, 0.85f) : new Color(0.15f, 0.88f, 1f, 0.85f));
                hudColor.a *= Mathf.Clamp01(scopeBlend);

                var rangeCenterStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = hudColor }
                };

                // Compact military readout placed just right-below center reticle
                string rangeStr = hasLock ? $"[{targetDistance:F0}m {currentTargetClassification}]" : $"{targetDistance:F0}m";
                GUI.Label(new Rect(cx + bDist + 6f, cy + 2f, 180f, 18f), rangeStr, rangeCenterStyle);

                // When locked on hostile target, draw subtle corner reticle brackets around center
                if (hasLock)
                {
                    float brkSize = bDist + 6f;
                    GUI.color = hudColor;
                    GUI.DrawTexture(new Rect(cx - brkSize, cy - brkSize, 6f, 1.5f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - brkSize, cy - brkSize, 1.5f, 6f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + brkSize - 6f, cy - brkSize, 6f, 1.5f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + brkSize - 1.5f, cy - brkSize, 1.5f, 6f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - brkSize, cy + brkSize - 1.5f, 6f, 1.5f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - brkSize, cy + brkSize - 6f, 1.5f, 6f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + brkSize - 6f, cy + brkSize - 1.5f, 6f, 1.5f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + brkSize - 1.5f, cy + brkSize - 6f, 1.5f, 6f), Texture2D.whiteTexture);
                }
            }

            // Subtle Chromatic Aberration Rim
            GUI.color = new Color(0f, 0.85f, 1f, 0.05f * Mathf.Clamp01(scopeBlend));
            GUI.DrawTexture(new Rect(x - 2, y - 2, d + 4, d + 4), scope);
            GUI.color = new Color(1f, 0.25f, 0f, 0.05f * Mathf.Clamp01(scopeBlend));
            GUI.DrawTexture(new Rect(x + 2, y + 2, d - 4, d - 4), scope);

            // Hit marker (X)
            if (hitMarkerTimer > 0)
            {
                var prevMat = GUI.matrix;
                GUI.color = new Color(1, 1, 1, hitMarkerTimer / 0.2f);
                GUIUtility.RotateAroundPivot(45, new Vector2(width / 2, height / 2));
                GUI.DrawTexture(new Rect(width / 2 - 10, height / 2 - 1, 20, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(width / 2 - 1, height / 2 - 10, 2, 20), Texture2D.whiteTexture);
                GUI.matrix = prevMat;
            }

            // Ballistics Telemetry
            float muzzleVel = CurrentWeaponIndex == 0 ? 853f : CurrentWeaponIndex == 1 ? 790f : 905f;
            float validDist = targetDistance > 5f ? targetDistance : 100f;
            float tof = validDist / muzzleVel;
            float gravity = 9.81f;
            float dropM = 0.5f * gravity * (tof * tof);
            float milDrop = (dropM / validDist) * 1000f;

            float windSpeed = BallisticsSystem.Wind.magnitude;
            string windArrow = crosswind > 0.4f ? "▶" : crosswind < -0.4f ? "◀" : "●";
            string zoomStr = (Config != null && Config.zoomLevels != null && Config.zoomLevels.Length > 0 ? (65f / Config.zoomLevels[currentZoomIndex]).ToString("F1") + "×" : "8.0×");

            // Tactical Ballistics HUD Readouts (Optic Cyan & Ballistic Amber)
            var tagStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 8,
                fontStyle = FontStyle.Bold,
                normal = { textColor = ThermalMode ? new Color(0.00f, 0.94f, 1.00f, 0.85f) : new Color(0.00f, 0.94f, 1.00f, 0.90f) }
            };
            var valStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = ThermalMode ? new Color(0.85f, 0.98f, 1.00f, 0.95f) : new Color(0.92f, 0.97f, 1.00f, 0.95f) }
            };

            // Top Compass Azimuth Heading Ribbon
            if (cam != null)
            {
                float yawDeg = cam.transform.eulerAngles.y;
                string dirStr = yawDeg < 22.5f || yawDeg >= 337.5f ? "N" : yawDeg < 67.5f ? "NE" : yawDeg < 112.5f ? "E" : yawDeg < 157.5f ? "SE" : yawDeg < 202.5f ? "S" : yawDeg < 247.5f ? "SW" : yawDeg < 292.5f ? "W" : "NW";
                string compassHeading = $"AZIMUTH: {yawDeg:000}° {dirStr}";
                var compStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 9,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.00f, 0.94f, 1.00f, 0.85f * Mathf.Clamp01(scopeBlend)) }
                };
                Rect compRect = new Rect(width / 2 - 90, height / 2 - d * 0.44f, 180, 16);
                GUI.color = new Color(0.067f, 0.078f, 0.090f, 0.75f * Mathf.Clamp01(scopeBlend));
                GUI.DrawTexture(compRect, Texture2D.whiteTexture);
                GUI.color = new Color(0.00f, 0.94f, 1.00f, 0.45f * Mathf.Clamp01(scopeBlend));
                GUI.DrawTexture(new Rect(compRect.x, compRect.y, compRect.width, 1), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(compRect.x, compRect.yMax - 1, compRect.width, 1), Texture2D.whiteTexture);
                GUI.Label(compRect, compassHeading, compStyle);
            }

            // 1. TOP-LEFT: RANGEFINDER (Target Distance)
            float tlX = width / 2 - d * 0.38f;
            float tlY = height / 2 - d * 0.32f;
            GUI.color = new Color(0.067f, 0.078f, 0.090f, 0.65f * Mathf.Clamp01(scopeBlend));
            GUI.DrawTexture(new Rect(tlX - 6, tlY - 4, 130, 36), Texture2D.whiteTexture);
            GUI.color = new Color(0.00f, 0.94f, 1.00f, 0.50f * Mathf.Clamp01(scopeBlend));
            GUI.DrawTexture(new Rect(tlX - 6, tlY - 4, 2, 36), Texture2D.whiteTexture);
            GUI.Label(new Rect(tlX, tlY, 120, 14), "// RANGEFINDER", tagStyle);
            GUI.Label(new Rect(tlX, tlY + 12, 140, 20), targetDistance > 0 ? $"{targetDistance:F0} M" : "--- M", valStyle);

            // 2. TOP-RIGHT: WINDAGE (Speed, Direction & Holdover Correction)
            float trX = width / 2 + d * 0.18f;
            float trY = height / 2 - d * 0.32f;
            GUI.color = new Color(0.067f, 0.078f, 0.090f, 0.65f * Mathf.Clamp01(scopeBlend));
            GUI.DrawTexture(new Rect(trX - 6, trY - 4, 150, 36), Texture2D.whiteTexture);
            GUI.color = new Color(1.00f, 0.60f, 0.00f, 0.50f * Mathf.Clamp01(scopeBlend));
            GUI.DrawTexture(new Rect(trX - 6, trY - 4, 2, 36), Texture2D.whiteTexture);
            GUI.Label(new Rect(trX, trY, 150, 14), "// WIND DEFLECTION", tagStyle);
            GUI.Label(new Rect(trX, trY + 12, 150, 20), $"{windSpeed:F1} M/S {windArrow} ({(crosswind >= 0 ? "+" : "")}{windHoldMils:F1} MIL)", valStyle);

            // 3. BOTTOM-LEFT: ELEVATION / BULLET DROP (Mil holdover & drop in meters)
            float blX = width / 2 - d * 0.38f;
            float blY = height / 2 + d * 0.22f;
            GUI.color = new Color(0.067f, 0.078f, 0.090f, 0.65f * Mathf.Clamp01(scopeBlend));
            GUI.DrawTexture(new Rect(blX - 6, blY - 4, 130, 36), Texture2D.whiteTexture);
            GUI.color = new Color(0.00f, 0.94f, 1.00f, 0.50f * Mathf.Clamp01(scopeBlend));
            GUI.DrawTexture(new Rect(blX - 6, blY - 4, 2, 36), Texture2D.whiteTexture);
            GUI.Label(new Rect(blX, blY, 140, 14), "// BDC ELEVATION", tagStyle);
            GUI.Label(new Rect(blX, blY + 12, 140, 20), $"+{milDrop:F1} MIL ({dropM:F2}m)", valStyle);

            // 4. BOTTOM-RIGHT: OPTIC ZOOM & AMMO
            float brX = width / 2 + d * 0.18f;
            float brY = height / 2 + d * 0.22f;
            GUI.color = new Color(0.067f, 0.078f, 0.090f, 0.65f * Mathf.Clamp01(scopeBlend));
            GUI.DrawTexture(new Rect(brX - 6, brY - 4, 150, 36), Texture2D.whiteTexture);
            GUI.color = new Color(0.00f, 0.98f, 0.40f, 0.50f * Mathf.Clamp01(scopeBlend));
            GUI.DrawTexture(new Rect(brX - 6, brY - 4, 2, 36), Texture2D.whiteTexture);
            GUI.Label(new Rect(brX, brY, 150, 14), "// CHAMBER / AMMO", tagStyle);
            string ammoDisplay = isReloading ? "RELOAD..." : (currentAmmo >= 0 ? $"{currentAmmo} / {MaxAmmo}" : $"{MaxAmmo}");
            GUI.Label(new Rect(brX, brY + 12, 150, 20), $"{zoomStr}  |  {ammoDisplay} RDS", valStyle);

            // Thermal Mode minimal badge
            if (ThermalMode)
            {
                var flirStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0f, 1f, 0.8f, 0.85f) } };
                GUI.Label(new Rect(width / 2 - 40, height / 2 - d * 0.38f, 80, 16), "FLIR THERMAL", flirStyle);
            }

            // Kill confirmation
            if (killTimer > 0)
            {
                GUIStyle killStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.2f, 0.1f, Mathf.Min(1f, killTimer)) }
                };
                GUI.Label(new Rect(width / 2 - 100, height / 2 - d * 0.2f, 200, 50), "ELIMINATED", killStyle);
            }

            // Breath stamina meter (only visible when holding breath or recovering stamina)
            if (sway != null && sway.breathStamina < 0.99f)
            {
                float barWidth = d * 0.26f;
                float barHeight = 4f;
                Rect barRect = new Rect(width / 2 - barWidth / 2, height / 2 + d * 0.28f, barWidth, barHeight);

                GUI.color = new Color(0, 0, 0, 0.6f);
                GUI.DrawTexture(barRect, Texture2D.whiteTexture);

                GUI.color = sway.breathStamina > 0.3f ? new Color(0.2f, 0.85f, 0.95f, 0.85f) : new Color(0.95f, 0.25f, 0.2f, 0.9f);
                GUI.DrawTexture(new Rect(barRect.x, barRect.y, barWidth * sway.breathStamina, barHeight), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }

        void OnGUI()
        {
            if (knifeBloodTimer > 0f)
            {
                DrawScreenBloodSplatter(Screen.width, Screen.height, Mathf.Clamp01(knifeBloodTimer / 1.0f));
            }
        }

        void DrawScreenBloodSplatter(float width, float height, float alpha)
        {
            if (alpha <= 0.01f) return;
            Matrix4x4 savedM = GUI.matrix;

            // 1. Red vignette edge pulses
            Color vignetteCol = new Color(0.45f, 0.02f, 0.02f, 0.35f * alpha);
            GUI.color = vignetteCol;
            GUI.DrawTexture(new Rect(0, 0, width * 0.25f, height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(width * 0.75f, 0, width * 0.25f, height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, 0, width, height * 0.18f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, height * 0.82f, width, height * 0.18f), Texture2D.whiteTexture);

            // 2. Arterial blood slash streak cutting diagonally across
            Color arterialCol = new Color(0.55f, 0.04f, 0.04f, 0.88f * alpha);
            GUI.color = arterialCol;
            GUIUtility.RotateAroundPivot(-28f, new Vector2(width * 0.58f, height * 0.38f));
            GUI.DrawTexture(new Rect(width * 0.35f, height * 0.36f, width * 0.45f, 7f), Texture2D.whiteTexture);
            GUI.color = new Color(0.25f, 0.01f, 0.01f, 0.95f * alpha);
            GUI.DrawTexture(new Rect(width * 0.42f, height * 0.37f, width * 0.32f, 3f), Texture2D.whiteTexture);
            GUI.matrix = savedM;

            // 3. Blood drops and splatter droplets in corners & center
            Color darkDropCol = new Color(0.40f, 0.02f, 0.02f, 0.90f * alpha);
            Color brightDropCol = new Color(0.68f, 0.05f, 0.05f, 0.82f * alpha);

            // Top-right splatter cluster
            GUI.color = darkDropCol;
            GUI.DrawTexture(new Rect(width * 0.72f, height * 0.15f, 16, 12), Texture2D.whiteTexture);
            GUI.color = brightDropCol;
            GUI.DrawTexture(new Rect(width * 0.74f, height * 0.19f, 22, 14), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(width * 0.79f, height * 0.12f, 12, 18), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(width * 0.83f, height * 0.22f, 8, 8), Texture2D.whiteTexture);

            // Bottom-left splatter cluster
            GUI.color = darkDropCol;
            GUI.DrawTexture(new Rect(width * 0.12f, height * 0.68f, 20, 14), Texture2D.whiteTexture);
            GUI.color = brightDropCol;
            GUI.DrawTexture(new Rect(width * 0.16f, height * 0.73f, 14, 22), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(width * 0.09f, height * 0.78f, 10, 10), Texture2D.whiteTexture);

            // Center-left impact spray dots
            GUI.color = brightDropCol;
            GUI.DrawTexture(new Rect(width * 0.42f, height * 0.44f, 6, 6), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(width * 0.38f, height * 0.48f, 9, 7), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(width * 0.45f, height * 0.52f, 5, 8), Texture2D.whiteTexture);

            GUI.matrix = savedM;
        }

        void OnDestroy()
        {
            if(rifle!=null) Destroy(rifle.gameObject);
            if(combatKnife!=null) Destroy(combatKnife.gameObject);
            if(muzzleFlash!=null) Destroy(muzzleFlash);
            if(tracerLine!=null) Destroy(tracerLine.gameObject);
            foreach(var item in owned) Destroy(item); TimeScaleController.ClearHitCam();
        }
    }
}
// 
