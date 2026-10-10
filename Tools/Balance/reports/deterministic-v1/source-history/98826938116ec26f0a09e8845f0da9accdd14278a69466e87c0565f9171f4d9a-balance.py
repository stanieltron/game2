"""Deterministic constraint evaluation / bounded economic-parameter search.

Runs the actual C# factory simulation. Never applies a candidate to game sources.
Exit codes: 0 all gates pass; 2 measured balance fails; 1 execution/input error.
"""
import argparse
import copy
import hashlib
import html
import json
import math
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
SPECIALISTS = {'structural', 'electrical', 'mechanical', 'fluid'}


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=True)


def digest(value):
    return hashlib.sha256(canonical(value).encode()).hexdigest()


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True), encoding='utf-8')


def scenarios(spec_path, config):
    base = read(spec_path.parent / config['reference'])
    minimum = read(spec_path.parent / config['minimum'])
    result = {}

    def add(name, policy, full=False, gate=None):
        policy = copy.deepcopy(policy)
        policy.update(name=name, trace=True, maxSeconds=43200)
        result[name] = {'policy': policy, 'full': full, 'gate': gate}

    def counts(s=6, m=6, delayed=False):
        policy = copy.deepcopy(base)
        for stage in policy['stages'][3 if delayed else 2:]:
            for target in stage:
                if target['spec'] == 'structural':
                    target['count'] = s
                elif target['spec'] == 'mechanical':
                    target['count'] = m
                elif target['spec'] == 'industrialGenerator' and max(s, m) >= 8:
                    target['count'] = max(4, target['count'])
        return policy

    add('reference', base)
    add('minimum', minimum)
    for name, count in [('under_2', 5), ('under_4', 4), ('over_2', 7), ('over_4', 8), ('over_6', 9), ('over_12', 12)]:
        add(name, counts(count, count), gate=name)
    # Uneven factories must not hide behind a deliberately overbuilt electrical pool.
    for name, s, m, gate in [('structural_plus_6', 12, 6, 'over_6'), ('mechanical_plus_6', 6, 12, 'over_6')]:
        add(name, counts(s, m), True, gate)
    add('over_6_late', counts(9, 9, True), True, 'over_6')
    reclaimed = counts(9, 9)
    reclaimed['reclaimAtBay'] = {'structural': 6, 'mechanical': 6}
    add('over_6_reclaim', reclaimed, True, 'over_6')
    for name, spec, n in [('smelter_7', 'industrialSmelter', 7), ('electrical_3', 'electrical', 3), ('iron_2', 'industrialMiner:iron', 2)]:
        policy = copy.deepcopy(base)
        for stage in policy['stages'][2:]:
            for target in stage:
                if target['spec'] == spec:
                    target['count'] = n
        add(name, policy, True)
    alternative = copy.deepcopy(base)
    alternative['recipeRule'] = 1
    add('alternate_recipe_priority', alternative, True)
    early = copy.deepcopy(base)
    early['stages'][0] = early['stages'][0][:12]
    add('early_double', early, True)
    return result


def score(results, definitions, targets, full=False):
    checks = []

    def check(name, actual, requirement, passed, violation):
        checks.append(dict(name=name, actual=actual, requirement=requirement,
                           passed=bool(passed), violation=round(float(violation), 10)))

    required = {name for name, d in definitions.items() if (full or not d['full']) and (name in ['reference', 'minimum'] or d['gate'])}
    for name, definition in sorted(definitions.items()):
        if full or not definition['full']:
            check('coverage:' + name, name in results, 'Scenario evaluated', name in results, 0 if name in results else 10)
    for name in sorted(required):
        result = results.get(name)
        ok = result is not None and result.get('success', False)
        check('complete:' + name, bool(ok), 'Constructed Fighter, no timeout', ok, 0 if ok else 10)
    ref = results.get('reference', {})
    if not ref.get('success'):
        return {'passed': False, 'loss': sum(c['violation'] for c in checks), 'checks': checks}
    seconds = ref['seconds']
    lo, hi = targets['fastMinutes']
    minutes = seconds / 60
    check('fast_minutes', minutes, [lo, hi], lo <= minutes <= hi,
          max(0, lo - minutes) / lo + max(0, minutes - hi) / hi)
    minimum = results.get('minimum', {})
    if minimum.get('success'):
        ratio = minimum['seconds'] / seconds
        lo, hi = targets['minimumRatio']
        check('minimum_ratio', ratio, [lo, hi], lo <= ratio <= hi,
              max(0, lo - ratio) / lo + max(0, ratio - hi) / hi)
    for name, definition in sorted(definitions.items()):
        if definition['full'] and not full:
            continue
        result = results.get(name, {})
        if not result.get('success'):
            continue  # Failures never count as successful overinvestment penalties.
        if definition['gate']:
            penalty = result['seconds'] / seconds - 1
            target = targets['penalties'][definition['gate']]
            check('penalty:' + name, penalty, '>= ' + str(target), penalty >= target,
                  max(0, target - penalty) / target)
        elif name not in ['reference', 'minimum']:
            advantage = seconds - result['seconds']
            tolerance = targets['maxCounterexampleAdvantageSeconds']
            check('counterexample:' + name, advantage, '<= ' + str(tolerance) + ' seconds',
                  advantage <= tolerance, max(0, advantage - tolerance) / seconds)
    return {'passed': all(c['passed'] for c in checks),
            'loss': round(sum(c['violation'] for c in checks), 10), 'checks': checks}


