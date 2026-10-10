using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BootstrapFactory
{
    public partial class FactoryGame : MonoBehaviour
    {
        public Simulation sim;
        Catalog catalog;
        Machine selected;
        string build="",panel="OPERATIONS",search="";
        int direction,speed=1,resumeSpeed=1,category,researchView=-1;
        readonly int[] speedSteps={1,4,16,64,256};
        bool help=true,deleteMode,confirmReset,qa,qaPreview;
        float accumulator,saveClock,cell=22;
        Vector2 pan=new Vector2(1,2),buildScroll,detailScroll,cargoScroll;
        Texture2D[] ground;Texture2D disc;
        GUIStyle text,small,title,sectionTitle,heading,button,muted;
        readonly Color ink=new Color(.055f,.075f,.09f),surface=new Color(.085f,.11f,.13f),line=new Color(.16f,.21f,.23f),white=new Color(.86f,.91f,.88f),sub=new Color(.49f,.61f,.62f),accent=new Color(.98f,.69f,.26f),green=new Color(.43f,.79f,.59f);
        const float W=1600,H=900;
        Rect map=new Rect(244,102,1014,712);
        string SavePath {get{return Path.Combine(Application.persistentDataPath,"factory-v1.json");}}
        float Scale {get{return Mathf.Min(Screen.width/W,Screen.height/H);}}
        Vector2 Mouse {get{return new Vector2(Input.mousePosition.x/Scale,(Screen.height-Input.mousePosition.y)/Scale);}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap(){if(FindFirstObjectByType<FactoryGame>()==null)new GameObject("Bootstrap Factory").AddComponent<FactoryGame>();}
        void Awake()
        {
            Application.targetFrameRate=60;Application.runInBackground=true;
            catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("catalog").text);
            sim=new Simulation(catalog);GenerateTextures();
            if(Camera.main==null){var c=new GameObject("Top Down Camera").AddComponent<Camera>();c.tag="MainCamera";c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=ink;}
            qa=Environment.GetCommandLineArgs().Contains("--factory-qa");
            if(!qa&&File.Exists(SavePath))Load();
            if(qa)StartCoroutine(QaCapture());
        }
        void GenerateTextures()
        {
            ground=new Texture2D[8];
            for(int i=0;i<8;i++){var t=new Texture2D(32,32);t.filterMode=FilterMode.Point;var rng=new System.Random(140+i);for(int y=0;y<32;y++)for(int x=0;x<32;x++){float n=(float)rng.NextDouble()*.018f;var c=new Color(.135f+n+i*.0015f,.17f+n,.15f+n);if(rng.NextDouble()<.025)c*=.8f;t.SetPixel(x,y,c);}t.Apply();ground[i]=t;}
            disc=new Texture2D(32,32);for(int y=0;y<32;y++)for(int x=0;x<32;x++)disc.SetPixel(x,y,Vector2.Distance(new Vector2(x,y),new Vector2(15.5f,15.5f))<15?Color.white:Color.clear);disc.Apply();
        }
        void Update()
        {
            float dt=Mathf.Min(Time.unscaledDeltaTime,.2f);
            if(!help&&!modal&&!deleting&&!confirmReset){accumulator+=dt*speed;while(accumulator>=.05f){sim.Tick(.05f);accumulator-=.05f;}}
            saveClock+=dt;if(!qa&&saveClock>60){saveClock=0;Save(false);}
            if(Input.GetKeyDown(KeyCode.F1))help=!help;
            if(!help&&GUIUtility.keyboardControl==0&&Input.GetKeyDown(KeyCode.C))OpenRecovery();
            if(help){beltDragging=false;return;}
            if(Input.GetKeyDown(KeyCode.Escape)&&modal){modal=false;return;}
            if(modal){if(Input.GetKeyDown(KeyCode.R)&&selected!=null)sim.Rotate(selected);return;}if(confirmReset)return;
            if((Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl))&&Input.GetKeyDown(KeyCode.Z)){sim.UndoDelete();selected=null;}
            if(beltDragging&&(Input.GetMouseButtonDown(1)||Input.GetKeyDown(KeyCode.Escape))){beltDragging=false;sim.notice="Belt route canceled";return;}
            if(Input.GetMouseButtonDown(0)||Input.GetMouseButtonUp(0)){markStrokeSeen.Clear();markStrokeModeSet=false;}
            UpdateDelete();
            if(Input.GetKeyDown(KeyCode.Delete)&&GUIUtility.keyboardControl==0&&marked.Count>0&&!deleting)DeleteMarked();
            if(Input.GetKeyDown(KeyCode.Tab))buildMenuOpen=!buildMenuOpen;
            if(Input.GetKeyDown(KeyCode.Escape)){build="";deleteMode=false;marked.Clear();confirmReset=false;GUIUtility.keyboardControl=0;}
            if(Input.GetKeyDown(KeyCode.R)&&(build!=""||GUIUtility.keyboardControl==0))RotateTool();
            if(GUIUtility.keyboardControl==0&&Input.GetKeyDown(KeyCode.Space))TogglePause();
            if(GUIUtility.keyboardControl==0)
            {
                if(Input.GetKeyDown(KeyCode.Period)||Input.GetKeyDown(KeyCode.Equals))ChangeSpeed(1);
                if(Input.GetKeyDown(KeyCode.Comma)||Input.GetKeyDown(KeyCode.Minus))ChangeSpeed(-1);
            }
            if(GUIUtility.keyboardControl==0&&Input.GetKeyDown(KeyCode.B)){build="belt";panel="OPERATIONS";deleteMode=false;}
            if(GUIUtility.keyboardControl==0&&Input.GetKeyDown(KeyCode.X)){deleteMode=!deleteMode;build="";}
            if(Input.GetKeyDown(KeyCode.F5))Save();
            if(Input.GetKeyDown(KeyCode.F9))Load();
            if(Input.GetKeyDown(KeyCode.Home))pan=new Vector2(1,2);
            if(GUIUtility.keyboardControl==0){pan.x+=(Input.GetKey(KeyCode.D)?1:0)*dt*18-(Input.GetKey(KeyCode.A)?1:0)*dt*18;pan.y+=(Input.GetKey(KeyCode.S)?1:0)*dt*18-(Input.GetKey(KeyCode.W)?1:0)*dt*18;}
            pan.x=Mathf.Clamp(pan.x,-120,120);pan.y=Mathf.Clamp(pan.y,-90,90);
            UpdateBeltDrag();
            Vector2 mouse=Mouse;if(MapInputAvailable(mouse))
            {
                float wheel=Input.mouseScrollDelta.y;if(wheel!=0)cell=Mathf.Clamp(cell+wheel*3,10,60);
                if(Input.GetMouseButton(2)){pan.x-=Input.GetAxis("Mouse X")*dt*10;pan.y+=Input.GetAxis("Mouse Y")*dt*10;}
                var tile=Tile(mouse);
                if(deleting||build=="belt")return;
                bool click=Input.GetMouseButtonDown(0)||(Input.GetMouseButton(0)&&deleteMode);
                if(click)
                {
                    GUIUtility.keyboardControl=0;
                    if(deleteMode){PaintMark(sim.At(tile.x,tile.y));selected=null;}
                    else if(build!=""){if(sim.PlacementError(build,tile.x,tile.y)=="")selected=sim.Place(build,tile.x,tile.y,direction);else if(Input.GetMouseButtonDown(0))sim.notice=sim.PlacementError(build,tile.x,tile.y);}
                    else{selected=sim.At(tile.x,tile.y);detailScroll=Vector2.zero;modal=selected!=null&&selected.def!="core"&&!sim.IsBay(selected);if(selected!=null&&selected.def=="core")coreRecovery=true;modalScroll=Vector2.zero;}
                }
            }
        }
        void RotateTool()
        {
            if(build!="")
            {
                direction=(direction+(beltDragging?2:1))%4;if(beltDragging)beltFacingOverride=true;GUIUtility.keyboardControl=0;
                sim.notice="Placement direction: "+new[]{"EAST","SOUTH","WEST","NORTH"}[direction]+". R rotates before placing.";
            }
            else if(selected!=null){sim.Rotate(selected);direction=selected.direction;}
        }
        void SetSpeed(int value){speed=value;if(value>0)resumeSpeed=value;}
        void TogglePause(){SetSpeed(speed==0?resumeSpeed:0);}
        void ChangeSpeed(int direction)
        {
            int index=Array.IndexOf(speedSteps,speed==0?resumeSpeed:speed);
            SetSpeed(speedSteps[Mathf.Clamp(index+direction,0,speedSteps.Length-1)]);
        }
        public void Save(bool notify=true)
        {
            try{string temp=SavePath+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(sim.world,true));if(File.Exists(SavePath))File.Copy(SavePath,SavePath+".bak",true);File.Copy(temp,SavePath,true);File.Delete(temp);if(notify)sim.notice="Factory saved";}catch(Exception e){sim.notice="Save failed: "+e.Message;}
        }
        public void Load()
        {
            try{if(!File.Exists(SavePath)){sim.notice="No saved factory yet";return;}var w=JsonUtility.FromJson<World>(File.ReadAllText(SavePath));if(w==null||w.version!=1||w.machines==null||w.machines.Count==0)throw new Exception("Unsupported save");if(w.rulesVersion<2&&!File.Exists(SavePath+".pre-v2.bak"))File.Copy(SavePath,SavePath+".pre-v2.bak");sim=new Simulation(catalog,w);marked.Clear();selected=null;help=false;sim.notice="Factory restored";}catch(Exception e){sim.notice="Load failed: "+e.Message;}
        }
        void OnApplicationQuit(){if(!qa)Save(false);}
        System.Collections.IEnumerator QaCapture()
        {
            string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../..","QA",Screen.width+"x"+Screen.height));Directory.CreateDirectory(path);
            yield return new WaitForSecondsRealtime(2);
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"01-guide.png"));
            yield return new WaitForSecondsRealtime(1);help=false;speed=0;
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"02-landing.png"));
            yield return new WaitForSecondsRealtime(1);
            // Render fixture only: never loaded or saved as a player factory.
            sim.world.cargo.Add("plate",500);sim.world.cargo.Add("wire",500);
            var mine=sim.Place("miner",-18,-4,0);sim.Place("belt",-16,-3,0);sim.Place("belt",-15,-3,0);
            var split=sim.Place("splitter",-14,-3,0);sim.Place("belt",-12,-3,0);sim.Place("belt",-11,-3,0);sim.Place("belt",-10,-3,3);sim.Place("storage",-10,-4,3);
            sim.Place("belt",-12,-2,0);sim.Place("belt",-11,-2,1);sim.Place("belt",-11,-1,1);sim.Place("storage",-11,0,1);
            sim.Place("miner",-15,19,0);sim.Place("generator",-13,19,0);
            foreach(var machine in sim.world.machines){machine.paid=true;machine.built=catalog.Building(machine.def).seconds;}
            mine.output.Add("iron",100);pan=new Vector2(-12,-2);cell=48;
            for(int i=0;i<85;i++)sim.Tick(.05f);
            sim.notice="LOGISTICS · Visible cargo follows belt corners and alternating splitter outputs";
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"03-production.png"));
            for(int i=0;i<3;i++)sim.Tick(.05f);
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"03b-motion.png"));
            selected=split;modal=true;
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"04-splitter-modal.png"));
            modal=false;deleting=true;deleteStart=new Vector2Int(-12,-3);deleteEnd=new Vector2Int(-10,0);
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"05-delete-selection.png"));
            deleting=false;int objectsBefore=sim.world.machines.Count;MarkMachines(DeleteTargets());
            if(sim.world.machines.Count!=objectsBefore)throw new Exception("Marking must not delete");
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"05b-marked.png"));
            DeleteMarked();if(sim.world.machines.Count>=objectsBefore)throw new Exception("Delete selected failed");sim.UndoDelete();selected=null;
            if(sim.world.machines.Count!=objectsBefore)throw new Exception("Undo selected deletion failed");
            buildMenuOpen=false;
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"06-undo.png"));
            cell=22;yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"07-normal-zoom.png"));
            build="belt";yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"08-belt-sockets.png"));
            build="smelter";qaPreview=true;cell=38;yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"09-building-preview.png"));
            direction=1;yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"10-rotated-preview.png"));
            build="belt";direction=0;
            for(int rotation=0;rotation<4;rotation++)
            {
                yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"11-belt-preview-"+rotation+".png"));
                var placed=sim.Place("belt",60+rotation*2,50,direction);
                if(placed==null||placed.direction!=rotation)throw new Exception("Preview/placement rotation mismatch");
                RotateTool();if(direction!=(rotation+1)%4)throw new Exception("Placement rotation failed");
            }
            Debug.Log("FACTORY_PREVIEW_ROTATION_PASSED");
            build="";qaPreview=false;direction=0;sim=new Simulation(catalog);
            sim.world.cargo.Add("plate",-111);sim.world.cargo.Add("wire",-40);sim.world.cargo.Add("refractory",-12);
            selected=sim.Place("smelter",15,-8,0);var warehouse=sim.Place("storage",10,-8,0);warehouse.built=15;warehouse.input.Add("plate",40);
            sim.world.cargo.Add("plate",8);pan=new Vector2(14,-7);cell=48;
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"12-missing-construction.png"));
            modal=true;yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"13-missing-modal.png"));
            modal=false;selected=null;panel="RESEARCH";researchView=-1;
            sim.world.produced.Add("plate",175);sim.world.produced.Add("wire",120);
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"14-research-materials.png"));
            sim.world.cargo.Add("plate",11);sim.StartResearch();if(!sim.world.researching)throw new Exception("Research failed with all requirements");
            for(int i=0;i<400;i++)sim.Tick(.05f);
            SetSpeed(0);yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"15-research-running.png"));
            researchUnlocks=true;yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"16-research-unlocks.png"));
            buildMenuOpen=true;panel="CARGO";build="generator";yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"17-build-shortage.png"));
            OpenRecovery();sim.StartDroneMining("silicate");
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"18-core-recovery.png"));
            sim.world.cargo.Add("silicate",2);sim.StartCoreCraft("emergency_refractory");sim.Tick(.05f);
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"19-core-crafting.png"));
            sim=new Simulation(catalog);modal=false;selected=null;qaPreview=false;build="belt";panel="OPERATIONS";pan=new Vector2(43,40);cell=48;
            int beforeDrag=sim.world.machines.Count;BeginBeltDrag(new Vector2Int(45,40));MoveBeltDrag(new Vector2Int(40,40));
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"20-long-belt-preview.png"));
            MoveBeltDrag(new Vector2Int(43,43));if(BeltPreviewTiles().Count!=3||sim.world.machines.Count!=beforeDrag)throw new Exception("Belt preview must shorten without building");
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"21-short-belt-preview.png"));
            CommitBeltDrag();if(sim.world.machines.Count!=beforeDrag+3||sim.At(40,40)!=null)throw new Exception("Belt release built the wrong route");
            build="";var markedBelt=sim.At(44,40);markStrokeSeen.Clear();markStrokeModeSet=false;PaintMark(markedBelt);PaintMark(markedBelt);
            if(!marked.Contains(markedBelt.uid))throw new Exception("Mark stroke toggled repeatedly");
            markStrokeSeen.Clear();markStrokeModeSet=false;PaintMark(markedBelt);if(marked.Contains(markedBelt.uid))throw new Exception("Click did not unmark");
            Debug.Log("FACTORY_DRAG_AND_UNMARK_PASSED");
            sim=new Simulation(catalog);sim.world.tech=4;build="";modal=false;panel="OPERATIONS";pan=new Vector2(42,41);cell=48;
            var project=sim.Place("fighterPad",40,40,0);selected=project;sim.world.cargo.Add("plate",-100);sim.SupplyProject(project);
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"22-bay-deliveries.png"));
            foreach(var item in catalog.items)sim.world.cargo.Add(item.id,10000);
            var projectGen=sim.Place("industrialGenerator",50,40,0);projectGen.built=180;projectGen.input.Add("carbon",10000);
            for(int phase=0;phase<5;phase++){sim.SupplyProject(project);for(int t=0;t<Simulation.BayPhases[phase].seconds+2;t++)sim.Tick(1);}
            sim.StartFighter(project);sim.SupplyProject(project);sim.Tick(60);
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"23-fighter-assembly.png"));
            for(int phase=0;phase<5;phase++){sim.SupplyProject(project);while(project.assemblyPhase==phase)sim.Tick(1);}
            if(!sim.BootstrapComplete)throw new Exception("QA Fighter failed to finish");sim.Tick(.05f);
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"24-persistent-fighter.png"));
            OpenRecovery();sim.StartDroneMining("iron");sim.StartDroneMining("copperOre");sim.StartDroneMining("silicate");
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"25-multiple-mining-drones.png"));
            Debug.Log("FACTORY_V2_RENDER_PASSED");
            sim=new Simulation(catalog);selected=sim.Place("workshop",40,40,0);selected.built=40;pan=new Vector2(41,41);cell=60;build="belt";modal=false;
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"26-full-edge-ports.png"));
            sim=new Simulation(catalog);selected=null;pan=new Vector2(40,40);cell=60;
            sim.CommitBeltRoute(Simulation.BeltLine(new Vector2Int(37,40),new Vector2Int(43,40),0),0);
            foreach(var m in sim.world.machines)m.built=catalog.Building(m.def).seconds;
            BeginBeltDrag(new Vector2Int(40,37));MoveBeltDrag(new Vector2Int(40,43));
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"27-auto-bridge-preview.png"));
            CommitBeltDrag();build="";
            var ironSource=sim.Place("storage",36,40,0);ironSource.input.Add("iron",30);
            var copperSource=sim.Place("storage",40,36,1);copperSource.input.Add("copper",30);
            sim.Place("storage",44,40,0);sim.Place("storage",40,44,1);
            foreach(var m in sim.world.machines){m.built=catalog.Building(m.def).seconds;m.paid=true;}
            for(int i=0;i<58;i++)sim.Tick(.05f);
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"28-independent-bridge-lanes.png"));
            selected=sim.At(40,40);modal=true;
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"29-bridge-inspector.png"));
            modal=false;build="belt";BeginBeltDrag(new Vector2Int(38,40));MoveBeltDrag(new Vector2Int(42,40));
            yield return new WaitForEndOfFrame();Capture(Path.Combine(path,"30-bridge-over-bridge-blocked.png"));
            beltDragging=false;Debug.Log("FACTORY_BRIDGE_RENDER_PASSED");
            yield return new WaitForSecondsRealtime(.5f);Debug.Log("FACTORY_RENDER_QA_COMPLETE");Application.Quit();
        }
        void Capture(string path)
        {
            var texture=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());Destroy(texture);
        }
        void Styles()
        {
            if(text!=null)return;
            text=new GUIStyle(GUI.skin.label){fontSize=14,normal={textColor=white},wordWrap=true};
            small=new GUIStyle(text){fontSize=11};muted=new GUIStyle(text){normal={textColor=sub}};
            title=new GUIStyle(text){fontSize=26,fontStyle=FontStyle.Bold};sectionTitle=new GUIStyle(title){fontSize=21};heading=new GUIStyle(text){fontSize=13,fontStyle=FontStyle.Bold};
            button=new GUIStyle(GUI.skin.button){fontSize=13,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(12,8,3,3),normal={textColor=white},hover={textColor=Color.white},active={textColor=ink}};
        }
        void OnGUI()
        {
            Styles();GUI.matrix=Matrix4x4.Scale(new Vector3(Scale,Scale,1));
            GUI.enabled=!modal&&!help&&!confirmReset;
            Box(new Rect(0,0,W,H),ink);DrawMap();DrawHeader();DrawBuildMenu();DrawRight();DrawFooter();DrawQuestTracker();DrawBuildWarning();GUI.enabled=true;
            if(modal)DrawBuildingModal();
            if(help)DrawHelp();
            if(confirmReset){Box(new Rect(520,310,560,210),surface);Label(546,333,508,32,"Abandon this factory?",title);Label(546,381,508,42,"This replaces your current save with a new landing.",text);if(Btn(new Rect(546,445,240,42),"Cancel"))confirmReset=false;if(Btn(new Rect(802,445,248,42),"Start new landing",true)){sim=new Simulation(catalog);marked.Clear();selected=null;build="";confirmReset=false;help=true;Save(false);}}
            GUI.matrix=Matrix4x4.identity;
        }
        void DrawHeader()
        {
            Box(new Rect(0,0,W,94),surface);Box(new Rect(0,92,W,2),line);
            Label(22,16,234,20,"B O O T S T R A P",heading,accent);Label(22,37,220,40,"FACTORY",title);
            Label(269,16,230,20,"LANDING SECTOR  /  01",small,sub);Label(269,38,340,30,"From first ore to first flight",text);
            float capacity=sim.generation>0?sim.generation:80;bool powerShort=sim.ratio<.99f||(sim.CoreCrafting&&sim.CoreReservedPower<79.9f);
            Label(622,16,180,22,"POWER GRID",small,sub);Label(622,38,240,27,$"{sim.demand+sim.critical+sim.CoreRequestedPower:0} / {capacity:0} PU",heading,powerShort?accent:green);
            Bar(new Rect(620,70,204,4),Mathf.Clamp01((sim.demand+sim.critical+sim.CoreRequestedPower)/capacity),powerShort?accent:green);
            Label(868,16,160,22,"CONSTRUCTION",small,sub);Label(868,40,180,24,$"{sim.AvailableBuilders} builders   /   {sim.world.machines.Count(m=>!sim.Complete(m))} queued",text);
            Label(1080,16,206,20,speed==0?"DEV SPEED / PAUSED":"DEV SPEED / "+speed+"x",small,accent);
            int[] speeds={0,1,4,16,64,256};for(int i=0;i<speeds.Length;i++)if(Btn(new Rect(1080+i*34,39,32,31),speeds[i]==0?"II":speeds[i].ToString(),speed==speeds[i])){if(speeds[i]==0)TogglePause();else SetSpeed(speeds[i]);}
            Label(1080,75,206,17,"Multipliers   ·   +/- to change",small,sub);
            if(Btn(new Rect(1291,26,88,40),"F5 Save"))Save();if(Btn(new Rect(1386,26,88,40),"F9 Load"))Load();if(Btn(new Rect(1481,26,94,40),"? Guide"))help=true;
        }
        void DrawBuildMenu()
        {
            Box(new Rect(10,102,224,712),surface);
            if(Btn(new Rect(20,113,204,38),buildMenuOpen?"BUILD MENU   [Tab]   Close":"BUILD MENU   [Tab]   Open",buildMenuOpen))buildMenuOpen=!buildMenuOpen;
            if(buildMenuOpen)
            {
                string[] cats={"START","EXPAND","FLIGHT"};for(int i=0;i<3;i++)if(Btn(new Rect(20+i*69,163,67,30),cats[i],category==i))category=i;
                Label(24,203,192,38,"Choose a building. R rotates its ports. Escape cancels placement.",small,sub);
                var buildings=catalog.buildings.Where(b=>b.id!="core"&&(category==0?b.tech==0:category==1?b.tech>0&&b.tech<4:b.tech==4)).ToArray();
                buildScroll=GUI.BeginScrollView(new Rect(18,248,208,340),buildScroll,new Rect(0,0,188,buildings.Length*108));
                int row=0;foreach(var b in buildings)
                {
                    float y=row++*108;bool unlocked=b.tech<=sim.world.tech;var r=new Rect(0,y,184,100);Box(r,build==b.id?new Color(.22f,.24f,.18f):ink);Box(new Rect(0,y,3,100),unlocked?BuildingColor(b.family):line);
                    Label(12,y+6,160,33,b.name,heading,unlocked?white:sub);Label(12,y+43,160,19,unlocked?$"{b.power:0} PU   /   {b.seconds:0}s   /   {b.size}x{b.size}":"Research level "+b.tech,small,sub);
                    string shortage=BuildShortage(b.id);Label(12,y+65,160,32,unlocked?(b.id=="belt"&&sim.world.beltKits>0?"Starter kit available":shortage==""?"Materials ready":"Need: "+shortage):"",small,shortage==""?green:accent);
                    if(GUI.Button(r,"",GUIStyle.none)){if(unlocked){build=b.id;panel="OPERATIONS";GUIUtility.keyboardControl=0;deleteMode=false;sim.notice=Cost(b.cost);selected=null;modal=false;}else sim.notice="Research "+catalog.techs[b.tech-1].name+" to unlock";}
                }
                GUI.EndScrollView();
            }
            else
            {
                Label(25,180,190,30,"MACHINE CONNECTIONS",heading,accent);
                Label(25,222,190,50,"CYAN SQUARE\nBelt feeds the machine.",text,new Color(.3f,.88f,.96f));
                Label(25,291,190,50,"ORANGE SQUARE\nBelt receives output.",text,accent);
                Label(25,370,190,90,"Select any building tool to show connection tiles. Building previews show their own squares. Inputs need a belt facing in; outputs allow side drops. R rotates.",text,sub);
                if(build!="")Label(25,482,190,80,"PLACING\n"+catalog.Building(build).name+"\nEscape to cancel",text,accent);
            }
            Label(25,602,190,25,$"MARKED FOR DELETE: {marked.Count}",heading,accent);
            if(Btn(new Rect(24,632,192,30),deleteMode?"X  Mark mode ON":"X  Mark for deletion",deleteMode)){deleteMode=!deleteMode;build="";}
            bool enabled=GUI.enabled;GUI.enabled=enabled&&marked.Count>0;
            if(Btn(new Rect(24,669,192,35),$"Delete [Del] ({marked.Count})",true))DeleteMarked();
            GUI.enabled=enabled;
            if(Btn(new Rect(24,711,93,29),"Clear marks"))marked.Clear();if(Btn(new Rect(123,711,93,29),"Undo delete")){sim.UndoDelete();selected=null;}
            Label(24,746,192,25,"Click marked items to unmark. Del deletes.",small,sub);
            if(Btn(new Rect(24,776,124,27),"Core / Recovery [C]",true))OpenRecovery();if(Btn(new Rect(154,776,62,27),"Reset"))confirmReset=true;
        }
        void DrawMap()
        {
            GUI.BeginGroup(map);var p=pan;float halfW=map.width/2,halfH=map.height/2;
            int xmin=Mathf.FloorToInt(p.x-halfW/cell)-1,xmax=Mathf.CeilToInt(p.x+halfW/cell)+1,ymin=Mathf.FloorToInt(p.y-halfH/cell)-1,ymax=Mathf.CeilToInt(p.y+halfH/cell)+1;
            for(int y=ymin;y<=ymax;y++)for(int x=xmin;x<=xmax;x++)
            {
                Rect r=TileRect(x,y);int h=Math.Abs(unchecked(x*73856093^y*19349663));GUI.DrawTexture(r,ground[h%8]);if(x< -120||x>=120||y< -90||y>=90)Box(r,ink);
                Box(new Rect(r.x,r.y,1,r.height),new Color(.17f,.21f,.18f));Box(new Rect(r.x,r.y,r.width,1),new Color(.17f,.21f,.18f));
                string resource=sim.Resource(x,y);if(resource!="")
                {Color c=ItemColor(resource);for(int i=0;i<3;i++){float a=(h>>(i*3))%9;float b=(h>>(i*5))%11;Rect ore=new Rect(r.x+4+a,r.y+4+b,cell*.32f,cell*.26f);Box(new Rect(ore.x+2,ore.y+2,ore.width,ore.height),new Color(.07f,.09f,.08f));Box(ore,c*(.7f+i*.13f));}}
                else if(h%43==0&&sim.At(x,y)==null){Box(new Rect(r.x+7,r.y+11,cell*.22f,cell*.13f),new Color(.21f,.25f,.2f));}
            }
            foreach(var deposit in sim.deposits)PatchLabel(deposit.x,deposit.y-deposit.ry-2,catalog.Name(deposit.item).ToUpper(),ItemColor(deposit.item));
            foreach(var m in sim.world.machines.OrderBy(m=>m.raised?2:m.def=="belt"?0:1)){Rect bounds=TileRect(m.x,m.y);bounds.width=bounds.height=catalog.Building(m.def).size*cell;if(bounds.Overlaps(new Rect(0,0,map.width,map.height)))DrawMachine(m);}
            DrawBeltSockets();
            DrawDeleteSelection();
            // Four small autonomous builders orbit the core or their individual construction sites.
            var jobs=sim.world.machines.Where(m=>!sim.Complete(m)&&m.paid&&m.enabled&&(!sim.IsBay(m)||m.phasePaid||m.input.Has(sim.CurrentPhase(m).cost))).Take(sim.AvailableBuilders).ToArray();
            for(int i=0;i<Math.Min(sim.AvailableBuilders,12);i++){float t=Time.unscaledTime*(i%2==0?1:-1)+i*1.57f;Vector2 pos=i<jobs.Length?new Vector2(jobs[i].x+.6f,jobs[i].y+.6f):new Vector2(1.5f,1.5f);pos+=new Vector2(Mathf.Cos(t),Mathf.Sin(t))*(i<jobs.Length?.6f:2.3f);Rect r=TileRect(pos.x,pos.y);Box(new Rect(r.x+3,r.y+5,13,8),ink);Box(new Rect(r.x,r.y,13,8),accent);Box(new Rect(r.x+4,r.y-3,5,14),white);}
            int miningIndex=0;foreach(var mining in sim.world.mining)
            {
                float angle=Time.unscaledTime+miningIndex++*1.7f;Rect target=TileRect(mining.x+.5f,mining.y+.5f);Vector2 pos=target.position+new Vector2(Mathf.Sin(angle)*12,Mathf.Cos(angle)*12);
                Box(new Rect(pos.x-6,pos.y-4,12,8),accent);Box(new Rect(pos.x-2,pos.y-8,4,16),white);
                Bar(new Rect(target.x-12,target.y+14,24,3),mining.progress/Simulation.DroneMiningSeconds,green);
            }
            DrawBeltDrag();
            if((map.Contains(Mouse)||(qa&&qaPreview))&&!help&&!modal&&!deleting&&!beltDragging)
            {
                Vector2Int tile=qa&&qaPreview?new Vector2Int(-8,-2):Tile(Mouse);var b=build!=""?catalog.Building(build):null;int size=b?.size??1;Rect r=TileRect(tile.x,tile.y);r.width=cell*size;r.height=cell*size;
                bool valid=build==""||(build=="belt"?sim.BeltRouteError(new[]{tile})=="":sim.PlacementError(build,tile.x,tile.y)=="");Color c=deleteMode?new Color(1,.3f,.2f):valid?accent:new Color(1,.3f,.2f);
                if(b!=null&&b.family=="belt")
                {
                    DrawBelt(new Machine{def="belt",x=tile.x,y=tile.y,direction=direction},r);
                    Arrow(r.center,direction,valid?white:c,Mathf.Clamp(cell*.38f,8,20));
                    Vector2 exit=r.center+new Vector2(Simulation.DX[direction],Simulation.DY[direction])*cell*.72f;
                    Arrow(exit,direction,c,Mathf.Clamp(cell*.25f,6,12));
                }
                else {Box(r,new Color(c.r,c.g,c.b,.12f));if(b!=null){Arrow(r.center,direction,c,10);DrawPlacementPorts(b.id,tile.x,tile.y,direction);}}
                Border(r,c,2);
            }
            GUI.EndGroup();
            Box(new Rect(map.x+16,map.y+16,235,48),new Color(.035f,.055f,.06f,.9f));Label(map.x+29,map.y+24,220,20,build!=""?catalog.Building(build).name:deleteMode?"MARK FOR DELETE":"LANDING SECTOR",heading,accent);Label(map.x+29,map.y+45,220,16,build!=""?"Output: "+new[]{"EAST","SOUTH","WEST","NORTH"}[direction]:"WASD pan  ·  Scroll zoom  ·  Home center",small,sub);
            DrawMinimap();
        }
        void PatchLabel(int x,int y,string value,Color color){Rect r=TileRect(x,y);Label(r.x-30,r.y,150,20,value,small,color);}
        void DrawMachine(Machine m)
        {
            var b=catalog.Building(m.def);Rect r=TileRect(m.x,m.y);r.width=b.size*cell;r.height=b.size*cell;
            if(m.raised){DrawBridge(m,r);return;}
            bool complete=sim.Complete(m);if(sim.IsBay(m)&&!complete)
            {
                Box(r,ink);Border(r,accent,2);float inset=5+Math.Min(m.projectPhase,3)*3;
                Border(new Rect(r.x+inset,r.y+inset,r.width-2*inset,r.height-2*inset),sub,3);
                Label(r.x+6,r.y+8,r.width-12,24,"BAY "+(m.projectPhase+1)+"/5",small,accent);
                var phase=sim.CurrentPhase(m);Bar(new Rect(r.x+5,r.yMax-9,r.width-10,4),(m.projectPhase+m.phaseProgress/phase.seconds)/5,green);
                if(m==selected)Border(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),accent,2);return;
            }
            if(!complete){DrawConstructionSite(m,r);return;}Color c=BuildingColor(b.family);
            if(m==selected)Border(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),accent,2);
            if(b.family=="belt")
            {
                DrawBelt(m,r);
            }
            else
            {
                Box(new Rect(r.x+5,r.y+7,r.width-5,r.height-5),new Color(0,0,0,.35f));Box(new Rect(r.x+2,r.y+2,r.width-5,r.height-5),ink);
                Rect body=new Rect(r.x+6,r.y+6,r.width-13,r.height-13);Box(body,c*.62f);Box(new Rect(body.x,body.y,body.width,4),c);
                foreach(Vector2 q in new[]{new Vector2(body.x+3,body.y+6),new Vector2(body.xMax-6,body.y+6),new Vector2(body.x+3,body.yMax-6),new Vector2(body.xMax-6,body.yMax-6)})Box(new Rect(q.x,q.y,3,3),white*.6f);
                Vector2 center=body.center;
                if(b.family=="miner")
                {Box(new Rect(center.x-5,body.y+9,10,body.height-14),ink);float angle=m.enabled?Time.unscaledTime*90:0;for(int i=0;i<4;i++){float a=(angle+i*90)*Mathf.Deg2Rad;Line(center,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*body.width*.32f,c,5);}Circle(center,6,white);}
                else if(b.family.Contains("melter"))
                {Box(new Rect(body.x+7,body.y+9,body.width-14,body.height-17),ink);Circle(center,body.width*.24f,m.working?new Color(1,.4f,.1f):new Color(.35f,.21f,.14f));Circle(center,body.width*.13f,m.working?accent:c);Box(new Rect(body.xMax-12,body.y-5,10,18),new Color(.3f,.35f,.34f));}
                else if(b.family=="generator")
                {for(int i=0;i<3;i++){Rect engine=new Rect(body.x+7+i*(body.width-14)/3,body.y+12,(body.width-20)/3,body.height-20);Box(engine,ink);Box(new Rect(engine.x+2,engine.y+3,engine.width-4,engine.height-6),c);}Circle(center,4,m.fuelSeconds>0?green:accent);}
                else if(b.family=="core")
                {Box(new Rect(body.x+8,center.y-8,body.width-16,16),ink);Box(new Rect(center.x-8,body.y+8,16,body.height-16),ink);Circle(center,body.width*.22f,c);Circle(center,body.width*.10f,white);Line(new Vector2(center.x,body.y+12),new Vector2(center.x+8,body.y-10),white,2);}
                else if(b.family=="fighter")
                {Border(new Rect(body.x+8,body.y+8,body.width-16,body.height-16),c,2);if(m.assemblyStarted||sim.world.fighter?.bayId==m.uid){Line(new Vector2(center.x-20,center.y),new Vector2(center.x+20,center.y),white,8);Line(new Vector2(center.x,center.y-26),new Vector2(center.x,center.y+26),white,10);Line(new Vector2(center.x-12,center.y+19),new Vector2(center.x+12,center.y+19),white,5);}else Label(body.x+9,center.y-10,body.width-18,23,"BAY",small,sub);}
                else if(b.family=="splitter"){Label(body.x+6,body.y+6,body.width-12,22,"1 → 2",heading,accent);Circle(center,6,accent);}
                else if(b.family=="storage"){Border(new Rect(body.x+3,body.y+6,body.width-6,body.height-9),c,2);}
                else if(b.family=="pole"){Circle(center,4,accent);}
                else{Box(new Rect(body.x+8,body.y+11,body.width-16,body.height-20),ink);Circle(center,body.width*.2f,c);Line(center,center+new Vector2(Mathf.Sin(Time.unscaledTime*2),Mathf.Cos(Time.unscaledTime*2))*body.width*.19f,white,3);}

                if(complete){Circle(new Vector2(r.x+9,r.yMax-8),3,!m.enabled?sub:m.status=="Needs inputs"||m.status=="Needs Carbon"?accent:green);}
            }
            if(!complete){Bar(new Rect(r.x+3,r.yMax-5,r.width-6,3),m.built/Math.Max(1,b.seconds),accent);}
            else if(m.working){var rec=catalog.Recipe(m.recipe);Bar(new Rect(r.x+6,r.yMax-5,r.width-12,3),m.progress/rec.seconds,green);}
        }
        void DrawRight()
        {
            Box(new Rect(1268,102,322,712),surface);
            if(build!="")panel="OPERATIONS";
            string[] tabs={"OPERATIONS","CARGO","RESEARCH"};for(int i=0;i<3;i++)if(Btn(new Rect(1279+i*101,113,98,32),tabs[i],panel==tabs[i])){panel=tabs[i];if(i>0)build="";detailScroll=Vector2.zero;}
            if(build!=""){DrawBlueprint();return;}
            if(panel=="CARGO"){DrawCargo();return;}if(panel=="RESEARCH"){DrawResearch();return;}
            if(selected==null){DrawObjectives();return;}
            if(selected.def=="core"&&coreRecovery){DrawCoreRecovery();return;}
            if(selected.def=="splitter"&&sim.Complete(selected)){Label(1287,170,280,55,"Six-port splitter",sectionTitle);if(Btn(new Rect(1287,250,280,42),"Configure ports"))modal=true;return;}
            var m=selected;var b=catalog.Building(m.def);
            if(sim.IsBay(m)){DrawProject(m,new Rect(1287,165,280,630));return;}
            Label(1287,165,280,52,b.name,sectionTitle);Label(1287,218,280,25,$"{m.x}, {m.y}   /   {b.size} x {b.size}   /   {b.power:0} PU",small,sub);
            Label(1287,250,280,26,!sim.Complete(m)?m.status??"Construction queued":m.status??"Standby",heading,accent);
            if(!sim.Complete(m))
            {
                Label(1287,281,280,24,ConstructionState(m),heading,accent);
                DrawConstructionMaterials(m,new Rect(1287,316,285,285));
                Bar(new Rect(1287,620,280,6),m.built/Math.Max(1,b.seconds),accent);
                Label(1287,647,280,40,$"Construction {m.built:0} / {b.seconds:0}s",text);
                Label(1287,702,280,85,"Only Command Cargo pays construction. Collect materials from storage or machine outputs. Drones begin automatically when supplied.",small,sub);return;
            }
            if(Btn(new Rect(1287,286,132,34),m.enabled?"Pause machine":"Enable",!m.enabled))m.enabled=!m.enabled;
            if(Btn(new Rect(1427,286,143,34),"Rotate [R]")){sim.Rotate(m);direction=m.direction;}
            if(m.def=="belt")
            {Label(1287,337,280,58,$"T1.{m.tier}  ·  {m.tier+2} items/sec\n4 PU per lane; crossings use 8 PU. No bridge over a bridge.",text);if(Btn(new Rect(1287,410,283,38),"Upgrade belt"))sim.Upgrade(m);return;}
            if(m.def=="core"||b.family=="storage")
            {
                Label(1287,340,280,40,"OUTPUT FILTER",heading,accent);Label(1287,371,280,56,"Choose cargo for the orange output. Cyan arrows are inputs.",text);
                if(m.def=="core"&&Btn(new Rect(1287,774,280,28),"Open Recovery / mining and crafting"))OpenRecovery();
                if(b.family=="storage"&&Btn(new Rect(1287,774,280,28),"Collect stored cargo ("+m.input.Total+"/40)"))sim.Collect(m);
                detailScroll=GUI.BeginScrollView(new Rect(1287,436,285,320),detailScroll,new Rect(0,0,261,catalog.items.Length*30+32));
                if(Btn(new Rect(0,0,255,28),"No filter",m.filter==""))m.filter="";int row=1;foreach(var i in catalog.items)if(Btn(new Rect(0,row++*30,255,28),i.name,m.filter==i.id))m.filter=i.id;GUI.EndScrollView();return;
            }
            if(Btn(new Rect(1287,330,132,36),"Feed inputs"))sim.Feed(m);if(Btn(new Rect(1427,330,143,36),"Collect output"))sim.Collect(m);
            Label(1287,384,280,20,"MACHINE BUFFERS",small,sub);
            Label(1287,410,280,70,"IN   "+InventoryText(m.input)+"\nOUT   "+InventoryText(m.output),small);
            var recipe=catalog.Recipe(m.recipe);
            if(recipe!=null){Label(1287,489,280,66,Cost(recipe.inputs)+"\n→ "+Cost(recipe.outputs)+$"  /  {recipe.seconds:0.#}s",small,sub);Bar(new Rect(1287,557,280,5),m.progress/recipe.seconds,green);}
            Label(1287,575,280,22,"SELECT RECIPE",heading,accent);
            var recipes=catalog.recipes.Where(r=>r.family==b.family&&r.tech<=sim.world.tech).ToArray();
            detailScroll=GUI.BeginScrollView(new Rect(1287,607,285,188),detailScroll,new Rect(0,0,260,recipes.Length*36));
            for(int i=0;i<recipes.Length;i++)if(Btn(new Rect(0,i*36,256,32),recipes[i].name,m.recipe==recipes[i].id))sim.SetRecipe(m,recipes[i].id);GUI.EndScrollView();
        }
        void DrawBlueprint()
        {
            var b=catalog.Building(build);if(build=="fighterPad"){Label(1287,170,280,65,b.name,sectionTitle);Label(1287,253,280,500,"Place a permanent project footprint.\n\nFive sequential phases: 8.7 minutes minimum, plus material production.\n\nFoundation: 180 Plate, 120 Beam, 60 Frame, 36 Refractory.\n\nPlace first, select the project, then deliver materials. Partial deliveries remain stored. One drone executes each Bay phase.\n\nPower varies from 20 to 120 PU; insufficient power stops work.\n\nThe commissioned Bay assembles one persistent Fighter in five further phases.",text,sub);return;}Label(1287,170,280,60,b.name,sectionTitle);
            if(Btn(new Rect(1287,213,280,30),"Rotate [R] / "+new[]{"EAST","SOUTH","WEST","NORTH"}[direction],true))RotateTool();
            Label(1287,247,280,24,"CONSTRUCTION MANIFEST",heading,accent);int row=0;
            foreach(var s in b.cost){int count=sim.world.cargo.Get(s.item),missing=Math.Max(0,s.amount-count);bool kit=b.id=="belt"&&sim.world.beltKits>0;Label(1287,287+row++*36,280,32,kit?"Paid with a starter belt kit":$"{catalog.Name(s.item)}   {count} / {s.amount}"+(missing>0?$"   Missing {missing}":"   Ready"),small,missing==0||kit?green:accent);}
            float y=302+row*36;Label(1287,y,280,72,$"{b.seconds:0} seconds  ·  One construction drone\n{b.size} x {b.size} tiles  ·  {b.power:0} PU",text);
            Label(1287,y+88,280,126,b.family=="miner"?"Place on a colored resource deposit. The resource under the top-left tile determines its output.":b.family=="belt"?$"Hold and drag a straight preview; release to build. Drag back to shorten. R reverses during a drag. Drag through perpendicular belts to auto-bridge. Existing bridges block crossing; single clicks reorient plain belts.\n\nStarter kits remaining: {sim.world.beltKits}":b.family=="generator"?$"Consumes {b.fuelRate:0} Carbon/minute to generate {b.generation:0} PU. Feed Carbon manually or connect a belt.":"Connect orange OUT ports to cyan IN ports. R rotates all ports. Select the finished machine to choose its recipe.",text,sub);
            Label(1287,740,280,50,"Missing materials leave a waiting blueprint. Reclaim returns supplied materials.",small,sub);
        }
        void DrawObjectives()
        {
            Label(1287,169,280,24,"THE FIRST FLIGHT",heading,accent);Label(1287,201,280,66,"Build an industry.\nPrepare for flight.",title);
            string[] steps={"Establish iron → steel → plate","Power expansion with Carbon","Produce Copper Wire","Research Basic Industry I","Build specialized production","Unlock industrial construction","Prepare flight infrastructure","Assemble Fighter Mk1"};
            bool[] done={sim.world.produced.Get("plate")>=20,sim.world.machines.Any(m=>catalog.Building(m.def).family=="generator"&&sim.Complete(m)),sim.world.produced.Get("wire")>=10,sim.world.tech>=1,sim.world.tech>=2,sim.world.tech>=3,sim.world.tech>=4,sim.BootstrapComplete};
            for(int i=0;i<steps.Length;i++){float y=282+i*36;Circle(new Vector2(1297,y+10),7,done[i]?green:line);Label(1315,y,251,32,steps[i],text,done[i]?green:white);}
            Label(1287,582,280,20,"RAID-READY STOCK",small,sub);
            int j=0;foreach(var s in Simulation.ReadyStock){int count=sim.ReadyAmount(s.item);Label(1287,606+j++*23,280,22,$"{catalog.Name(s.item)}    {count} / {s.amount}",small,count>=s.amount?green:sub);}
            if(sim.RaidReady&&!sim.world.won){if(Btn(new Rect(1287,773,282,31),"Confirm raid readiness",true)){sim.world.won=true;sim.notice="MISSION COMPLETE · Fighter Mk1 and raid reserves ready. Raids begin beyond this prototype.";Save(false);}}
            else if(sim.world.won)Label(1287,775,280,25,"MISSION COMPLETE",heading,green);
        }
        void DrawCargo()
        {
            Label(1287,170,280,27,"COMMAND CARGO",heading,accent);Label(1287,204,280,51,"Building and research costs use this stock. Collect machine output or route belts into the Core.",small,sub);
            search=GUI.TextField(new Rect(1287,266,280,29),search);var list=catalog.items.Where(i=>string.IsNullOrEmpty(search)||i.name.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0).ToArray();
            cargoScroll=GUI.BeginScrollView(new Rect(1287,309,285,461),cargoScroll,new Rect(0,0,260,list.Length*39));int row=0;foreach(var i in list){float y=row++*39;Box(new Rect(0,y,260,35),ink);Circle(new Vector2(13,y+17),5,ItemColor(i.id));Label(28,y+8,183,24,i.name,small);Label(211,y+7,48,24,sim.world.cargo.Get(i.id).ToString(),heading,accent);}GUI.EndScrollView();Label(1287,781,280,22,$"Reserved belt kits: {sim.world.beltKits}",small,sub);
        }
        void DrawResearch()
        {
            int view=researchView<0?Math.Min(sim.world.tech,3):researchView;
            for(int i=0;i<catalog.techs.Length;i++)
            {
                var entry=catalog.techs[i];string state=i<sim.world.tech?"DONE":i==sim.world.tech?"NEXT":"LOCKED";
                if(Btn(new Rect(1287,167+i*35,282,31),entry.name+" / "+state,view==i))researchView=i;
            }
            var t=catalog.techs[view];bool complete=view<sim.world.tech,active=view==sim.world.tech,running=active&&sim.world.researching,funded=complete||running;
            Label(1287,315,280,38,t.name,sectionTitle);
            if(Btn(new Rect(1287,354,136,29),"Requirements",!researchUnlocks))researchUnlocks=false;
            if(Btn(new Rect(1431,354,136,29),"Unlocks",researchUnlocks))researchUnlocks=true;
            if(researchUnlocks)DrawResearchUnlocks(t.id);
            else
            {
            if(view==0)
            {
                Label(1287,403,280,23,$"Produced Plate {sim.world.produced.Get("plate")}/20 | Wire {sim.world.produced.Get("wire")}/10",small,sim.world.produced.Get("plate")>=20&&sim.world.produced.Get("wire")>=10?green:accent);
            }
            else Label(1287,385,280,40,active||complete?"Prerequisite technology completed.":"Requires: "+catalog.techs[view-1].name,small,active||complete?green:accent);
            Label(1287,427,280,20,funded?"MATERIALS PAID":"MATERIALS / COMMAND CARGO",heading,accent);
            for(int i=0;i<t.cost.Length;i++)
            {
                var cost=t.cost[i];int have=sim.world.cargo.Get(cost.item),missing=funded?0:Math.Max(0,cost.amount-have);
                int elsewhere=sim.world.machines.Sum(m=>m.output.Get(cost.item)+(catalog.Building(m.def).family=="storage"?m.input.Get(cost.item):0));
                float y=450+i*42;Color color=missing>0?accent:green;
                Label(1287,y,280,19,catalog.Name(cost.item),heading,color);
                string count=funded?$"{cost.amount}/{cost.amount} Paid":$"{have}/{cost.amount}"+(missing>0?$" / Missing {missing}":" / Ready")+(elsewhere>0?$" / {elsewhere} uncollected":"");
                Label(1287,y+19,280,20,count,small,color);Bar(new Rect(1287,y+39,280,2),funded?1:have/(float)cost.amount,color);
            }
            }
            float progress=complete?1:running?sim.world.research/t.seconds:0;
            Label(1287,674,280,22,complete?"RESEARCH COMPLETE":$"{progress*100:0}%   /   {(running?sim.world.research:0):0}s of {t.seconds:0}s   /   {t.power:0} PU",heading,complete?green:accent);
            Bar(new Rect(1287,703,280,6),progress,green);
            string reason=complete?"Technology unlocked.":!active?"Complete the previous technology first.":running?(speed==0?"Paused. Press Space to resume.":$"Researching at {sim.ratio*100:0}% power. Materials already paid."):sim.ResearchError();
            if(reason=="")reason="Ready. Start research below.";
            Label(1287,718,280,44,reason,small,sub);
            bool enabled=GUI.enabled;GUI.enabled=enabled&&active&&!running&&sim.ResearchError()=="";
            if(Btn(new Rect(1287,769,280,33),running?"Research in progress":"Start research",true))sim.StartResearch();GUI.enabled=enabled;
        }
        void DrawFooter()
        {
            Box(new Rect(10,824,1580,66),surface);Box(new Rect(10,824,3,66),accent);
            Label(27,835,1040,23,sim.notice,text);Label(27,863,1120,19,"LMB build / inspect    ·    RMB drag mark / Del delete / Ctrl+Z undo    ·    R rotate    ·    B belt    ·    X mark    ·    SPACE pause    ·    +/- dev speed    ·    F1 guide",small,sub);
            Label(1225,837,340,23,sim.CoreCrafting?$"CORE CRAFT / {sim.CoreReservedPower/2:0}% SPEED":sim.ratio<.99f?$"BROWNOUT  ·  {sim.ratio*100:0}% operating speed":"GRID STABLE",heading,sim.ratio<.99f?accent:green);
            Label(1225,864,340,20,"SOL 01   /   "+TimeSpan.FromSeconds(sim.world.time).ToString(@"hh\:mm\:ss"),small,sub);
        }
        void DrawHelp()
        {
            Box(new Rect(0,0,W,H),new Color(.02f,.035f,.04f,.86f));Rect r=new Rect(346,150,908,600);Box(r,surface);Box(new Rect(r.x,r.y,5,r.height),accent);
            Label(384,178,830,24,"FIELD MANUAL  /  BOOTSTRAP PROTOCOL",heading,accent);Label(384,219,810,49,"A factory begins with one working loop.",title);
            Label(384,289,394,103,"01   MINE & REFINE\nPlace a Basic Miner on the iron patch. Put a Smelter and Workshop nearby. The orange arrow marks each output port.",text);
            Label(820,289,394,103,"02   CONNECT OR CARRY\nConnect orange outputs to cyan inputs. Belts accept rear/side drop-offs and turn toward their arrow. A splitter selects one input and two outputs from six ports.",text);
            Label(384,414,394,103,"03   GROW YOUR POWER\nThe Core supplies 80 PU. A Carbon Miner plus Generator adds 160 PU. Empty belts still draw 4 PU each. Reclaim returns construction materials.",text);
            Label(820,414,394,103,"04   BUILD TOWARD FLIGHT\nProduce 20 Plate and 10 Wire, then research. Complete five Bay phases, then five assembly phases for one persistent Fighter. Stock fuel, ammo and repair parts.",text);
            Label(384,540,820,45,"Stuck? Press C. Assign mining drones with + / -, then make emergency Plate, Wire or Refractory. Each batch takes 150s on Core power alone.",small,sub);
            Label(384,601,820,27,"WASD pan · Wheel zoom · R rotate · RMB drag mark · Ctrl+Z undo · +/- dev speed · Space pause · F5/F9 save/load",small,sub);
            if(Btn(new Rect(384,656,350,48),"Begin landing operations   →",true)){help=false;}
            Label(760,665,444,40,"Simulation is paused while this guide is open.",small,sub);
        }
        Rect TileRect(float x,float y){return new Rect((x-pan.x)*cell+map.width/2,(y-pan.y)*cell+map.height/2,cell,cell);}
        Vector2Int Tile(Vector2 p){return new Vector2Int(Mathf.FloorToInt((p.x-map.x-map.width/2)/cell+pan.x),Mathf.FloorToInt((p.y-map.y-map.height/2)/cell+pan.y));}
        string InventoryText(Inventory i){var a=i.stacks.Where(s=>s.amount>0).Select(s=>s.amount+" "+catalog.Name(s.item));string t=string.Join(", ",a);return t==""?"Empty":t;}
        string Cost(Stack[] list){return string.Join(" + ",list.Select(s=>s.amount+" "+catalog.Name(s.item)));}
        Color BuildingColor(string family)
        {switch(family){case "core":return new Color(.39f,.73f,.73f);case "miner":return new Color(.75f,.64f,.39f);case "smelter":case "industrialSmelter":return new Color(.77f,.42f,.26f);case "belt":return new Color(.75f,.65f,.31f);case "generator":return new Color(.47f,.66f,.44f);case "electrical":return new Color(.4f,.65f,.8f);case "fluid":case "fuel":return new Color(.49f,.64f,.69f);case "fighter":return new Color(.7f,.75f,.78f);default:return new Color(.6f,.64f,.6f);}}
        Color ItemColor(string id){switch(id){case "iron":case "steel":case "plate":return new Color(.51f,.69f,.76f);case "copperOre":case "copper":case "wire":return new Color(.84f,.46f,.25f);case "carbon":return new Color(.32f,.36f,.4f);case "silicate":case "refractory":return new Color(.72f,.7f,.52f);default:return accent;}}
        void Box(Rect r,Color c){Color old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
        void Border(Rect r,Color c,float n){Box(new Rect(r.x,r.y,r.width,n),c);Box(new Rect(r.x,r.yMax-n,r.width,n),c);Box(new Rect(r.x,r.y,n,r.height),c);Box(new Rect(r.xMax-n,r.y,n,r.height),c);}
        void Circle(Vector2 p,float radius,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(new Rect(p.x-radius,p.y-radius,radius*2,radius*2),disc);GUI.color=old;}
        void Line(Vector2 a,Vector2 b,Color c,float width)
        {
            if(Event.current.type!=EventType.Repaint)return;
            Vector2 delta=b-a;
            // Never rotate GUI.matrix inside a scaled BeginGroup: Unity's pivot conversion
            // mixes group and screen coordinates at non-unit UI scales.
            if(Mathf.Abs(delta.y)<.001f){Box(new Rect(Mathf.Min(a.x,b.x),a.y-width/2,Mathf.Abs(delta.x),width),c);return;}
            if(Mathf.Abs(delta.x)<.001f){Box(new Rect(a.x-width/2,Mathf.Min(a.y,b.y),width,Mathf.Abs(delta.y)),c);return;}
            Vector2 n=new Vector2(-delta.y,delta.x).normalized*(width/2);
            Vector2[] corners={a+n,b+n,b-n,a-n};
            float bottom=corners.Min(p=>p.y),top=corners.Max(p=>p.y),step=1f/Scale;
            // Rasterize the diagonal stroke with horizontal spans in the same local
            // coordinate space as tiles, cargo and the placement outline.
            for(float y=bottom;y<top;y+=step)
            {
                float h=Mathf.Min(step,top-y),scan=y+h/2,left=float.PositiveInfinity,right=float.NegativeInfinity;
                for(int i=0;i<4;i++)
                {
                    Vector2 start=corners[i],end=corners[(i+1)%4];
                    if(scan<Mathf.Min(start.y,end.y)||scan>=Mathf.Max(start.y,end.y))continue;
                    float x=start.x+(scan-start.y)*(end.x-start.x)/(end.y-start.y);
                    left=Mathf.Min(left,x);right=Mathf.Max(right,x);
                }
                if(right>left)Box(new Rect(left,y,right-left,h),c);
            }
        }
        void Arrow(Vector2 p,int d,Color c,float size){Vector2 v=new Vector2(Simulation.DX[d],Simulation.DY[d]);Vector2 normal=new Vector2(-v.y,v.x);Line(p-v*size*.6f,p+v*size*.5f,c,2);Line(p+v*size*.5f,p-v*size*.2f+normal*size*.5f,c,2);Line(p+v*size*.5f,p-v*size*.2f-normal*size*.5f,c,2);}
        void Label(float x,float y,float w,float h,string value,GUIStyle style,Color? color=null){var old=style.normal.textColor;if(color.HasValue)style.normal.textColor=color.Value;GUI.Label(new Rect(x,y,w,h),value,style);style.normal.textColor=old;}
        bool Btn(Rect r,string value,bool active=false){Box(r,active?new Color(.33f,.29f,.19f):new Color(.13f,.17f,.19f));Border(r,active?accent:line,1);Label(r.x+8,r.y+Math.Max(3,(r.height-20)/2),r.width-12,r.height-4,value,small,active?accent:white);return GUI.Button(r,"",GUIStyle.none);}
        void Bar(Rect r,float progress,Color c){Box(r,line);Box(new Rect(r.x,r.y,r.width*Mathf.Clamp01(progress),r.height),c);}
    }
}
