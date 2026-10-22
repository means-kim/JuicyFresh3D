from pathlib import Path
import re
import shutil
import json

root = Path(__file__).resolve().parents[1]
dest = root / 'tmp/ChargeMotionValidation'
guid_re = re.compile(r'guid: ([0-9a-f]{32})')
index = {}
existing = {}
for meta in (root / 'Assets').rglob('*.meta'):
    m = guid_re.search(meta.read_text(encoding='utf-8-sig', errors='replace'))
    if m:
        index[m[1]] = Path(str(meta)[:-5])
for meta in (dest / 'Assets').rglob('*.meta'):
    m = guid_re.search(meta.read_text(encoding='utf-8-sig', errors='replace'))
    if m:
        existing[m[1]] = Path(str(meta)[:-5])

queue = [root / 'Assets/01.Scenes/KwangHeeScene.unity']
seen = set()
copied = []
while queue:
    src = queue.pop()
    if src in seen or not src.is_file():
        continue
    seen.add(src)
    meta = Path(str(src) + '.meta')
    meta_text = meta.read_text(encoding='utf-8-sig', errors='replace') if meta.exists() else ''
    m = guid_re.search(meta_text)
    # Preserve original production script GUIDs for scene references.
    if src.suffix == '.cs' and src.name in ('BossCombatController.cs', 'BossMovement.cs', 'BossAttackController.cs'):
        target = dest / 'Assets' / src.name
    else:
        target = existing.get(m[1], dest / src.relative_to(root)) if m else dest / src.relative_to(root)
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, target)
    if meta.exists():
        shutil.copy2(meta, str(target) + '.meta')
    copied.append(str(src.relative_to(root)))
    refs = set(guid_re.findall(meta_text))
    if src.suffix.lower() in ('.prefab', '.unity', '.controller', '.mat', '.asset', '.overridecontroller'):
        refs.update(guid_re.findall(src.read_text(encoding='utf-8-sig', errors='replace')))
    for guid in refs:
        if guid in index:
            queue.append(index[guid])

for name in ('DynamicsManager.asset', 'TimeManager.asset', 'TagManager.asset'):
    source = root / 'ProjectSettings' / name
    if source.exists():
        shutil.copy2(source, dest / 'ProjectSettings' / name)
report = {'copied_count': len(copied), 'scripts': [s for s in copied if s.endswith('.cs')], 'assets': copied}
(root / 'tmp/boss-scene-copy.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({k: report[k] for k in ('copied_count', 'scripts')}, ensure_ascii=False, indent=2))
