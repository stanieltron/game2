"""Seeded batched search over investment timing, machine families and starter routes."""
import copy,json,random,hashlib,shutil
import calibrate_fighter as c
s=c.s;OUT=c.OUT

def key(route):return json.dumps({k:v for k,v in route.items() if k!='id'},sort_keys=True)

def main():
    economy=json.loads((OUT/'refine-summary.json').read_text())[0];ident=economy['id'];catalog=OUT/'catalogs'/(ident+'.json')
    old=[r for r in json.loads((OUT/'refine-results.json').read_text()) if r['id'].startswith(ident+':')];allrows=list(old);seen={key(r['result']['route']) for r in old};rng=random.Random(73145)
    candidates=[]
    for allocation in ['', 'I','C','II','CI','CII','CIII','CCII','CCIII','CICII']:
        for replace in [True,False]:
            for timing in [1,2,4]:
                r=c.route(4,allocation,timing);r['replaceAll']=replace
                if key(r) not in seen:seen.add(key(r));candidates.append(r)
    rows=c.run([dict(id=f'{ident}:seed{i}',catalog=str(catalog),route=r) for i,r in enumerate(candidates)],'search-seeds');allrows+=rows
    for batch in range(8):
        elite=sorted((r for r in allrows if r['result']['success']),key=lambda r:r['result']['seconds'])[:24];new=[]
        while len(new)<64:
            route=copy.deepcopy(rng.choice(elite)['result']['route']);op=rng.randrange(5)
            if op==0:
                a=list(route['allocation']);j=rng.randrange(len(a)+1)
                if a and rng.random()<.4:a.pop(min(j,len(a)-1))
                elif len(a)<9:a.insert(j,rng.choice('IC'))
                route['allocation']=''.join(a)
            elif op==1:route['replaceAll']=not route['replaceAll']
            else:
                stage=rng.randrange(4);family=rng.randrange(7);value=max(1,min(20,route['widths'][stage][family]+rng.choice([-3,-2,-1,1,2,4])))
                for t in range(stage,4):route['widths'][t][family]=value
                if op==4:
                    for t in range(4):route['widths'][t][family]=value
            for t in range(1,4):route['widths'][t]=[max(a,b) for a,b in zip(route['widths'][t],route['widths'][t-1])]
            route['id']=f'search-{batch}-{len(new)}';signature=key(route)
            if signature in seen:continue
            seen.add(signature);new.append(dict(id=f'{ident}:{route["id"]}',catalog=str(catalog),route=route))
        rows=c.run(new,f'search-{batch}');allrows+=rows;best=min((r for r in allrows if r['result']['success']),key=lambda r:r['result']['seconds'])
        print(f'Search {(batch+1)*64} / 512; best {best["result"]["seconds"]/60:.2f} min',flush=True)
    best=min((r for r in allrows if r['result']['success']),key=lambda r:r['result']['seconds']);s.write(OUT/'search-results.json',allrows);s.write(OUT/'selected.json',dict(economy=economy,trial=best));shutil.copyfile(catalog,OUT/'candidate.catalog.json')
    # Controlled removals and excess investment relative to the selected route.
    probes=[]
    for family,name in enumerate(['iron','copper','smelting','structural','electrical','mechanical','fluid']):
        for factor in [0,2,3]:
            route=copy.deepcopy(best['result']['route']);route['id']=f'{name}-'+('minimum' if factor==0 else f'{factor}x')
            for w in route['widths']:w[family]=1 if factor==0 else min(60,w[family]*factor)
            probes.append(dict(id=route['id'],catalog=str(catalog),route=route))
    for allocation in ['','III','CIII','CCII']:
        route=copy.deepcopy(best['result']['route']);route['id']='starter-'+(allocation or 'minimum');route['allocation']=allocation;probes.append(dict(id=route['id'],catalog=str(catalog),route=route))
    route=copy.deepcopy(best['result']['route']);route['id']='late-expansion';route['widths']=[c.route(1)['widths'][0]]*3+[route['widths'][-1]];probes.append(dict(id=route['id'],catalog=str(catalog),route=route))
    probeRows=c.run(probes,'controls');s.write(OUT/'controls-summary.json',[dict(id=r['id'],success=r['result']['success'],minutes=r['result']['seconds']/60) for r in probeRows])
    print('Selected economy',ident,'route',best['result']['route'],'minutes',best['result']['seconds']/60,flush=True)

if __name__=='__main__':main()