def tune(catalog, parameters):
    catalog = copy.deepcopy(catalog)
    for building in catalog['buildings']:
        if building['family'] in SPECIALISTS or building['id'] == 'industrialSmelter':
            for cost in building['cost']:
                cost['amount'] = max(1, math.floor(cost['amount'] * parameters['capital'] + .5))
    for recipe in catalog['recipes']:
        if recipe['family'] in SPECIALISTS:
            recipe['seconds'] = round(recipe['seconds'] * parameters['cycles'], 6)
    return catalog


class Run:
    def __init__(self, output, manifest, definitions, workers):
        self.output, self.definitions, self.workers = output, definitions, workers
        self.identity = digest(manifest)
        self.source_files = manifest['sourceHashes']
        self.batch = 0

    def verify_sources(self):
        for name, expected in self.source_files.items():
            actual = hashlib.sha256((ROOT / name).read_bytes()).hexdigest()
            if actual != expected:
                raise RuntimeError('Source changed during run: ' + name)

    def simulate(self, candidate, full, step):
        self.verify_sources()
        catalog = candidate['catalog']
        cat_path = self.output / 'catalogs' / (digest(catalog) + '.json')
        write(cat_path, catalog)
        cases, outputs = [], {}
        for name, definition in self.definitions.items():
            if definition['full'] and not full:
                continue
            policy = definition['policy']
            key = digest({'run': self.identity, 'catalog': catalog, 'policy': policy, 'step': step})
            policy_path = self.output / 'policies' / (digest(policy) + '.json')
            write(policy_path, policy)
            path = self.output / 'cache' / (key + '.json')
            outputs[name] = path
            if path.exists():
                # Fail closed on corrupted caches; completion alone is not a checksum.
                checksum = path.with_suffix('.sha256')
                if not checksum.exists():
                    # An interrupted batch can leave a partial replay. Preserve it,
                    # then recompute rather than treating it as verified cache data.
                    orphan = path.with_suffix('.uncommitted')
                    if orphan.exists():
                        orphan = path.with_suffix('.' + hashlib.sha256(path.read_bytes()).hexdigest()[:12] + '.uncommitted')
                    path.replace(orphan)
                    cases.append([str(cat_path), str(policy_path), str(path)])
                elif checksum.read_text().strip() != hashlib.sha256(path.read_bytes()).hexdigest():
                    raise RuntimeError('Invalid cache checksum: ' + str(path))
            else:
                cases.append([str(cat_path), str(policy_path), str(path)])
        if cases:
            self.batch += 1
            case_path = self.output / f'batch-{self.batch:03d}.json'
            write(case_path, cases)
            (self.output / 'cache').mkdir(exist_ok=True)
            command = ['pwsh', '-NoProfile', '-File', str(HERE / 'run_cases.ps1'),
                       '-Cases', str(case_path), '-Step', str(step), '-Workers', str(self.workers)]
            print(f"SIMULATE {candidate['id']}: {len(cases)} routes, step {step}", flush=True)
            subprocess.run(command, check=True, cwd=ROOT)
            for row in cases:
                path = Path(row[2])
                path.with_suffix('.sha256').write_text(hashlib.sha256(path.read_bytes()).hexdigest(), encoding='utf-8')
        self.verify_sources()
        results = {name: read(path) for name, path in outputs.items()}
        write(self.output / 'indexes' / f"{candidate['id']}-{step}.json", {name: str(path.relative_to(self.output)) for name, path in outputs.items()})
        return results


