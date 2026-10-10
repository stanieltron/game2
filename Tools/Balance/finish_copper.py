"""Refine the promising iron/copper cost region, then challenge exact allocations."""
import copy,itertools,json,shutil
import calibrate_copper as c
s=c.s;OUT=c.OUT

def main():
    base=json.loads((s.HERE/'reports/starter-fifteen/candidate.catalog.json').read_text());params={};cases=[]
    for n,(plate,wire,industryWire) in enumerate(itertools.product([640,800,960],[1400,1600,1800],[2,4,8])):
        ident=f'f{n:02}';p=dict(researchPlate=plate,researchWire=wire,basicWireMultiplier=1,industrialWireMultiplier=industryWire,industrialIronMultiplier=1);params[ident]=p;cat=copy.deepcopy(base)
        for cost in cat['techs'][0]['cost']:
            if cost['item']=='plate':cost['amount']=plate
            if cost['item']=='wire':cost['amount']=wire
        for b in cat['buildings']:
            if b['id'] in ['industrialMiner','industrialSmelter','structural','electrical']:
                for cost in b['cost']:
                    if cost['item']=='wire':cost['amount']*=industryWire
        path=OUT/'catalogs'/(ident+'.json');s.write(path,cat)
        for i,k in c.COUNTS:
            for a in c.allocations(i,k):cases.append([str(path),a,ident,'freeLogistics','replaceAll'])
    s.write(OUT/'finish-parameters.json',params);print(f'Promising region: {len(cases)} runs',flush=True)
    ranked=c.summarize(c.run(cases,'finish'),params);s.write(OUT/'finish-summary.json',ranked)
    for r in ranked[:8]:print(r['id'],r['parameters'],'ratio',round(r['ratio'],2),{k:round(t,2) for k,t in r['times'].items()},flush=True)
    # Prefer a genuine 4–2 minimum, then score distance from timing/penalty targets.
    eligible=[r for r in ranked if r['times']['4-2']<=min(r['times'].values())+.0001]
    short=(eligible+ [r for r in ranked if r not in eligible])[:3];cases=[]
    for row in short:
        for i,k in c.COUNTS:
            for a in c.allocations(i,k,True):cases.append([str(OUT/'catalogs'/(row['id']+'.json')),a,row['id'],'freeLogistics','replaceAll'])
    print(f'Final allocation challenge: {len(cases)} runs',flush=True)
    ranked=c.summarize(c.run(cases,'final'),{r['id']:r['parameters'] for r in short})
    old=json.loads((OUT/'challenge-summary.json').read_text());ranked+=old
    ranked.sort(key=lambda r:(r['times']['4-2']>min(r['times'].values())+.0001,r['loss']))
    s.write(OUT/'final-summary.json',ranked);selected=ranked[0];s.write(OUT/'selected.json',selected);shutil.copyfile(OUT/'catalogs'/(selected['id']+'.json'),OUT/'candidate.catalog.json')
    for r in ranked:print('FINAL',r['id'],'ratio',round(r['ratio'],2),{k:round(t,2) for k,t in r['times'].items()},flush=True)

if __name__=='__main__':main()
