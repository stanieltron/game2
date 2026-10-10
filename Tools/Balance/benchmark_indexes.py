import subprocess
import time
import balance as b
import calibrate
p=b.HERE/'reports/calibration-v2';out=b.HERE/'reports/performance'
manifest=b.read(p/'manifest.json');index=b.read(p/'screen.index.json');cases=[];originals=[]
base=b.read(p/'catalog.snapshot.json')
for i,name in [(0,'minimum'),(1,'reference'),(1,'over_6_reclaim')]:
    cat=p/'catalogs'/f'e{i:04d}.json';policy=calibrate.supported(manifest['definitions'][name]['policy'],b.read(cat),base)
    policy.update(name=name,trace=True,maxSeconds=43200)
    pp=out/f'check-{i}-{name}.policy.json';b.write(pp,policy)
    replay=out/f'check-{i}-{name}.json';cases.append([str(cat),str(pp),str(replay)])
    originals.append(p/index[f'{i}:{name}'])
b.write(out/'indexed.cases.json',cases);t=time.monotonic()
subprocess.run(['pwsh','-NoProfile','-File',str(b.HERE/'run_cases_indexed.ps1'),'-Cases',str(out/'indexed.cases.json'),'-Step','.5','-Workers','1'],check=True)
matches=[b.digest(b.read(c[2]))==b.digest(b.read(old)) for c,old in zip(cases,originals)]
b.write(out/'verification.json',dict(exactMatches=matches,wallSeconds=time.monotonic()-t))
assert all(matches),matches
print('Three additional full replays match exactly:',matches)
