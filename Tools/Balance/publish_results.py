"""Publish saved deterministic results into the existing local HTML viewer.

This only formats existing evidence; it never runs or changes the game model.
"""
import argparse
import html
import json
import shutil
from pathlib import Path

HERE = Path(__file__).resolve().parent


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def dump(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2), encoding='utf-8')


def package(run):
    manifest = read(run / 'manifest.json')
    source = run / 'sources'
    destination = source / 'Assets/Resources/catalog.json'
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(run / 'catalog.snapshot.json', destination)
    spec = source / 'Tools/Balance/spec'
    if 'config' in manifest:
        dump(spec / 'targets.json', manifest['config'])
        dump(spec / manifest['config']['reference'], manifest['definitions']['reference']['policy'])
        dump(spec / manifest['config']['minimum'], manifest['definitions']['minimum']['policy'])
    else:
        dump(spec / 'widths.json', manifest['axes'])
        # The actual generated policies are in the manifest; archive the generator's
        # reference input too so its starter template can be reproduced.
        shutil.copy2(HERE / 'spec/reference.policy.json', spec / 'reference.policy.json')


def publish(balance_run, widths_run):
    web = HERE / 'web'
    if balance_run:
        manifest = read(balance_run / 'manifest.json')
        summary = read(balance_run / 'summary.json')
        page = (balance_run / 'report.html').read_text(encoding='utf-8')
        rows = []
        for candidate in summary['candidates']:
            for check in candidate.get('validation', {}).get('checks', []):
                if check['passed']:
                    continue
                value = f"{check['actual']:.2%}" if check['name'].startswith('penalty:') else str(check['actual'])
                rows.append('<tr>' + ''.join('<td>' + html.escape(str(v)) + '</td>' for v in
                                           [candidate['id'], check['name'], value, check['requirement']]) + '</tr>')
        evidence = '<section><h2>Full validation: counterexamples and refund tests</h2><table><tr><th>Candidate</th><th>Failed gate</th><th>Measured</th><th>Required</th></tr>' + ''.join(rows) + '</table><p>'
        evidence += html.escape('Fresh deterministic replay checks: ' + ', '.join(c['id'] + ': ' + str(c.get('deterministicReplay', 'not validated')) for c in summary['candidates'])) + '</p></section>'
        page = page.replace('<section><h2>Reproduce</h2>', evidence + '<section><h2>Reproduce</h2>')
        if ' --budget ' not in page:
            page = page.replace(' --output ', f" --budget {len(manifest['candidates'])} --output ")
        (web / 'balance-tests.html').write_text(page, encoding='utf-8')
        package(balance_run)
    if widths_run:
        summary = read(widths_run / 'summary.json')
        manifest = read(widths_run / 'manifest.json')
        page = (widths_run / 'report.html').read_text(encoding='utf-8')
        for name in ['widths.html', 'balance-v2.html']:
            (web / name).write_text(page, encoding='utf-8')
        if summary['ranking']:
            best = summary['ranking'][0]['id']
            dump(widths_run / 'best-policy.json', manifest['definitions'][best]['policy'])
            for path in sorted((widths_run / 'indexes').glob('*.json')):
                index = read(path)
                if best in index:
                    shutil.copy2(widths_run / index[best], widths_run / 'best-replay.json')
                    break
        package(widths_run)
        if balance_run:
            shutil.copy2(web / 'balance-tests.html', widths_run / 'balance-tests.html')
    print('Published local width results and balance tests.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--balance', type=Path)
    parser.add_argument('--widths', type=Path)
    args = parser.parse_args()
    publish(args.balance, args.widths)
