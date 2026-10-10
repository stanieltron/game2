"""Challenge the one-hour candidate with many allocations and no belt bottleneck."""
import json,random,subprocess
from pathlib import Path
import calibrate_starter as s
OUT=s.HERE/'reports/starter-one-hour'
def main():
    if OUT.exists():raise ValueError('Existing output')
    p=dict(research=32,capital=2,speed=4,refund=.25)
    base=json.loads((s.ROOT/'Assets/Resources/catalog.json').read_text());cat=OUT/'candidate.catalog.json';s.write(cat,s.tune(base,p));s.write(OUT/'parameters.json',p)
    cases=[]
    for n in list(range(1,13))+[20,40]:
        sequences=set()
        if n==1:sequences.add('')
        elif n<=8:
            for mask in range(1<<(n-2)):sequences.add('I'+''.join('I' if mask&(1<<k) else 'C' for k in range(n-2)))
        else:
            rng=random.Random(719+n)
            for iron in range(1,n):
                sequences.add('I'*iron+'C'*(n-1-iron))
                tail=list('I'*(iron-1)+'C'*(n-1-iron));rng.shuffle(tail);sequences.add('I'+''.join(tail))
            for _ in range(64 if n<=12 else 24):
                sequences.add('I'+''.join(rng.choice('IC') for _ in range(n-2)))
        for a in sorted(sequences):cases.append([str(cat),a,'one-hour','freeLogistics'])
    s.write(OUT/'cases.json',cases)
    subprocess.run(['pwsh','-NoProfile','-File',str(s.HERE/'starter_calibration.ps1'),'-Cases',str(OUT/'cases.json'),'-Output',str(OUT/'results.json')],check=True)
    rows=json.loads((OUT/'results.json').read_text());best=[]
    for n in sorted(set(r['path'] for r in rows)):
        valid=[r for r in rows if r['path']==n and r['result']['success']]
        if valid:best.append(min(valid,key=lambda r:(r['result']['seconds'],r['allocation'])))
    s.write(OUT/'best-paths.json',best)
    for r in best:print(r['path'],r['allocation'],round(r['result']['seconds']/60,2),flush=True)
if __name__=='__main__':main()
