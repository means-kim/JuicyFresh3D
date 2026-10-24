# 현재 작업 인계

## 현재 목표

- 앞으로의 작업 기준은 `Assets/01.Scenes/MinSu/KatanaReferenceTest.unity`와 `Assets/03.Prefabs/Minsu/Katana_Reference.prefab`이다. 사용자 요청에 따라 이 씬과 프리팹에서 기능을 이어서 개발한다. 기존 에이리언은 보관용이다.

## 완료 사항

- [x] Katana_Reference의 원본 검·검집 메시에 기존 Edge_Sword / Edge_Scabbard 머터리얼을 적용했다. EdgeTextures의 BaseColor·MetallicSmoothness·Emission 연결과 테스트 씬 반영을 확인하고 프리뷰로 외형 검증했다. 뼈대·무기 Transform·애니메이션·외부 원본 에셋은 변경하지 않았다.

- [x] 테스트 씬 참조 점검: Ground의 비어 있던 NavMeshSurface 데이터에 `Assets/01.Scenes/MinSu/Navigation/MinSuScene_NavMesh.asset`을 연결하고 MonsterMovement_s의 추적 대상을 Katana_Reference로 명시했다. 카메라·HUD·프리팹 내부 공격/VFX 참조 및 씬·프리팹·컨트롤러의 에셋 GUID 연결을 점검했다. 비교용 프리팹 내부 참조는 정상이라 변경하지 않았다. 플레이 모드에서 몬스터의 NavMesh 등록, 추적 대상, 카메라와 HUD의 Katana_Reference 연결을 확인했다. 참조 관련 콘솔 오류는 없고 기존 NoSubscription 오류만 남았다.

- [x] 원인 확인: 이전 보정은 납도 자세로 시작하는 1타와 파지 자세로 시작하는 2~4타를 각각 같은 소켓 기본 자세에 맞췄다. 1타→2타 연결에서 소켓 로컬 위치 0.685073, 회전 140.107도 차이가 발생하며 1타에서 검이 손을 벗어났다.
- [x] `Assets/03.Prefabs/Minsu/Katana_Reference.prefab` 생성. 9CG_Katana의 원본 뼈대·메시·검·검집과 Avatar를 사용한다. 기존 이동·4타 콤보·피해·차징 컴포넌트를 복사하고 검 충돌/VFX 참조를 새 무기에 연결했다.
- [x] `Assets/07.Animations/Minsu/KatanaReference/`에 전용 `KatanaReferenceAC.controller`와 `KatanaReferenceAttack1.anim`~`KatanaReferenceAttack4.anim` 생성. 원본 VERSION1 Combo01을 복사하고 반복 재생만 해제했다. 모든 애니메이션 곡선이 원본과 동일하며 Transform 경로 누락이 없는 것을 검증했다. 재생 속도 1.3, 기존 연결 시점 0.32/0.34/0.36과 전환 시간 0.025초 유지.
- [x] `Assets/01.Scenes/MinSu/KatanaReferenceTest.unity` 생성. 기존 씬의 복사본에서 비교용 플레이어를 활성화하고 에이리언을 비활성화했다. 카메라 등의 외부 참조 4개를 비교용 플레이어로 연결했다.
- [x] 임시 프리뷰에서 1타 10%의 손잡이 파지 및 4타 98%의 납도 외형 확인. 이전 1타의 검 이탈 현상이 비교용 모델에서 나타나지 않았다.
- [x] 비교용 씬 플레이 모드에서 입력 시뮬레이션으로 1~4회 클릭의 단계 연결, 종료 후 Locomotion 복귀, 히트박스 비활성화, 피격·사망 취소 확인.
- [x] 피해 20/25/35/45와 중복 피해 방지 확인. 이 검사는 TryHit 직접 호출을 포함하며 실제 물리 접촉 검증과 구분한다.
- [x] 기존 에이리언·보정 클립·MinSuScene·외부 원본 에셋을 보존했다. 테스트 후 플레이 모드를 종료하고 이전 플레이 시작 씬 설정을 복원했다. 해당 검증 당시 MinSuScene의 저장되지 않은 변경을 보존했다. 현재 작업 씬은 KatanaReferenceTest이다.

## 남은 문제 / 검증 한계

- [ ] 사용자가 KatanaReferenceTest 씬에서 실제 좌클릭 조작과 적 접촉을 확인한다. 이동·점프·차징 컴포넌트는 유지했지만 새 모델에서 해당 동작 전체를 시각 검증하지 않았다.
- [x] 사용자가 테스트 씬과 비교용 프리팹을 향후 작업 기준으로 지정했다. 에이리언 외형 리깅은 현재 요청 범위가 아니다.
- 기존 에이리언의 `KatanaAttack1.anim`~`KatanaAttack4.anim` 보정 문제는 비교를 위해 그대로 남아 있다. 비교용 컨트롤러는 이 보정본을 사용하지 않는다.
- Unity 콘솔의 기존 AI 서비스 NoSubscription 오류는 남아 있다. 이번 작업에서 C# 파일은 변경하지 않아 dotnet format은 재실행하지 않았다. 이전 C# 변경 당시 지정 검사는 종료 코드 0이었다.
- git diff --check는 이전 에이리언 프리팹/컨트롤러의 Unity YAML 빈 값 뒤 공백을 보고한다. 관련 없는 기존 변경은 수정하지 않았다.
- 변경 사항은 미커밋이며 AGENTS.md에 사용자 변경이 있다.

## 다음 작업

1. AGENTS.md와 git status를 확인하고 사용자 요청을 따른다.
2. 테스트 씬: Assets/01.Scenes/MinSu/KatanaReferenceTest.unity. 원본 MinSuScene의 저장되지 않은 변경을 임의로 버리거나 덮어쓰지 않는다.
3. 기능 수정은 Katana_Reference 프리팹과 KatanaReferenceAC를 우선한다. 별도 요청 없이 기존 에이리언 보정본이나 원본 외부 에셋을 변경하지 않는다.
