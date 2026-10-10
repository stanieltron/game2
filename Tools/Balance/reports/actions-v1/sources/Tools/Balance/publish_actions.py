"""Archive action-search inputs and publish completed route ledgers to the local report."""
import argparse
import hashlib
import json
import shutil
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]


def archive(output):
    paths = list((ROOT / 'Assets/Scripts').glob('Factory*.cs')) + [ROOT / 'Assets/Resources/catalog.json']
    paths += [HERE / name for name in ('Runner.cs', 'SearchPolicy.cs', 'FactoryPlanner.cs', 'ActionPlanner.cs',
                                      'ActionSearch.cs', 'load.ps1', 'run_actions.ps1', 'publish_actions.py')]
    paths += list((HERE / 'spec').glob('*.policy.json'))
    hashes = {}
    for source in paths:
        relative = source.relative_to(ROOT)
        destination = output / 'sources' / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, destination)
        hashes[str(relative)] = hashlib.sha256(source.read_bytes()).hexdigest()
    (output / 'source-hashes.json').write_text(json.dumps(hashes, indent=2))


def publish(output):
    routes = {}
    for name in ('fastest', 'slowest', 'minimum-seed'):
        replay = output / f'{name}.replay.json'
        if replay.exists():
            routes[name] = json.loads(replay.read_text(encoding='utf-8-sig'))
            routes[name]['program'] = json.loads((output / f'{name}.program.json').read_text(encoding='utf-8-sig'))
    (HERE / 'web/action-routes.json').write_text(json.dumps(routes, separators=(',', ':')), encoding='utf-8')
    shutil.copy2(output / 'results.json', HERE / 'web/action-programs.json')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('output', type=Path)
    parser.add_argument('--archive', action='store_true')
    args = parser.parse_args()
    if args.archive:
        archive(args.output)
    else:
        publish(args.output)
