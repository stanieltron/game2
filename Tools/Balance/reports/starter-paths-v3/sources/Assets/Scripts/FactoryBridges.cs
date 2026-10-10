using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BootstrapFactory
{
    public partial class Simulation
    {
        readonly Dictionary<int,Machine> bridges=new Dictionary<int,Machine>();
        public Machine BridgeAt(int x,int y){bridges.TryGetValue(Key(x,y),out var m);return m;}
        public Machine ConnectionTarget(FactoryPort source)
        {
            var upper=BridgeAt(source.NextX,source.NextY);
            return upper!=null&&upper.direction==source.side?upper:At(source.NextX,source.NextY);
        }
        public bool WouldBridge(ICollection<Vector2Int> tiles,Vector2Int tile,int facing)
        {
            var under=At(tile.x,tile.y);if(under==null||under.def!="belt"||under.direction%2==facing%2)return false;
            var step=new Vector2Int(DX[facing],DY[facing]);
            return tiles.Contains(tile-step)&&tiles.Contains(tile+step);
        }
        public int NewBeltCount(ICollection<Vector2Int> tiles,int facing)=>tiles.Distinct().Count(t=>At(t.x,t.y)==null||WouldBridge(tiles,t,facing));
        Machine PlaceBridge(int x,int y,int facing)
        {
            var m=new Machine{uid=world.nextId++,def="belt",x=x,y=y,direction=facing,raised=true};
            if(world.beltKits>0){world.beltKits--;m.paid=true;}else Pay(m);
            world.machines.Add(m);Reindex();return m;
        }
    }
    public partial class FactoryGame
    {
        void DrawBridge(Machine m,Rect r,bool preview=false)
        {
            var rail=preview?accent:new Color(.55f,.79f,.87f);
            Vector2 a=BeltPoint(r,(m.direction+2)%4,1),b=BeltPoint(r,m.direction,1);
            Line(a+new Vector2(3,4),b+new Vector2(3,4),ink,cell*.83f);
            Line(a,b,rail,cell*.72f);Line(a,b,ink,cell*.51f);
            for(int i=0;i<2;i++){float t=Mathf.Repeat(sim.world.time*(m.tier+2)+i*.5f,1);Arrow(Vector2.Lerp(a,b,t),m.direction,rail,4);}
            foreach(float t in new[]{.08f,.92f})
            {
                Vector2 center=Vector2.Lerp(a,b,t),cross=new Vector2(-Simulation.DY[m.direction],Simulation.DX[m.direction])*cell*.4f;
                Line(center-cross,center+cross,rail,3);
            }
            if(m.packets!=null)foreach(var item in m.packets)
            {
                Vector2 pos=Vector2.Lerp(a,b,item.position);float size=Mathf.Clamp(cell*.28f,6,12);
                Box(new Rect(pos.x-size/2,pos.y-size/2,size,size),ItemColor(item.item));Border(new Rect(pos.x-size/2,pos.y-size/2,size,size),white,1);
            }
            Arrow(BeltPoint(r,m.direction,.65f),m.direction,white,5);
            if(!preview&&!sim.Complete(m)){Border(r,accent,1);Bar(new Rect(r.x+3,r.yMax-5,cell-6,3),m.built,accent);}
        }
    }
}
