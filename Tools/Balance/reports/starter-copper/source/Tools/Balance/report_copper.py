"""Validate and publish the copper-cost experiment, preserving unmet targets."""
import copy,json,html,hashlib,shutil
import calibrate_copper as c
s=c.s;OUT=c.OUT

def main():
    selected=json.loads((OUT/'selected.json').read_text());cat=json.loads((OUT/'candidate.catalog.json').read_text());old=json.loads((s.HERE/'reports/starter-fifteen/candidate.catalog.json').read_text())
    stripped=copy.deepcopy(cat)
    for group in ['buildings','techs']:
        for x,y in zip(stripped[group],old[group]):x['cost']=y['cost']
    assert stripped==old,'A non-cost mechanic changed'
    cases=[[str(OUT/'candidate.catalog.json'),selected['best'][k]['allocation'],selected['id'],'freeLogistics','replaceAll'] for k in ['4-1','4-2','3-3','4-3']]
    for r in c.run(cases,'replay',1):
        z=r['result'];key=f'{z["iron"]}-{z["copper"]}';assert r==selected['best'][key]
        assert z['counts']['industrialSmelter']==z['iron']+z['copper']
    print('PASS: exact serial replays; only material costs changed')
    research=cat['techs'][0]['seconds'];data=copy.deepcopy(selected)
    for r in data['best'].values():
        z=r['result'];z['fundingWaitMinutes']=max(0,z['researchSeconds']-z['expandedSeconds']-research)/60
    trials=sum(len(json.loads(p.read_text())) for p in OUT.glob('*-results.json') if p.name!='replay-results.json')
    changes=[]
    for group in ['techs','buildings']:
        for a,b in zip(old[group],cat[group]):
            if a['cost']!=b['cost']:
                fmt=lambda x:', '.join(f'{v["amount"]} {v["item"]}' for v in x)
                changes.append(f'<tr><td>{html.escape(str(b.get("name",b.get("id","Research"))))}</td><td>{fmt(a["cost"])}</td><td>{fmt(b["cost"])}</td></tr>')
    funding1=data['best']['4-1']['result']['fundingWaitMinutes'];funding2=data['best']['4-2']['result']['fundingWaitMinutes']
    page='''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Copper bottleneck balance</title><style>:root{color-scheme:dark}body{background:#10191d;color:#e5eeeb;font:16px system-ui;max-width:1100px;margin:30px auto;padding:20px}a{color:#9ddacc}p{line-height:1.6;color:#c0d0cb}h1{font-size:36px}section{background:#1a292e;border:1px solid #38534e;padding:20px;border-radius:10px;margin:20px 0}table{border-collapse:collapse;width:100%;font-variant-numeric:tabular-nums}th,td{padding:10px;border-bottom:1px solid #38534e;text-align:left}.scroll{overflow:auto;max-height:530px}.metrics{display:flex;gap:16px;flex-wrap:wrap}.metrics div{flex:1;background:#24383c;padding:20px;border-radius:10px}.metrics b{display:block;font-size:30px;color:#9ddacc}select{font:inherit;background:#24383c;color:inherit;padding:8px}.best{background:#335b4f}button{font:inherit;background:none;border:0;color:inherit;cursor:pointer;text-decoration:underline}#matrix td{min-width:90px}</style>
<p><a href="starter-fifteen.html">← Previous 4–1 experiment</a> · MATERIAL-COST EXPERIMENT</p><h1>Make the second copper line matter.</h1><p>One set = one miner, one smelter, one universal workshop. Counts below refer to iron and copper sets, not individual machines.</p>
<div class="metrics"><div>4 iron + 2 copper<b>TIME42 min</b></div><div>4 iron + 1 copper<b>TIME41 min</b></div><div>Total-time penalty<b>RATIO×</b></div></div>
<p><b>Partial match:</b> 4–2 is the fastest tested mix, but the 15-minute target and 2× total-time penalty are not achieved. The closest competing mix is only MARGIN seconds slower. Do not treat this as a proven global optimum.</p>
<section><h2>Where the waiting happens</h2><table><thead><tr><th>Factory</th><th>After expansion: wait to afford research</th><th>Total completion</th></tr></thead><tbody><tr><td>4 iron + 1 copper</td><td>WAIT41 min</td><td>TIME41 min</td></tr><tr><td>4 iron + 2 copper</td><td>WAIT42 min</td><td>TIME42 min</td></tr></tbody></table><p>This funding wait is WAITRATIO× longer with one copper line. It excludes the 45-second research timer, earlier expansion and later upgrades. It measures waiting for the full research bill; it is not a separately instrumented copper-only stall counter.</p></section>
<section><h2>Factory mix comparison</h2><p>Completion minutes. Click a value to inspect its best tested build sequence.</p><div class="scroll"><table id="matrix"></table></div><p>TRIALS simulations in this experiment. Finalists tested every ordering of added iron/copper sets within the 1–6 iron × 1–4 copper grid, including copper-first expansion. Each machine within a set and every industrial replacement still follows a fixed construction policy.</p></section>
<section><h2>Material costs changed</h2><div class="scroll"><table><thead><tr><th>Research / building</th><th>Previous profile</th><th>New profile</th></tr></thead><tbody>CHANGES</tbody></table></div><p>All production rates, recipe inputs, energy, construction/research durations and refunds are identical to the preceding 15-minute profile. Power remains binary and refunds 25%. No count-based surcharge. The live game catalog is unchanged; candidate saved in <code>Tools/Balance/reports/starter-copper/candidate.catalog.json</code>.</p></section>
<section><h2>Build sequence</h2><label>Iron–copper sets <select id="route"></select></label><p>Every interval includes production, waiting and construction. Expansion starts with one iron set and one copper set; C means adding a copper set, I an iron set.</p><p id="sequence"></p><div class="scroll"><table><thead><tr><th>Milestone</th><th>Elapsed min</th><th>Interval min</th></tr></thead><tbody id="ledger"></tbody></table></div></section>
<section><h2>Scope of this result</h2><p>Endpoint: research industrial technology and upgrade every metal set, leaving basic coal power. Larger factories finish with more industrial machines. This is not a fixed Fighter endpoint. Empty starting stock, already-built starter sets and 15 belt tiles; silica/refractory costs omitted. Ideal manual transfers have no throughput or player-effort limit; belt power is charged. Basic machines continue producing alongside industrial machines.</p></section>
<script>const data=DATA;const keys=Object.keys(data.best).sort((a,b)=>{const x=a.split('-').map(Number),y=b.split('-').map(Number);return x[0]-y[0]||x[1]-y[1]});const select=document.querySelector('#route');keys.forEach(k=>{const o=document.createElement('option');o.value=k;o.textContent=k;select.appendChild(o)});select.value='4-2';function render(){let last=0;const r=data.best[select.value];document.querySelector('#sequence').textContent='Added sets: '+(r.allocation||'none');document.querySelector('#ledger').replaceChildren(...r.result.marks.map(m=>{const tr=document.createElement('tr');[m.action,(m.seconds/60).toFixed(2),((m.seconds-last)/60).toFixed(2)].forEach(v=>{const td=document.createElement('td');td.textContent=v;tr.appendChild(td)});last=m.seconds;return tr}))}select.onchange=render;render();const matrix=document.querySelector('#matrix');const head=document.createElement('tr');['Iron sets ↓ / Copper sets →',1,2,3,4].forEach(v=>{const th=document.createElement('th');th.textContent=v;head.appendChild(th)});matrix.appendChild(head);for(let i=1;i<=6;i++){const tr=document.createElement('tr');const th=document.createElement('th');th.textContent=i;tr.appendChild(th);for(let c=1;c<=4;c++){const td=document.createElement('td'),k=i+'-'+c;if(data.times[k]!==undefined){const b=document.createElement('button');b.textContent=data.times[k].toFixed(2);b.onclick=()=>{select.value=k;render();document.querySelector('#route').scrollIntoView({behavior:'smooth'})};td.appendChild(b);if(k==='4-2')td.className='best'}else td.textContent='Not tested';tr.appendChild(td)}matrix.appendChild(tr)}</script></html>'''
    vals={'TIME42':f'{selected["times"]["4-2"]:.2f}','TIME41':f'{selected["times"]["4-1"]:.2f}','WAIT41':f'{funding1:.2f}','WAIT42':f'{funding2:.2f}','WAITRATIO':f'{funding1/funding2:.2f}','RATIO':f'{selected["ratio"]:.2f}','MARGIN':f'{selected["margin"]*60:.0f}','TRIALS':str(trials),'CHANGES':''.join(changes),'DATA':json.dumps(data).replace('</','<\\/')}
    for k,v in vals.items():page=page.replace(k,v)
    (OUT/'report.html').write_text(page,encoding='utf-8');(s.HERE/'web/starter-copper.html').write_text(page,encoding='utf-8')
    files=list(s.HERE.glob('*.cs'))+list(s.HERE.glob('*.ps1'))+[s.HERE/x for x in ['calibrate_starter.py','calibrate_copper.py','refine_copper.py','finish_copper.py','robust_copper.py','report_copper.py']]+list((s.ROOT/'Assets/Scripts').glob('Factory*.cs'));hashes={}
    for f in files:
        rel=f.relative_to(s.ROOT);dest=OUT/'source'/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(f,dest);hashes[str(rel)]=hashlib.sha256(f.read_bytes()).hexdigest()
    s.write(OUT/'source-sha256.json',hashes)
    print('Published starter-copper.html; funding waits:',funding1,funding2,'ratio:',funding1/funding2)

if __name__=='__main__':main()
