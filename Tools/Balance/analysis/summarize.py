"""Summarize traced campaigns and expand the current catalog's material tree."""
import csv
import json
import math
import hashlib
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Tools/Balance/reports'
C = json.loads((ROOT / 'Assets/Resources/catalog.json').read_text())
E = json.loads((OUT / 'economy.json').read_text())
assert E['catalogHash'] == hashlib.sha256((ROOT / 'Assets/Resources/catalog.json').read_bytes().decode('utf-8-sig').encode()).hexdigest(), 'Re-export current economy before analyzing'

def stock(stacks):
    result = Counter()
    for s in stacks:
        result[s['item']] += s['amount']
    return result

def aggregate(groups):
    result = Counter()
    for group in groups:
        result.update(stock(group))
    return result

recipes = {}
for r in C['recipes']:
    if r['family'] != 'emergency':
        assert len(r['outputs']) == 1, 'Expansion needs a multi-output policy'
        recipes.setdefault(r['outputs'][0]['item'], r)

def depth(item, visiting=()):
    assert item not in visiting, 'Recipe dependency cycle'
    r = recipes.get(item)
    return 0 if r is None else 1 + max((depth(s['item'], visiting+(item,)) for s in r['inputs']), default=0)

def expand(demand):
    """Pool batches across all requested items; ignore starting stock and buildings."""
    needed = Counter(demand)
    batches = {}
    for item in sorted(recipes, key=depth, reverse=True):
        count = needed.pop(item, 0)
        if not count:
            continue
        r = recipes[item]
        n = math.ceil(count/r['outputs'][0]['amount'])
        batches[r['id']] = n
        needed.update({s['item']:s['amount']*n for s in r['inputs']})
    return dict(needed), batches

direct = {
    'research': aggregate(t['cost'] for t in C['techs']),
    'bay': aggregate(p['cost'] for p in E['bayPhases']),
    'fighter': aggregate(p['cost'] for p in E['fighterPhases']),
}
combined = sum(direct.values(), Counter())
raw, batches = expand(combined)
summary = {
    'scope': 'Gross pooled-batch materials for four technologies, all Bay phases, and Fighter assembly. Excludes buildings, generator fuel, startup milestones, raid stock; does not subtract starter inventory. Uses ordinary recipes, not emergency recovery.',
    'direct': direct,
    'grossRaw': raw,
    'expandedBatchesUsingFirstOrdinaryRecipe': batches,
    'nominalResearchSeconds': sum(t['seconds'] for t in C['techs']),
    'nominalBaySeconds': sum(p['seconds'] for p in E['bayPhases']),
    'nominalFighterSeconds': sum(p['seconds'] for p in E['fighterPhases']),
    'techTree': []
}
for tech in range(5):
    buildings = [b for b in C['buildings'] if b['tech'] == tech]
    effective = []
    for r in C['recipes']:
        compatible = [b for b in C['buildings'] if b['family'] == r['family']]
        gate = max(r['tech'], min((b['tech'] for b in compatible), default=0))
        if gate == tech:
            effective.append(r)
    summary['techTree'].append({'tech':tech, 'research':None if tech==0 else C['techs'][tech-1], 'buildings':buildings, 'recipes':effective})
(OUT/'full-tech-analysis.json').write_text(json.dumps(summary, indent=2)+'\n')

def duration(seconds):
    seconds = round(seconds)
    return f'{seconds//3600:02}:{seconds//60%60:02}:{seconds%60:02}'

for policy in ('baseline','pause-idle','retire-basic-generator'):
    d = json.loads((OUT/f'campaign-{policy}.json').read_text())
    assert d['fighterConstructed']
    assert sum(s['seconds'] for s in d['stages']) == d['seconds']
    assert sum(d['waits'].values()) == d['seconds']
    with (OUT/f'campaign-{policy}-timeline.csv').open('w',newline='') as f:
        w = csv.writer(f)
        w.writerow(['stage','start','finish','elapsed','material_preparation','phase_or_research_work','mean_grid_ratio'])
        for s in d['stages']:
            split = s['stage'] != 'Bootstrap and production milestone' and not s['stage'].startswith('Optional')
            w.writerow([s['stage'],duration(s['start']),duration(s['end']),duration(s['seconds']),duration(s['preparationSeconds']) if split else '',duration(s['seconds']-s['preparationSeconds']) if split else '',round(s['meanRatio'],4)])
    print(policy, duration(d['seconds']), 'brownout hours',sum(s['brownoutSeconds'] for s in d['stages'])/3600)
    print('wait categories', {k:duration(sum(v for n,v in d['waits'].items() if n.startswith(k))) for k in ('production:','construction','research','bay-phase','fighter-phase','raw:')})
print('Direct materials:',dict(direct))
print('Gross raw:',raw)
print('Nominal research / Bay / Fighter seconds:',summary['nominalResearchSeconds'],summary['nominalBaySeconds'],summary['nominalFighterSeconds'])
