import json
from pathlib import Path
from collections import Counter

path = Path(r'C:/Users/akh10/.codex/sessions/2026/10/19/rollout-2026-10-19T18-23-37-01a15379-3d57-7312-b98f-32192de72697.jsonl')
records = Counter()
messages = Counter()
content_types = Counter()
phases = Counter()
message_keys = Counter()
first = None
last = None
for line in path.open(encoding='utf-8'):
    try:
        item = json.loads(line)
    except json.JSONDecodeError:
        continue
    records[item.get('type')] += 1
    payload = item.get('payload', {})
    if item.get('type') == 'response_item' and payload.get('type') == 'message':
        role = payload.get('role')
        channel = payload.get('channel')
        messages[(role, channel)] += 1
        if role == 'assistant':
            phases[payload.get('phase')] += 1
            message_keys[str(sorted(payload.keys()))] += 1
        if role in ('user', 'assistant') and channel in (None, 'final', 'commentary'):
            for content in payload.get('content', []):
                content_types[content.get('type')] += 1
            timestamp = item.get('timestamp')
            first = first or timestamp
            last = timestamp
print(json.dumps({'records': dict(records), 'message_roles': {str(k): v for k,v in messages.items()},
    'user_visible_content_types': dict(content_types), 'assistant_phases': dict(phases),
    'assistant_key_shapes': dict(message_keys), 'first': first, 'last': last}, ensure_ascii=False, indent=2))
