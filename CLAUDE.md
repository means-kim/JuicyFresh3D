# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 개요

- Unity 6 (`6000.3.22f1`) + URP 17.3 기반 3D 카타나 액션 게임. 팀 프로젝트이며 멤버별로 씬(`Assets/01.Scenes/<이름>Scene.unity`)과 하위 폴더(`Minsu/` 등)를 나눠 쓴다.
- 주요 패키지: Input System, Cinemachine 3, AI Navigation(NavMesh), Visual Effect Graph, ProBuilder, Unity MCP(`com.coplaydev.unity-mcp`).
- 프로젝트 코드: `Assets/02.Scripts/`. 그 외 `Assets/` 최상위의 번호 없는 폴더(`BloodDecalsAndEffects`, `Decrepit Dungeon LITE`, `KatanaAnimationPack`, `UNI VFX` 등)는 외부 에셋이므로 수정하지 않는다.
- 현재 작업 기준 씬/프리팹: `Assets/01.Scenes/MinSu/KatanaReferenceTest.unity`, `KatanaDungeonArena.unity`, `Assets/03.Prefabs/Minsu/Katana_Reference.prefab`, 몬스터 `Monster_Maynard` 프리팹.

## 명령어

- 코드 스타일 검사(Husky pre-commit과 동일, **사용자가 지시한 경우에만** 실행):

  ```
  dotnet format JuicyFresh3D.sln --verify-no-changes --severity warn --include Assets/02.Scripts/
  ```

- 환경 문제로 실패해 `--no-restore`로 재검사했다면 이를 구분해 보고하고, 기본 검사가 통과했다고 주장하지 않는다.
- Husky.Net은 `dotnet-tools.json`의 로컬 도구(`dotnet husky`)로 설치된다.
- 빌드·테스트 명령은 없다. 컴파일과 동작 확인은 Unity 에디터에서 사용자가 직접 한다.

## 테스트·검증 정책

- 테스트는 사용자가 직접 진행한다. 기능 구현·수정 요청만으로 테스트 실행 승인을 받은 것으로 보지 않는다.
- 별도 지시 없이 플레이 모드 진입, 입력 시뮬레이션, 런타임 테스트, 검증용 컴파일·빌드, 코드 스타일 검사를 실행하거나 테스트용 코드를 추가하지 않는다.
- Unity 컴파일·동작 확인과 코드 스타일 검사는 별개로 판단한다. 실행하지 않은 검증은 "미실행"으로 명시하고, 실행 확인을 반복해서 요청하지 않는다.

## 코드 규칙

- 프로젝트 C# **클래스**는 PascalCase 뒤에 `_s`를 붙이고 파일명을 일치시킨다(예: `Health_s` / `Health_s.cs`). 중첩 private 클래스도 동일(`WalkState_s`). struct·interface·enum에는 강제하지 않는다(예: `MovementState`, `DamagePopupKind`).
- `_s`는 `.editorconfig`보다 우선하는 프로젝트 예외다. 검사에서 `_s` 관련 이름 경고가 나오면 설정 차이로 보고만 하고, 접미사를 제거하거나 `.editorconfig`를 바꾸지 않는다.
- 스타일은 `.editorconfig`를 따른다: 공백 4칸, 여는 중괄호는 새 줄, 단일 행 조기 반환만 중괄호 생략 허용, 빈 줄에 공백 없음.
- 이름: private 필드 `_camelCase`, private const / private static readonly는 PascalCase, 지역 변수 camelCase.
- 외부 에셋·패키지·Unity 자동 생성 코드는 규칙 적용을 위해 수정하지 않는다.
- 스크립트를 이동·이름 변경할 때 `.meta` GUID와 씬·프리팹 참조를 보존한다.
- 코드 리뷰 기준은 `Docs/CodeReviewGuide.md`를 참고한다.

## 아키텍처

컴포넌트 분리형 MonoBehaviour 구조이며, 컴포넌트 간 통신은 주로 C# `event`로 이루어진다. 새 기능은 기존 이벤트를 구독하는 별도 컴포넌트로 붙이는 방식이 일반적이다.

