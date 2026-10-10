"""Coarse material/rate search for the early industrial milestone; no live edits."""
import copy
import itertools
import json
import subprocess
import time
from pathlib import Path

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
OUT=HERE/'reports/starter-calibration-v1'
def write(p,v):
    p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,indent=2))
def allocations(n):
    if n==1:return ['']
    result=set()
    for share in [1,.75,.5]:
        s='I'
        for i in range(1,n-1):s+='I' if s.count('I')/(i+1)<share else 'C'
        result.add(s)
    return sorted(result)
def tune(base,p):
    c=copy.deepcopy(base);c['buildingRefund']=p['refund']
    for b in c['buildings']:
        b['cost']=[s for s in b['cost'] if s['item'] not in ['silicate','refractory']]
        if b['id'] in ['industrialMiner','industrialSmelter','structural','electrical']:
            for s in b['cost']:s['amount']=max(1,round(s['amount']*p['capital']))
        if b['family']=='miner':b['rate']*=p['speed']
    for r in c['recipes']:
        if r['family'] in ['smelter','industrialSmelter','workshop','structural','electrical']:r['seconds']/=p['speed']
    for t in c['techs']:
        t['cost']=[s for s in t['cost'] if s['item'] not in ['silicate','refractory']]
    for s in c['techs'][0]['cost']:s['amount']=max(1,round(s['amount']*p['research']))
    return c
def score(times):
    if not all(n in times for n in [1,3,4,5,6,8,12,40]):return 1000
    best=min(times,key=times.get);mid=min(times[n] for n in [4,5,6]);loss=abs(mid-60)/60
    loss+=max(0,2-times[1]/mid)*2+max(0,times[1]/mid-3)
    loss+=max(0,2-times[12]/mid)*2+max(0,times[12]/mid-3)
    loss+=max(0,1.3*times[12]/times[40]-1)*2
    loss+=max(0,1.2-times[3]/mid)+max(0,1.25-times[8]/mid)
    loss+=max(0,(mid-times[best])/mid)*5
    return loss
def main():
    if OUT.exists():raise ValueError('Choose a new output directory')
    base=json.loads((ROOT/'Assets/Resources/catalog.json').read_text());rows=[];cases=[]
    parameters=[dict(research=r,capital=c,speed=2,refund=.25) for r,c in itertools.product([12,20,32],[2,4,8])]
    parameters.insert(0,dict(research=1,capital=1,speed=1,refund=1))
    for i,p in enumerate(parameters):
        ident=f'c{i:02}';cat=OUT/'catalogs'/f'{ident}.json';write(cat,tune(base,p));rows.append(dict(id=ident,parameters=p))
        for n in [1,3,4,5,6,8,12,40]:
            for allocation in allocations(n):cases.append([str(cat),allocation,ident])
    write(OUT/'cases.json',cases);write(OUT/'parameters.json',rows)
    start=time.monotonic();proc=subprocess.Popen(['pwsh','-NoProfile','-File',str(HERE/'starter_calibration.ps1'),'-Cases',str(OUT/'cases.json'),'-Output',str(OUT/'results.json')],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    for line in proc.stdout:
        print(line.rstrip(),flush=True)
        write(OUT/'progress.json',dict(message=line.strip(),elapsedSeconds=time.monotonic()-start,total=len(cases)))
    if proc.wait():raise RuntimeError('Simulator failed')
    results=json.loads((OUT/'results.json').read_text())
    for row in rows:
        times={};chosen={}
        for trial in results:
            if trial['id']!=row['id'] or not trial['result']['success']:continue
            n=trial['path'];t=trial['result']['seconds']/60
            if t<times.get(n,float('inf')):times[n]=t;chosen[n]=trial['allocation']
        row.update(times=times,allocations=chosen,loss=score(times))
    rows.sort(key=lambda r:r['loss']);write(OUT/'summary.json',rows)
    for row in rows:print(row['id'],row['parameters'],round(row['loss'],3),{k:round(v,1) for k,v in row['times'].items()},flush=True)
if __name__=='__main__':main()
