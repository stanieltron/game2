"""Matched whole-chain balance experiments; isolated catalogs, fixed policy families."""
import argparse,copy,json,subprocess,concurrent.futures
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);p.add_argument('--round',type=int,default=1);a=p.parse_args()
if a.output.exists() and any(a.output.iterdir()):raise SystemExit('Choose an empty output directory')
a.output.mkdir(parents=True,exist_ok=True)
base=json.loads((ROOT/'Assets/Resources/catalog.json').read_text());(a.output/'baseline.catalog.json').write_text(json.dumps(base,indent=2))
def stacks(**kw):return [dict(item=k,amount=v) for k,v in kw.items()]
def candidate(kind):
 c=copy.deepcopy(base);b={x['id']:x for x in c['buildings']};r={x['id']:x for x in c['recipes']}
 if kind=='baseline':return c,1
 b['miner']['power']=14;b['smelter']['power']=24;b['workshop']['power']=28
 b['industrialMiner'].update(rate=30,power=35,cost=stacks(plate=24,beam=6,wire=8))
 b['industrialSmelter'].update(power=60,cost=stacks(plate=32,beam=8,refractory=8,wire=8))
 b['industrialGenerator']['cost']=stacks(plate=36,beam=8,wire=20,refractory=8)
 for key in ['structural','electrical','mechanical','fluid']:
  for x in b[key]['cost']:x['amount']*=2
 for key in ['industrialSmelter_steel','industrialSmelter_copper']:r[key]['seconds']=2
 for key,v in r.items():
  if key.startswith('spec_'):v['seconds']*=2
 for key in ['work_frame','spec_frame']:r[key]['inputs']=stacks(beam=3,plate=3)
 for key in ['drive']:r[key]['inputs']=stacks(motor=3,bearing=3,frame=2)
 for key in ['cabinet']:r[key]['inputs']=stacks(power=3,control=3,frame=2,bus=3)
 c['techs'][0]['cost']=stacks(plate=120,wire=40,refractory=8)
 c['techs'][1]['cost']=stacks(frame=12,motorCore=12,electrical=12,wire=40)
 c['techs'][2]['cost']=stacks(frame=20,motor=10,pump=6,power=6,control=6)
 c['techs'][3].update(cost=stacks(frame=32,motor=16,pump=12,power=12,control=12,drive=8),seconds=180)
 if kind=='moderate':
  c['techs'][0]['cost']=stacks(plate=100,wire=30,refractory=6)
  for key in ['structural','electrical','mechanical','fluid']:
   for x in b[key]['cost']:x['amount']=max(1,round(x['amount']*.75))
 if a.round>=2:
  # Preserve a production investment, without forcing every industrial purchase
  # through a slow universal workshop before specialized fabrication exists.
  b['industrialMiner']['cost']=stacks(plate=18,beam=4,wire=8)
  b['industrialSmelter']['cost']=stacks(plate=24,beam=6,refractory=6,wire=8)
  b['industrialGenerator']['cost']=stacks(plate=24,beam=6,wire=12,refractory=6)
  if kind in ['productive','lean-capital']:
   for key,v in r.items():
    if key.startswith('spec_'):v['seconds']*=.75
   c['techs'][1]['cost']=stacks(frame=8,motorCore=8,electrical=8,wire=30)
   c['techs'][2]['cost']=stacks(frame=16,motor=8,pump=4,power=4,control=4)
 if a.round>=3:
  if kind in ['lean-capital','efficient','efficient-40']:
   for key in ['structural','electrical','mechanical','fluid']:
    for x in b[key]['cost']:x['amount']=max(1,round(x['amount']*.75))
 if a.round>=4:
  for key,v in r.items():
   if key.startswith('spec_'):v['seconds']=next(x['seconds'] for x in base['recipes'] if x['id']==key)
  c['techs'][1]['cost']=stacks(frame=8,motorCore=8,electrical=8,wire=30)
  c['techs'][2]['cost']=stacks(frame=16,motor=8,pump=4,power=4,control=4)
  if kind=='efficient-40':
   b['industrialMiner']['rate']=40
   for key in ['industrialSmelter_steel','industrialSmelter_copper']:r[key]['seconds']=1.5
 return c,.4

