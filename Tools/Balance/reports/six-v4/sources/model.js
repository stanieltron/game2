/* Capacity model, deliberately separate from the game's chronological simulator. */
(function(root){
function calculate(data, counts, opts={}) {
 const buildings=Object.fromEntries(data.buildings.map(b=>[b.id,b])), rawIds=['iron','copperOre','carbon','silicate'];
 const count=id=>Math.max(0,Number(counts[id])||0), add=(o,k,v)=>o[k]=(o[k]||0)+v;
 const mining={};rawIds.forEach(id=>mining[id]=(count('miner:'+id)*buildings.miner.rate+count('industrialMiner:'+id)*buildings.industrialMiner.rate)/60);
 const families={};for(const b of data.buildings) if(b.family!=='miner')add(families,b.family,count(b.id));
 const variants={};for(const r of data.recipes)if(r.family!=='emergency')for(const o of r.outputs)(variants[o.item]??=[]).push(r);
 const chosen={},depths={};
 function recipe(item){if(chosen[item])return chosen[item];const list=variants[item];if(!list)return null;
  const active=list.filter(r=>families[r.family]>0), pool=active.length?active:list;
  return chosen[item]=[...pool].sort((a,b)=>a.seconds/a.outputs[0].amount-b.seconds/b.outputs[0].amount)[0];}
 function depth(item,trail=[]){if(item in depths)return depths[item];if(trail.includes(item))throw Error('Recipe cycle: '+item);const r=recipe(item);return depths[item]=r?1+Math.max(0,...r.inputs.map(s=>depth(s.item,[...trail,item]))):0;}
 const ordered=data.items.map(i=>i.id).sort((a,b)=>depth(b)-depth(a));
 const project={},capital={};const includeBay=opts.target!=='fighter',includeFighter=opts.target!=='bay';
 for(const p of [...(includeBay?data.bayPhases:[]),...(includeFighter?data.fighterPhases:[])])for(const s of p.cost)add(project,s.item,s.amount);
 if(opts.research)for(const t of data.techs)for(const s of t.cost)add(project,s.item,s.amount);
 let construction=0,power=0,gen=0,fuel=0;
 for(const b of data.buildings){let n=b.family==='miner'?rawIds.reduce((s,id)=>s+count(b.id+':'+id),0):count(b.id);
  if(b.id==='core'||b.id==='fighterPad')continue;
  for(const s of b.cost)add(capital,s.item,s.amount*n);
  construction+=b.seconds*n;power+=b.power*n;gen+=b.generation*n;fuel+=b.fuelRate*n/60;
 }
 // Each active production pool has one configurable mixed-input trunk.
 const routes=[];
 if(opts.logistics){
  const ids=[...new Set(Object.values(chosen).filter(r=>families[r.family]>0).map(r=>r.family))];
  if(fuel)ids.push('generation');
  for(const id of ids){const setting=opts.routes?.[id]||{};
   const lanes=Math.max(0,Math.min(100,Math.floor(Number(setting.lanes??1)||0))),tier=Math.max(1,Math.min(3,Math.floor(Number(setting.tier)||1))),length=Math.max(1,Math.min(100,Math.floor(Number(setting.length)||5)));
   const tiles=lanes*length;routes.push({id,lanes,tier,length,tiles,capacity:lanes*(tier+2),items:{}});
   for(const cost of buildings.belt.cost)add(capital,cost.item,cost.amount*tiles);
   if(tier>=2){add(capital,'plate',tiles);add(capital,'wire',tiles);}
   if(tier>=3){add(capital,'plate',tiles);add(capital,'bus',tiles);}
   power+=tiles*buildings.belt.power;construction+=tiles*buildings.belt.seconds;
  }
 }
 // Belts use the actual 30 starter kits before plates when cache credit is enabled.
 if(opts.starter&&capital.plate)capital.plate-=Math.min(count('belt')+routes.reduce((n,r)=>n+r.tiles,0),30);
 function expand(input,useStock=false){const needs={...input},work={},batches={},missing=new Set();
  const cache=useStock?{plate:120,wire:50,refractory:24}:{};
  for(const item of ordered){let n=Math.max(0,(needs[item]||0)-(cache[item]||0));delete needs[item];if(!n)continue;const r=recipe(item);
   if(!r){add(needs,item,n);continue;}
   const amount=r.outputs.find(o=>o.item===item).amount, batch=Math.ceil(n/amount);
   batches[r.id]=batch;add(work,r.family,batch*r.seconds);
   if(!families[r.family])missing.add(r.family);
   for(const s of r.inputs)add(needs,s.item,batch*s.amount);
  }return {raw:needs,work,batches,missing:[...missing]};
 }
 const total={...project};for(const [k,v]of Object.entries(capital))add(total,k,v);
 const projectExpansion=expand(project),gross=expand(total),net=expand(total,opts.starter);
 for(const id of rawIds)if((net.raw[id]||0)>0&&!mining[id])net.missing.push(id+' extraction');
 const phaseTime=[...(includeBay?data.bayPhases:[]),...(includeFighter?data.fighterPhases:[])].reduce((s,p)=>s+p.seconds,0);
 // Reserve project peak power across the estimate; conservative all-on grid.
 const projectPower=Math.max(...(includeBay?data.bayPhases:[]).map(p=>p.power),...(includeFighter?data.fighterPhases:[]).map(p=>p.power),0);
 power+=Math.max(projectPower,opts.research?Math.max(...data.techs.map(t=>t.power)):0);
 const recipeList=Object.values(chosen).filter((r,i,a)=>a.findIndex(x=>x.id===r.id)===i);
 const demands={},outputs={};
 for(const r of recipeList){const batches=net.batches[r.id]||0;if(!batches)continue;
  for(const x of r.inputs)add(demands[r.family]??={},x.item,batches*x.amount);
  for(const x of r.outputs)add(outputs[r.family]??={},x.item,batches*x.amount);
 }
 for(const route of routes)route.items=demands[route.id]||{};
 const coalRoute=routes.find(r=>r.id==='generation');
 const coalCapacity=Math.min(mining.carbon,coalRoute?coalRoute.capacity:Infinity);
 // Conservative sustained all-on check. Fuel shortages are outages, never fractional speed.
 const duty=fuel?(coalCapacity>=fuel?1:0):0,supply=80+gen*duty,ratio=supply>=power?1:0;
 if(!ratio)net.missing.push(duty||!gen?'power capacity':'sustained generator fuel');
 const loads=[];
 for(const [family,work]of Object.entries(net.work))loads.push({id:family,label:family,seconds:families[family]?work/families[family]:Infinity});
 for(const id of rawIds)if(net.raw[id])loads.push({id,label:id+' mining',seconds:mining[id]?net.raw[id]/mining[id]:Infinity});
 if(opts.logistics){
  for(const route of routes){const total=Object.values(route.items).reduce((a,b)=>a+b,0);
   if(total&&!route.capacity)net.missing.push(route.id+' input belt');
   if(total)loads.push({id:route.id,label:route.id+' input belt',seconds:route.capacity?total/route.capacity:Infinity});
  }
  // Machine output budget is shared by all products, not multiplied by port count.
  for(const [family,items]of Object.entries(outputs))loads.push({id:family,label:family+' output transfer',seconds:Object.values(items).reduce((a,b)=>a+b,0)/(3*families[family])});
 }
 loads.sort((a,b)=>b.seconds-a.seconds);
 const production=loads.length?loads[0].seconds/ratio:0, research=opts.research?data.techs.reduce((s,t)=>s+t.seconds,0)/ratio:0;
 const seconds=net.missing.length?Infinity:production+phaseTime/ratio+research+construction/4;
 // Rates allocate each machine pool by its share of the goal's recipe work.
 // Consumers share each source in proportion to their material requirement.
 const totalDemand={};for(const items of Object.values(demands))for(const [id,n]of Object.entries(items))add(totalDemand,id,n);
 const flows=[];
 for(const [family,items]of Object.entries(demands)){
  const poolSeconds=net.work[family]/families[family],route=routes.find(r=>r.id===family);
  const total=Object.values(items).reduce((a,b)=>a+b,0);
  const inputs=Object.entries(items).map(([item,amount])=>{
   const source=rawIds.includes(item)?item:recipe(item)?.family;
   const available=rawIds.includes(item)?mining[item]:(outputs[source]?.[item]||0)/(net.work[source]/families[source]);
   const supply=available*amount/totalDemand[item];
   const need=amount/poolSeconds, belt=route?route.capacity*amount/total:Infinity;
   return {item,source,amount,supply:supply*ratio*60,need:need*ratio*60,belt:Number.isFinite(belt)?belt*ratio*60:Infinity};
  });
  const utilization=Math.min(1,...inputs.map(x=>x.need?Math.min(x.supply,x.belt)/x.need:1));
  for(const input of inputs){input.flow=input.need*utilization;
   input.limit=input.belt<input.need&&input.belt<=input.supply?'Belt limited':input.supply<input.need?'Upstream shortage':input.supply>input.need*1.05?'Excess upstream capacity':'Matched';}
  flows.push({id:family,inputs,utilization,need:total/poolSeconds*ratio*60,capacity:route?route.capacity*ratio*60:Infinity});
 }
 if(coalRoute)flows.push({id:'generation',utilization:duty,need:fuel*60,capacity:coalRoute.capacity*ratio*60,inputs:[{item:'carbon',source:'carbon',supply:mining.carbon*ratio*60,need:fuel*60,belt:coalRoute.capacity*ratio*60,flow:fuel*duty*60,limit:duty<.999?'Fuel shortage':'Matched'}]});
 const carbon=Number.isFinite(seconds)?Math.ceil((fuel*duty+(net.raw.carbon||0)/(seconds||1))*seconds):null;
 const energy=Number.isFinite(seconds)?Math.min(power,supply)*seconds/3600:null;
 return {routes,flows,seconds,production,research,assembly:phaseTime/ratio,construction:construction/4,ratio,supply,power,duty,fuel,carbon,energy,mining,
  raw:net.raw,gross:gross.raw,projectRaw:projectExpansion.raw,capitalRaw:Object.fromEntries(rawIds.map(id=>[id,(gross.raw[id]||0)-(projectExpansion.raw[id]||0)])),
  credit:Object.fromEntries(rawIds.map(id=>[id,(gross.raw[id]||0)-(net.raw[id]||0)])),capital,project,work:net.work,batches:net.batches,missing:net.missing,loads,chosen};
}
root.FactoryModel={calculate};if(typeof module!=='undefined')module.exports=root.FactoryModel;
})(typeof window==='undefined'?globalThis:window);
