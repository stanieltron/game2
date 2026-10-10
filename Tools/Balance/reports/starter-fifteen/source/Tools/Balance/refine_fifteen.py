"""Target the original all-upgraded endpoint near 15 minutes at legacy path 4."""
import itertools,json,subprocess,hashlib,shutil
import calibrate_starter as s
import calibrate_fifteen as coarse
OUT=coarse.OUT

def main():
    base=json.loads((s.ROOT/'Assets/Resources/catalog.json').read_text());params={};cases=[]
    for i,(capital,basic,construction) in enumerate(itertools.product([.125,.25,.5],[.5,1],[.125,.25])):
        ident=f'r{i:02}';p=dict(research=8,capital=capital,speed=5,refund=.25,basicCost=basic,construction=construction);params[ident]=p;c=s.tune(base,p)
        for b in c['buildings']:
            b['seconds']*=construction
            if b['id'] in ['miner','smelter','workshop']:
                for cost in b['cost']:cost['amount']=max(1,round(cost['amount']*basic))
        cat=OUT/'catalogs'/(ident+'.json');s.write(cat,c)
        for n in [1,2,3,4,5,6,7,8,10,12]:
            for a in s.allocations(n):cases.append([str(cat),a,ident,'freeLogistics','replaceAll'])
    s.write(OUT/'refine-parameters.json',params);print(f'Refinement: {len(cases)} simulations',flush=True)
    rows=coarse.run(cases,'refine');rank=[]
    for ident,p in params.items():
        best={}
        for r in rows:
            if r['id']!=ident or not r['result']['success']:continue
            n=r['path']
            if n not in best or r['result']['seconds']<best[n]['result']['seconds']:best[n]=r
        t={n:r['result']['seconds']/60 for n,r in best.items()}
        if len(t)!=10:continue
        loss=abs(t[1]-30)+2*abs(t[4]-15)+10*max(0,t[4]-min(t.values()))
        rank.append(dict(id=ident,parameters=p,times=t,best=best,loss=loss))
    rank.sort(key=lambda r:r['loss']);s.write(OUT/'refine-summary.json',rank)
    for r in rank:print(r['id'],round(r['loss'],2),{n:round(t,2) for n,t in r['times'].items()},flush=True)
    # All allocations through path 10 (first addition iron); broader samples above.
    cases=[]
    for candidate in rank[:3]:
        ident=candidate['id'];cat=OUT/'catalogs'/(ident+'.json')
        for n in list(range(1,11))+[12,20]:
            if n==1:seq=['']
            elif n<=10:seq=['I'+''.join('I' if mask&(1<<k) else 'C' for k in range(n-2)) for mask in range(1<<(n-2))]
            else:seq=sorted(set(s.allocations(n)+['I'*k+'C'*(n-1-k) for k in range(1,n)]))
            for a in seq:cases.append([str(cat),a,ident,'freeLogistics','replaceAll'])
    print(f'Challenge: {len(cases)} simulations',flush=True);rows=coarse.run(cases,'challenge');final=[]
    for candidate in rank[:3]:
        best={}
        for r in rows:
            if r['id']!=candidate['id'] or not r['result']['success']:continue
            n=r['path']
            if n not in best or r['result']['seconds']<best[n]['result']['seconds']:best[n]=r
        t={n:r['result']['seconds']/60 for n,r in best.items()};loss=abs(t[1]-30)+2*abs(t[4]-15)+10*max(0,t[4]-min(t.values()))
        wider=sorted(n for n in t if n>=4)
        loss+=10*sum(max(0,t[a]-t[b]) for a,b in zip(wider,wider[1:]))
        final.append(dict(id=candidate['id'],parameters=candidate['parameters'],times=t,best=best,loss=loss))
    final.sort(key=lambda r:r['loss']);s.write(OUT/'final-summary.json',final);s.write(OUT/'selected.json',final[0]);shutil.copyfile(OUT/'catalogs'/(final[0]['id']+'.json'),OUT/'candidate.catalog.json')
    for r in final:print('FINAL',r['id'],{n:round(t,2) for n,t in r['times'].items()},flush=True)

if __name__=='__main__':main()
