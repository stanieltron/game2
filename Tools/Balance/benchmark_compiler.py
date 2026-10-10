import json
import time
import subprocess
from pathlib import Path
import balance as b
p=b.HERE/'reports/calibration-v2'
manifest=b.read(p/'manifest.json')
policy=manifest['definitions']['reference']['policy']
policy.update(name='reference',trace=True,maxSeconds=43200)
pp=p/'compiler-check.policy.json';b.write(pp,policy)
output=p/'compiler-optimized.json'
b.write(p/'compiler-check.cases.json',[[str(p/'catalogs/e0000.json'),str(pp),str(output)]])
t=time.monotonic()
subprocess.run(['pwsh','-NoProfile','-File',str(b.HERE/'run_cases_optimized.ps1'),'-Cases',str(p/'compiler-check.cases.json'),'-Step','.5','-Workers','1'],check=True)
result=b.read(output)
# Find the baseline replay by its full deterministic input key, not by similar timing.
catalog=b.read(p/'catalogs/e0000.json')
key=b.digest(dict(catalog=catalog,policy=policy,step=.5,source=manifest['sourceHashes']))
original=b.read(p/'cache'/(key+'.json'))
proof=dict(wallSeconds=time.monotonic()-t,exactReplayMatch=b.digest(result)==b.digest(original),seconds=result['seconds'])
b.write(p/'compiler-check.json',proof)
print(proof)
