"""Cross-check unresolved BAC names against the pre-existing read-only decoder."""
from pathlib import Path
import gc
import json
import re
import sys

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT/'tools/effect_editor'))
from map_scene import MapScene

report = ROOT/'reports/map-editor-v2/corpus-render'
rows = json.loads((report/'corpus-render.json').read_text(encoding='utf-8'))['rows']
results = []
for row in rows:
    messages = [w for w in row['warnings'] if 'BAC 無對應' in w]
    if not messages: continue
    scene = MapScene(row['path'])
    missing = sum(obj[2] not in scene.object_names for layer in scene.layers for obj in layer['objects'])
    expected = int(re.search(r'：(\d+) 筆', messages[0]).group(1))
    results.append(dict(file=Path(row['path']).name, csharp_unresolved_records=expected,
                        python_unresolved_records=missing, counts_agree=missing == expected,
                        python_warnings=sorted(scene.warnings)))
    print(results[-1]['file'], expected, missing)
    del scene
    gc.collect()
(report/'bac-python-crosscheck.json').write_text(json.dumps(results, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
assert all(result['counts_agree'] for result in results)
