using System;
using System.Linq;
using UnityEngine;

namespace BootstrapFactory
{
    public partial class FactoryGame
    {
        bool coreRecovery=true,researchUnlocks;
        Vector2 recoveryScroll,unlockScroll;
        void OpenRecovery()
        {
            build="";deleteMode=false;modal=false;panel="OPERATIONS";coreRecovery=true;
            selected=sim.world.machines.First(m=>m.def=="core");GUIUtility.keyboardControl=0;
        }
        void DrawCoreRecovery()
        {
            Label(1287,166,280,32,"CORE / RECOVERY",sectionTitle,accent);
            if(Btn(new Rect(1287,209,280,28),"Show Core output routing"))coreRecovery=false;
            Label(1287,250,280,22,"DRONE MINING / 2 RAW PER MIN",heading,accent);
            Label(1287,278,280,34,$"{sim.world.mining.Count} mining / {sim.AvailableBuilders} construction drones. No grid power.",small,sub);
            string[] ids={"iron","copperOre","carbon","silicate"};
            for(int i=0;i<4;i++)
            {
                float y=316+i*29;int count=sim.world.mining.Count(j=>j.resource==ids[i]);
                Label(1287,y+3,176,24,catalog.Name(ids[i])+"  x"+count,small,white);
                if(Btn(new Rect(1465,y,48,26),"-"))sim.RemoveMiningDrone(ids[i]);
                if(Btn(new Rect(1519,y,48,26),"+"))sim.StartDroneMining(ids[i]);
            }
            Label(1287,445,280,23,"EMERGENCY CORE / 200 PU",heading,accent);
            Label(1287,473,280,42,"60s nominal; 150s on Core power alone. Priority load. Surplus powers the factory.",small,sub);
            if(sim.CoreCrafting)
            {
                var recipe=catalog.Recipe(sim.world.coreRecipe);
                Label(1287,527,280,54,recipe.name+"\n"+Cost(recipe.outputs),text,green);
                Label(1287,597,280,37,$"{sim.world.coreProgress:0} / {sim.CoreCraftSeconds(recipe):0}s / {sim.CoreReservedPower:0} /200 PU supplied",small,accent);
                Bar(new Rect(1287,644,280,6),sim.world.coreProgress/sim.CoreCraftSeconds(recipe),green);
                if(Btn(new Rect(1287,675,280,34),"Cancel batch / refund ingredients"))sim.CancelCoreCraft();
            }
            else
            {
                var recipes=sim.CoreRecipes;recoveryScroll=GUI.BeginScrollView(new Rect(1287,522,286,277),recoveryScroll,new Rect(0,0,260,recipes.Length*90));
                for(int i=0;i<recipes.Length;i++)
                {
                    var recipe=recipes[i];float y=i*90;string error=sim.CoreCraftError(recipe.id);
                    Label(0,y,256,20,recipe.name+" / 60s nominal",heading,accent);
                    Label(0,y+22,256,30,Cost(recipe.inputs)+" -> "+Cost(recipe.outputs),small,sub);
                    bool enabled=GUI.enabled;GUI.enabled=enabled&&error=="";
                    if(Btn(new Rect(0,y+57,256,29),error==""?"Craft one batch":error,error==""))sim.StartCoreCraft(recipe.id);GUI.enabled=enabled;
                }
                GUI.EndScrollView();
            }
        }
        void DrawResearchUnlocks(int tech)
        {
            var entries=catalog.buildings.Where(b=>b.tech==tech).Select(b=>"BUILDING: "+b.name).Concat(
                catalog.recipes.Where(r=>r.tech==tech&&catalog.buildings.Any(b=>b.family==r.family&&b.tech<=tech)).Select(r=>"RECIPE: "+r.name+" ("+catalog.buildings.First(b=>b.family==r.family).name+")")).ToList();
            if(tech==4)entries.Add("PROJECT: Fighter Mk1 / five sequential assembly phases in the commissioned Bay");
            if(tech==1)entries.Add("UPGRADE: Belt T1.2 / 4 items per second");
            if(tech==3)entries.Add("UPGRADE: Belt T1.3 / 5 items per second");
            Label(1287,405,280,25,"WHAT THIS RESEARCH UNLOCKS",heading,accent);
            unlockScroll=GUI.BeginScrollView(new Rect(1287,438,286,222),unlockScroll,new Rect(0,0,260,entries.Count*49));
            for(int i=0;i<entries.Count;i++)Label(0,i*49,255,46,entries[i],small,white);GUI.EndScrollView();
        }
        string BuildShortage(string id)
        {
            if(id=="belt")
            {
                int count=beltDragging?sim.NewBeltCount(BeltPreviewTiles(),direction):1;
                int beltMissing=Math.Max(0,count-sim.world.beltKits-sim.world.cargo.Get("plate"));
                return beltMissing>0?beltMissing+" Steel Plate":"";
            }
            var missing=catalog.Building(id).cost.Where(s=>sim.world.cargo.Get(s.item)<s.amount).Select(s=>(s.amount-sim.world.cargo.Get(s.item))+" "+catalog.Name(s.item));
            return string.Join(", ",missing);
        }
        void DrawBuildWarning()
        {
            if(build==""||modal||help)return;if(build=="fighterPad"){Rect bayInfo=new Rect(514,195,730,64);Box(bayInfo,ink);Border(bayInfo,accent,1);Label(526,203,705,23,"FIGHTER BAY / PLACE PROJECT FOOTPRINT",heading,accent);Label(526,231,705,24,"Select after placing to deliver phase 1: 60 Plate, 40 Beam, 20 Frame, 12 Refractory.",small,white);return;}string missing=BuildShortage(build);
            bool shortfall=missing!="";
            Rect r=new Rect(514,195,730,64);Box(r,ink);Border(r,shortfall?accent:green,1);
            Label(r.x+12,r.y+7,705,21,shortfall?"MISSING MATERIALS / WILL PLACE AN UNFUNDED BLUEPRINT":"MATERIALS READY TO BUILD",heading,shortfall?accent:green);
            Label(r.x+12,r.y+32,705,27,shortfall?missing:build=="belt"&&beltDragging?$"{BeltPreviewTiles().Count} tiles. New tiles and bridge lanes cost materials. Crossing lanes keep separate cargo.":"Cost: "+Cost(catalog.Building(build).cost),small,white);
        }
    }
}
