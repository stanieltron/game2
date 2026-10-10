"""Coarse-to-fine search whose descendants always come from a measured improvement.

Rejected candidates never receive nearby refinement. Parameter interactions can
reopen a direction only after another accepted setting changes the context.
"""
import argparse
import copy
import hashlib
import subprocess
import time
from pathlib import Path
import balance as b
import calibrate

BOUNDS={'capital':(.25,12),'cycles':(.25,2.5),'refund':(0,1),'power':(.5,2),
        'starterPower':(1,1.8),'generatorCapital':(.5,2.5),'feedSpeed':(.5,2),'researchCapital':(.5,2)}
STEPS=dict(capital=.5,cycles=.5,refund=.5,power=.5,starterPower=.4,generatorCapital=.75,feedSpeed=.5,researchCapital=.5)


def children(parent,steps,seen):
    if parent.get('decision') not in ['baseline','accepted']:
        return []
    result=[]
    for key,step in steps.items():
        for sign in [-1,1]:
            values=copy.deepcopy(parent['parameters']);lo,hi=BOUNDS[key]
            values[key]=round(max(lo,min(hi,values[key]+sign*step)),5)
            keyhash=b.digest(values)
            if values==parent['parameters'] or keyhash in seen:continue
            seen.add(keyhash)
            result.append(dict(parameters=values,parent=parent['id'],change=dict(parameter=key,fromValue=parent['parameters'][key],toValue=values[key]),decision='pending'))
    return result


def choose(parent,rows,minimum_improvement=.01):
    winner=min(rows,key=lambda r:(r['score']['loss'],r['id']))
    if winner['score']['loss']>=parent['score']['loss']-minimum_improvement:
        for r in rows:r['decision']='rejected: no useful improvement; no refinement'
        return None
    for r in rows:r['decision']='accepted' if r is winner else 'rejected: weaker than accepted candidate; no refinement'
    return winner


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--budget',type=int,default=24)
    p.add_argument('--workers',type=int,default=15);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    if a.budget<2 or a.workers<1:p.error('Positive workers and budget >= 2 required')
    output=a.output.resolve();output.mkdir(parents=True,exist_ok=True)
    if (output/'manifest.json').exists():raise ValueError('Choose a new output directory')
    base=b.read(b.ROOT/'Assets/Resources/catalog.json');config=b.read(b.HERE/'spec/targets.json')
    defs=b.scenarios(b.HERE/'spec/targets.json',config)
    defs={n:dict(d,full=False) for n,d in defs.items() if not d['full'] or n=='over_6_reclaim'}
    sources=list((b.ROOT/'Assets/Scripts').glob('Factory*.cs'))+[b.HERE/n for n in ['FactoryPlanner.cs','SearchPolicy.cs','Runner.cs','BatchSearch.cs','BalanceCases.cs','load.ps1','run_cases.ps1','balance.py','calibrate.py','directional_search.py']]
    hashes={str(s.relative_to(b.ROOT)):hashlib.sha256(s.read_bytes()).hexdigest() for s in sources}
    b.write(output/'manifest.json',dict(catalogHash=b.digest(base),definitions=defs,targets=config['targets'],bounds=BOUNDS,steps=STEPS,budget=a.budget,sourceHashes=hashes,step=.5))
    for source in sources:
        target=output/'sources'/source.relative_to(b.ROOT);target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(source.read_bytes())
    b.write(output/'catalog.snapshot.json',base)
    rows=[dict(id='d000',parameters=calibrate.candidates(1)[0],parent=None,decision='baseline')]
    completed=scheduled=0;started=time.monotonic();round_number=0
    def progress(phase):
        data=dict(phase=phase,completed=completed,scheduled=scheduled,candidateBudget=a.budget,
                  evaluated=sum('score' in r for r in rows),rows=rows,elapsedSeconds=time.monotonic()-started,
                  rule='Only an accepted improvement can have children; rejected candidates get no refinement. All search runs use the same coarse timestep.')
        b.write(output/'summary.json',data);b.write(b.HERE/'web/directional-progress.json',data)
    def evaluate(batch):
        nonlocal completed,scheduled,round_number
        round_number+=1;cases=[];results={}
        for row in batch:
            cat=b.tune(base,row['parameters']);cp=output/'catalogs'/(row['id']+'.json');b.write(cp,cat)
            for name,d in defs.items():
                policy=calibrate.supported(d['policy'],cat,base);policy.update(name=name,trace=True,maxSeconds=43200)
                pp=output/'policies'/(row['id']+'-'+name+'.json');b.write(pp,policy)
                rp=output/'replays'/(row['id']+'-'+name+'.json');rp.parent.mkdir(exist_ok=True)
                cases.append([str(cp),str(pp),str(rp)]);results[row['id'],name]=rp
        scheduled+=len(cases);progress('coarse comparisons');path=output/f'round-{round_number}.cases.json';b.write(path,cases)
        process=subprocess.Popen(['pwsh','-NoProfile','-File',str(b.HERE/'run_cases.ps1'),'-Cases',str(path),'-Step','.5','-Workers',str(a.workers)],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,encoding='utf-8',errors='replace')
        with (output/f'round-{round_number}.log').open('w',encoding='utf-8') as log:
            for line in process.stdout:
                log.write(line);log.flush()
                if '.json ' in line:
                    completed+=1;progress('coarse comparisons');print(f'DIRECTIONAL {completed}/{scheduled}: {line.strip()}',flush=True)
            if process.wait():raise RuntimeError('Simulation batch failed')
        for name,expected in hashes.items():
            if hashlib.sha256((b.ROOT/name).read_bytes()).hexdigest()!=expected:raise RuntimeError('Source changed: '+name)
        for row in batch:
            r={n:b.read(results[row['id'],n]) for n in defs};row['score']=b.score(r,defs,config['targets'])
            row['times']={n:v['seconds']/60 if v['success'] else None for n,v in r.items()}
    evaluate(rows);center=rows[0];steps=dict(STEPS);seen={b.digest(center['parameters'])}
    while len(rows)<a.budget:
        proposals=children(center,steps,seen)[:a.budget-len(rows)]
        if not proposals:break
        for index,row in enumerate(proposals,len(rows)):row['id']=f'd{index:03d}'
        rows.extend(proposals);evaluate(proposals);winner=choose(center,proposals)
        if winner is None:
            progress('complete: no improving coarse direction; stopped')
            break
        center=winner;steps={k:v/2 for k,v in steps.items()}
        progress('refining accepted improvement only')
    b.write(output/'best-parameters.json',center['parameters']);b.write(output/'best-catalog.json',b.tune(base,center['parameters']))
    progress('complete')
    print(f"DONE {completed}/{scheduled}; {len(rows)} candidates; best {center['id']}; coarse gates {'PASS (needs validation)' if center['score']['passed'] else 'FAIL'}; no fine-timestep reruns",flush=True)


if __name__=='__main__':main()
