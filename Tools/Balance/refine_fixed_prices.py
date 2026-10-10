"""Reproducible fixed-price sensitivity search; never modifies the live catalog."""
import json, subprocess, hashlib, shutil, sys
from pathlib import Path
import calibrate_starter as s

OUT=s.HERE/'reports/starter-fixed-refinement'
PATHS=[1,2,3,4,5,6,8,12,20,40]

def run(cases,name):
    if '--resume' in sys.argv and (OUT/(name+'-results.json')).exists():
        if json.loads((OUT/(name+'-cases.json')).read_text())!=cases:raise ValueError('Resume cases differ')
        return json.loads((OUT/(name+'-results.json')).read_text())
    s.write(OUT/(name+'-cases.json'),cases)
    subprocess.run(['pwsh','-NoProfile','-File',str(s.HERE/'starter_calibration.ps1'),'-Cases',str(OUT/(name+'-cases.json')),'-Output',str(OUT/(name+'-results.json')),'-Workers','15'],check=True)
    return json.loads((OUT/(name+'-results.json')).read_text())

def summarize(rows,parameters):
    result=[]
    for ident,p in parameters.items():
        best={}
        for r in rows:
            if r['id']!=ident or not r['result']['success']:continue
            n=r['path']
            if n not in best or r['result']['seconds']<best[n]['result']['seconds']:best[n]=r
        t={n:r['result']['seconds']/60 for n,r in best.items()}
        if not all(n in t for n in PATHS):continue
        mid=min(t[n] for n in [4,5,6]); optimum=min(t,key=t.get)
        # Keep the accepted one-hour / 2–3x narrow-factory result; improve width 12.
        eligible=54<=mid<=66 and optimum in [4,5,6] and 2<=t[1]/mid<=3
        result.append(dict(id=ident,parameters=p,times=t,eligible=eligible,ratio12=t[12]/mid,best=best))
    return sorted(result,key=lambda r:(not r['eligible'],-r['ratio12'],abs(min(r['times'][n] for n in [4,5,6])-60)))

def main():
    if OUT.exists() and '--resume' not in sys.argv:raise ValueError('Output already exists; use --resume to reuse completed batches')
    base=json.loads((s.ROOT/'Assets/Resources/catalog.json').read_text())
    old=json.loads((s.HERE/'reports/starter-one-hour/best-paths.json').read_text())
    previous={r['path']:r['allocation'] for r in old}
    settings=[{}]
    for key,values in [('basicCost',[2,4,8]),('powerCost',[4,12]),('capital',[3,5]),('industrialRate',[.5,.25]),('refund',[0,1]),('research',[24,40])]:
        settings.extend({key:v} for v in values)
    settings += [dict(basicCost=4,research=40),dict(capital=4,research=40,speed=5),dict(powerCost=8,research=40,speed=5)]
    parameters={};cases=[]
    for i,changes in enumerate(settings):
        p=dict(research=32,capital=2,speed=4,refund=.25,basicCost=1,powerCost=1,industrialRate=1);p.update(changes)
        ident=f'f{i:02}';parameters[ident]=p;c=s.tune(base,p)
        for b in c['buildings']:
            factor=p['basicCost'] if b['id'] in ['miner','smelter','workshop'] else p['powerCost'] if b['id']=='generator' else 1
            for cost in b['cost']:cost['amount']=max(1,round(cost['amount']*factor))
            if b['id']=='industrialMiner':b['rate']*=p['industrialRate']
        for r in c['recipes']:
            if r['family'] in ['industrialSmelter','structural','electrical']:r['seconds']/=p['industrialRate']
        cat=OUT/'catalogs'/(ident+'.json');s.write(cat,c)
        for n in PATHS:
            seq=set(s.allocations(n));seq.add(previous[n])
            for a in sorted(seq):cases.append([str(cat),a,ident,'freeLogistics'])
    s.write(OUT/'parameters.json',parameters)
    print(f'Coarse pass: {len(parameters)} economies, {len(cases)} simulations',flush=True)
    summary=summarize(run(cases,'coarse'),parameters);s.write(OUT/'coarse-summary.json',summary)
    for r in summary:print(r['id'],r['eligible'],round(r['ratio12'],3),{n:round(t,1) for n,t in r['times'].items()},flush=True)
    # Challenge promising economies against exactly the same 568 allocations as baseline.
    ids=['f00']+[r['id'] for r in summary if r['eligible'] and r['id']!='f00'][:2]
    original=json.loads((s.HERE/'reports/starter-one-hour/cases.json').read_text());cases=[]
    for ident in ids:
        for row in original:
            cases.append([str(OUT/'catalogs'/(ident+'.json')),row[1],ident,'freeLogistics'])
    print(f'Challenge pass: {len(cases)} simulations',flush=True)
    results=run(cases,'challenge');summary=summarize(results,{i:parameters[i] for i in ids});s.write(OUT/'summary.json',summary)
    for r in summary:print('FINAL',r['id'],r['eligible'],round(r['ratio12'],3),{n:round(t,1) for n,t in r['times'].items()},flush=True)
    chosen=next(r for r in summary if r['eligible']);s.write(OUT/'selected.json',chosen)
    shutil.copyfile(OUT/'catalogs'/(chosen['id']+'.json'),OUT/'candidate.catalog.json')
    files=list(s.HERE.glob('*.cs'))+list(s.HERE.glob('*.ps1'))+[Path(__file__),s.HERE/'calibrate_starter.py',s.HERE/'report_fixed_refinement.py',s.HERE/'verify_fixed_refinement.py']+list((s.ROOT/'Assets/Scripts').glob('Factory*.cs'))
    manifest={}
    for f in files:
        relative=f.relative_to(s.ROOT);dest=OUT/'source'/relative;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(f,dest);manifest[str(relative)]=hashlib.sha256(f.read_bytes()).hexdigest()
    s.write(OUT/'source-sha256.json',manifest)

if __name__=='__main__':main()
