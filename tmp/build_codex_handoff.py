from pathlib import Path
from collections import Counter
from datetime import datetime, timezone, timedelta
import hashlib
import json
import shutil
import subprocess
import zipfile

root = Path(__file__).resolve().parents[1]
out = root / 'Codex_Handoff_2026-10-23'
session = Path(r'C:/Users/akh10/.codex/sessions/2026/10/19/rollout-2026-10-19T18-23-37-01a15379-3d57-7312-b98f-32192de72697.jsonl')
out.mkdir(exist_ok=True)
messages = []
counts = Counter()

for line in session.open(encoding='utf-8'):
    try:
        record = json.loads(line)
    except json.JSONDecodeError:
        continue
    if record.get('type') != 'response_item':
        continue
    payload = record.get('payload', {})
    if payload.get('type') != 'message':
        continue
    role = payload.get('role')
    if role not in ('user', 'assistant'):
        continue
    # Export only user-visible assistant text. Never export reasoning, tools, or host instructions.
    if role == 'assistant' and payload.get('phase') not in ('commentary', 'final_answer'):
        continue
    if payload.get('channel') not in (None, 'commentary', 'final'):
        continue
    text_parts = []
    omitted = Counter()
    for content in payload.get('content', []):
        if content.get('type') in ('input_text', 'output_text'):
            text_parts.append(content.get('text', ''))
        else:
            omitted[content.get('type', 'unknown')] += 1
    if not text_parts and not omitted:
        continue
    item = {'timestamp': record.get('timestamp'), 'role': role,
            'phase': payload.get('phase'), 'text': '\n\n'.join(text_parts),
            'omitted_attachment_types': dict(omitted)}
    messages.append(item)
    counts[role] += 1

kst = timezone(timedelta(hours=9))
now = datetime.now(kst).isoformat(timespec='seconds')
lines = ['# JuicyFresh3D 대화 텍스트 추출', '',
         f'내보내기 시각: {now}', '',
         f'사용자 {counts["user"]}개, 어시스턴트 {counts["assistant"]}개 / 총 {len(messages)}개 메시지.', '',
         '사용자와 어시스턴트의 표시용 텍스트를 로컬 기록에서 추출했다. 진행 안내도 포함한다. '
         '도구 호출·결과 원문, 시스템/개발자 설정 및 내부 추론은 포함하지 않는다. '
         '이미지·영상 원본은 포함하지 않으며 텍스트에 있던 파일 경로는 유지한다. '
         '아래 대화는 과거 기록이므로 현재 작업 지시는 01_HANDOFF.md와 사용자의 최신 요청을 기준으로 한다.', '']
for i, msg in enumerate(messages, 1):
    stamp = msg['timestamp'] or ''
    if stamp:
        stamp = datetime.fromisoformat(stamp.replace('Z', '+00:00')).astimezone(kst).strftime('%Y-%m-%d %H:%M:%S KST')
    label = '사용자' if msg['role'] == 'user' else ('Codex 진행 안내' if msg['phase'] == 'commentary' else 'Codex 답변')
    lines += ['---', '', f'## {i:03d}. {label} · {stamp}', '', msg['text'], '']
    if msg['omitted_attachment_types']:
        lines += ['> 비텍스트 첨부 ' + ', '.join(f'{k} {v}개' for k,v in msg['omitted_attachment_types'].items()) + ': 원본 파일은 이 패키지에 포함하지 않음.', '']

(out / '02_CONVERSATION.md').write_text('\n'.join(lines), encoding='utf-8')
(out / '03_CONVERSATION.json').write_text(json.dumps(messages, ensure_ascii=False, indent=2), encoding='utf-8')

source_files = [
    'Assets/02.Scripts/KwangHee/Boss/BossAttackController.cs',
    'Assets/02.Scripts/KwangHee/Boss/BossCombatController.cs',
    'Assets/02.Scripts/KwangHee/Boss/BossMovement.cs',
    'Assets/02.Scripts/KwangHee/Player/PlayerMove.cs',
    'Assets/02.Scripts/KwangHee/Player/PlayerAttack.cs',
    'Assets/07.Animations/KwnagHee/BossMonsterAnimator.controller',
    'Assets/07.Animations/KwnagHee/PlayerMoveAnimator.controller',
    'Assets/01.Scenes/KwangHeeScene.unity',
    'ProjectSettings/ProjectVersion.txt',
    'Packages/manifest.json',
]
for rel in list(source_files):
    if (root / (rel + '.meta')).is_file():
        source_files.append(rel + '.meta')
for rel in source_files:
    dest = out / 'source_snapshot' / rel
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(root / rel, dest)

reports = [
    'boss-dodge-result.txt', 'player-combo45-result.txt', 'player-combo-blend-result.txt',
    'player-attack-result.txt', 'player-eight-way-result.txt', 'player-eight-way-validation.json',
    'actual-boss-root-fixed.txt', 'actual-boss-attack-finish-regression.txt',
    'actual-boss-throw-timing-fixed.txt', 'actual-boss-throw-timing-fixed-30fps.txt',
    'player-combo45-pose-analysis.txt',
]
(out / 'test_reports').mkdir(exist_ok=True)
for name in reports:
    source = root / 'tmp' / name
    if source.is_file():
        shutil.copy2(source, out / 'test_reports' / name)

