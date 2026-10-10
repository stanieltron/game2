"""Cost-only cross-resource tuning, followed by allocation challenge."""
import copy,itertools,json,shutil
import calibrate_copper as c
s=c.s;OUT=c.OUT

def main():
    base=json.loads((s.HERE/'reports/starter-fifteen/candidate.catalog.json').read_text());params={};cases=[]
    for n,(plate,wire,basic,industryIron) in enumerate(itertools.product([240,480,960,1440],[1200,1600,2000],[1,4],[.25,1])):
        ident=f'r{n:02}';p=dict(researchPlate=plate,researchWire=wire,basicWireMultiplier=basic,industrialWireMultiplier=4,industrialIronMultiplier=industryIron);params[ident]=p;cat=copy.deepcopy(base)
        for cost in cat['techs'][0]['cost']:
            if cost['item']=='wire':cost['amount']=wire
            if cost['item']=='plate':cost['amount']=plate
        for b in cat['buildings']:
            for cost in b['cost']:
                if b['id'] in ['miner','smelter','workshop','generator'] and cost['item']=='wire':cost['amount']*=basic
                if b['id'] in ['industrialMiner','industrialSmelter','structural','electrical']:
                    if cost['item']=='wire':cost['amount']*=4
                    if cost['item'] in ['plate','beam']:cost['amount']=max(1,round(cost['amount']*industryIron))
        path=OUT/'catalogs'/(ident+'.json');s.write(path,cat)
        for i,k in c.COUNTS:
            for a in c.allocations(i,k):cases.append([str(path),a,ident,'freeLogistics','replaceAll'])
    s.write(OUT/'refine-parameters.json',params);print(f'Refinement: {len(cases)} runs',flush=True)
    ranked=c.summarize(c.run(cases,'refine'),params);s.write(OUT/'refine-summary.json',ranked)
    for r in ranked[:10]:print(r['id'],r['parameters'],'ratio',round(r['ratio'],2),{k:round(t,2) for k,t in r['times'].items()},flush=True)
    # Carry three strongest refined candidates plus the strongest coarse candidate.
    old=json.loads((OUT/'summary.json').read_text())[0];short=ranked[:3]+[old];cases=[]
    for row in short:
        for i,k in c.COUNTS:
            for a in c.allocations(i,k,True):cases.append([str(OUT/'catalogs'/(row['id']+'.json')),a,row['id'],'freeLogistics','replaceAll'])
    print(f'Allocation challenge: {len(cases)} runs',flush=True)
    ranked=c.summarize(c.run(cases,'challenge'),{r['id']:r['parameters'] for r in short});s.write(OUT/'challenge-summary.json',ranked)
    for r in ranked:print('FINAL',r['id'],'ratio',round(r['ratio'],2),{k:round(t,2) for k,t in r['times'].items()},flush=True)

if __name__=='__main__':main()