def target(spec,n):return dict(spec=spec,count=n)
def policy(early=3,late=2,variant='normal'):
 initial=[target('miner:iron',1),target('miner:copperOre',1),target('miner:carbon',1),target('smelter',1),target('workshop',1),target('generator',1),target('miner:silicate',1)]
 if early>=2:initial +=[target('smelter',2),target('workshop',2)]
 if early>=3:initial +=[target('miner:iron',2),target('miner:carbon',2),target('generator',2),target('smelter',3),target('workshop',3)]
 if early>=4:initial +=[target('miner:carbon',2),target('miner:copperOre',2),target('generator',3),target('smelter',4),target('workshop',4)]
 stages=[initial]
 for tech in [1,2,3]:
  k=1 if tech==1 else late
  stage=[target('industrialMiner:carbon',1 if k<=2 else 2),target('industrialGenerator',max(2,k+1)),target('industrialMiner:iron',max(1,k*2)),target('industrialMiner:copperOre',k),target('industrialMiner:silicate',1),target('industrialSmelter',k*3),target('structural',k*2),target('electrical',k*2),target('mechanical',k*2)]
  if tech>=2:stage.append(target('fluid',k))
  if variant=='no-industrial':
   stage=[target(x['spec'].replace('industrialMiner','miner').replace('industrialSmelter','smelter').replace('industrialGenerator','generator'),x['count']*(3 if x['spec'].startswith('industrial') else 1)) for x in stage]
  if variant=='weak-power':
   for x in stage:
    if x['spec']=='industrialGenerator':x['count']=1
  if variant in ['weak-structural','weak-electrical','weak-mechanical','weak-mining']:
   for x in stage:
    if x['spec']==variant.replace('weak-','') or (variant=='weak-mining' and x['spec'] in ['industrialMiner:iron','industrialMiner:copperOre']):x['count']=1
  if a.round>=2 and tech==1 and variant!='no-industrial':
   first=[target('structural',1),target('electrical',1),target('industrialMiner:carbon',1),target('industrialGenerator',1),target('industrialMiner:iron',1),target('industrialSmelter',1),target('mechanical',1)]
   if variant=='weak-power':first=[x for x in first if x['spec']!='industrialGenerator']+[target('industrialGenerator',1)]
   stage=first+stage
  stages.append(stage)
 return dict(name=f'e{early}-l{late}-{variant}',scale=late,lanes=2,tilesPerLane=5,recipeRule=0,upgrade=variant!='no-industrial',reclaim=True,transport=True,trace=True,maxSeconds=28800,stages=stages,**(dict(laneTargets=[1,1,2,2]) if a.round>=3 else {}))
configs=[('rush',1,2,'normal'),('double',2,2,'normal'),('triple',3,2,'normal'),('overbuild',4,2,'normal'),('small-late',3,1,'normal'),('large-late',3,3,'normal'),('weak-power',3,2,'weak-power'),('weak-structural',3,2,'weak-structural'),('weak-electrical',3,2,'weak-electrical'),('weak-mechanical',3,2,'weak-mechanical'),('weak-mining',3,2,'weak-mining'),('no-industrial',3,2,'no-industrial')]
jobs=[]
for kind in (['baseline','moderate','investment'] if a.round==1 else (['productive','moderate'] if a.round==2 else (['productive','lean-capital'] if a.round==3 else ['efficient','efficient-40']))):
 c,phase=candidate(kind);d=a.output/kind;d.mkdir();(d/'catalog.snapshot.json').write_text(json.dumps(c,indent=2));(d/'experiment.json').write_text(json.dumps(dict(phaseScale=phase)))
 for name,early,late,variant in configs:
  q=policy(early,late,variant);q['name']=name;(d/(name+'.policy.json')).write_text(json.dumps(q,indent=2));jobs.append((kind,d,name,phase))
def run(job):
 kind,d,name,phase=job;out=d/(name+'.replay.json')
 run=subprocess.run(['pwsh','-NoProfile','-File',str(ROOT/'Tools/Balance/balance_probe.ps1'),'-Catalog',str(d/'catalog.snapshot.json'),'-Policy',str(d/(name+'.policy.json')),'-Output',str(out),'-PhaseScale',str(phase)],cwd=ROOT,capture_output=True,text=True)
 if run.returncode:raise RuntimeError(run.stderr+run.stdout)
 r=json.loads(out.read_text());ms={m['stage']:round(m['seconds']/60,2) for m in r['milestones']}
 row=dict(balance=kind,policy=name,success=r['success'],minutes=round(r['seconds']/60,2),brown=round(r['brownoutSeconds']/60,2),energy=round(r['energyPUHours']),milestones=ms,error=r['error'])
 print(json.dumps(row),flush=True);return row
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as ex:rows=list(ex.map(run,jobs))
(a.output/'comparison.json').write_text(json.dumps(rows,indent=2))
