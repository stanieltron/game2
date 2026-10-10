using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace BootstrapFactory
{
    public partial class FactoryGame
    {
        bool modal,deleting,buildMenuOpen=true;
        readonly HashSet<int> marked=new HashSet<int>();
        Vector2 modalScroll;
        Vector2Int deleteStart,deleteEnd;
        readonly Rect questRect=new Rect(514,113,730,76);
        readonly Rect miniRect=new Rect(1072,675,170,120);
        void UpdateDelete()
        {
            if(qa)return;
            if(Input.GetMouseButtonDown(1)&&map.Contains(Mouse)&&!questRect.Contains(Mouse)&&!miniRect.Contains(Mouse))
            {build="";deleteMode=true;deleting=true;deleteStart=deleteEnd=Tile(Mouse);}
            if(!deleting)return;
            deleteEnd=Tile(new Vector2(Mathf.Clamp(Mouse.x,map.x,map.xMax-1),Mathf.Clamp(Mouse.y,map.y,map.yMax-1)));
            if(Input.GetMouseButtonUp(1))
            {
                var targets=DeleteTargets();bool remove=targets.Length>0&&targets.All(m=>marked.Contains(m.uid));
                if(remove){foreach(var m in targets)marked.Remove(m.uid);sim.notice=$"{targets.Length} objects unmarked";}
                else MarkMachines(targets);
                selected=null;deleting=false;
            }
        }
        Machine[] DeleteTargets()
        {
            int x0=Math.Min(deleteStart.x,deleteEnd.x),x1=Math.Max(deleteStart.x,deleteEnd.x)+1,y0=Math.Min(deleteStart.y,deleteEnd.y),y1=Math.Max(deleteStart.y,deleteEnd.y)+1;
            return sim.world.machines.Where(m=>m.def!="core"&&m.x<x1&&m.y<y1&&m.x+catalog.Building(m.def).size>x0&&m.y+catalog.Building(m.def).size>y0).ToArray();
        }
        void MarkMachines(IEnumerable<Machine> machines)
        {
            GUIUtility.keyboardControl=0;
            foreach(var m in machines)if(m!=null&&m.def!="core")marked.Add(m.uid);
            sim.notice=$"{marked.Count} objects marked. Use Delete selected to remove them; Escape clears marks.";
        }
        void DeleteMarked()
        {
            sim.DeleteGroup(sim.world.machines.Where(m=>marked.Contains(m.uid)).ToArray());marked.Clear();selected=null;
        }
        void DrawDeleteSelection()
        {
            foreach(var m in sim.world.machines.Where(m=>marked.Contains(m.uid)))
            {Rect b=TileRect(m.x,m.y);b.width=b.height=cell*catalog.Building(m.def).size;Box(b,new Color(1,.2f,.15f,.22f));Border(b,new Color(1,.35f,.25f),3);}
            if(!deleting)return;int x=Math.Min(deleteStart.x,deleteEnd.x),y=Math.Min(deleteStart.y,deleteEnd.y);
            Rect r=TileRect(x,y);r.width=(Math.Abs(deleteEnd.x-deleteStart.x)+1)*cell;r.height=(Math.Abs(deleteEnd.y-deleteStart.y)+1)*cell;
            Box(r,new Color(1,.2f,.15f,.15f));Border(r,new Color(1,.35f,.25f),2);
            foreach(var m in DeleteTargets()){Rect b=TileRect(m.x,m.y);b.width=b.height=cell*catalog.Building(m.def).size;Border(b,new Color(1,.35f,.25f),2);}
            Label(Mathf.Clamp(r.x,8,map.width-410),Mathf.Max(8,r.y-24),405,22,$"{(DeleteTargets().Length>0&&DeleteTargets().All(m=>marked.Contains(m.uid))?"UNMARK":"MARK")} {DeleteTargets().Length} OBJECTS / Release to apply",heading,accent);
        }
        Vector2 PortPosition(Machine m,FactoryPort p,Rect r)
        {
            float unit=r.width/catalog.Building(m.def).size;
            return new Vector2(r.x+(p.x-m.x+.5f)*unit,r.y+(p.y-m.y+.5f)*unit)+new Vector2(Simulation.DX[p.side],Simulation.DY[p.side])*unit*.49f;
        }
        void DrawPorts(Machine m,Rect r,bool diagram=false)
        {
            // Inspection retains numbered splitter sockets so role selection is usable.
            if(!diagram)return;
            foreach(var port in sim.Ports(m))
            {
                if(!port.input&&!port.output&&m.def!="splitter")continue;
                Vector2 p=PortPosition(m,port,r);Color color=port.output?accent:port.input?new Color(.3f,.88f,.96f):sub;
                Box(new Rect(p.x-7,p.y-7,14,14),ink);Border(new Rect(p.x-7,p.y-7,14,14),color,2);
                if(m.def=="splitter")Label(p.x+10,p.y-9,25,20,(port.index+1).ToString(),small,color);
            }
        }
        void DrawBeltSockets()
        {
            if(build==""||help||modal||deleting)return;
            foreach(var m in sim.world.machines)
            {
                if(m.def=="belt")continue;
                foreach(var port in sim.Ports(m))
                {
                    if(!port.input&&!port.output)continue;
                    if(sim.PlacementError("belt",port.NextX,port.NextY)!="")continue;
                    Rect tile=TileRect(port.NextX,port.NextY);
                    if(!tile.Overlaps(new Rect(0,0,map.width,map.height)))continue;
                    Color color=port.output?accent:new Color(.3f,.88f,.96f);
                    Box(new Rect(tile.x+2,tile.y+2,tile.width-4,tile.height-4),new Color(color.r,color.g,color.b,.16f));
                    Border(new Rect(tile.x+2,tile.y+2,tile.width-4,tile.height-4),color,2);
                }
            }
        }
        void DrawPlacementPorts(string id,int x,int y,int facing)
        {
            if(id=="belt")return;
            var preview=new Machine{def=id,x=x,y=y,direction=facing};
            foreach(var port in sim.Ports(preview))
            {
                if(!port.input&&!port.output)continue;
                Rect tile=TileRect(port.NextX,port.NextY);
                Color color=port.output?accent:new Color(.3f,.88f,.96f);
                var neighbor=port.output?sim.ConnectionTarget(port):sim.At(port.NextX,port.NextY);
                var upper=sim.BridgeAt(port.NextX,port.NextY);if(port.input&&upper!=null&&upper.direction==(port.side+2)%4)neighbor=upper;
                if(neighbor!=null)
                {
                    // Input belts must point into the machine. Outputs may drop onto a transverse belt.
                    bool connects=sim.Ports(neighbor).Any(other=>other.x==port.NextX&&other.y==port.NextY&&other.side==(port.side+2)%4&&(port.input?other.output:other.input));
                    if(!connects)color=new Color(1,.3f,.2f);
                }
                else if(port.NextX< -120||port.NextX>=120||port.NextY< -90||port.NextY>=90)color=new Color(1,.3f,.2f);
                Box(new Rect(tile.x+2,tile.y+2,tile.width-4,tile.height-4),new Color(color.r,color.g,color.b,.18f));
                Border(new Rect(tile.x+2,tile.y+2,tile.width-4,tile.height-4),color,2);
            }
        }
        Vector2 BeltPoint(Rect r,int side,float amount){return r.center+new Vector2(Simulation.DX[side],Simulation.DY[side])*cell*.5f*amount;}
        void DrawBelt(Machine m,Rect r)
        {
            Color rail=BuildingColor("belt");Box(new Rect(r.x+1,r.y+1,cell-2,cell-2),new Color(.07f,.085f,.085f));
            var entries=new List<int>();
            for(int side=0;side<4;side++)if(side!=m.direction&&((!m.raised&&sim.BridgeAt(m.x,m.y)==null)||side==(m.direction+2)%4))
            {
                var neighbor=sim.At(m.x+Simulation.DX[side],m.y+Simulation.DY[side]);
                if(sim.BridgeAt(m.x+Simulation.DX[side],m.y+Simulation.DY[side]) is Machine upper&&upper.direction==(side+2)%4)neighbor=upper;
                if(neighbor!=null&&sim.Ports(neighbor).Any(p=>p.output&&p.NextX==m.x&&p.NextY==m.y))entries.Add(side);
            }
            if(entries.Count==0)entries.Add((m.direction+2)%4);
            foreach(int entry in entries){Line(BeltPoint(r,entry,1),r.center,rail,cell*.64f);Line(BeltPoint(r,entry,1),r.center,new Color(.19f,.2f,.15f),cell*.44f);}
            Line(r.center,BeltPoint(r,m.direction,1),rail,cell*.64f);Line(r.center,BeltPoint(r,m.direction,1),new Color(.19f,.2f,.15f),cell*.44f);
            // Direction chevrons animate using simulation time, so they stop when paused.
            for(int i=0;i<2;i++){float t=Mathf.Repeat(sim.world.time*(m.tier+2)+i*.5f,1);Vector2 a=t<.5f?Vector2.Lerp(BeltPoint(r,entries[0],1),r.center,t*2):Vector2.Lerp(r.center,BeltPoint(r,m.direction,1),(t-.5f)*2);Arrow(a,t<.5f?(entries[0]+2)%4:m.direction,rail*.8f,4);}
            if(m.packets!=null)foreach(var cargo in m.packets)
            {
                float t=cargo.position;Vector2 p=t<.5f?Vector2.Lerp(BeltPoint(r,cargo.entrySide,.9f),r.center,t*2):Vector2.Lerp(r.center,BeltPoint(r,m.direction,.9f),(t-.5f)*2);
                float size=Mathf.Clamp(cell*.32f,7,14);Box(new Rect(p.x-size/2+2,p.y-size/2+2,size,size),ink);Box(new Rect(p.x-size/2,p.y-size/2,size,size),ItemColor(cargo.item));Border(new Rect(p.x-size/2,p.y-size/2,size,size),white,1);
            }
            // A permanent exit arrow remains readable underneath moving cargo.
            Arrow(BeltPoint(r,m.direction,.8f),m.direction,accent,4);
        }
        void DrawMinimap()
        {
            Rect r=miniRect;Box(r,ink);Border(r,line,1);float sx=(r.width-12)/240f,sy=(r.height-26)/180f;Vector2 origin=new Vector2(r.x+6,r.y+21);
            foreach(var d in sim.deposits)Circle(origin+new Vector2((d.x+120)*sx,(d.y+90)*sy),2,ItemColor(d.item));
            foreach(var m in sim.world.machines)Box(new Rect(origin.x+(m.x+120)*sx,origin.y+(m.y+90)*sy,3,3),accent);
            float x0=Mathf.Clamp(pan.x-map.width/cell/2,-120,120),y0=Mathf.Clamp(pan.y-map.height/cell/2,-90,90),x1=Mathf.Clamp(pan.x+map.width/cell/2,-120,120),y1=Mathf.Clamp(pan.y+map.height/cell/2,-90,90);
            Border(new Rect(origin.x+(x0+120)*sx,origin.y+(y0+90)*sy,(x1-x0)*sx,(y1-y0)*sy),white,1);Label(r.x+8,r.y+3,155,17,"240 x 180 · CLICK TO TRAVEL",small,sub);
            if(!modal&&!help&&GUI.Button(r,"",GUIStyle.none)){Vector2 local=Event.current.mousePosition-origin;pan=new Vector2(Mathf.Clamp(local.x/sx-120,-120,120),Mathf.Clamp(local.y/sy-90,-90,90));}
        }
        void DrawQuestTracker()
        {
            Box(questRect,ink);Border(questRect,line,1);int step=sim.world.questStep;
            if(step>=QuestNames.Length){Label(530,124,530,24,"FLIGHT CHECKLIST COMPLETE",heading,green);Label(530,151,650,24,"Continue expanding your factory. All quest steps have been claimed.",small,sub);return;}
            bool ready=QuestSatisfied(step);Label(530,121,535,23,$"DO THIS  {step+1}/{QuestNames.Length} · {QuestNames[step]}",heading,accent);Label(530,148,535,34,QuestHints[step]+"  "+QuestProgress(step),small,sub);
            bool enabled=GUI.enabled;GUI.enabled=enabled&&ready;if(Btn(new Rect(1093,132,135,34),ready?"Complete step ✓":"In progress",ready)){sim.world.questStep++;sim.notice="Step complete: "+QuestNames[step];if(sim.world.questStep==QuestNames.Length){sim.world.won=true;Save(false);}}
            GUI.enabled=enabled;
        }
        static readonly string[] QuestNames={"Build an Iron Miner","Build a Primitive Smelter","Build a Universal Workshop","Deliver an item by belt","Produce 20 Steel Plate","Build Carbon power","Produce 10 Copper Wire","Research Basic Industry I","Produce a Structural Frame","Research Component Industry I","Research Industrial Construction I","Research Orbital Operations I","Assemble Fighter Mk1","Stock the first raid"};
        static readonly string[] QuestHints={"Place a Basic Miner on the blue iron patch west of the Core.","Build a Smelter. Connect the miner's orange OUT to a cyan IN.","Build a Workshop and select Steel Plate.","Place a directed belt between ports. Side drop-offs turn toward its arrow.","Feed Crude Steel to the Workshop. Collect output or belt it to the Core.","Build a Carbon Miner and Generator; deliver Carbon to its cyan input.","Mine Copper, smelt it, then select Copper Wire in the Workshop.","Open RESEARCH and pay the first technology's material cost.","Use a Structural Fabricator for Frames. Reclaim replaced starter machines.","Upgrade mining, smelting and Carbon power; then fund component research.","Expand parallel production lines before funding the next research.","Prefabricate Fighter subsystems while researching Orbital Operations.","Complete the five-phase Fighter Bay, then its five Fighter assembly phases.","Keep your Fighter operational; stock 30 Fuel, 300 Ammo, 20 Plate, 4 Armor, 2 Motors, 2 Pumps and 2 Controls."};
        bool Built(string family,string resource=null)=>sim.world.machines.Any(m=>catalog.Building(m.def).family==family&&sim.Complete(m)&&(resource==null||m.resource==resource));
        bool QuestSatisfied(int i)
        {
            switch(i){case 0:return Built("miner","iron");case 1:return Built("smelter");case 2:return Built("workshop");case 3:return sim.world.beltDeliveries>0;case 4:return sim.world.produced.Get("plate")>=20;case 5:return Built("miner","carbon")&&Built("generator")&&sim.world.produced.Get("carbon")>0;case 6:return sim.world.produced.Get("wire")>=10;case 7:return sim.world.tech>=1;case 8:return sim.world.produced.Get("frame")>0;case 9:return sim.world.tech>=2;case 10:return sim.world.tech>=3;case 11:return sim.world.tech>=4;case 12:return sim.BootstrapComplete;case 13:return sim.RaidReady;default:return false;}
        }
        string QuestProgress(int i){if(i==4)return $"{sim.world.produced.Get("plate")}/20";if(i==6)return $"{sim.world.produced.Get("wire")}/10";return QuestSatisfied(i)?"Ready to complete.":"";}
        Vector2 constructionScroll;
        string ConstructionState(Machine m)
        {
            if(m.paid)return m.built>0?"UNDER CONSTRUCTION":"WAITING FOR A DRONE";
            var missing=catalog.Building(m.def).cost.Where(c=>sim.world.cargo.Get(c.item)<c.amount).ToArray();
            return missing.Length==0?"MATERIALS READY / QUEUED":"MISSING MATERIALS";
        }
        void DrawConstructionMaterials(Machine m,Rect area)
        {
            var costs=catalog.Building(m.def).cost;
            constructionScroll=GUI.BeginScrollView(area,constructionScroll,new Rect(0,0,area.width-22,costs.Length*65));
            for(int i=0;i<costs.Length;i++)
            {
                var cost=costs[i];int have=sim.world.cargo.Get(cost.item),missing=m.paid?0:Math.Max(0,cost.amount-have);
                float y=i*65;Color color=missing>0?new Color(1,.48f,.3f):green;
                Label(0,y,area.width-28,20,catalog.Name(cost.item),heading,color);
                Label(0,y+21,area.width-28,21,m.paid?$"Supplied {cost.amount} / {cost.amount}":$"Cargo {have} / Need {cost.amount}   |   "+(missing>0?$"Missing {missing}":"Ready"),small,color);
                int stored=sim.world.machines.Where(other=>catalog.Building(other.def).family=="storage").Sum(other=>other.input.Get(cost.item));
                if(!m.paid&&stored>0)Label(0,y+42,area.width-28,18,$"{stored} in storage: collect into Command Cargo",small,sub);
            }
            GUI.EndScrollView();
        }
        void DrawConstructionSite(Machine m,Rect r)
        {
            Color color=m.paid?new Color(.3f,.78f,.94f):new Color(1,.53f,.25f);
            Box(r,new Color(.035f,.09f,.13f,.85f));
            for(float x=r.x+4;x<r.xMax-3;x+=12)for(float y=r.y+4;y<r.yMax-3;y+=12)Box(new Rect(x,y,2,2),color*.45f);
            for(float x=r.x;x<r.xMax;x+=12){float length=Mathf.Min(7,r.xMax-x);Box(new Rect(x,r.y,length,2),color);Box(new Rect(x,r.yMax-2,length,2),color);}
            for(float y=r.y;y<r.yMax;y+=12){float length=Mathf.Min(7,r.yMax-y);Box(new Rect(r.x,y,2,length),color);Box(new Rect(r.xMax-2,y,2,length),color);}
            Label(r.center.x-7,r.center.y-13,18,26,m.paid?"+":"!",heading,color);
            if(r.width>=55)Label(r.x+4,r.y+4,r.width-8,20,"BLUEPRINT",small,color);
            if(m==selected)Border(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),accent,2);
            Bar(new Rect(r.x+4,r.yMax-7,r.width-8,3),m.built/Math.Max(1,catalog.Building(m.def).seconds),color);
        }
        void DrawBuildingModal()
        {
            if(selected==null){modal=false;return;}var m=selected;var b=catalog.Building(m.def);
            Box(new Rect(0,190,W,H-190),new Color(.01f,.025f,.03f,.68f));Rect window=new Rect(325,203,950,580);Box(window,surface);Border(window,line,2);
            GUI.BeginGroup(window);Label(24,20,780,34,b.name,title);Label(24,62,760,22,$"Tile {m.x}, {m.y}  ·  {b.size} x {b.size}  ·  {b.power} PU  ·  "+(m.status??"Ready"),text,sub);
            if(Btn(new Rect(847,20,78,34),"Close ✕"))modal=false;
            if(Btn(new Rect(24,105,126,36),"Rotate [R]"))sim.Rotate(m);
            if(Btn(new Rect(160,105,126,36),m.enabled?"Pause machine":"Enable"))m.enabled=!m.enabled;
            if(Btn(new Rect(296,105,126,36),"Feed inputs"))sim.Feed(m);
            if(Btn(new Rect(432,105,126,36),"Collect output"))sim.Collect(m);
            Label(620,112,300,24,"Simulation paused while inspecting",small,sub);
            Rect diagram=new Rect(112,210,150,150);Box(diagram,BuildingColor(b.family)*.45f);Border(diagram,BuildingColor(b.family),3);DrawPorts(m,diagram,true);
            Label(24,164,376,25,"CYAN = INPUT   ·   ORANGE = OUTPUT",heading,accent);
            Label(24,387,376,72,"INPUT BUFFER\n"+InventoryText(m.input),small);Label(24,469,376,62,"OUTPUT BUFFER\n"+InventoryText(m.output),small);
            if(sim.IsBay(m)){DrawProject(m,new Rect(451,160,461,377));}
            else if(!sim.Complete(m))
            {
                Label(451,174,461,25,ConstructionState(m),heading,accent);
                DrawConstructionMaterials(m,new Rect(451,207,461,225));
                Bar(new Rect(451,444,450,7),m.built/Math.Max(1,b.seconds),accent);
                Label(451,460,450,65,$"Construction {m.built:0} / {b.seconds:0}s.\nMaterials come from Command Cargo. Close this window to resume construction.",small,sub);
            }
            else if(b.family=="splitter")
            {
                Label(450,172,460,50,"Choose one input and any two outputs. Selecting an occupied role swaps its previous port.",text);
                string[] roles={"INPUT","OUTPUT A","OUTPUT B"};int[] values={m.splitInput,m.splitA,m.splitB};
                for(int row=0;row<3;row++){Label(450,238+row*66,120,25,roles[row],heading,row==0?new Color(.3f,.88f,.96f):accent);for(int i=0;i<6;i++)if(Btn(new Rect(554+i*58,232+row*66,50,34),(i+1).ToString(),values[row]==i))sim.SetSplitterRole(m,row,i);}
                Label(450,444,450,66,"Alternates A / B when both can accept cargo. If one is blocked, it uses the other. All six numbered ports rotate with the building.",text,sub);
            }
            else if(b.family=="core"||b.family=="storage")
            {
                Label(450,174,460,42,"Output filter · choose the item to dispatch",heading,accent);
                modalScroll=GUI.BeginScrollView(new Rect(450,220,468,300),modalScroll,new Rect(0,0,440,(catalog.items.Length+1)*33));
                if(Btn(new Rect(0,0,428,30),"No filter",m.filter==""))m.filter="";
                for(int i=0;i<catalog.items.Length;i++)if(Btn(new Rect(0,(i+1)*33,428,30),catalog.items[i].name,m.filter==catalog.items[i].id))m.filter=catalog.items[i].id;GUI.EndScrollView();
            }
            else if(b.family=="belt")
            {Label(451,182,450,105,$"Direction: {new[]{"EAST","SOUTH","WEST","NORTH"}[m.direction]}\nT1.{m.tier} · {m.tier+2} items/s · {m.input.Total}/3 items\nOrdinary belts accept rear/side drops. Bridges carry two isolated straight lanes; no third layer.",text);if(Btn(new Rect(451,322,260,38),"Upgrade belt tier"))sim.Upgrade(m);
                if(sim.BridgeAt(m.x,m.y)!=null){Label(451,382,450,36,m.raised?"UPPER BRIDGE LANE / isolated cargo":"LOWER CROSSING LANE / isolated cargo",heading,accent);if(Btn(new Rect(451,432,330,34),m.raised?"Inspect lower lane":"Inspect upper bridge lane"))selected=m.raised?sim.At(m.x,m.y):sim.BridgeAt(m.x,m.y);Label(451,485,450,37,"Deleting this crossing reclaims both lanes; Ctrl+Z restores both.",small,sub);}}
            else
            {
                var recipe=catalog.Recipe(m.recipe);Label(450,173,460,84,recipe==null?b.family=="miner"?$"Extracts {catalog.Name(m.resource)} at {b.rate}/min.":$"Burns {b.fuelRate} Carbon/min to generate {b.generation} PU.":Cost(recipe.inputs)+"\n→ "+Cost(recipe.outputs)+$" / {recipe.seconds:0.#}s",text);
                var list=catalog.recipes.Where(r=>r.family==b.family&&r.tech<=sim.world.tech).ToArray();modalScroll=GUI.BeginScrollView(new Rect(450,269,468,258),modalScroll,new Rect(0,0,440,list.Length*36));for(int i=0;i<list.Length;i++)if(Btn(new Rect(0,i*36,430,33),list[i].name,m.recipe==list[i].id))sim.SetRecipe(m,list[i].id);GUI.EndScrollView();
            }
            Label(24,546,890,22,"ESC closes · Orange arrows point OUT · Cyan arrows point IN · Right-drag to mark; Delete selected removes the group; Ctrl+Z restores it.",small,sub);GUI.EndGroup();
        }
    }
}
