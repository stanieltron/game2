"""Coarse 15-minute study with explicit endpoint and factory-size definitions."""
import itertools,json,subprocess,sys
import calibrate_starter as s
OUT=s.HERE/'reports/starter-fifteen'

def run(cases,name):
    path=OUT/(name+'-results.json')
    if path.exists():return json.loads(path.read_text())
    s.write(OUT/(name+'-cases.json'),cases)
    subprocess.run(['pwsh','-NoProfile','-File',str(s.HERE/'starter_calibration.ps1'),'-Cases',str(OUT/(name+'-cases.json')),'-Output',str(path),'-Workers','15'],check=True)
    return json.loads(path.read_text())

def main():
    base=json.loads((s.ROOT/'Assets/Resources/catalog.json').read_text());params={};cases=[]
    for i,(research,basic,capital) in enumerate(itertools.product([5,7,9],[.5,1,2,4],[.5,1])):
        ident=f'm{i:02}';p=dict(research=research,capital=capital,speed=4,refund=.25,basicCost=basic,construction=.25);params[ident]=p
        c=s.tune(base,p)
        for b in c['buildings']:
            b['seconds']*=p['construction']
            if b['id'] in ['miner','smelter','workshop']:
                for cost in b['cost']:cost['amount']=max(1,round(cost['amount']*basic))
        cat=OUT/'catalogs'/(ident+'.json');s.write(cat,c)
        for n in [1,2,3,4,5,6,7,8,10,12]:
            for a in s.allocations(n):
                for mode in ['fixedEndpoint','replaceAll']:
                    cases.append([str(cat),a,ident+'-'+mode,'freeLogistics',mode])
    s.write(OUT/'parameters.json',params)
    print(f'{len(params)} economies, {len(cases)} endpoint/allocation trials',flush=True)
    results=run(cases,'coarse');summary=[]
    for ident,p in params.items():
        for mode in ['fixedEndpoint','replaceAll']:
            rows=[r for r in results if r['id']==ident+'-'+mode and r['result']['success']];best={}
            for r in rows:
                n=r['path']
                if n not in best or r['result']['seconds']<best[n]['result']['seconds']:best[n]=r
            if len(best)!=10:continue
            times={n:r['result']['seconds']/60 for n,r in best.items()};opt=min(times,key=times.get)
            loss=abs(times[1]-30)+2*abs(times[opt]-15)
            summary.append(dict(id=ident,mode=mode,parameters=p,times=times,optimum=opt,loss=loss,best=best))
    summary.sort(key=lambda r:r['loss']);s.write(OUT/'summary.json',summary)
    for r in summary[:12]:print(r['id'],r['mode'],'opt',r['optimum'],{n:round(t,1) for n,t in r['times'].items()},flush=True)

if __name__=='__main__':main()
