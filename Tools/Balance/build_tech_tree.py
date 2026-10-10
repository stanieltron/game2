"""Build the single local tech-tree page from catalogs and actual project phases."""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
HERE = ROOT / 'Tools/Balance'
source = (ROOT / 'Assets/Scripts/FactoryMegaproject.cs').read_text()
phases = {}
for key in ('Bay', 'Fighter'):
    block = source.split(key + 'Phases={', 1)[1].split('};', 1)[0]
    phases[key] = [dict(name=name, seconds=int(seconds), power=int(power),
                        cost=[dict(item=i, amount=int(n)) for i, n in re.findall(r'S\("([^"]+)",(\d+)\)', rest)])
                   for name, seconds, power, rest in re.findall(r'new ProjectPhase\("([^"]+)",(\d+),(\d+)(.*)', block)]
    assert len(phases[key]) == 5
data = dict(phases=phases, profiles={
    'live': json.loads((ROOT / 'Assets/Resources/catalog.json').read_text()),
    'experimental': json.loads((HERE / 'reports/fighter-progression/fine.catalog.json').read_text())})
template = (HERE / 'minimum-factory.html.in').read_text(encoding='utf-8')
(HERE / 'web/index.html').write_text(template.replace('__DATA__', json.dumps(data).replace('</', '<\\/')), encoding='utf-8')
print('Built Tools/Balance/web/index.html from two catalogs and 10 project phases.')
