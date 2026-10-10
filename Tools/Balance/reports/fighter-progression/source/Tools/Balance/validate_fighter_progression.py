"""Promote better controls, refine nearby capacities, and measure whole-factory waste."""
import copy,json
import calibrate_fighter as c
s=c.s;OUT=c.OUT

def main():
    selected=json.loads((OUT/'selected.json').read_text());pool=json.loads((OUT/'search-results.json').read_text())+json.loads((OUT/'controls-results.json').read_text());cat=OUT/'candidate.catalog.json'
    for iteration in range(2):
        best=min((r for r in pool if r['result']['success']),key=lambda r:r['result']['seconds']);cases=[]
        for family in range(7):
            for count in [1,2,3,4,6,8,12]:
                route=copy.deepcopy(best['result']['route']);route['id']=f'local-{iteration}-{family}-{count}'
                for w in route['widths']:w[family]=count
                cases.append(dict(id=route['id'],catalog=str(cat),route=route))
        pool+=c.run(cases,f'local-{iteration}')
    best=min((r for r in pool if r['result']['success']),key=lambda r:r['result']['seconds']);selected['trial']=best;s.write(OUT/'selected.json',selected)
    cases=[]
    for factor in [1,2,3,4]:
        route=copy.deepcopy(best['result']['route']);route['id']=f'factory-{factor}x'
        route['widths']=[[min(100,n*factor) for n in w] for w in route['widths']];cases.append(dict(id=route['id'],catalog=str(cat),route=route))
    route=c.route(1,'',1);route['id']='minimum';cases.append(dict(id='minimum',catalog=str(cat),route=route))
    for allocation in ['', 'III','CIII','CCII']:
        route=copy.deepcopy(best['result']['route']);route['id']='starter-'+(allocation or 'minimum');route['allocation']=allocation;cases.append(dict(id=route['id'],catalog=str(cat),route=route))
    for family,name in enumerate(['iron','copper','smelting','structural','electrical','mechanical','fluid']):
        route=copy.deepcopy(best['result']['route']);route['id']=name+'-target1'
        for w in route['widths']:w[family]=1
        cases.append(dict(id=route['id'],catalog=str(cat),route=route))
    rows=c.run(cases,'final-controls');s.write(OUT/'final-controls-summary.json',[dict(id=r['id'],success=r['result']['success'],minutes=r['result']['seconds']/60,error=r['result']['error']) for r in rows])
    # Exact serial replay of best and minimum; all share an identical completed-Fighter endpoint.
    replayCases=[dict(id=best['id'],catalog=str(cat),route=best['result']['route']),cases[4]]
    replay=c.run(replayCases,'replay',1)
    for r,expected in zip(replay,[best,rows[4]]):
        for field,value in expected['result'].items():assert r['result'][field]==value,f'Replay differs: {field}'
        assert r['result']['success'] and r['result']['powerOffSeconds']==0
        assert next(x['amount'] for x in r['result']['produced']['stacks'] if x['item']=='fighter')==1
    print('PASS: exact serial replays; one completed Fighter; no fractional-power operation')
    print('BEST',best['result']['seconds']/60,best['result']['route'])
    for r in rows:print(r['id'],r['result']['success'],round(r['result']['seconds']/60,2),r['result']['error'])

if __name__=='__main__':main()
