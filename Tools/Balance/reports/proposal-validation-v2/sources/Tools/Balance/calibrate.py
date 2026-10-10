"""Deterministic multi-fidelity economy search. Candidate catalogs stay isolated."""
import argparse
import copy
import hashlib
import html
import json
import math
import subprocess
import time
from pathlib import Path
import balance as b
import widths


def halton(n, base):
    value, fraction = 0, 1
    while n:
        fraction /= base
        n, digit = divmod(n, base)
        value += digit * fraction
    return value


def candidates(count):
    baseline = dict(capital=1, cycles=1, refund=1, power=1, starterPower=1,
                    generatorCapital=1, feedSpeed=1, researchCapital=1)
    rows = [baseline]
    # Controlled capital/speed/refund combinations precede broad low-discrepancy exploration.
    for capital in [2, 4, 8, 12]:
        for cycles in [.5, 1, 1.5]:
            for refund in [0, .5]:
                rows.append(dict(baseline, capital=capital, cycles=cycles, refund=refund))
    ranges = {'capital': (1, 12), 'cycles': (.35, 1.8), 'refund': (0, .75),
              'power': (.5, 2), 'starterPower': (1, 1.8), 'generatorCapital': (.7, 2.5),
              'feedSpeed': (.8, 2), 'researchCapital': (.6, 1.6)}
    i = 1
    while len(rows) < count:
        rows.append({k: round(lo + (hi-lo)*halton(i, prime), 5)
                     for (k, (lo, hi)), prime in zip(ranges.items(), [2,3,5,7,11,13,17,19])})
        i += 1
    return rows[:count]


def supported(policy, catalog, original):
    """Scale generator/coal targets with changed power, keeping the same investment policy.

    This is a conservative support heuristic, not an optimal power dispatcher.
    """
    p = copy.deepcopy(policy)
    defs = {x['id']: x for x in catalog['buildings']}
    old = {x['id']: x for x in original['buildings']}
    for stage in p['stages']:
        for x in stage:
            if x['spec'] in ['generator', 'industrialGenerator']:
                basic = x['spec'] == 'generator'
                ids = ['miner','smelter','workshop'] if basic else ['industrialMiner','industrialSmelter','structural','mechanical','electrical','fluid']
                ratio = sum(defs[k]['power'] for k in ids)/sum(old[k]['power'] for k in ids)
                # Never remove baseline supporting power in a controlled comparison.
                x['count'] = max(x['count'], math.ceil(x['count']*ratio))
        for x in stage:
            if x['spec'] in ['miner:carbon','industrialMiner:carbon']:
                g = 'generator' if x['spec'] == 'miner:carbon' else 'industrialGenerator'
                n = max([v['count'] for v in stage if v['spec'] == g] or [1])
                x['count'] = max(x['count'], math.ceil(n*defs[g]['fuelRate']/defs[x['spec'].split(':')[0]]['rate']))
    return p