def report(output, summary):
    esc = html.escape
    rows = []
    for item in summary['candidates']:
        for check in item['screen']['checks']:
            if not check['passed']:
                rows.append(f"<tr><td>{esc(item['id'])}</td><td>{esc(check['name'])}</td><td>{esc(str(check['actual']))}</td><td>{esc(str(check['requirement']))}</td></tr>")
    cards = ''.join(f"<tr><td>{esc(c['id'])}</td><td>{esc(str(c['parameters']))}</td><td>{c['screen']['loss']:.3f}</td><td>{'PASS' if c.get('passed') else 'FAIL / unvalidated'}</td></tr>" for c in summary['candidates'])
    page = f'''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Deterministic balance tests</title><style>body{{background:#10191d;color:#e5eeeb;font:15px system-ui;max-width:1150px;margin:40px auto;padding:24px}}h1{{font-size:40px}}p{{line-height:1.6;color:#b6c9c5}}section{{padding:24px;background:#1a292e;border:1px solid #38534e;border-radius:12px;margin:24px 0}}td,th{{text-align:left;padding:12px;border-bottom:1px solid #38534e}}table{{width:100%;border-collapse:collapse}}.status{{font-size:36px;color:{'#8edbb5' if summary['passed'] else '#f3b372'}}}code{{color:#9ddacc}}a{{color:#9ddacc}}</style><h1>Balance is a test, not a judgment call.</h1><div class="status">{'PASS' if summary['passed'] else 'FAIL — target experience not achieved'}</div><p>Same catalog, policies, source hashes and timestep produce the same result. Failed routes never count as successful penalties. Candidates remain isolated; this tool does not change the game.</p><section><h2>Explicit requirements</h2><pre>{esc(json.dumps(summary['targets'], indent=2))}</pre><p>“Over 6” means six extra machines total: three structural and three mechanical. Full validation also tries six extra in a single family, delayed investment and reclaiming surplus machines before the Bay.</p></section><section><h2>Parameter search</h2><table><tr><th>Candidate</th><th>Multipliers</th><th>Violation score ↓</th><th>Outcome</th></tr>{cards}</table><p>Violation score sums normalized shortfalls; zero is necessary but not sufficient. Full scenario validation, fine-step confirmation and a deterministic replay must also pass. The search is bounded to the configured candidate list; failure does not prove no possible balance exists.</p></section><section><h2>Measured failures in screening</h2><table><tr><th>Candidate</th><th>Test</th><th>Measured</th><th>Required</th></tr>{''.join(rows)}</table></section><section><h2>Reproduce</h2><p><code>python Tools/Balance/balance.py {summary['mode']} --output {esc(str(output))} --resume</code></p><p>See summary.json for full validation checks, indexes/ for exact replays, and manifest.json for inputs and source hashes. Simulator limitations: pooled belt budgets, automatic cargo and consumer pausing; not physical layouts or player construction time. No globally optimal build-order claim.</p></section></html>'''
    (output / 'report.html').write_text(page, encoding='utf-8')


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=['evaluate', 'search'])
    parser.add_argument('--spec', type=Path, default=HERE / 'spec/targets.json')
    parser.add_argument('--catalog', type=Path, default=ROOT / 'Assets/Resources/catalog.json')
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--workers', type=int, default=3)
    parser.add_argument('--budget', type=int, default=8)
    parser.add_argument('--resume', action='store_true')
    args = parser.parse_args(argv)
    if args.workers < 1 or args.budget < 1:
        parser.error('workers and budget must be positive')
    config, catalog = read(args.spec), read(args.catalog)
    for key in ['screenStep', 'validationStep', 'confirmStep']:
        if not 0 < config['search'][key] <= .5:
            parser.error('Simulation steps must be in (0, 0.5]')
    if any(p[k] <= 0 for p in config['search']['candidates'] for k in ['capital', 'cycles']):
        parser.error('Economic multipliers must be positive')
    if any(v <= 0 for v in config['targets']['penalties'].values()):
        parser.error('Penalty targets must be positive')
    definitions = scenarios(args.spec, config)
    sources = list((ROOT / 'Assets/Scripts').glob('Factory*.cs')) + [HERE / name for name in ['FactoryPlanner.cs', 'SearchPolicy.cs', 'Runner.cs', 'BatchSearch.cs', 'BalanceCases.cs', 'load.ps1', 'run_cases.ps1', 'balance.py']]
    hashes = {str(path.relative_to(ROOT)): hashlib.sha256(path.read_bytes()).hexdigest() for path in sorted(sources)}
    parameters = config['search']['candidates'][:args.budget] if args.mode == 'search' else [{'capital': 1, 'cycles': 1}]
    manifest = {'config': config, 'catalogHash': digest(catalog), 'definitions': definitions,
                'sourceHashes': hashes, 'mode': args.mode, 'candidates': parameters}
    output = args.output.resolve()
    if output.exists() and any(output.iterdir()):
        if not args.resume or not (output / 'manifest.json').exists() or read(output / 'manifest.json') != manifest:
            parser.error('Output exists or inputs changed. Choose a new output; --resume requires identical inputs.')
    output.mkdir(parents=True, exist_ok=True)
    write(output / 'manifest.json', manifest)
    write(output / 'catalog.snapshot.json', catalog)
    for path in sources:
        dest = output / 'sources' / path.relative_to(ROOT)
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes(path.read_bytes())
    run = Run(output, manifest, definitions, args.workers)
    candidates = []
    summary = {'mode': args.mode, 'targets': config['targets'], 'passed': False, 'candidates': candidates}
    for index, params in enumerate(parameters):
        candidate = {'id': f'candidate-{index:03d}', 'parameters': params, 'catalog': tune(catalog, params)}
        results = run.simulate(candidate, False, config['search']['screenStep'])
        result = {'id': candidate['id'], 'parameters': params,
                  'screen': score(results, definitions, config['targets']), 'passed': False}
        candidates.append(result)
        write(output / 'summary.json', summary)
        report(output, summary)
        print(f"SCORE {candidate['id']} loss={result['screen']['loss']:.4f}", flush=True)
    ranked = sorted(candidates, key=lambda c: (c['screen']['loss'], c['id']))
    for result in ranked[:config['search']['finalists']]:
        candidate = {'id': result['id'], 'parameters': result['parameters'], 'catalog': tune(catalog, result['parameters'])}
        results = run.simulate(candidate, True, config['search']['validationStep'])
        result['validation'] = score(results, definitions, config['targets'], True)
        # Always verify an actual fresh replay, including when the economy fails.
        repeat = output / (candidate['id'] + '-repeat.json')
        if not repeat.exists():
            repeat_cases = [[str(output / 'catalogs' / (digest(candidate['catalog']) + '.json')),
                             str(output / 'policies' / (digest(definitions['reference']['policy']) + '.json')), str(repeat)]]
            write(output / (candidate['id'] + '-repeat.cases.json'), repeat_cases)
            subprocess.run(['pwsh', '-NoProfile', '-File', str(HERE / 'run_cases.ps1'), '-Cases', str(output / (candidate['id'] + '-repeat.cases.json')), '-Step', str(config['search']['validationStep']), '-Workers', '1'], check=True, cwd=ROOT)
        result['deterministicReplay'] = digest(read(repeat)) == digest(results['reference'])
        # Confirmation is deliberately not spent on candidates already known to fail.
        if result['validation']['passed']:
            fine = run.simulate(candidate, True, config['search']['confirmStep'])
            result['confirmation'] = score(fine, definitions, config['targets'], True)
            result['stepStable'] = all(abs(fine[name]['seconds'] - r['seconds']) <= config['targets']['maxStepDifferenceSeconds'] for name, r in results.items() if r['success'] and fine[name]['success'])
            result['passed'] = result['confirmation']['passed'] and result['stepStable'] and result['deterministicReplay']
        summary['passed'] = any(c['passed'] for c in candidates)
        write(output / 'summary.json', summary)
        report(output, summary)
    print('PASS' if summary['passed'] else 'FAIL: no fully validated candidate meets the targets', flush=True)
    print(output / 'report.html', flush=True)
    return 0 if summary['passed'] else 2


if __name__ == '__main__':
    sys.exit(main())
