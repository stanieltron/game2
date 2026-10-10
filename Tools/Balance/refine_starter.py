import json,subprocess
from pathlib import Path
import calibrate_starter as s
OUT=s.HERE/'reports/starter-calibration-v2'
def allocations(n):
    if n==1:return ['']
    seq=set()
    for share in [.1,.25,.5,.75,1]:
        a='I'
        for i in range(1,n-1):a+='I' if a.count('I')/(i+1)<share else 'C'
        seq.add(a)
    return sorted(seq)
def main():
    if OUT.exists():raise ValueError('Existing output')
    base=json.loads((s.ROOT/'Assets/Resources/catalog.json').read_text());rows=[];cases=[]
    for i,(research,capital,speed) in enumerate([(20,2,3),(32,2,4),(32,4,4),(48,4,5),(48,4,6),(64,4,6),(64,8,8)]):
        p=dict(research=research,capital=capital,speed=speed,refund=.25);ident=f'r{i:02}';cat=OUT/'catalogs'/f'{ident}.json';s.write(cat,s.tune(base,p));rows.append(dict(id=ident,parameters=p))
        for n in [1,2,3,4,5,6,8,12]:
            for allocation in allocations(n):cases.append([str(cat),allocation,ident])
    s.write(OUT/'cases.json',cases);s.write(OUT/'parameters.json',rows)
    subprocess.run(['pwsh','-NoProfile','-File',str(s.HERE/'starter_calibration.ps1'),'-Cases',str(OUT/'cases.json'),'-Output',str(OUT/'results.json')],check=True)
    rs=json.loads((OUT/'results.json').read_text())
    for row in rows:
        times={};chosen={}
        for r in rs:
            if r['id']!=row['id'] or not r['result']['success']:continue
            n=r['path'];t=r['result']['seconds']/60
            if t<times.get(n,float('inf')):times[n]=t;chosen[n]=r['allocation']
        row.update(times=times,allocations=chosen)
        print(row,flush=True)
    s.write(OUT/'summary.json',rows)
if __name__=='__main__':main()