class Search:
    def __init__(self, args):
        self.args, self.output = args, args.output.resolve()
        self.base = b.read(b.ROOT/'Assets/Resources/catalog.json')
        self.config = b.read(b.HERE/'spec/targets.json')
        self.defs = b.scenarios(b.HERE/'spec/targets.json', self.config)
        self.params = candidates(args.candidates)
        self.started = time.monotonic()
        paths = list((b.ROOT/'Assets/Scripts').glob('Factory*.cs')) + [b.HERE/n for n in
                ['FactoryPlanner.cs','SearchPolicy.cs','Runner.cs','BatchSearch.cs','BalanceCases.cs',
                 'run_cases.ps1','load.ps1','balance.py','widths.py','calibrate.py']]
        self.manifest = dict(parameters=self.params, config=self.config, definitions=self.defs,
                             catalogHash=b.digest(self.base), budget=args.candidates, finalists=args.finalists,
                             sourceHashes={str(p.relative_to(b.ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths})
        cache_sources=self.output/'cache-sources.json'
        self.cache_sources=self.manifest['sourceHashes']
        if args.resume and (self.output/'manifest.json').exists():
            self.cache_sources=b.read(cache_sources) if cache_sources.exists() else b.read(self.output/'manifest.json')['sourceHashes']
        b.prepare_output(self.output,self.manifest,args.resume)
        b.write(cache_sources,self.cache_sources)
        for p in paths:
            dest=self.output/'sources'/p.relative_to(b.ROOT);dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(p.read_bytes())
        b.write(self.output/'catalog.snapshot.json',self.base)
        self.cats = [b.tune(self.base,p) for p in self.params]
        self.rows = [dict(id=f'e{i:04d}',parameters=p) for i,p in enumerate(self.params)]
        self.completed, self.planned, self.phase = 0, 0, 'preparing'

    def progress(self):
        ranked = sorted([r for r in self.rows if 'screen' in r], key=lambda r:(r['screen']['loss'],r['id']))
        data=dict(phase=self.phase, completed=self.completed, planned=self.planned,
                  candidatesScreened=len(ranked), candidateBudget=len(self.rows), elapsedSeconds=time.monotonic()-self.started,
                  best=ranked[0] if ranked else None, candidates=self.rows)
        b.write(self.output/'progress.json',data)
        b.write(b.HERE/'web/calibration-progress.json',data)
        return data

    def batch(self, jobs, phase, step):
        self.phase=phase;self.planned+=len(jobs)
        cases=[]; outputs={}
        for i,name,policy in jobs:
            cat=self.cats[i]
            catpath=self.output/'catalogs'/f'e{i:04d}.json';b.write(catpath,cat)
            policy=copy.deepcopy(policy);policy.update(name=name,trace=True,maxSeconds=43200)
            key=b.digest(dict(catalog=cat,policy=policy,step=step,source=self.cache_sources))
            pp=self.output/'policies'/(b.digest(policy)+'.json');b.write(pp,policy)
            result=self.output/'cache'/(key+'.json');result.parent.mkdir(parents=True,exist_ok=True)
            outputs[i,name]=result
            checksum=result.with_suffix('.sha256')
            if result.exists() and checksum.exists() and checksum.read_text().strip()==hashlib.sha256(result.read_bytes()).hexdigest():
                self.completed+=1
            else:
                if result.exists():result.replace(result.with_suffix('.uncommitted'))
                cases.append([str(catpath),str(pp),str(result)])
        self.progress()
        if cases:
            cp=self.output/(phase+'.cases.json');b.write(cp,cases)
            command=['pwsh','-NoProfile','-File',str(b.HERE/'run_cases.ps1'),'-Cases',str(cp),'-Step',str(step),'-Workers',str(self.args.workers)]
            process=subprocess.Popen(command,cwd=b.ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,encoding='utf-8',errors='replace')
            with (self.output/(phase+'.log')).open('w',encoding='utf-8') as log:
                for line in process.stdout:
                    log.write(line);log.flush()
                    if '.json ' in line:
                        self.completed+=1
                        self.progress()
                        print(f'{phase}: {self.completed}/{self.planned} simulations; {line.strip()}',flush=True)
                if process.wait():raise RuntimeError('Batch failed: '+phase)
            for _,_,path in cases:
                path=Path(path);path.with_suffix('.sha256').write_text(hashlib.sha256(path.read_bytes()).hexdigest(),encoding='utf-8')
        for name,expected in self.manifest['sourceHashes'].items():
            if hashlib.sha256((b.ROOT/name).read_bytes()).hexdigest()!=expected:raise RuntimeError('Source changed during run: '+name)
        for _,_,filename in cases:
            path=Path(filename)
            path.with_suffix('.sha256').write_text(hashlib.sha256(path.read_bytes()).hexdigest(),encoding='utf-8')
        results={k:b.read(path) for k,path in outputs.items()}
        b.write(self.output/(phase+'.index.json'),{f'{i}:{name}':str(p.relative_to(self.output)) for (i,name),p in outputs.items()})
        return results

    def jobs(self, ids, names):
        return [(i,n,supported(self.defs[n]['policy'],self.cats[i],self.base)) for i in ids for n in names]

    def execute(self):
        # Only these three tests contribute to coarse rejection. No untested gate can pass.
        names=['reference','minimum','over_6_reclaim']
        defs={n:dict(self.defs[n],full=False) for n in names}
        results=self.batch(self.jobs(range(len(self.rows)),names),'screen',.5)
        for i,row in enumerate(self.rows):
            r={n:results[i,n] for n in names}
            row['screen']=b.score(r,defs,self.config['targets'])
            row['screenTimes']={n:v['seconds']/60 if v['success'] else None for n,v in r.items()}
        ranked=sorted(range(len(self.rows)),key=lambda i:(self.rows[i]['screen']['loss'],i))
        self.progress()
        ids=ranked[:min(16,len(ranked))]
        names=[n for n,d in self.defs.items() if not d['full']]+['over_6_reclaim']
        defs={n:dict(self.defs[n],full=False) for n in names}
        results=self.batch(self.jobs(ids,names),'shortlist',.25)
        for i in ids:
            r={n:results[i,n] for n in names}
            self.rows[i]['shortlist']=b.score(r,defs,self.config['targets'])
            self.rows[i]['shortlistTimes']={n:v['seconds']/60 if v['success'] else None for n,v in r.items()}
        ids=sorted(ids,key=lambda i:(self.rows[i]['shortlist']['loss'],i))[:self.args.finalists]
        results=self.batch(self.jobs(ids,list(self.defs)),'validation',.125)
        for i in ids:
            r={n:results[i,n] for n in self.defs}
            self.rows[i]['validation']=b.score(r,self.defs,self.config['targets'],True)
            self.rows[i]['validationTimes']={n:v['seconds']/60 if v['success'] else None for n,v in r.items()}
        best=min(ids,key=lambda i:(self.rows[i]['validation']['loss'],i))
        # Challenge the winning economy with separately generated staged factories.
        wd,metadata,total=widths.definitions(self.cats[best],48)
        wr=self.batch([(best,n,d['policy']) for n,d in wd.items()],'width-challenge',.25)
        widthrows=[dict(id=n,parameters=metadata[n],result=wr[best,n]) for n in wd]
        widths.render(self.output,widthrows,48,total,.25,b.read(b.HERE/'spec/widths.json'))
        (b.HERE/'web/calibration-widths.html').write_bytes((self.output/'report.html').read_bytes())
        winners=sorted([r for r in widthrows if r['result']['success']],key=lambda r:(r['result']['seconds'],r['id']))
        self.rows[best]['widthChallenge']=dict(evaluated=48,bestSeconds=winners[0]['result']['seconds'] if winners else None,
                                               bestParameters=winners[0]['parameters'] if winners else None)
        # Fine confirmation is expensive and cannot rescue a large known target failure.
        if not self.rows[best]['validation']['passed']:
            row=self.rows[best]
            row.update(passed=False,confirmationSkipped='Full validation already fails',stepStable=None,deterministicReplay=None)
            b.write(self.output/'best-catalog.json',self.cats[best]);b.write(self.output/'best-parameters.json',self.params[best])
            self.phase='complete';summary=self.progress();summary.update(bestCandidate=row,passed=False)
            b.write(self.output/'summary.json',summary);b.write(b.HERE/'web/calibration-progress.json',summary)
            print(f"COMPLETE {self.completed}/{self.planned}; best {row['id']}; FAIL; unnecessary fine confirmation skipped",flush=True)
            return
        # Confirm only candidates that pass every validation gate.
        fine=self.batch(self.jobs([best],list(self.defs)),'confirmation',.0625)
        fine={n:fine[best,n] for n in self.defs}
        row=self.rows[best];row['confirmation']=b.score(fine,self.defs,self.config['targets'],True)
        row['stepStable']=all(abs(fine[n]['seconds']-results[best,n]['seconds'])<=120 for n in self.defs if fine[n]['success'] and results[best,n]['success'])
        replay=self.batch([(best,'fresh-replay',supported(self.defs['reference']['policy'],self.cats[best],self.base))],'replay',.0625)
        row['deterministicReplay']=b.digest(replay[best,'fresh-replay'])==b.digest(fine['reference'])
        row['passed']=row['confirmation']['passed'] and row['stepStable'] and row['deterministicReplay'] and bool(winners) and winners[0]['result']['seconds']>=fine['reference']['seconds']-30
        b.write(self.output/'best-catalog.json',self.cats[best]);b.write(self.output/'best-parameters.json',self.params[best])
        self.phase='complete';summary=self.progress();summary.update(bestCandidate=row,passed=row['passed'])
        b.write(self.output/'summary.json',summary)
        b.write(b.HERE/'web/calibration-progress.json',summary)
        print(f"COMPLETE {self.completed}/{self.planned}; best {row['id']}; {'PASS' if row['passed'] else 'FAIL'}",flush=True)


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--candidates',type=int,default=128);p.add_argument('--workers',type=int,default=8)
    p.add_argument('--finalists',type=int,default=3);p.add_argument('--output',type=Path,required=True)
    p.add_argument('--resume',action='store_true');a=p.parse_args()
    if min(a.candidates,a.workers,a.finalists)<1:p.error('Budgets must be positive')
    Search(a).execute()


if __name__=='__main__':main()
