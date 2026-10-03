using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    public sealed class TrainingMission : MonoBehaviour
    {
        readonly List<GameObject> targets = new List<GameObject>();
        readonly List<Vector3> targetOrigins = new List<Vector3>();
        Camera cameraView;
        Material targetMaterial;
        float yaw, pitch, remaining, cooldown, flash, reload;
        int hits, shots, ammo, score, combo;
        float comboTimer, hitPop;
        bool zoom;
        SniperPresentation presentation;
        GameObject rooftop;
        public bool Finished => hits == 6 || remaining <= 0;
        public int Hits => hits;
        public int Ammo => ammo;
        public void Begin(Camera camera)
        {
            cameraView=camera;
            if(presentation==null) { presentation=gameObject.AddComponent<SniperPresentation>(); presentation.Initialize(camera); }
            if(targetMaterial==null)
            {
                var shader=Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse") ?? Shader.Find("Diffuse");
                if(shader!=null)
                {
                    targetMaterial=new Material(shader); targetMaterial.color=new Color(1,.28f,.06f);
                    if(targetMaterial.HasProperty("_EmissionColor")) { targetMaterial.EnableKeyword("_EMISSION"); targetMaterial.SetColor("_EmissionColor",new Color(.5f,.08f,0)); }
                }
            }
            foreach(var target in targets) if(target!=null) { target.SetActive(false); Destroy(target); }
            targets.Clear(); targetOrigins.Clear(); hits=0; shots=0; ammo=5; score=0; combo=0; comboTimer=0; hitPop=0; remaining=90; reload=0; cooldown=0; zoom=false;
            cameraView.transform.position=new Vector3(0,73,-235);
            if(rooftop==null)
            {
                rooftop=GameObject.CreatePrimitive(PrimitiveType.Cube); rooftop.name="Training rooftop";
                rooftop.transform.SetParent(transform); rooftop.transform.position=new Vector3(0,69,-235); rooftop.transform.localScale=new Vector3(24,4,18);
            }
            cameraView.transform.LookAt(new Vector3(0,45,0));
            yaw=cameraView.transform.eulerAngles.y; pitch=cameraView.transform.eulerAngles.x;
            // Training boards are mounted on platforms above the capped building height.
            for(int i=0;i<6;i++)
            {
                var target=GameObject.CreatePrimitive(PrimitiveType.Cube);
                target.name="Training Board "+(i+1); target.transform.SetParent(transform);
                target.transform.position=new Vector3((i%3-1)*65,68,-35+(i/3)*90);
                targetOrigins.Add(target.transform.position);
                target.transform.localScale=new Vector3(6,9,.6f);
                target.GetComponent<Renderer>().sharedMaterial=targetMaterial;
                targets.Add(target);
                var baseObject=GameObject.CreatePrimitive(PrimitiveType.Cube); baseObject.name="Board stand";
                baseObject.transform.SetParent(target.transform,false); baseObject.transform.localPosition=new Vector3(0,-.6f,0); baseObject.transform.localScale=new Vector3(1.5f,.2f,8);
                var ring=GameObject.CreatePrimitive(PrimitiveType.Cylinder); ring.name="Bullseye"; ring.transform.SetParent(target.transform,false);
                ring.transform.localPosition=new Vector3(0,0,-.6f); ring.transform.localRotation=Quaternion.Euler(90,0,0); ring.transform.localScale=new Vector3(.65f,.15f,.43f);
                ring.GetComponent<Collider>().enabled=false; Destroy(ring.GetComponent<Collider>());
            }
            Physics.SyncTransforms();
        }
        void Update()
        {
            if(cameraView==null) return;
            flash=Mathf.Max(0,flash-Time.deltaTime); cooldown=Mathf.Max(0,cooldown-Time.deltaTime);
            comboTimer=Mathf.Max(0,comboTimer-Time.deltaTime); if(comboTimer<=0) combo=0;
            hitPop=Mathf.Max(0,hitPop-Time.deltaTime);
            presentation.Animate(zoom,reload,false);
            if(Finished) return;
            remaining=Mathf.Max(0,remaining-Time.deltaTime);
            for(int i=0;i<targets.Count;i++) if(targets[i]!=null && targets[i].activeSelf)
            {
                var origin=targetOrigins[i];
                targets[i].transform.position=origin+new Vector3(Mathf.Sin(Time.time*(.7f+i*.09f))*10f,Mathf.Sin(Time.time*(1.1f+i*.13f))*3f,0);
                targets[i].transform.LookAt(cameraView.transform.position);
            }
            if(reload>0) { reload-=Time.deltaTime; if(reload<=0) ammo=5; }
            float sensitivity=zoom?.055f:.16f;
            if(Input.touchCount>0)
            {
                foreach(var touch in Input.touches)
                    if(touch.position.y>Screen.height*.24f && touch.phase==TouchPhase.Moved) { yaw+=touch.deltaPosition.x*sensitivity; pitch-=touch.deltaPosition.y*sensitivity; }
            }
            else if(Input.GetMouseButton(0) && Input.mousePosition.y>Screen.height*.24f)
            { yaw+=Input.GetAxis("Mouse X")*(zoom?.5f:1.8f); pitch-=Input.GetAxis("Mouse Y")*(zoom?.5f:1.8f); }
            pitch=Mathf.Clamp(pitch,-20,65); yaw=Mathf.Clamp(yaw,-70,70);
            cameraView.transform.rotation=Quaternion.Euler(pitch,yaw,0);
            cameraView.fieldOfView=Mathf.Lerp(cameraView.fieldOfView,zoom?22:60,Time.deltaTime*12);
            if(Input.GetKeyDown(KeyCode.Space)) Fire();
            if(Input.GetKeyDown(KeyCode.Z)) zoom=!zoom;
            if(Input.GetKeyDown(KeyCode.R)) Reload();
        }
        public void Fire()
        {
            if(Finished || reload>0 || cooldown>0) return;
            if(ammo==0) { presentation.EmptySound(); cooldown=.2f; return; }
            shots++; ammo--; cooldown=.35f; flash=.15f;
            presentation.Shot();
            if(Physics.Raycast(cameraView.ViewportPointToRay(new Vector3(.5f,.5f)),out var hit,700))
            {
                var target=hit.collider.gameObject;
                if(targets.Contains(target) && target.activeSelf) { target.SetActive(false); hits++; combo=comboTimer>0?combo+1:1; comboTimer=3.5f; score+=100*combo; hitPop=1.2f; presentation.HitSound(); }
            }
        }
        void Reload() { if(!Finished && reload<=0 && ammo<5) { reload=1.4f; presentation.ReloadSound(); } }
        void OnGUI()
        {
            if(cameraView==null) return;
            GUI.matrix=Matrix4x4.identity;
            float s=Mathf.Max(.4f, Mathf.Min(Screen.height/720f, Screen.width/800f)), w=Screen.width/s,h=Screen.height/s;
            GUI.matrix=Matrix4x4.Scale(new Vector3(s,s,1));
            if(zoom && !Finished) presentation.DrawScope(w,h);
            var style=new GUIStyle(GUI.skin.box){fontSize=20,alignment=TextAnchor.MiddleCenter};
            GUI.Box(new Rect(15,15,w-130,55),"ROOFTOP RANGE   |   TARGETS "+hits+" / 6   |   "+Mathf.CeilToInt(remaining)+"s   |   AMMO "+ammo+" / 5   |   SCORE "+score,style);
            if(GUI.Button(new Rect(w-105,15,90,55),presentation.Muted?"UNMUTE":"MUTE")) presentation.ToggleMute();
            GUI.Label(new Rect(15,h-25,w-30,25),"LIVE CONTRACT 01  |  Fictional targets in a real-world sector");
            if(Finished)
            {
                GUI.Box(new Rect(w/2-210,h/2-115,420,205),(hits==6?"MISSION COMPLETE":"TIME EXPIRED")+"\n"+hits+" / 6 targets  |  "+score+" points\nSTREAK x"+combo,style);
                if(GUI.Button(new Rect(w/2-100,h/2+20,200,50),"RETRY MISSION")) Begin(cameraView);
                return;
            }
            GUI.color=flash>0?Color.yellow:Color.white;
            GUI.DrawTexture(new Rect(w/2-18,h/2-1,36,2),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(w/2-1,h/2-18,2,36),Texture2D.whiteTexture); GUI.color=Color.white;
            if(GUI.Button(new Rect(20,h-95,125,65),zoom?"UNSCOPE [Z]":"SCOPE [Z]")) zoom=!zoom;
            if(GUI.Button(new Rect(w/2-65,h-95,130,65),reload>0?"RELOADING...":"RELOAD [R]")) Reload();
            if(GUI.Button(new Rect(w-150,h-95,130,65),"FIRE [SPACE]")) Fire();
            if(hitPop>0) GUI.Label(new Rect(w/2-150,h/2-120-hitPop*25,300,50),"+"+(100*combo)+"   COMBO x"+combo,new GUIStyle(GUI.skin.label){fontSize=28,alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold});
            GUI.Label(new Rect(20,80,w-40,40),"LIVE TARGETS  |  Build a streak before the contract expires",new GUIStyle(GUI.skin.label){fontSize=18,alignment=TextAnchor.MiddleCenter});
        }
        void OnDestroy() { if(targetMaterial!=null) Destroy(targetMaterial); }
    }
}
