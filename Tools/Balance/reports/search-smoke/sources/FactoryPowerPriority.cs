using System.Collections.Generic;
using System.Linq;

namespace BootstrapFactory
{
    public partial class Simulation
    {
        HashSet<int> CriticalCarbonMiners()
        {
            var generators=world.machines.Where(m=>Complete(m)&&m.enabled&&data.Building(m.def).family=="generator").ToArray();
            var active=generators.Where(m=>m.fuelSeconds>0||m.input.Get("carbon")>0).ToArray();
            var last=active.Length==1?active[0]:active.Length==0&&generators.Length==1?generators[0]:null;
            var result=new HashSet<int>();if(last==null)return result;
            foreach(var miner in world.machines.Where(m=>m.enabled&&Complete(m)&&m.resource=="carbon"&&data.Building(m.def).family=="miner"))
            {
                var visited=new HashSet<int>();var queue=new Queue<Machine>();queue.Enqueue(miner);
                while(queue.Count>0)
                {
                    var m=queue.Dequeue();if(!visited.Add(m.uid))continue;
                    if(m.filter!=""&&m.filter!="carbon")continue;
                    foreach(var output in Ports(m).Where(p=>p.output))
                    {
                        var target=ConnectionTarget(output);if(target==null||!target.enabled||!Complete(target))continue;
                        if(!Ports(target).Any(p=>p.input&&p.x==output.NextX&&p.y==output.NextY&&p.side==(output.side+2)%4))continue;
                        if(target==last){result.Add(miner.uid);queue.Clear();break;}
                        var family=data.Building(target.def).family;if(family=="belt"||family=="splitter"||family=="storage")queue.Enqueue(target);
                    }
                }
            }
            return result;
        }
    }
}
