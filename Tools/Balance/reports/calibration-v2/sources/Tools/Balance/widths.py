"""Deterministic staged factory-width sweep using the game's C# simulation.

No price tuning or game-file mutation. --trials 0 runs the whole configured grid.
"""
import argparse
import copy
import hashlib
import itertools
import json
import math
from pathlib import Path
import balance

HERE, ROOT = balance.HERE, balance.ROOT


def policy(catalog, vector, lanes, order):
    buildings = {b['id']: b for b in catalog['buildings']}
    base = balance.read(HERE / 'spec/reference.policy.json')
    base['stages'] = [copy.deepcopy(base['stages'][0][:7])]
    base['laneTargets'] = [1, 1, lanes, lanes]
    base.update(trace=True, maxSeconds=43200, recipeRule=0, reclaim=True)
    base.pop('reclaimAtBay', None)

    def target(spec, n):
        return dict(spec=spec, count=int(n))

    # Fixed starter followed by optional purchases, never free initial buildings.
    for n in range(2, vector[0] + 1):
        g = max(1, math.ceil((n * 66 + 100 - 80) / buildings['generator']['generation']))
        base['stages'][0] += [target('miner:carbon', max(1, math.ceil(g / 2))),
                              target('generator', g), target('miner:iron', math.ceil(n * 2 / 3)),
                              target('smelter', n), target('workshop', n)]
    for tech, width in enumerate(vector[1:], 1):
        counts = {'industrialMiner:iron': max(1, math.ceil(width / 2)),
                  'industrialMiner:copperOre': max(1, math.ceil(width / 3)),
                  'industrialMiner:silicate': 1, 'industrialMiner:carbon': 1,
                  'industrialSmelter': width, 'structural': width,
                  'electrical': max(1, math.ceil(width * 2 / 3)), 'mechanical': width}
        if tech >= buildings['fluid']['tech']:
            counts['fluid'] = max(1, math.ceil(width / 3))
        # Purchase enough nominal power, including a conservative pooled-belt allowance.
        # Idle/excess consumer pausing still uses the common benchmark dispatcher.
        for _ in range(8):
            load = sum(buildings[spec.split(':')[0]]['power'] * n for spec, n in counts.items())
            load += (35 if tech == 1 else 30 * lanes) * buildings['belt']['power']
            load += 180 if tech == 3 else catalog['techs'][min(tech, len(catalog['techs']) - 1)]['power']
            generators = max(1, math.ceil((load - 80) / buildings['industrialGenerator']['generation']))
            coal = max(1, math.ceil(generators * buildings['industrialGenerator']['fuelRate'] / buildings['industrialMiner']['rate']))
            if coal == counts['industrialMiner:carbon']:
                break
            counts['industrialMiner:carbon'] = coal
        counts['industrialGenerator'] = generators
        sequence = ['industrialMiner:carbon', 'industrialGenerator', 'industrialMiner:iron',
                    'industrialSmelter', 'structural', 'electrical', 'mechanical',
                    'industrialMiner:copperOre', 'industrialMiner:silicate', 'fluid']
        if tech == 1:
            # Get one operating unit before funding whole-family expansion.
            first = sequence[:7]
            if order == 'specialists-first':
                first = ['structural', 'electrical', 'industrialMiner:carbon', 'industrialGenerator',
                         'industrialMiner:iron', 'industrialSmelter', 'mechanical']
            stage = [target(spec, 1) for spec in first]
        else:
            stage = []
        stage += [target(spec, counts[spec]) for spec in sequence if spec in counts]
        base['stages'].append(stage)
    return base


def definitions(catalog, trials, axes=None):
    axes = axes or balance.read(HERE / 'spec/widths.json')
    combinations = [(v, lanes, order) for v in itertools.product(*(axes[k] for k in ['starter', 'industry', 'components', 'later']))
                    if v[1] <= v[2] <= v[3]
                    for lanes in axes['lanes'] for order in axes['orders']]
    if 0 < trials < len(combinations):
        # Evenly spaced indexes through the complete canonical grid; no randomness.
        indexes = sorted({round(i * (len(combinations) - 1) / max(1, trials - 1)) for i in range(trials)})
        selected = [combinations[i] for i in indexes]
    else:
        selected = combinations
    defs, metadata = {}, {}
    for i, (v, lanes, order) in enumerate(selected):
        name = f'w{i:04d}'
        value = policy(catalog, v, lanes, order)
        value['name'] = name
        defs[name] = dict(policy=value, full=False, gate=None)
        metadata[name] = dict(widths=list(v), lanes=lanes, order=order)
    return defs, metadata, len(combinations)


