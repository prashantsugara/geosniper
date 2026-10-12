using UnityEngine;

namespace GeoSniper
{
    public sealed partial class GeoSniperGame
    {
        static bool lobbyVisibleTracked;
        static readonly Color LobbyInk = CommandGUI.ThemeBg;
        static readonly Color LobbyPanel = CommandGUI.ThemeCard;
        static readonly Color LobbyLine = CommandGUI.ThemeBorder;
        static readonly Color LobbyMuted = CommandGUI.Muted;
        static readonly Color LobbyWhite = CommandGUI.Text;
        static readonly Color LobbyAmber = CommandGUI.AccentGold;
        static readonly string[] LobbyModes = { "CAMPAIGN", "WORLD SECTOR", "GPS RANGE", "SNIPER DUEL" };
        static readonly LobbyTab[] LobbyFooterTabs = { LobbyTab.Home, LobbyTab.Armory, LobbyTab.Location, LobbyTab.Rewards };
        static readonly string[] LobbyFooterIcons = { "▲", "†", "◉", "★" };
        static readonly string[] LobbyFooterLabels = { "OVERVIEW", "ARMORY", "SECTOR", "REWARDS" };
        static readonly string[] LobbyWeapons = { "BARRETT .50", "M24 TACTICAL", "MK12 SPR" };
        static readonly string[] LobbyDescriptions = {
            "Work through tactical contracts. Find your target, plan the shot, and complete the objective.",
            "Deploy into a real-world city. Use your location or choose a sector on the map.",
            "Practice movement and ballistics against non-engaging patrol targets in a live GPS sector.",
            "A rooftop standoff against an AI marksman. Find the rival before they lock onto you."
        };
        GUIStyle lobbyText;
        Texture2D brandLogo;
        bool rotatingLobby;
        readonly System.Collections.Generic.List<Material> lobbyStageMaterials = new System.Collections.Generic.List<Material>();

        readonly System.Collections.Generic.List<Material> lobbyRifleMaterials = new System.Collections.Generic.List<Material>();

        Transform lobbyWarBackdrop;
        Texture2D lobbyContactShadow;
        void BuildLobbyBackdrop()
        {
            var texture=Resources.Load<Texture2D>("Textures/WarLobbyBackground");
            if(texture==null)return;
            var material=new Material(Shader.Find("Unlit/Texture")){mainTexture=texture};
            lobbyStageMaterials.Add(material);
            var plate=GameObject.CreatePrimitive(PrimitiveType.Quad);plate.name="Battlefield background";
            plate.transform.SetParent(armoryRoom.transform,false);lobbyWarBackdrop=plate.transform;
            plate.GetComponent<Renderer>().sharedMaterial=material;
            var collider=plate.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            CreateLobbyContactShadow();
        }
        void CreateLobbyContactShadow()
        {
            lobbyContactShadow=new Texture2D(64,64,TextureFormat.RGBA32,false){name="Operator contact shadow",wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[64*64];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float radius=new Vector2((x-31.5f)/31.5f,(y-31.5f)/31.5f).magnitude;
                pixels[y*64+x]=new Color(0,0,0,Mathf.Pow(Mathf.Clamp01(1-radius),1.6f)*.6f);
            }
            lobbyContactShadow.SetPixels(pixels);lobbyContactShadow.Apply(false,true);
            var material=new Material(Shader.Find("Sprites/Default")){mainTexture=lobbyContactShadow};lobbyStageMaterials.Add(material);
            var shadow=GameObject.CreatePrimitive(PrimitiveType.Quad);shadow.name="Operator ground shadow";
            shadow.transform.SetParent(armoryRoom.transform,false);shadow.transform.position=new Vector3(.05f,.015f,3.1f);
            shadow.transform.rotation=Quaternion.Euler(90,0,0);shadow.transform.localScale=new Vector3(1.55f,1.1f,1);
            shadow.GetComponent<Renderer>().sharedMaterial=material;var collider=shadow.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
        }
        void FrameWarBackdrop()
        {
            if(lobbyWarBackdrop==null || view==null)return;
            const float distance=10f;
            float height=2*distance*Mathf.Tan(view.fieldOfView*Mathf.Deg2Rad*.5f);
            var texture=lobbyWarBackdrop.GetComponent<Renderer>().sharedMaterial.mainTexture;
            float aspect=texture.width/(float)texture.height;
            height=Mathf.Max(height,height*view.aspect/aspect);
            lobbyWarBackdrop.position=view.transform.position+view.transform.forward*distance;
            lobbyWarBackdrop.rotation=view.transform.rotation;
            lobbyWarBackdrop.localScale=new Vector3(height*aspect,height,1);
        }

