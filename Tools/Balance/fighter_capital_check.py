import copy,json
import calibrate_fighter as c
s=c.s;OUT=c.OUT

def main():
    base=json.loads((OUT/'candidate.catalog.json').read_text());best=json.loads((OUT/'selected.json').read_text())['trial']['result']['route'];cases=[]
    for price in [1,2,4,8]:
        cat=copy.deepcopy(base)
        for b in cat['buildings']:
            if b['id'] in ['mechanical','fluid','industrialGenerator']:
                for cost in b['cost']:cost['amount']*=price
        path=OUT/'catalogs'/f'capital-{price}.json';s.write(path,cat)
        for width in [1,2,4]:
            route=copy.deepcopy(best);route['id']=f'capital{price}-capacity{width}';route['widths']=[[n*width for n in w] for w in route['widths']];cases.append(dict(id=route['id'],catalog=str(path),route=route))
        route=c.route(1,'');route['id']=f'capital{price}-minimum';cases.append(dict(id=route['id'],catalog=str(path),route=route))
    rows=c.run(cases,'capital-check')
    for r in rows:print(r['id'],r['result']['success'],round(r['result']['seconds']/60,2),r['result']['error'],flush=True)

if __name__=='__main__':main()
