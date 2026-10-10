"""Validate balance at 50 ms after coarse search; never present coarse timings as final."""
import copy,json,shutil
import calibrate_fighter as c
s=c.s;OUT=c.OUT

def main():
    selection=json.loads((OUT/'published-selection.json').read_text());base=json.loads((OUT/'published.catalog.json').read_text());best=selection['trial']['result']['route'];cases=[]
    for research in [2,3,4,6]:
        cat=copy.deepcopy(base)
        for t in cat['techs'][1:]:
            for cost in t['cost']:cost['amount']=round(cost['amount']*research/2)
        path=OUT/'catalogs'/f'fine-research{research}.json';s.write(path,cat)
        for label,route in [('best',best),('minimum',c.route(1,''))]:cases.append(dict(id=f'research{research}-{label}',catalog=str(path),route=route,step=.05))
    rows=c.run(cases,'fine-economy');rank=[]
    for research in [2,3,4,6]:
        b=next(r for r in rows if r['id']==f'research{research}-best');m=next(r for r in rows if r['id']==f'research{research}-minimum');t=b['result']['seconds']/60;ratio=m['result']['seconds']/b['result']['seconds']
        rank.append(dict(research=research,bestMinutes=t,minimumMinutes=m['result']['seconds']/60,loss=abs(t-60)+10*max(0,2-ratio)))
    rank.sort(key=lambda r:r['loss']);s.write(OUT/'fine-economy-summary.json',rank)
    for r in rank:print(r,flush=True)
    research=rank[0]['research'];catalog=OUT/'catalogs'/f'fine-research{research}.json';cases=[]
    # Recheck starter proportions under the actual Fighter endpoint and fine time step.
    for iron in range(1,6):
        for copper in range(1,6):
            for allocation in sorted({'I'*(iron-1)+'C'*(copper-1),'C'*(copper-1)+'I'*(iron-1)}):
                route=copy.deepcopy(best);route['id']=f'fine-start-{allocation or "none"}';route['allocation']=allocation;cases.append(dict(id=route['id'],catalog=str(catalog),route=route,step=.05))
    rows=c.run(cases,'fine-starters');rows.append(next(r for r in json.loads((OUT/'fine-economy-results.json').read_text()) if r['id']==f'research{research}-best'))
    winner=min((r for r in rows if r['result']['success']),key=lambda r:r['result']['seconds']);cases=[]
    for f in range(7):
        for n in [1,2,3,4,6,8]:
            route=copy.deepcopy(winner['result']['route']);route['id']=f'fine-family-{f}-{n}'
            for w in route['widths']:w[f]=n
            cases.append(dict(id=route['id'],catalog=str(catalog),route=route,step=.05))
    rows+=c.run(cases,'fine-families');winner=min((r for r in rows if r['result']['success']),key=lambda r:r['result']['seconds'])
    s.write(OUT/'fine-selection.json',dict(research=research,step=.05,trial=winner));shutil.copyfile(catalog,OUT/'fine.catalog.json');cases=[]
    for factor in [1,2,3,4]:
        route=copy.deepcopy(winner['result']['route']);route['id']=f'capacity-{factor}x';route['widths']=[[v*factor for v in w] for w in route['widths']];cases.append(dict(id=route['id'],catalog=str(catalog),route=route,step=.05))
    route=c.route(1,'');route['id']='minimum';cases.append(dict(id='minimum',catalog=str(catalog),route=route,step=.05))
    for allocation in ['', 'III','CIII','CCII']:
        route=copy.deepcopy(winner['result']['route']);route['id']='starter-'+(allocation or 'minimum');route['allocation']=allocation;cases.append(dict(id=route['id'],catalog=str(catalog),route=route,step=.05))
    for f,name in enumerate(['iron','copper','smelting','structural','electrical','mechanical','fluid']):
        route=copy.deepcopy(winner['result']['route']);route['id']=name+'-target1'
        for w in route['widths']:w[f]=1
        cases.append(dict(id=route['id'],catalog=str(catalog),route=route,step=.05))
    controls=c.run(cases,'fine-controls')
    for r in controls:print(r['id'],round(r['result']['seconds']/60,2),r['result']['success'],flush=True)
    detail=c.run([dict(id=winner['id'],catalog=str(catalog),route=winner['result']['route'],step=.05,trace=True)],'fine-detail',1)[0]
    for f in ['seconds','stages','produced','capitalSpent']:assert detail['result'][f]==winner['result'][f]
    resolution=[]
    for label,route in [('best',winner['result']['route']),('minimum',cases[4]['route'])]:resolution.append(dict(id=label,catalog=str(catalog),route=route,step=1/60))
    check=c.run(resolution,'fine-resolution')
    for r in check:print('60Hz',r['id'],r['result']['seconds']/60,flush=True)
    print('WINNER',winner['result']['seconds']/60,winner['result']['route'],flush=True)

if __name__=='__main__':main()
