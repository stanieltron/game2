"""Coarse late-game economy tuning without altering the starter profile."""
import copy,itertools,json,shutil
import calibrate_fighter as c
s=c.s;OUT=c.OUT

def summarize(rows,params):
    summaries=[]
    for ident,p in params.items():
        valid=[r for r in rows if r['id'].startswith(ident+':') and r['result']['success']]
        if not valid:continue
        best=min(valid,key=lambda r:r['result']['seconds']);minimum=next((r for r in valid if r['result']['route']['id']=='minimum-width1-at1'),None)
        if minimum is None:continue
        minutes=best['result']['seconds']/60;ratio=minimum['result']['seconds']/best['result']['seconds']
        loss=abs(minutes-60)+10*max(0,2-ratio)
        summaries.append(dict(id=ident,parameters=p,loss=loss,best=best,minimum=minimum,minutes=minutes,ratio=ratio))
    return sorted(summaries,key=lambda r:r['loss'])

def main():
    base=json.loads((OUT/'baseline.catalog.json').read_text());params={};cases=[]
    for i,(speed,capital,research) in enumerate(itertools.product([2,4],[2,8],[2,8])):
        ident=f'e{i}';p=dict(lateSpeed=speed,lateCapital=capital,lateResearch=research);params[ident]=p;cat=copy.deepcopy(base)
        for r in cat['recipes']:
            if r['family'] in ['mechanical','fluid']:r['seconds']/=speed
        for b in cat['buildings']:
            if b['id'] in ['mechanical','fluid','industrialGenerator']:
                for cost in b['cost']:cost['amount']=max(1,round(cost['amount']*capital))
        for t in cat['techs'][1:]:
            for cost in t['cost']:cost['amount']*=research
        path=OUT/'catalogs'/(ident+'.json');s.write(path,cat)
        for a in ['', 'CIII']:
            for n in [1,2,3,4,6,8,12]:
                for timing in [1,2,4]:cases.append(dict(id=f'{ident}:{a}-{n}-{timing}',catalog=str(path),route=c.route(n,a,timing)))
    s.write(OUT/'parameters.json',params);print(f'Economy pass: {len(cases)} Fighter simulations',flush=True)
    rows=c.run(cases,'economy');summary=summarize(rows,params);s.write(OUT/'economy-summary.json',summary)
    for r in summary:print(r['id'],r['parameters'],'best',round(r['minutes'],1),r['best']['result']['route']['id'],'minimum',round(r['minimum']['result']['seconds']/60,1),flush=True)

if __name__=='__main__':main()
