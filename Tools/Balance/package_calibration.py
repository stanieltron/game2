"""Complete standalone source/input snapshots for saved calibration reports."""
import shutil
import sys
from pathlib import Path
HERE=Path(__file__).resolve().parent
for name in sys.argv[1:]:
    output=Path(name).resolve();sources=output/'sources'
    dest=sources/'Assets/Resources/catalog.json';dest.parent.mkdir(parents=True,exist_ok=True)
    shutil.copyfile(output/'catalog.snapshot.json',dest)
    for filename in ['targets.json','reference.policy.json','minimum.policy.json','widths.json']:
        dest=sources/'Tools/Balance/spec'/filename;dest.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(HERE/'spec'/filename,dest)
    shutil.copyfile(HERE/'widths_template.html',sources/'Tools/Balance/widths_template.html')
    print('Packaged',output)
