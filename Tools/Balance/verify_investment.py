"""Checks design contracts against saved, live-rule investment benchmarks."""
import json
from pathlib import Path
root=Path(__file__).resolve().parent
p=root/'reports/investment-v2'
read=lambda n:json.loads((p/(n+'.replay.json')).read_text())
r={n:read(n) for n in ['rush','double','triple','small-late','large-late','weak-power','weak-fuel','weak-mining','weak-structural','weak-mechanical','weak-electrical','weak-fluid','supported-basic-only']}
assert all(x['success'] for x in r.values())
best=r['triple']['seconds'];assert 90*60<=best<=120*60
assert r['rush']['seconds']-r['double']['seconds']>=5*60
assert r['double']['seconds']-best>=5*60
for n in ['weak-power','weak-fuel','weak-mining','weak-structural','weak-mechanical','weak-electrical','small-late']:
 assert r[n]['seconds']-best>=10*60,n
assert r['supported-basic-only']['seconds']-best>=30*60
for name in ['rush','double','triple','weak-power','weak-mining']:
 fine=json.loads((p/(name+'.quarter.json')).read_text());assert fine['success'];assert abs(fine['seconds']-r[name]['seconds'])<30
c=json.loads((p/'catalog.snapshot.json').read_text());b={x['id']:x for x in c['buildings']};recipes={x['id']:x for x in c['recipes']}
assert b['industrialMiner']['rate']/b['miner']['rate']==5
assert b['industrialMiner']['power']/b['industrialMiner']['rate']==.5*b['miner']['power']/b['miner']['rate']
assert recipes['smelter_steel']['seconds']/recipes['industrialSmelter_steel']['seconds']==5
assert b['industrialSmelter']['power']*recipes['industrialSmelter_steel']['seconds']==.5*b['smelter']['power']*recipes['smelter_steel']['seconds']
print('PASS: target pacing, early reinvestment, later branch deficits, power/fuel, basic-only viability, efficiency, timestep stability')
