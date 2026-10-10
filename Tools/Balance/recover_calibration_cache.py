"""Recover fully written replay files after stopping all workers of a known run.

Only use on this run's recorded case list, after its processes have exited.
"""
import hashlib
import json
import sys
from pathlib import Path

path=Path(sys.argv[1])
saved=bad=0
for catalog,policy,filename in json.loads(path.read_text(encoding='utf-8')):
    result=Path(filename)
    if not result.exists():continue
    try:
        raw=result.read_bytes();value=json.loads(raw)
        assert isinstance(value['success'],bool) and 'seconds' in value and 'counts' in value
        result.with_suffix('.sha256').write_text(hashlib.sha256(raw).hexdigest(),encoding='utf-8')
        saved+=1
    except (ValueError,AssertionError,KeyError):bad+=1
print(f'Preserved {saved} complete results; {bad} incomplete files will be recomputed.')
