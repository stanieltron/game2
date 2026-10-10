using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using BootstrapFactory;

public static class FactoryBuild
{
    [MenuItem("Factory/Create Landing Scene")]
    public static void CreateScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Bootstrap Factory").AddComponent<FactoryGame>();
        var camera=new GameObject("Top Down Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.05f,.07f,.08f);
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/Landing.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Landing.unity",true)};
        PlayerSettings.companyName="Landing Works";PlayerSettings.productName="Bootstrap Factory";
        PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
        AssetDatabase.SaveAssets();Debug.Log("FACTORY_SCENE_READY");
    }
    [MenuItem("Factory/Build Windows")]
    public static void BuildWindows()=>BuildWindowsTo("Builds/BootstrapFactory");
    [MenuItem("Factory/Build Windows Update")]
    public static void BuildWindowsUpdate()=>BuildWindowsTo("Builds/BootstrapFactory-Updated");
    static void BuildWindowsTo(string folder)
    {
        CreateScene();RunTests();Directory.CreateDirectory(folder);
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Landing.unity"},locationPathName=folder+"/BootstrapFactory.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        File.WriteAllText("Logs/build-result.txt",report.summary.result+"; errors="+report.summary.totalErrors+"; size="+report.summary.totalSize);
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Player build failed");Debug.Log("FACTORY_BUILD_SUCCEEDED");
    }
    static void Assert(bool condition,string name){if(!condition)throw new Exception("TEST FAILED: "+name);Debug.Log("PASS: "+name);}
    static void Advance(Simulation s,float seconds){for(int i=0;i<Mathf.RoundToInt(seconds*20);i++)s.Tick(.05f);}
    static Machine Ready(Simulation s,string id,int x,int y,int dir=0){var m=s.Place(id,x,y,dir);if(m==null)throw new Exception(s.notice);m.built=s.data.Building(id).seconds;m.paid=true;return m;}
    [MenuItem("Factory/Run Simulation Tests")]
    public static void RunTests()
    {
        var d=JsonUtility.FromJson<Catalog>(File.ReadAllText("Assets/Resources/catalog.json"));
        Assert(d.items.Length==38&&d.recipes.Length==53&&d.buildings.Length==20,"complete v2 catalog");
        foreach(var r in d.recipes){Assert(r.seconds>0&&r.inputs.Concat(r.outputs).All(s=>s.amount>0&&d.items.Any(i=>i.id==s.item)),"valid recipe "+r.id);}
        var sim=new Simulation(d);Assert(sim.world.cargo.Get("plate")==120&&sim.world.drones==4&&sim.world.beltKits==30,"exact landing stock");
        Assert(sim.Place("miner",20,0,0)==null,"miner requires deposit");
        Assert(sim.Place("workshop",0,0,0)==null,"overlapping footprints rejected");
        Assert(sim.Place("industrialMiner",-9,-2,0)==null,"industrial machine research gate");
        var miner=Ready(sim,"miner",-18,-4);Advance(sim,10.1f);Assert(miner.output.Get("iron")==1,"miner six ore per minute");
        var smelter=Ready(sim,"smelter",-16,-4);Advance(sim,11);Assert(smelter.output.Get("steel")>=1,"adjacent output feeds smelter");
        var work=Ready(sim,"workshop",-14,-4);Advance(sim,12);Assert(sim.world.produced.Get("plate")>=1,"bootstrap ore to plate chain");
        sim.Collect(work);Assert(sim.world.cargo.Get("plate")>70,"manual output collection");
        var b1=Ready(sim,"belt",12,-1);var b2=Ready(sim,"belt",13,-1);var b3=Ready(sim,"belt",14,-1);b1.input.Add("iron",1);Advance(sim,.35f);Assert(b1.input.Total+b2.input.Total+b3.input.Total==1,"belt transport conserves items");
        Assert(b3.input.Total==0,"items do not cross multiple belts in one tick");
        var throughput=new Simulation(d);var belt=Ready(throughput,"belt",-1,1);int before=throughput.world.cargo.Get("iron");
        for(int i=0;i<200;i++){if(belt.input.Total<3)belt.input.Add("iron",1);throughput.Tick(.05f);}Assert(throughput.world.cargo.Get("iron")-before>=29,"T1.1 delivers nominal three items per second");
        var unpaid=new Simulation(d);unpaid.world.beltKits=0;unpaid.world.cargo.Add("plate",-120);var ghost=unpaid.Place("belt",10,0,0);unpaid.Remove(ghost);Assert(unpaid.world.beltKits==0,"unpaid blueprints cannot create free kits");
        int plates=sim.world.cargo.Get("plate");sim.Remove(work);Assert(sim.world.cargo.Get("plate")==plates+14,"reclaim refunds construction");
        sim=new Simulation(d);sim.world.cargo.Add("plate",1000);sim.world.cargo.Add("wire",1000);
        for(int i=0;i<5;i++)sim.Place("storage",15+i,0,0);Advance(sim,5);Assert(sim.world.machines.Count(m=>m.built>0&&m.def!="core")==4,"four drones work on four sites");
        Advance(sim,11);Assert(sim.world.machines.Count(m=>m.def=="storage"&&sim.Complete(m))==4,"no stacking drones on a single site");
        sim=new Simulation(d);sim.world.cargo.Add("plate",1000);sim.world.cargo.Add("wire",1000);
        var coal=Ready(sim,"miner",-15,19);var gen=Ready(sim,"generator",-13,19);for(int i=0;i<30;i++)Ready(sim,"belt",i-15,15);float beforeCoal=60/d.Building("miner").rate-.5f;Advance(sim,beforeCoal);Assert(coal.progress>=beforeCoal-.1f&&sim.ratio==0,"critical miner feeding last generator survives power cutoff");
        Advance(sim,1.5f);Assert(sim.generation==240,"carbon transport starts generator");
        foreach(var m in sim.world.machines.Where(m=>m.def=="belt"))m.enabled=false;Advance(sim,.1f);Assert(sim.ratio==1,"pausing consumers recovers power");
        sim=new Simulation(d);sim.StartResearch();Assert(!sim.world.researching,"starter cache does not satisfy production milestone");
        sim.world.produced.Add("plate",20);sim.world.produced.Add("wire",10);sim.StartResearch();Advance(sim,61);Assert(sim.world.tech==1,"research costs and timed unlock");
        var ws=Ready(sim,"workshop",5,0);sim.SetRecipe(ws,"work_bearing");ws.input.Add("rod",1);ws.input.Add("plate",1);Advance(sim,.1f);sim.SetRecipe(ws,"work_wire");Assert(sim.world.cargo.Get("rod")==1,"recipe change refunds active ingredients");
        sim.world.tech=2;sim.world.cargo.Add("plate",100);sim.world.cargo.Add("beam",10);sim.world.cargo.Add("motor",1);sim.world.cargo.Add("control",1);var spec=Ready(sim,"structural",8,0);sim.SetRecipe(spec,"spec_frame");Assert(spec.changeover==20,"specialized recipe changeover delay");
        var copy=JsonUtility.FromJson<World>(JsonUtility.ToJson(sim.world));Assert(new Simulation(d,copy).world.machines.Count==sim.world.machines.Count,"save reload preserves world");
        // Every recipe input and every build cost is reachable without requiring a later technology.
        var reachable=new System.Collections.Generic.HashSet<string>(new[]{"iron","copperOre","carbon","silicate","plate","wire","refractory"});
        for(int tech=0;tech<=4;tech++)
        {
            bool changed;do{changed=false;foreach(var r in d.recipes.Where(r=>r.tech<=tech))if(d.buildings.Any(b=>b.family==r.family&&b.tech<=tech&&b.cost.All(s=>reachable.Contains(s.item)))&&r.inputs.All(s=>reachable.Contains(s.item)))foreach(var o in r.outputs)changed|=reachable.Add(o.item);}while(changed);
            if(tech<4)Assert(d.techs[tech].cost.All(s=>reachable.Contains(s.item)),"technology "+(tech+1)+" has reachable inputs");
        }
        Assert(reachable.Contains("airframe")&&reachable.Contains("ammoHandling")&&reachable.Contains("drone"),"fighter and drone dependency graphs reachable");
        sim=new Simulation(d);sim.world.tech=4;sim.world.cargo.Add(Simulation.ReadyStock);Assert(!sim.RaidReady,"stock alone cannot substitute for persistent Fighter");sim.world.cargo.Add("ammo",-1);Assert(!sim.RaidReady,"raid checkpoint rejects missing ammunition");
        var researchCase=new Simulation(d);researchCase.world.produced.Add("plate",175);researchCase.world.produced.Add("wire",120);researchCase.world.cargo.Add("plate",-117);researchCase.world.cargo.Add("wire",-42);
        Assert(researchCase.ResearchError().Contains("17 Steel Plate")&&researchCase.ResearchError().Contains("2 Copper Wire"),"research explains exact shortages after production milestone");
        researchCase.StartResearch();Assert(!researchCase.world.researching&&researchCase.world.cargo.Get("plate")==3,"blocked research does not charge materials");
        researchCase.world.cargo.Add("plate",17);researchCase.world.cargo.Add("wire",2);researchCase.StartResearch();researchCase.StartResearch();
        Assert(researchCase.world.researching&&researchCase.world.cargo.Get("plate")==0&&researchCase.world.cargo.Get("wire")==0,"research starts when collected and charges only once");
        Advance(researchCase,30);Assert(researchCase.world.research>29,"research timer advances after payment");
        researchCase=new Simulation(d,JsonUtility.FromJson<World>(JsonUtility.ToJson(researchCase.world)));Advance(researchCase,31);Assert(researchCase.world.tech==1&&!researchCase.world.researching,"paid research survives reload and unlocks technology");
        FactoryLogisticsTests.Run(d);FactoryRecoveryTests.Run(d);FactoryMegaprojectTests.Run(d);FactoryBeltPlacementTests.Run(d);FactoryBridgeTests.Run(d);
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/simulation-tests.txt","PASS: catalog, placement, construction, production, logistics, brownout, research, changeover, persistence, dependency graph, raid readiness");
        Debug.Log("FACTORY_TESTS_PASSED");
    }
}