def render(output, rows, total, grid_size, step, axes):
    slim = []
    for row in rows:
        result = row['result']
        slim.append(dict(id=row['id'], **row['parameters'], success=result['success'], seconds=result['seconds'],
                         error=result['error'], counts=result['counts'], milestones=result['milestones'],
                         energy=result['energyPUHours'], powerWait=result['powerWaitMachineSeconds'],
                         capitalWait=result['capitalWaitSeconds'], capital=result['capitalSpent'],
                         builds=[e for e in result['events'] if e['kind'] in ['funding', 'reclaim']]))
    payload = json.dumps(dict(rows=slim, total=total, grid=grid_size, step=step, axes=axes), separators=(',', ':')).replace('</', '<\\/')
    template = (HERE / 'widths_template.html').read_text(encoding='utf-8')
    (output / 'report.html').write_text(template.replace('__DATA__', payload), encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--trials', type=int, default=48)
    parser.add_argument('--workers', type=int, default=3)
    parser.add_argument('--step', type=float, default=.25)
    parser.add_argument('--batch', type=int, default=8)
    parser.add_argument('--resume', action='store_true')
    parser.add_argument('--grid', type=Path, default=HERE / 'spec/widths.json')
    args = parser.parse_args()
    if args.trials < 0 or args.workers < 1 or args.batch < 1 or not 0 < args.step <= .5:
        parser.error('Use nonnegative trials, positive workers/batch, and step in (0, .5].')
    catalog = balance.read(ROOT / 'Assets/Resources/catalog.json')
    axes = balance.read(args.grid)
    if any(not axes[k] or any(not isinstance(n, int) or n < 1 for n in axes[k]) for k in ['starter', 'industry', 'components', 'later', 'lanes']):
        parser.error('Width/lanes axes must contain positive integers.')
    if not axes['orders'] or any(s not in ['industry-first', 'specialists-first'] for s in axes['orders']):
        parser.error('Unsupported investment order.')
    defs, metadata, grid_size = definitions(catalog, args.trials, axes)
    if not defs:
        parser.error('No nondecreasing industrial progression exists in this grid.')
    sources = list((ROOT / 'Assets/Scripts').glob('Factory*.cs')) + [HERE / name for name in
               ['FactoryPlanner.cs', 'SearchPolicy.cs', 'Runner.cs', 'BatchSearch.cs', 'BalanceCases.cs',
                'load.ps1', 'run_cases.ps1', 'balance.py', 'widths.py', 'widths_template.html']]
    hashes = {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(sources)}
    manifest = dict(catalogHash=balance.digest(catalog), sourceHashes=hashes, definitions=defs,
                    metadata=metadata, gridSize=grid_size, step=args.step, selected=len(defs), axes=axes)
    output = args.output.resolve()
    try:
        balance.prepare_output(output, manifest, args.resume)
    except ValueError as error:
        parser.error(str(error))
    balance.write(output / 'catalog.snapshot.json', catalog)
    for source in sources:
        dest = output / 'sources' / source.relative_to(ROOT)
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes(source.read_bytes())
    runner = balance.Run(output, manifest, defs, args.workers)
    rows = []
    names = list(defs)
    for start in range(0, len(names), args.batch):
        runner.definitions = {name: defs[name] for name in names[start:start + args.batch]}
        results = runner.simulate({'id': f'widths-{start:04d}', 'catalog': catalog}, False, args.step)
        rows += [dict(id=name, parameters=metadata[name], result=result) for name, result in results.items()]
        winners = sorted((r for r in rows if r['result']['success']), key=lambda r: (r['result']['seconds'], r['id']))
        summary = dict(evaluated=len(rows), planned=len(defs), gridSize=grid_size,
                       successes=len(winners), failures=len(rows)-len(winners), step=args.step,
                       ranking=[dict(id=r['id'], parameters=r['parameters'], seconds=r['result']['seconds'], counts=r['result']['counts']) for r in winners])
        balance.write(output / 'summary.json', summary)
        render(output, rows, len(defs), grid_size, args.step, axes)
        print(f"WIDTHS {len(rows)}/{len(defs)}; completed {len(winners)}; best {winners[0]['result']['seconds']/60 if winners else 'none'} min", flush=True)
    return 0 if any(r['result']['success'] for r in rows) else 2


if __name__ == '__main__':
    raise SystemExit(main())
