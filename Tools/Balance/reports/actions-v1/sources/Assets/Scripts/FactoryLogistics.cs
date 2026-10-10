using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BootstrapFactory
{
    [Serializable] public class BeltCargo { public string item; public float position; public int entrySide; }
    public struct FactoryPort
    {
        public int index,x,y,side; public bool input,output;
        public int NextX=>x+Simulation.DX[side]; public int NextY=>y+Simulation.DY[side];
    }
    [Serializable] public class Deposit { public int x,y,rx,ry;public string item; }
    public partial class Simulation
    {
        public readonly List<Deposit> deposits=new List<Deposit>();
        readonly Dictionary<int,string> resourceGrid=new Dictionary<int,string>();
        class DeleteAction { public List<Machine> machines=new List<Machine>();public Inventory refund=new Inventory();public int kits; }
        readonly List<DeleteAction> undo=new List<DeleteAction>();
        public int UndoCount=>undo.Count;
        void GenerateDeposits()
        {
            var seeds=world.mapVersion==0?new[]{new[]{-9,-2},new[]{9,-6},new[]{-7,9},new[]{11,8}}:new[]{new[]{-18,-4},new[]{21,-12},new[]{-15,19},new[]{23,18}};
            string[] types={"iron","copperOre","carbon","silicate"};
            for(int i=0;i<4;i++)deposits.Add(new Deposit{x=seeds[i][0],y=seeds[i][1],rx=4,ry=4,item=types[i]});
            var rng=new System.Random(world.seed);int attempts=0;
            while(deposits.Count<68&&attempts++<5000)
            {
                int x=rng.Next(-112,113),y=rng.Next(-82,83);if(Math.Abs(x)<30&&Math.Abs(y)<27)continue;
                int rx=rng.Next(3,7),ry=rng.Next(3,7);if(deposits.Any(d=>Math.Abs(d.x-x)<d.rx+rx+4&&Math.Abs(d.y-y)<d.ry+ry+4))continue;
                deposits.Add(new Deposit{x=x,y=y,rx=rx,ry=ry,item=types[rng.Next(4)]});
            }
            foreach(var d in deposits)for(int x=d.x-d.rx;x<=d.x+d.rx;x++)for(int y=d.y-d.ry;y<=d.y+d.ry;y++)
                if((x-d.x)*(x-d.x)/(float)(d.rx*d.rx)+(y-d.y)*(y-d.y)/(float)(d.ry*d.ry)<=1)resourceGrid[Key(x,y)]=d.item;
            // Old saves keep their existing extraction sites when loaded into the expanded sector.
            foreach(var m in world.machines.Where(m=>!string.IsNullOrEmpty(m.resource)))resourceGrid[Key(m.x,m.y)]=m.resource;
        }
        public List<FactoryPort> Ports(Machine m)
        {
            var b=data.Building(m.def);var ports=new List<FactoryPort>();int n=b.size;
            if(b.family=="pole")return ports;
            if(b.family=="splitter")
            {
                int[] px={0,0,1,1,0,1},py={0,1,0,1,0,1},side={2,2,0,0,3,1};
                for(int i=0;i<6;i++){int x=px[i],y=py[i];for(int r=0;r<m.direction;r++){int old=x;x=n-1-y;y=old;}
                    ports.Add(new FactoryPort{index=i,x=m.x+x,y=m.y+y,side=(side[i]+m.direction)%4,input=i==m.splitInput,output=i==m.splitA||i==m.splitB});}
            }
            else
            {
                for(int local=0;local<4;local++)
                {
                    bool output=local==0&&b.family!="generator";bool input=local!=0&&b.family!="miner";
                    if(b.family=="generator")input=true;
                    if(b.family=="belt"&&(m.raised||BridgeAt(m.x,m.y)!=null))input=local==2;
                    int side=(local+m.direction)%4;
                    for(int lane=0;lane<n;lane++)
                    {
                        int x=side==0?n-1:side==2?0:lane,y=side==1?n-1:side==3?0:lane;
                        ports.Add(new FactoryPort{index=local*n+lane,x=m.x+x,y=m.y+y,side=side,input=input,output=output});
                    }
                }
            }
            return ports;
        }
        public void Rotate(Machine m) {if(m==null)return;if(m.raised||BridgeAt(m.x,m.y)!=null){notice="Bridge crossings keep their two axes. Delete and rebuild the crossing to change it.";return;}m.direction=(m.direction+1)%4;notice="Rotated "+data.Building(m.def).name;}
        public bool ConfigureSplitter(Machine m,int input,int a,int b)
        {
            if(m.def!="splitter"||input<0||a<0||b<0||input>5||a>5||b>5||input==a||input==b||a==b){notice="Choose three distinct ports";return false;}
            m.splitInput=input;m.splitA=a;m.splitB=b;m.splitNext=0;return true;
        }
        public void SetSplitterRole(Machine m,int role,int port)
        {
            int[] roles={m.splitInput,m.splitA,m.splitB};int old=roles[role];int occupied=Array.IndexOf(roles,port);roles[role]=port;if(occupied>=0&&occupied!=role)roles[occupied]=old;
            ConfigureSplitter(m,roles[0],roles[1],roles[2]);
        }
        bool PortAccepts(Machine target,FactoryPort source,string item)
        {
            return Ports(target).Any(p=>p.input&&p.x==source.NextX&&p.y==source.NextY&&p.side==(source.side+2)%4)&&Accepts(target,item);
        }
        public void SyncPackets(Machine m)
        {
            if(m.packets==null)m.packets=new List<BeltCargo>();
            foreach(var s in m.input.stacks)
            {
                int difference=s.amount-m.packets.Count(p=>p.item==s.item);
                for(int i=0;i<difference;i++)m.packets.Add(new BeltCargo{item=s.item,entrySide=(m.direction+2)%4});
                for(int i=0;i< -difference;i++){var p=m.packets.LastOrDefault(v=>v.item==s.item);if(p!=null)m.packets.Remove(p);}
            }
        }
        void Transport(float dt)
        {

            var moves=new List<Tuple<Machine,Machine,string,FactoryPort>>();var reserved=new HashSet<int>();
            foreach(var m in world.machines)
            {
                var b=data.Building(m.def);if(b.family=="belt")SyncPackets(m);
                if(!Complete(m)||!m.enabled)continue;
                float speed=poweredRecovery.Contains(m.uid)?1:ratio;if(speed==0){m.status="Power off";continue;}
                float rate=b.family=="belt"?m.tier+2:3;
                m.transport=Math.Min(1.2f,m.transport+dt*speed*rate);
                if(b.family=="belt")
                {
                    for(int i=0;i<m.packets.Count;i++){float limit=i==0?1:Math.Max(0,m.packets[i-1].position-.31f);m.packets[i].position=Math.Min(limit,m.packets[i].position+dt*speed*rate);}
                    m.status=m.packets.Count==0?"Empty":m.packets[0].position>=.999f?"Waiting for drop-off":"Transporting";
                    if(m.packets.Count==0||m.packets[0].position<.999f)continue;
                }
                if(m.transport<.999f)continue;
                var outputs=Ports(m).Where(p=>p.output).OrderBy(p=>(p.index-m.outputNext+4*b.size)%(4*b.size)).ToList();
                if(b.family=="splitter")outputs=outputs.OrderBy(p=>p.index==(m.splitNext==0?m.splitA:m.splitB)?0:1).ToList();
                Inventory source=b.family=="core"?world.cargo:b.family=="storage"||b.family=="belt"||b.family=="splitter"?m.input:m.output;
                bool queued=false;
                foreach(var port in outputs)
                {
                    var target=ConnectionTarget(port);if(target==null||target==m||!target.enabled||(!Complete(target)&&!IsBay(target))||reserved.Contains(target.uid))continue;
                    foreach(var s in source.stacks)
                    {
                        if(s.amount<=0||(m.filter!=""&&s.item!=m.filter)||(b.family=="core"&&m.filter==""))continue;
                        if(b.family=="belt"&&s.item!=m.packets[0].item)continue;
                        if(!PortAccepts(target,port,s.item))continue;
                        moves.Add(Tuple.Create(m,target,s.item,port));reserved.Add(target.uid);queued=true;break;
                    }
                    if(queued)break;
                }
            }
            // Commit simultaneously: arrivals cannot be forwarded again in this tick.
            foreach(var move in moves)
            {
                var m=move.Item1;var t=move.Item2;string item=move.Item3;var b=data.Building(m.def);var port=move.Item4;
                var from=b.family=="core"?world.cargo:b.family=="belt"||b.family=="storage"||b.family=="splitter"?m.input:m.output;
                if(from.Get(item)<=0)continue;from.Add(item,-1);
                if(b.family=="belt"){m.packets.RemoveAt(0);world.beltDeliveries++;}
                if(b.family=="splitter")m.splitNext=port.index==m.splitA?1:0;
                if(t.def=="core")world.cargo.Add(item,1);else t.input.Add(item,1);
                if(t.def=="belt")t.packets.Add(new BeltCargo{item=item,entrySide=(port.side+2)%4});
                m.outputNext=(port.index+1)%(4*b.size);m.transport=Math.Max(0,m.transport-1);
            }
        }
        public void DeleteGroup(IEnumerable<Machine> selection)
        {
            var chosen=selection.Where(m=>m!=null).ToArray();
            var group=chosen.Concat(world.machines.Where(m=>m.def=="belt"&&chosen.Any(c=>c.def=="belt"&&c.x==m.x&&c.y==m.y))).Where(m=>m!=null&&m.def!="core"&&!(IsBay(m)&&(m.projectPhase>0||m.assemblyStarted||world.fighter?.bayId==m.uid))&&world.machines.Contains(m)).Distinct().ToArray();if(group.Length==0){notice="Nothing to reclaim; Core and permanent Bay structures are protected";return;}
            var action=new DeleteAction();var before=world.cargo.stacks.ToDictionary(s=>s.item,s=>s.amount);int kits=world.beltKits;
            foreach(var m in group)action.machines.Add(JsonUtility.FromJson<Machine>(JsonUtility.ToJson(m)));
            foreach(var m in group)Remove(m);
            foreach(var s in world.cargo.stacks){int old=before.TryGetValue(s.item,out int v)?v:0;if(s.amount>old)action.refund.Add(s.item,s.amount-old);}
            action.kits=world.beltKits-kits;undo.Add(action);if(undo.Count>30)undo.RemoveAt(0);notice=$"Reclaimed {group.Length} objects. Ctrl+Z to undo.";
        }
        public bool UndoDelete()
        {
            if(undo.Count==0){notice="No deletion to undo";return false;}
            var a=undo[undo.Count-1];
            foreach(var m in a.machines){var b=data.Building(m.def);for(int x=m.x;x<m.x+b.size;x++)for(int y=m.y;y<m.y+b.size;y++)if(At(x,y)!=null){notice="Undo blocked: clear the original footprint first";return false;}}
            if(!world.cargo.Has(a.refund.stacks)||world.beltKits<a.kits){notice="Undo needs the refunded materials back in Command Cargo";return false;}
            world.cargo.Spend(a.refund.stacks);world.beltKits-=a.kits;world.machines.AddRange(a.machines);undo.RemoveAt(undo.Count-1);Reindex();notice=$"Restored {a.machines.Count} objects and their contents";return true;
        }
    }
}
