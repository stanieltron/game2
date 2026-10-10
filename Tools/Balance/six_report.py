"""Validate measured six-v4 runs and publish the local comparison page."""
import csv
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPORT = HERE / 'reports/six-v4'
data = []
names = [('Minimum upgraded factory', 'minimum')]
names += [(f'{n} structural + {n} mechanical; 4 electrical', f'n{n}') for n in [4, 5, 6, 7, 8, 12]]
for label, name in names:
    result = json.loads((REPORT / f'live-{name}.json').read_text(encoding='utf-8-sig'))
    assert result['success'], (name, result['error'])
    assert result['step'] == .125
    assert all(sample['ratio'] in [0, 1] for sample in result['samples'])
    data.append(dict(label=label, **result))

best = data[3]
assert best['counts']['structural'] == best['counts']['mechanical'] == best['counts']['industrialSmelter'] == 6
assert best['counts']['electrical'] == 4 and best['counts']['fluid'] == 2
assert all(best['seconds'] < result['seconds'] for i, result in enumerate(data) if i != 3)
for n in [5, 6, 7, 8]:
    path = REPORT / f'live-quarter-n{n}.json'
    if path.exists():
        result = json.loads(path.read_text(encoding='utf-8-sig'))
        reference = next(d for d in data if d['label'].startswith(f'{n} structural'))
        assert result['success'] and abs(result['seconds'] - reference['seconds']) < 120

fine = {}
for n in [6, 7, 8]:
    fine[n] = json.loads((REPORT / f'live-sixteenth-n{n}.json').read_text(encoding='utf-8-sig'))
    assert fine[n]['success'] and fine[n]['step'] == .0625
assert fine[6]['seconds'] < fine[7]['seconds'] < fine[8]['seconds']

root = HERE.parents[1]
profile = json.loads((HERE / 'profiles/six-v4.json').read_text(encoding='utf-8'))
assert profile['projectPhaseSource'] in (root / 'Assets/Scripts/FactoryMegaproject.cs').read_text(encoding='utf-8-sig')
catalog = json.loads((root / 'Assets/Resources/catalog.json').read_text(encoding='utf-8-sig'))
for group in ['buildings', 'recipes', 'techs']:
    for entry in catalog[group]:
        for key, value in profile[group][str(entry['id'])].items():
            assert entry[key] == value, (group, entry['id'], key)

page = (HERE / 'six_report_template.html').read_text(encoding='utf-8')
page = page.replace('__DATA__', json.dumps(data, separators=(',', ':')))
for path in [HERE / 'web/balance-v2.html', HERE / 'web/balance-v4.html', REPORT / 'report.html']:
    path.write_text(page, encoding='utf-8')

for filename, result in [('best-build-order.csv', best), ('minimum-build-order.csv', data[0])]:
    with (REPORT / filename).open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.writer(stream)
        writer.writerow(['Elapsed seconds', 'Stage', 'Action', 'Building', 'Wait to afford seconds'])
        for event in result['events']:
            if event['kind'] in ['funding', 'reclaim']:
                writer.writerow([event['time'], event['stage'], event['kind'], event['detail'], event.get('waitSeconds', '')])

print('Six-v4 validated. Best minutes:', best['seconds'] / 60,
      'minimum / best:', data[0]['seconds'] / best['seconds'])