- **체력/피격 허브 – `Health_s`**: `HealthChanged`, `Damaged`, `DamagePresented(피해량, 명중 위치, DamagePopupKind)`, `Healed`, `Died` 이벤트를 제공한다. 몬스터의 `MonsterHitStun_s`, `MonsterHitFlash_s`, `MonsterBloodVFX_s`, `MonsterDamagePopup_s`, `HealthBar_s` 등이 이를 구독해 각자 반응한다(`[RequireComponent(typeof(Health_s))]`).
- **플레이어 공격 파이프라인**: 애니메이션 이벤트 → `PlayerComboAnimation_s`(`BeginComboSlash/BeginFinalComboSlash/EndComboSlash(int step)`, `SlashOpened/SlashClosed`) → `SwordHitDetector_s`(BoxCollider 겹침, 베기 ID로 중복 판정 방지) → `Health_s.TakeDamage(...)` 성공 시 `HitConfirmed` 이벤트 → 히트스톱(`HitStop_s`), 카메라 Impulse, `KatanaAudio_s`, `MonsterKnockback_s` 등. 판정·SFX·VFX 타이밍은 Animation Clip 이벤트에 걸려 있으므로 클립을 수정할 때 기존 이벤트·곡선을 보존한다.
- **차징 스킬**: `KatanaChargeSkill_s` / `PlayerChargeSkill_s`가 `ChargeStarted/ProgressChanged/StageChanged/Stopped` 이벤트를 내고, `PlayerChargeVFXController_s`(Original/Uni/Both 스타일)가 VFX를 일괄 제어한다. 컨트롤러가 있으면 `PlayerChargeVFX_s`는 직접 구독하지 않는다.
- **상태 머신**: `PlayerMovement_s`와 `MonsterState_s`는 내부 private `State_s` 중첩 클래스로 상태를 구현한다. 몬스터 Hit 상태 타이머는 `MonsterHitStun_s`, 넉백 이동은 `MonsterKnockback_s`(NavMesh 경계 검사)가 담당한다.
- **몬스터 AI/스폰**: `NavMeshAgent` 기반 `MonsterMovement_s`, `MonsterSpawner_s`(동시 생존 수 제한, NavMesh 샘플링·경로 검사). 혈흔은 `BloodSprayPool_s` 런타임 풀을 사용한다.
- **시간 처리 주의**: `HitStop_s`가 `Time.timeScale = 0`으로 히트스톱을 구현한다. 게임 로직(경직·넉백·스폰)은 `Time.deltaTime`으로 히트스톱 중 멈추고, UI·피드백(체력바, 데미지 팝업, 피격 플래시, 차징 카메라)은 `Time.unscaledDeltaTime`을 쓴다. 새 코드에서 어느 쪽인지 의도적으로 선택한다. 별도 일시정지 시스템과 timeScale 소유권 통합은 미해결 과제다.

## 작업 방식

- 요청 기능과 직접 관련된 파일부터 확인하고, 원인을 못 찾을 때만 전체 탐색한다. 같은 대화에서 변경 없는 설정 파일을 반복해서 읽지 않는다.
- 기존 구현을 우선 활용하고 불필요한 리팩터링을 하지 않는다. 변경 범위를 최소화하고, 범위를 벗어나는 변경은 미리 알린다. 관련 없는 사용자 변경을 되돌리지 않는다.
- C# 분석·수정은 파일 도구를 우선 쓰고, 에디터 상태 확인·조작이 필요할 때만 Unity MCP를 쓴다. 필요한 GameObject/Component/Animator만 선택적으로 조회하고, 전체 Hierarchy나 동일 상태 스크린샷을 반복 요청하지 않는다. MCP 호출이 반복 실패하면 원인을 분석하고 접근 방식을 바꾼다.
- 씬·프리팹·애니메이션 수정 시 참조와 GUID를 보존하고, 요청하지 않은 애니메이션·에셋은 수정하지 않는다. 직렬화 파일에 대규모 diff가 생기면 원인을 확인한다.

## 커밋

- 제목: `type: 설명` 또는 `type(scope): 설명`. 허용 type: `asset|build|chore|ci|design|docs|feat|fix|hotfix|perf|refactor|revert|style|test`.
- 제목 외에 비어 있지 않은 본문을 최소 한 줄 작성한다(`.husky/commit-msg`가 검사).
- 이모지는 훅이 type에 맞춰 자동으로 앞에 붙이므로 직접 쓰지 않는다.
- pre-commit 훅이 위 `dotnet format` 검사를 실행한다. 훅을 우회하지 않는다(`--no-verify` 금지).

## 작업 인계 (TASKS.md)

- 작업 시작 시 루트의 `TASKS.md`를 확인하고, 여러 대화에 걸친 작업은 여기에 진행 상태를 기록한다.
- 현재 목표 / 완료 사항(체크리스트) / 남은 문제 / 다음 작업만 간결하게 유지하고, 오래된 완료 기록과 과정 설명은 누적하지 않는다.
- 작업 완료 보고 시 실제 변경한 파일과 구현 내용, 수행한 검증과 미실행 검증, 미해결 문제를 구분해 알린다.
