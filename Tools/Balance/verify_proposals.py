"""Validate numerical-model proposals in the optimized, actual C# game simulation."""
import argparse
import hashlib
import subprocess
import time
from pathlib import Path
import balance as b
import calibrate


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--proposals',type=Path,required=True)
    p.add_argument('--output',type=Path,required=True);p.add_argument('--workers',type=int,default=15);a=p.parse_args()
    output=a.output.resolve();output.mkdir(parents=True,exist_ok=True)
    if (output/'manifest.json').exists():raise ValueError('Choose a new output directory')
    parameters=b.read(a.proposals/'parameters.json');prediction=b.read(a.proposals/'predictions.json')
    original=b.read(b.ROOT/'Assets/Resources/catalog.json');config=b.read(b.HERE/'spec/targets.json')
    defs=b.scenarios(b.HERE/'spec/targets.json',config)
    sources=list((b.ROOT/'Assets/Scripts').glob('Factory*.cs'))+[b.HERE/n for n in ['FactoryPlanner.cs','SearchPolicy.cs','Runner.cs','BatchSearch.cs','BalanceCases.cs','load_optimized.ps1','run_cases_optimized.ps1','balance.py','calibrate.py','surrogate_search.py','verify_proposals.py']]
    hashes={str(p.relative_to(b.ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sources}
    b.write(output/'manifest.json',dict(parameters=parameters,catalogHash=b.digest(original),sourceHashes=hashes,definitions=defs,config=config,compiler='optimized'))
    for source in sources:
        dest=output/'sources'/source.relative_to(b.ROOT);dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(source.read_bytes())
    b.write(output/'catalog.snapshot.json',original)
    rows=[dict(id=f'p{i:03d}',parameters=v) for i,v in enumerate(parameters)]
    completed=planned=0;started=time.monotonic()
    def progress(phase):
        d=dict(predictions=prediction['predictions'],heldOutLogRMSE=prediction['heldOutLogRMSE'],phase=phase,completed=completed,planned=planned,candidates=rows,elapsedSeconds=time.monotonic()-started)
        b.write(output/'summary.json',d);b.write(b.HERE/'web/calibration-proposals.json',d)
    def batch(ids,names,step,phase):
        nonlocal completed,planned
        cases=[];paths={}
        for i in ids:
            cat=b.tune(original,parameters[i]);cp=output/'catalogs'/f'p{i:03d}.json';b.write(cp,cat)
            for name in names:
                pol=calibrate.supported(defs[name]['policy'],cat,original);pol.update(name=name,trace=True,maxSeconds=43200)
                pp=output/'policies'/f'p{i:03d}-{name}.json';b.write(pp,pol)
                replay=output/'replays'/f'{phase}-p{i:03d}-{name}.json';replay.parent.mkdir(exist_ok=True)
                cases.append([str(cp),str(pp),str(replay)]);paths[i,name]=replay
        planned+=len(cases);progress(phase);casefile=output/(phase+'.cases.json');b.write(casefile,cases)
        cmd=['pwsh','-NoProfile','-File',str(b.HERE/'run_cases_optimized.ps1'),'-Cases',str(casefile),'-Step',str(step),'-Workers',str(a.workers)]
        worker=subprocess.Popen(cmd,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,encoding='utf-8',errors='replace')
        with (output/(phase+'.log')).open('w',encoding='utf-8') as log:
            for line in worker.stdout:
                log.write(line);log.flush()
                if '.json ' in line:
                    completed+=1;progress(phase);print(f'PROPOSAL VALIDATION {completed}/{planned}: {line.strip()}',flush=True)
            if worker.wait():raise RuntimeError('Proposal validation failed')
        for name,expected in hashes.items():
            if hashlib.sha256((b.ROOT/name).read_bytes()).hexdigest()!=expected:raise RuntimeError('Source changed: '+name)
        return {key:b.read(path) for key,path in paths.items()}
    names=['reference','minimum','over_6_reclaim'];r=batch(range(len(rows)),names,.5,'screen')
    subset={n:dict(defs[n],full=False) for n in names}
    for i,row in enumerate(rows):
        data={n:r[i,n] for n in names};row['screen']=b.score(data,subset,config['targets'])
        row['times']={n:v['seconds']/60 if v['success'] else None for n,v in data.items()}
    winners=sorted([i for i in range(len(rows)) if rows[i]['screen']['passed']],key=lambda i:(rows[i]['screen']['loss'],i))[:2]
    if not winners:
        progress('complete: coarse targets failed; no fine replays')
        print('All proposals fail coarse targets. No fine-resolution tests scheduled.',flush=True)
        return
    r=batch(winners,list(defs),.125,'validation')
    for i in winners:
        data={n:r[i,n] for n in defs};rows[i]['validation']=b.score(data,defs,config['targets'],True)
        rows[i]['times']={n:v['seconds']/60 if v['success'] else None for n,v in data.items()}
        rows[i]['passed']=False  # No fine-step/width/global-optimum claim from this stage.
    progress('complete')
    print('PROPOSAL CHECK COMPLETE: inspect measured gates; no automatically applied balance.',flush=True)


if __name__=='__main__':main()
