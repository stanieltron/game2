"""Cost-only copper sensitivity search with explicit iron/copper counts."""
import copy,itertools,json,subprocess,random
from pathlib import Path
import calibrate_starter as s
OUT=s.HERE/'reports/starter-copper'
COUNTS=[(1,1),(2,1),(2,2),(3,1),(3,2),(3,3),(4,1),(4,2),(4,3),(4,4),(5,1),(5,2),(5,3),(6,2),(6,3)]

def allocations(i,c,exhaustive=False):
    a='I'*(i-1)+'C'*(c-1)
    if exhaustive:return sorted(set(''.join(p) for p in itertools.permutations(a)))
    shuffled=list(a);random.Random(174+i*11+c).shuffle(shuffled)
    return sorted({a,a[::-1],''.join(shuffled)})

def run(cases,name,workers=15):
    file=OUT/(name+'-results.json');casefile=OUT/(name+'-cases.json')
    if file.exists():
        assert json.loads(casefile.read_text())==cases,'Cached cases differ'
        return json.loads(file.read_text())
    s.write(casefile,cases)
    subprocess.run(['pwsh','-NoProfile','-File',str(s.HERE/'starter_calibration.ps1'),'-Cases',str(casefile),'-Output',str(file),'-Workers',str(workers)],check=True)
    return json.loads(file.read_text())

def summarize(rows,parameters):
    summaries=[]
    for ident,p in parameters.items():
        best={}
        for r in rows:
            if r['id']!=ident or not r['result']['success']:continue
            key=f'{r["result"]["iron"]}-{r["result"]["copper"]}'
            if key not in best or r['result']['seconds']<best[key]['result']['seconds']:best[key]=r
        if len(best)<len(COUNTS):continue
        t={k:r['result']['seconds']/60 for k,r in best.items()};goal=t['4-2'];ratio=t['4-1']/goal
        loss=abs(goal-15)+8*abs(ratio-2)+15*max(0,goal-min(t.values()))
        summaries.append(dict(id=ident,parameters=p,times=t,ratio=ratio,loss=loss,best=best))
    return sorted(summaries,key=lambda r:r['loss'])

def main():
    base=json.loads((s.HERE/'reports/starter-fifteen/candidate.catalog.json').read_text());params={};cases=[]
    for n,(wire,basic,industrial) in enumerate(itertools.product([800,1200,1600,2000],[1,4,12,32],[1,4])):
        ident=f'c{n:02}';p=dict(researchWire=wire,basicWireMultiplier=basic,industrialWireMultiplier=industrial);params[ident]=p;cat=copy.deepcopy(base)
        for cost in cat['techs'][0]['cost']:
            if cost['item']=='wire':cost['amount']=wire
        for b in cat['buildings']:
            factor=basic if b['id'] in ['miner','smelter','workshop','generator'] else industrial if b['id'] in ['industrialMiner','industrialSmelter','structural','electrical'] else 1
            for cost in b['cost']:
                if cost['item']=='wire':cost['amount']*=factor
        path=OUT/'catalogs'/(ident+'.json');s.write(path,cat)
        for i,c in COUNTS:
            for a in allocations(i,c):cases.append([str(path),a,ident,'freeLogistics','replaceAll'])
    s.write(OUT/'parameters.json',params);print(f'Cost-only coarse search: {len(params)} economies, {len(cases)} simulations',flush=True)
    summary=summarize(run(cases,'coarse'),params);s.write(OUT/'summary.json',summary)
    for r in summary[:12]:print(r['id'],r['parameters'],'ratio',round(r['ratio'],2),{k:round(t,1) for k,t in r['times'].items()},flush=True)

if __name__=='__main__':main()
