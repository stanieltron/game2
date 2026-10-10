"""Compare matched basic-smelter expansion paths against industrial research gates."""
import argparse,copy,json,subprocess,concurrent.futures
from datetime import datetime
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,default=ROOT/'Tools/Balance/reports'/('industrial-window-'+datetime.now().strftime('%Y%m%d-%H%M%S')));a=p.parse_args()
if a.output.exists() and any(a.output.iterdir()):raise SystemExit('Output is not empty; choose a new directory.')
a.output.mkdir(parents=True,exist_ok=True)
catalog=json.loads((ROOT/'Assets/Resources/catalog.json').read_text());base=json.loads((ROOT/'Tools/Balance/reports/search-validated/best-policy.json').read_text())
jobs=[]
for name,plate,wire,timer in [('current',20,10,60),('research-60',60,20,60),('research-100',100,30,60),('timer-600',20,10,600)]:
 c=copy.deepcopy(catalog);c['techs'][0]['cost']=[dict(item='plate',amount=plate),dict(item='wire',amount=wire),dict(item='refractory',amount=4)];c['techs'][0]['seconds']=timer
 d=a.output/name;d.mkdir(exist_ok=True);(d/'catalog.snapshot.json').write_text(json.dumps(c,indent=2))
 for n in range(1,5):
  policy=copy.deepcopy(base);policy['name']=f'{name}-smelters-{n}'
  # Establish the same viable starter factory before funding optional expansion.
  # Otherwise a large initial target can spend the workshop/generator's starter stock.
  for target in policy['stages'][0]:
   if target['spec']=='smelter':target['count']=1
  if n>=2:policy['stages'][0].append(dict(spec='smelter',count=2))
  if n>=3:policy['stages'][0].extend([dict(spec='miner:iron',count=2),dict(spec='smelter',count=3)])
  if n>=4:policy['stages'][0].extend([dict(spec='miner:copperOre',count=2),dict(spec='smelter',count=4)])
  path=d/f'policy-{n}.json';path.write_text(json.dumps(policy,indent=2));out=d/f'replay-{n}.json';jobs.append((name,n,path,out))
def run(job):
 name,n,path,out=job
 if not out.exists():
  result=subprocess.run(['pwsh','-NoProfile','-File',str(ROOT/'Tools/Balance/search.ps1'),'-Policy',str(path),'-Output',str(out)],cwd=ROOT,capture_output=True,text=True)
  if result.returncode:raise RuntimeError(result.stderr+result.stdout)
 r=json.loads(out.read_text());unlock=next((m['seconds']/60 for m in r['milestones'] if m['tech']==1),None)
 row=dict(profile=name,smelters=n,success=r['success'],fighterMinutes=r['seconds']/60,industrialUnlocked=unlock,fundingMinutes=r['capitalWaitSeconds']/60,brownoutMinutes=r['brownoutSeconds']/60,error=r['error'])
 print(json.dumps(row),flush=True);return row
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as ex:rows=list(ex.map(run,jobs))
(a.output/'comparison.json').write_text(json.dumps(rows,indent=2))