        void LobbyBlock(string name, Vector3 position, Vector3 scale, Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(armoryRoom.transform, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            var collider = block.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
        }

        void ReleaseLobbyStageMaterials()
        {
            foreach (var material in lobbyStageMaterials) if (material != null) Destroy(material);
            lobbyStageMaterials.Clear();
            if(lobbyContactShadow!=null)Destroy(lobbyContactShadow);lobbyContactShadow=null;lobbyWarBackdrop=null;
            foreach (var material in lobbyRifleMaterials) if (material != null) Destroy(material);
            lobbyRifleMaterials.Clear();
        }

        void OnDestroy()
        {
            StopBriefingAudio();
            ReleaseLobbyStageMaterials();
            if (armoryRoom != null) Destroy(armoryRoom);
            if (lobbyMusic != null) Destroy(lobbyMusic);
            lobbyMusicClip=null;
        }

        void LobbyFill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void LobbyLabel(Rect rect, string text, int size, Color color, bool bold = false,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            if (lobbyText == null) lobbyText = new GUIStyle(GUI.skin.label) { padding = new RectOffset(0, 0, 0, 0) };
            lobbyText.fontSize = size;
            lobbyText.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            lobbyText.normal.textColor = color;
            lobbyText.alignment = alignment;
            lobbyText.wordWrap = true;
            GUI.Label(rect, text, lobbyText);
        }

        bool LobbyButton(Rect rect,string text,bool active=false,bool primary=false) => GameButtonChrome.Draw(rect,text,primary?GameButtonTone.Amber:active?GameButtonTone.Cyan:GameButtonTone.Steel,primary?19:12);

        void DrawVignette(float w, float h, float topY, float botY)
        {
            int steps = 12;
            for (int i = 0; i < steps; i++)
            {
                float t = (float)(steps - i) / steps;
                float a = t * t * 0.65f;
                LobbyFill(new Rect(i * 4f, topY, 4f, botY - topY), new Color(0, 0, 0, a));
                LobbyFill(new Rect(w - (i + 1) * 4f, topY, 4f, botY - topY), new Color(0, 0, 0, a));
            }
        }

        void DrawMainMenu(float ignoredWidth,float ignoredHeight)
        {
            var previous=GUI.matrix;var safe=Screen.safeArea;
            float scale=CommandGUI.GetCanvasScale(safe,out float w,out float h);
            GUI.matrix=Matrix4x4.TRS(new Vector3(safe.x,Screen.height-safe.yMax,0),Quaternion.identity,new Vector3(scale,scale,1));
            try
            {
                float navH=62;
                DrawVignette(w,h,0,h-navH);
                if(!lobbyVisibleTracked)
                {
                    lobbyVisibleTracked=true;
                    GameAnalyticsManager.TrackCustomEvent("lobby_visible",new System.Collections.Generic.Dictionary<string,object>{{"saved_stage",PlayerPrefs.GetInt("GeoSniper.ActiveNodeId",0)+1}});
                }
                LobbyFill(new Rect(0,0,w,60),new Color(.025f,.065f,.10f,.86f));
                LobbyLabel(new Rect(24,12,330,32),"GEO SNIPER",25,CommandGUI.Text,true);
                LobbyLabel(new Rect(w-350,17,245,26),PlayerPrefs.GetInt("GeoSniper.Credits",0).ToString("N0")+" CR  /  "+PlayerPrefs.GetInt("GeoSniper.Gold",0)+" GOLD",12,CommandGUI.Muted,false,TextAnchor.MiddleRight);
                if(CommandGUI.DrawButton(new Rect(w-78,12,54,36),"⚙",false,17))SwitchTab(LobbyTab.Settings);
                LobbyFill(new Rect(24,52,150,2),new Color(1f,.49f,.13f,.9f));
                var node=CampaignNodeGraph.GetNode(GetDefaultSelectedNodeId());
                float panelW=Mathf.Min(420,w*.42f),panelX=w-panelW-30,panelY=Mathf.Max(100,(h-navH-470)*.5f);
                var card=new Rect(panelX,panelY,panelW,310);LobbyFill(card,new Color(.025f,.07f,.11f,.90f));
                LobbyFill(new Rect(card.x,card.y,card.width,2),new Color(1f,.56f,.18f,.6f));
                LobbyFill(new Rect(card.x,card.y,3,card.height),CommandGUI.AccentGold);
                LobbyLabel(new Rect(card.x+24,card.y+22,panelW-48,22),"STORY CAMPAIGN / "+((node?.id??0)+1)+" OF 50",12,CommandGUI.AccentGold,true);
                LobbyLabel(new Rect(card.x+24,card.y+61,panelW-48,66),node?.title??"GLASS MERIDIAN",27,CommandGUI.Text,true);
                LobbyLabel(new Rect(card.x+24,card.y+140,panelW-48,62),node?.objective??"Choose your next mission.",16,CommandGUI.Text);
                LobbyLabel(new Rect(card.x+24,card.y+210,panelW-48,26),CampaignLocationLabel(),12,CommandGUI.Muted);
                if(CommandGUI.DrawGoldPlayButton(new Rect(card.x+22,card.yMax-66,panelW-44,48),node!=null && node.id==0 && !CampaignNodeGraph.IsNodeCompleted(0)?"START STORY":"CONTINUE STORY"))
                {
                    GameAnalyticsManager.TrackCustomEvent("deploy_tap",new System.Collections.Generic.Dictionary<string,object>{{"action","story_card"},{"stage_index",(node?.stageIndex??0)+1}});
                    OpenCampaignMission();
                }
                if(GameButtonChrome.Draw(new Rect(panelX,card.yMax+18,panelW,54),"MULTIPLAYER / 1v1",GameButtonTone.Cyan,17))OpenDuelLobby();
                if(CommandGUI.DrawButton(new Rect(panelX,card.yMax+86,panelW,40),"PRACTICE RANGE",false,13))
                {PlayerPrefs.SetInt("GeoSniper.SelectedMode",2);ExecuteDeploy();}
                LobbyFill(new Rect(16,h-navH-98,Mathf.Min(430,panelX-40),80),new Color(.015f,.045f,.07f,.72f));
                LobbyLabel(new Rect(28,h-navH-85,Mathf.Max(260,panelX-70),26),"RANK "+PlayerProgression.Level+" / "+WeaponCatalog.Get(WeaponCatalog.SelectedOwnedWeapon).Name,15,CommandGUI.Text,true);
                LobbyLabel(new Rect(28,h-navH-53,Mathf.Max(260,panelX-70),22),"The streets look familiar. The mission is yours.",13,CommandGUI.Muted);
                if(loadFailed)
                {
                    var failure=new Rect(panelX,card.y, panelW,310);LobbyFill(failure,CommandGUI.ThemeCard);
                    LobbyLabel(new Rect(failure.x+24,failure.y+30,panelW-48,126),mapNotice,16,CommandGUI.Text);
                    if(CommandGUI.DrawButton(new Rect(failure.x+24,failure.y+190,panelW-48,46),"RETRY",true,14))RetryDeployment();
                    if(!pendingMultiplayerDuel && CommandGUI.DrawButton(new Rect(failure.x+24,failure.y+248,panelW-48,38),"PLAY BUNDLED SECTOR",false,12))DeployBundledRecovery();
                }
                int clicked=CommandGUI.DrawNavBar(new Rect(0,h-navH,w,navH),LobbyFooterIcons,LobbyFooterLabels,0);
                if(clicked>0 && clicked<LobbyFooterTabs.Length)SwitchTab(LobbyFooterTabs[clicked]);
            }
            finally{GUI.matrix=previous;}
        }

        void FrameLobbyOperator()
        {
            // AAA Landscape framing: Operative standing proudly in armory staging bay.
            // Soldier center is at Vector3(0.05f, 0.04f, 3.1f).
            float verticalSpan = 2.45f;
            view.fieldOfView = 36f;
            float depth = verticalSpan / (2f * Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * 0.5f));
            // Camera position: offset to center-left so soldier is framed between left dossier and right deploy cluster
            float camOffsetX = 0.85f;
            float yawRad = lobbyYaw * Mathf.Deg2Rad;
            float orbitRadius = 0.04f;
            view.transform.position = new Vector3(
                camOffsetX + Mathf.Sin(yawRad) * orbitRadius,
                1.15f,
                (3.1f - depth) + Mathf.Cos(yawRad) * orbitRadius);
            view.transform.LookAt(new Vector3(camOffsetX * 0.6f, 0.95f, 3.1f));
            view.backgroundColor = new Color(0.035f, 0.08f, 0.12f);
            FrameWarBackdrop();
        }
    }
}