def git(*args):
    completed = subprocess.run(['git', '--no-optional-locks', *args], cwd=root, capture_output=True, text=True, encoding='utf-8', errors='replace')
    return completed.stdout.strip() if completed.returncode == 0 else 'Unavailable'

readme = f'''# 다른 컴퓨터 Codex에 전달하는 방법

작성 시각: {now}

## 가장 빠른 사용법

1. 다른 컴퓨터에 최신 JuicyFresh3D Unity 프로젝트를 준비한다.
2. 이 ZIP을 풀고 **01_HANDOFF.md를 Codex 대화에 첨부**한다.
3. 아래 문장을 함께 보낸다.

```text
이 파일은 이전 컴퓨터에서 진행한 JuicyFresh3D 프로젝트의 인수인계 내용이야.
먼저 01_HANDOFF.md를 읽고, 현재 프로젝트의 실제 코드·씬·Git 상태와 비교해서 맥락을 이어받아줘.
최근 완료한 기능은 보스 근접 공격 40~60프레임의 회피 Window와 범위 내 실제 구르기 성공 판정이야.
성공 시 인자 없는 DodgeSucceeded 이벤트와 '회피성공!' 로그를 출력하도록 구현했어.
필요하면 02_CONVERSATION.md에서 세부 대화를 검색해.
source_snapshot은 비교 참고용이므로 더 최신 파일을 확인 없이 덮어쓰지 마.
우선 현재 상태를 이해한 내용을 짧게 알려줘. 다음 작업은 내가 요청할게.
```

## 포함 파일

- [01_HANDOFF.md](01_HANDOFF.md): 최신 결정, 구조, 입력·애니메이션·회피 판정, 과거 문제 해결, 협업 방식.
- [02_CONVERSATION.md](02_CONVERSATION.md): 사용자 {counts['user']}개 / Codex {counts['assistant']}개의 대화 텍스트.
- [03_CONVERSATION.json](03_CONVERSATION.json): 같은 대화의 검색·가공용 데이터.
- `source_snapshot/`: 현재 핵심 스크립트 5개, Animator·씬 등 비교용 파일과 관련 메타.
- `test_reports/`: 이전 작업에서 실행한 테스트 결과.
- `manifest.json`: 생성 시점 Git 상태와 파일 SHA-256.

대화는 도구가 읽을 수 있는 일반 Markdown/JSON 텍스트다. 새 Codex 대화에 첨부해 맥락으로 사용한다.

## 프로젝트 파일과 구분

이 패키지는 Unity 프로젝트 전체 백업이 아니다. 에셋 FBX·텍스처·이미지·영상 원본,
Library·Temp 캐시, Git 저장소 및 로그인 정보는 포함하지 않는다.
Unity 프로젝트는 Git 또는 별도 파일 전송으로 준비한다. 작업 중인 소스가 커밋보다 최신일 수 있으므로 manifest의 스테이징/미스테이징 상태를 확인한다.
기존 컴퓨터의 절대 경로는 새 컴퓨터의 프로젝트 경로로 바꾼다.

첨부 이미지·영상은 대화에 기록된 경로만 보존한다. 과거의 모든 화면을 재현하려면 원본 미디어를 별도로 전달해야 한다.
'''
(out / '00_README.md').write_text(readme, encoding='utf-8')

manifest = {
    'created_at': now, 'project': 'JuicyFresh3D', 'session_id': '01a15379-3d57-7312-b98f-32192de72697',
    'transcript_source': 'This conversation local user-visible message records only',
    'transcript_counts': dict(counts), 'transcript_first_timestamp': messages[0]['timestamp'],
    'transcript_last_timestamp': messages[-1]['timestamp'],
    'branch': git('branch', '--show-current'), 'head_commit': git('rev-parse', 'HEAD'),
    'git_status_project_assets': git('status', '--short', '--', 'Assets', 'Packages', 'ProjectSettings'),
    'files': []
}
for path in sorted(out.rglob('*')):
    if path.is_file() and path != out / 'manifest.json':
        data = path.read_bytes()
        manifest['files'].append({'path': path.relative_to(out).as_posix(), 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()})
(out / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')

zip_path = root / 'JuicyFresh3D_Codex_Handoff_2026-10-23.zip'
with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as archive:
    for path in sorted(out.rglob('*')):
        if path.is_file():
            archive.write(path, Path(out.name) / path.relative_to(out))
with zipfile.ZipFile(zip_path) as archive:
    assert archive.testzip() is None
    archive_names = set(archive.namelist())
    assert str(Path(out.name) / '01_HANDOFF.md').replace('\\','/') in archive_names
    assert len(archive_names) == len(manifest['files']) + 1
for entry in manifest['files']:
    assert hashlib.sha256((out / entry['path']).read_bytes()).hexdigest() == entry['sha256']
print(json.dumps({'folder': str(out), 'zip': str(zip_path), 'zip_bytes': zip_path.stat().st_size,
    'messages': len(messages), 'roles': dict(counts), 'files': len(manifest['files']) + 1,
    'first_message_date': messages[0]['timestamp'], 'last_message_date': messages[-1]['timestamp'],
    'head': manifest['head_commit'], 'archive_verified': True}, ensure_ascii=False, indent=2))
