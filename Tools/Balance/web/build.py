"""Bundle current catalog and C# project phases into one offline HTML file."""
import json,re
from pathlib import Path
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
data=json.loads((ROOT/'Assets/Resources/catalog.json').read_text(encoding='utf-8'))
source=(ROOT/'Assets/Scripts/FactoryMegaproject.cs').read_text(encoding='utf-8')
for key,name in [('bayPhases','BayPhases'),('fighterPhases','FighterPhases')]:
    block=source.split('ProjectPhase[] '+name+'={',1)[1].split('};',1)[0]
    data[key]=[]
    for line in block.splitlines():
        m=re.search(r'new ProjectPhase\("([^"]+)",(\d+),(\d+)',line)
        if m:data[key].append(dict(name=m[1],seconds=int(m[2]),power=int(m[3]),cost=[dict(item=k,amount=int(v)) for k,v in re.findall(r'S\("([^"]+)",(\d+)\)',line)]))
assert len(data['bayPhases'])==len(data['fighterPhases'])==5
(HERE/'data.json').write_text(json.dumps(data,indent=2))
html=(HERE/'template.html').read_text(encoding='utf-8').replace('/*__DATA__*/','const DATA='+json.dumps(data,separators=(',',':'))+';').replace('/*__MODEL__*/',(HERE/'model.js').read_text(encoding='utf-8')).replace('/*__APP__*/',(HERE/'app.js').read_text(encoding='utf-8'))
(HERE/'factory-planner.html').write_text(html,encoding='utf-8')
print(HERE/'factory-planner.html')
