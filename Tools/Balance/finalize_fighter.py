import copy,json,shutil
import calibrate_fighter as c
from search_fighter_progression import key
s=c.s;OUT=c.OUT

def main():
    prior=json.loads((OUT/'selected.json').read_text());s.write(OUT/'pre-capital-selection.json',prior)
    cat=OUT/'catalogs/capital-4.json';pool=json.loads((OUT/'search-results.json').read_text())+json.loads((OUT/'controls-results.json').read_text())
    for n in [0,1]:pool+=json.loads((OUT/f'local-{n}-results.json').read_text())
    seeds=[];seen=set()
    for row in sorted((r for r in pool if r['result']['success']),key=lambda r:r['result']['seconds']):
        route=row['result']['route'];signature=key(route)
        if signature in seen:continue
        seen.add(signature);seeds.append(dict(id=f'capital4-seed{len(seeds)}',catalog=str(cat),route=route))
        if len(seeds)==32:break
    rows=c.run(seeds,'capital-final-seeds');best=min(rows,key=lambda r:r['result']['seconds']);cases=[]
    for family in range(7):
        for count in [1,2,3,4,6,8,12]:
            route=copy.deepcopy(best['result']['route']);route['id']=f'capital4-local-{family}-{count}'
            for w in route['widths']:w[family]=count
            cases.append(dict(id=route['id'],catalog=str(cat),route=route))
    rows+=c.run(cases,'capital-final-local');best=min((r for r in rows if r['result']['success']),key=lambda r:r['result']['seconds']);cases=[]
    for factor in [1,2,3,4]:
        route=copy.deepcopy(best['result']['route']);route['id']=f'capacity-{factor}x';route['widths']=[[n*factor for n in w] for w in route['widths']];cases.append(dict(id=route['id'],catalog=str(cat),route=route))
    route=c.route(1,'');route['id']='minimum';cases.append(dict(id='minimum',catalog=str(cat),route=route))
    for a in ['', 'III','CIII','CCII']:
        route=copy.deepcopy(best['result']['route']);route['id']='starter-'+(a or 'minimum');route['allocation']=a;cases.append(dict(id=route['id'],catalog=str(cat),route=route))
    for f,name in enumerate(['iron','copper','smelting','structural','electrical','mechanical','fluid']):
        route=copy.deepcopy(best['result']['route']);route['id']=name+'-target1'
        for w in route['widths']:w[f]=1
        cases.append(dict(id=route['id'],catalog=str(cat),route=route))
    controls=c.run(cases,'published-controls');valid=rows+controls;winner=min((r for r in valid if r['result']['success']),key=lambda r:r['result']['seconds'])
    selected=dict(economy='capital-4',parameters=dict(mechanicalFluidSpeed=8,otherAdvancedSpeed=4,lateResearchCost=2,mechanicalFluidGeneratorCost=4),trial=winner,controlBasis=best)
    s.write(OUT/'published-selection.json',selected);shutil.copyfile(cat,OUT/'published.catalog.json')
    detail=c.run([dict(id=winner['id'],catalog=str(cat),route=winner['result']['route'],trace=True)],'detail',1)[0]
    for f in ['seconds','success','capitalSpent','produced','stages']:assert detail['result'][f]==winner['result'][f]
    assert detail['result']['powerOffSeconds']==0
    replay=c.run([dict(id=winner['id'],catalog=str(cat),route=winner['result']['route']),cases[4]],'published-replay',1)
    for actual,expected in zip(replay,[winner,controls[4]]):assert actual['result']==expected['result']
    for r in replay:assert r['result']['success'] and next(s['amount'] for s in r['result']['produced']['stacks'] if s['item']=='fighter')==1
    print('PASS: exact best/minimum serial replay; detailed ledger matches; one Fighter; binary power')
    print('BEST',winner['result']['seconds']/60,winner['result']['route'])
    for r in controls:print(r['id'],round(r['result']['seconds']/60,2),r['result']['success'])

if __name__=='__main__':main()
