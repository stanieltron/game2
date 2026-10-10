import copy,itertools,json
import calibrate_fighter as c
from tune_fighter import summarize
s=c.s;OUT=c.OUT

def main():
    base=json.loads((OUT/'baseline.catalog.json').read_text());params={};cases=[]
    for i,(mechanical,parts,capital,research) in enumerate(itertools.product([4,8],[2,4],[1,2],[2,6])):
        ident=f'p{i:02}';params[ident]=dict(mechanicalFluidSpeed=mechanical,otherAdvancedSpeed=parts,lateCapital=capital,lateResearch=research);cat=copy.deepcopy(base)
        for r in cat['recipes']:
            if r['family'] in ['mechanical','fluid']:r['seconds']/=mechanical
            elif r['family'] in ['structural','electrical'] and not any(o['item'] in ['plate','beam','wire','refractory'] for o in r['outputs']):r['seconds']/=parts
        for b in cat['buildings']:
            if b['id'] in ['mechanical','fluid','industrialGenerator']:
                for cost in b['cost']:cost['amount']=max(1,round(cost['amount']*capital))
        for t in cat['techs'][1:]:
            for cost in t['cost']:cost['amount']*=research
        path=OUT/'catalogs'/(ident+'.json');s.write(path,cat)
        for a in ['', 'CIII']:
            for n in [1,2,3,4,6,8,12]:
                for timing in [1,2,4]:cases.append(dict(id=f'{ident}:{a}-{n}-{timing}',catalog=str(path),route=c.route(n,a,timing)))
    s.write(OUT/'refine-parameters.json',params);print(f'Advanced-part pass: {len(cases)} Fighter simulations',flush=True)
    ranked=summarize(c.run(cases,'refine'),params);s.write(OUT/'refine-summary.json',ranked)
    for r in ranked:print(r['id'],r['parameters'],round(r['minutes'],1),r['best']['result']['route']['id'],'minimum',round(r['minimum']['result']['seconds']/60,1),flush=True)

if __name__=='__main__':main()
