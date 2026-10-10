"""Replay representative selected routes serially against the parallel search."""
import json,subprocess
import refine_fixed_prices as s

def main():
    selected=json.loads((s.OUT/'selected.json').read_text())
    paths=['1','5','12'];cases=[[str(s.OUT/'candidate.catalog.json'),selected['best'][n]['allocation'],selected['id'],'freeLogistics'] for n in paths]
    s.s.write(s.OUT/'replay-cases.json',cases)
    subprocess.run(['pwsh','-NoProfile','-File',str(s.s.HERE/'starter_calibration.ps1'),'-Cases',str(s.OUT/'replay-cases.json'),'-Output',str(s.OUT/'replay-results.json'),'-Workers','1'],check=True)
    for row in json.loads((s.OUT/'replay-results.json').read_text()):
        assert row==selected['best'][str(row['path'])],f'Replay differs: path {row["path"]}'
        r=row['result'];assert r['success'] and r['counts']['industrialSmelter']==row['path']+1
        assert not any(r['counts'].get(k,0) for k in ['smelter','workshop','miner:iron','miner:copperOre'])
    print('PASS: paths 1, 5, 12 match parallel results exactly; complete industrial endpoints')

if __name__=='__main__':main()
