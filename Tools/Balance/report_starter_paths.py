"""Summarize the explicit starter-expansion comparison; preserve raw replays."""
import csv
import hashlib
import html
import json
import shutil
import sys
from pathlib import Path

root=Path(__file__).resolve().parents[2]
out=Path(sys.argv[1]).resolve()
rows=json.loads((out/'results.json').read_text(encoding='utf-8-sig'))
best=[]
for width in range(1,11):
    candidates=[r for r in rows if r['path']==width and r['success']]
    if not candidates: raise RuntimeError(f'No successful route for path {width}')
    best.append(min(candidates,key=lambda r:(r['seconds'],r['allocation'])))
for r in rows:
    if not r['success']: continue
    assert r['counts']['industrialMiner:iron']==r['iron']
    assert r['counts']['industrialMiner:copperOre']==r['copper']
    assert r['counts']['industrialSmelter']==r['iron']+r['copper']
    assert r['counts']['structural']==r['iron']
    assert r['counts']['electrical']==r['copper']
    assert r['seconds']>=r['researchSeconds']>=r['expandedSeconds']
(out/'best-paths.json').write_text(json.dumps(best,indent=2))
with (out/'comparison.csv').open('w',newline='') as f:
    writer=csv.writer(f);writer.writerow(['path','iron_sets','copper_sets','basic_expansion_min','research_complete_min','industrial_complete_min','basic_generators','coal_miners'])
    for r in best: writer.writerow([r['path'],r['iron'],r['copper'],r['expandedSeconds']/60,r['researchSeconds']/60,r['seconds']/60,r['counts'].get('generator',0),r['counts'].get('miner:carbon',0)])
def minute(s): return f'{s/60:.1f}'
def line(r):
    return f'<tr><td>{r["path"]}</td><td>{r["iron"]} / {r["copper"]}</td><td>{minute(r["expandedSeconds"])}</td><td>{minute(r["researchSeconds"])}</td><td><strong>{minute(r["seconds"])}</strong></td><td>{r["counts"].get("generator",0)} / {r["counts"].get("miner:carbon",0)}</td></tr>'
ledger=[]
for r in best:
    entries=''.join(f'<tr><td>{minute(m["seconds"])}</td><td>{html.escape(m["action"])}</td></tr>' for m in r['marks'])
    purchases=''.join(f'<tr><td>{minute(e["time"])}</td><td>{html.escape(e["detail"])}</td><td>{minute(e["waitSeconds"])}</td></tr>' for e in r['events'] if e['kind']=='funding')
    ledger.append(f'<details><summary>Path {r["path"]}: {r["iron"]} iron + {r["copper"]} copper sets · {minute(r["seconds"])} min</summary><p>Expansion choices: {html.escape(r["allocation"] or "none")} (I = iron, C = copper).</p><table><tr><th>Elapsed min</th><th>Milestone</th></tr>{entries}</table><h3>Every funded purchase</h3><table><tr><th>Elapsed min</th><th>Machine</th><th>Funding wait min</th></tr>{purchases}</table></details>')
page='''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Starter expansion paths</title><style>body{background:#10191d;color:#e5eeeb;font:16px system-ui;max-width:1200px;margin:36px auto;padding:24px}p{line-height:1.6;color:#b6c9c5}a{color:#9ddacc}table{width:100%;border-collapse:collapse}td,th{text-align:left;padding:12px;border-bottom:1px solid #38534e}section,details{background:#1a292e;border-radius:12px;padding:20px;margin:22px 0}summary{cursor:pointer}strong{color:#9ddacc}.scroll{overflow:auto}</style><p><a href="action-search.html">← Fighter search</a></p><h1>Expand now, upgrade later.</h1><p>Each path begins with one iron and one copper set: miner + smelter + universal workshop. Added set 1 is iron; subsequent sets may be iron or copper. Each additional set is completed before the next. Research follows basic expansion; industrial replacements then help fund subsequent replacements.</p><section><h2>Time to replace every metal set</h2><p>All values below are elapsed minutes from an already-built, empty-stock starting factory. Larger paths finish with larger industrial factories. They are not comparisons to a common production target.</p><div class="scroll"><table><tr><th>Path</th><th>Iron / copper sets</th><th>Basic expansion done</th><th>Research done</th><th>All industrial done</th><th>Basic generators / coal miners</th></tr>''' + ''.join(map(line,best)) + '''</table></div></section><section><h2>Rules used</h2><p>Silicate and refractory construction/research costs are removed in the experimental catalog. No saved materials, coal miners or generators at the start. The Core provides 80 power; manual transfers are used until the first coal-power system is funded. Fifteen already-owned belt tiles are then enabled. Later belts use the existing pooled throughput model.</p><p>Basic coal miners and basic generators are purchased as needed for nameplate power, fuel throughput and 20 power of research headroom. Power equipment stays basic. Replacements reserve their net additional power, and the obsolete machine is reclaimed when its replacement completes. Current 100% refunds apply.</p><p>An industrial set means an industrial miner, industrial smelter, and structural machine for iron or electrical machine for copper. There is no industrial universal workshop. Recipe feeding is automatic and smelters/workshops share production tasks. Machine purchase order inside a basic set is miner → smelter → workshop.</p><p>512 iron/copper expansion sequences evaluated at 0.5-second ticks with 15 workers. Each row is the fastest successful allocation for its path under this fixed industrial replacement order: first iron set, first copper set, remaining iron sets, remaining copper sets. This does not prove the globally fastest build order.</p></section><h2>Build orders and waiting times</h2>''' + ''.join(ledger) + '</html>'
(out/'report.html').write_text(page,encoding='utf-8')
(root/'Tools/Balance/web/starter-paths.html').write_text(page,encoding='utf-8')
sources=[root/'Assets/Resources/catalog.json']+list((root/'Assets/Scripts').glob('Factory*.cs'))+[root/'Tools/Balance'/n for n in ['StarterPaths.cs','starter_paths.ps1','FactoryPlanner.cs','ActionPlanner.cs','SearchPolicy.cs','Runner.cs','load.ps1','report_starter_paths.py']]
hashes={}
for src in sources:
    rel=src.relative_to(root);dest=out/'sources'/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dest);hashes[str(rel)]=hashlib.sha256(src.read_bytes()).hexdigest()
(out/'manifest.json').write_text(json.dumps({'sources':hashes,'successful':sum(r['success'] for r in rows),'evaluated':len(rows),'landingStock':False,'replaceAll':True,'step':.5,'workers':15},indent=2))
print((out/'comparison.csv').read_text())
