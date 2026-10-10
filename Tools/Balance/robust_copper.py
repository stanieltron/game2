"""Challenge the near-tie with fixed electrical-specialist material prices."""
import copy,itertools,json,shutil,sys
import calibrate_copper as c
s=c.s;OUT=c.OUT

def rank(rows,params):
    result=c.summarize(rows,params)
    for r in result:
        t=r['times'];r['margin']=min(v for k,v in t.items() if k!='4-2')-t['4-2']
        r['loss']+=12*max(0,.3-r['margin'])
    return sorted(result,key=lambda r:(r['margin']<0,r['loss']))

def main():
    params={};cases=[]
    for n,(parent,electrical,structural) in enumerate(itertools.product(['f10','f22'],[2,4,8,16],[.5,1])):
        ident=f'b{n:02}';p=dict(parent=parent,electricalIronMultiplier=electrical,structuralIronMultiplier=structural);params[ident]=p
        cat=json.loads((OUT/'catalogs'/(parent+'.json')).read_text())
        for b in cat['buildings']:
            if b['id'] in ['electrical','structural']:
                factor=electrical if b['id']=='electrical' else structural
                for cost in b['cost']:
                    if cost['item'] in ['plate','beam']:cost['amount']=max(1,round(cost['amount']*factor))
        path=OUT/'catalogs'/(ident+'.json');s.write(path,cat)
        for i,k in c.COUNTS:
            for a in c.allocations(i,k):cases.append([str(path),a,ident,'freeLogistics','replaceAll'])
    s.write(OUT/'robust-parameters.json',params);print(f'Near-tie check: {len(cases)} simulations',flush=True)
    ranked=rank(c.run(cases,'robust'),params);s.write(OUT/'robust-summary.json',ranked);cases=[]
    for r in ranked[:3]:
        for i,k in c.COUNTS:
            for a in c.allocations(i,k,True):cases.append([str(OUT/'catalogs'/(r['id']+'.json')),a,r['id'],'freeLogistics','replaceAll'])
    print(f'Robust allocation challenge: {len(cases)} simulations',flush=True)
    ranked=rank(c.run(cases,'robust-challenge'),{r['id']:r['parameters'] for r in ranked[:3]});s.write(OUT/'robust-final.json',ranked)
    for r in ranked:print(r['id'],'margin',round(r['margin'],2),'ratio',round(r['ratio'],2),{k:round(t,2) for k,t in r['times'].items()},flush=True)
    chosen=ranked[0];s.write(OUT/'selected.json',chosen);shutil.copyfile(OUT/'catalogs'/(chosen['id']+'.json'),OUT/'candidate.catalog.json')

def grid():
    rows=json.loads((OUT/'robust-final.json').read_text());cases=[]
    for r in rows:
        for i in range(1,7):
            for k in range(1,5):
                for a in c.allocations(i,k,True):cases.append([str(OUT/'catalogs'/(r['id']+'.json')),a,r['id'],'freeLogistics','replaceAll'])
    ranked=rank(c.run(cases,'grid'),{r['id']:r['parameters'] for r in rows});s.write(OUT/'grid-summary.json',ranked)
    s.write(OUT/'selected.json',ranked[0]);shutil.copyfile(OUT/'catalogs'/(ranked[0]['id']+'.json'),OUT/'candidate.catalog.json')

if __name__=='__main__':
    if '--grid' in sys.argv:grid()
    else:main()
