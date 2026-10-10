import subprocess,concurrent.futures,json
from pathlib import Path
root=Path('Tools/Balance/reports/investment-v2')
def run(p):
 out=p.with_name(p.name.replace('.policy.json','.replay.json'))
 r=subprocess.run(['pwsh','-NoProfile','-File','Tools/Balance/search.ps1','-Policy',str(p),'-Output',str(out)],capture_output=True,text=True)
 if r.returncode:raise RuntimeError(r.stderr)
 x=json.loads(out.read_text());print(p.stem,x['success'],round(x['seconds']/60,2),flush=True)
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as e:list(e.map(run,root.glob('*.policy.json')))
