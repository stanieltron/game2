"""Deterministic, staged investments to one constructed Fighter."""
import json,subprocess,copy,itertools
from pathlib import Path
import calibrate_starter as s
OUT=s.HERE/'reports/fighter-progression'

def run(cases,name,workers=15):
    file=OUT/(name+'-results.json');inputs=OUT/(name+'-cases.json')
    if file.exists():
        assert json.loads(inputs.read_text())==cases
        return json.loads(file.read_text())
    s.write(inputs,cases)
    subprocess.run(['pwsh','-NoProfile','-File',str(s.HERE/'fighter_progression.ps1'),'-Cases',str(inputs),'-Output',str(file),'-Workers',str(workers)],check=True)
    return json.loads(file.read_text())

def route(n,allocation='CIII',timing=1):
    stages=[]
    for tech in range(1,5):
        k=n if tech>=timing else 1
        stages.append([max(1,(k+1)//2),max(1,(k+2)//3),max(2,k),max(1,k),max(1,k),max(1,k),max(1,(k+1)//2)])
    return dict(id=f'{allocation or "minimum"}-width{n}-at{timing}',allocation=allocation,widths=stages,replaceAll=True)

def main():
    cat=OUT/'baseline.catalog.json';s.write(cat,json.loads((s.HERE/'reports/starter-copper/candidate.catalog.json').read_text()))
    cases=[]
    for a in ['', 'CIII']:
        for n in [1,2,3,4,6,8,12]:
            for timing in [1,2,4]:cases.append(dict(id=f'{a}-{n}-{timing}',catalog=str(cat),route=route(n,a,timing)))
    rows=run(cases,'baseline')
    for r in sorted(rows,key=lambda r:r['result']['seconds']):
        z=r['result'];print(r['id'],z['success'],round(z['seconds']/60,1),z['error'],flush=True)

if __name__=='__main__':main()
