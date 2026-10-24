# 현재 작업 인계

## 현재 목표

- 작업 기준: `Assets/01.Scenes/MinSu/KatanaReferenceTest.unity`, `Assets/03.Prefabs/Minsu/Katana_Reference.prefab`.
- 우클릭 유지 시 납도 차징, 해제 시 Combo_Attack_02_01 발도 → Combo_Attack_02_04와 코드 돌진.

## 완료 사항

- [x] 독립 `KatanaChargeSkill_s.cs`와 `PlayerDash_s.cs` 추가. 기존 에이리언 `PlayerChargeSkill_s.cs`는 변경하지 않았다.
- [x] 비교용 프리팹의 차징 컴포넌트 교체 및 테스트 씬 카메라 연결. 공통 이동/회전/검 판정/VFX/카메라에 새 스킬 연동을 추가하고 기존 스킬 분기는 보존했다.
- [x] 전용 KatanaChargeDraw/Dash 클립 생성. 원본 클립은 보존하고 복사본의 RootT XZ 이동을 제거했다. 동작2에 동작1 발도 완료 시점의 무기 파지 Transform을 유지했다.
- [x] Charging(동작1 1프레임 고정) → ChargeDraw(약 12프레임까지) → ChargingAttack(동작2, 속도 1.8) 연결. 최대 차징 1.5초, 중간 단계 0.6초, 피해 30/50/80, 돌진 6m. Inspector에서 조정 가능.
- [x] 지상 시작, 짧은 클릭 준비 보장, 해제 방향 고정, 벽/낭떠러지 정지, 실제 이동 경로 피해 및 대상당 1회 피해, 피격/사망/비활성화 취소 구현.
- [x] Unity 플레이 모드 입력 시뮬레이션: 짧은 클릭/2단계/3단계 모두 6m 이동, 두 대상에 각각 30/50/80 피해 1회. 0.25초 프레임 간격에서도 6m 및 80 피해 확인.
- [x] 벽 테스트 약 2.045m에서 정지하고 벽 뒤 두 대상 피해 없음. 낭떠러지 약 3.992m에서 정지. 차징 중 및 공격 시작 시 피격/사망 취소 확인. 정상 종료 후 적 충돌 무시 복원 확인.
- [x] 납도/발도/돌진 핵심 자세 프리뷰 확인. 기존 기본 4타 콤보 클립과 외부 원본 에셋은 보존했다.
- [x] 지정 `dotnet format JuicyFresh3D.sln --verify-no-changes --severity warn --include Assets/02.Scripts/` 종료 코드 0. 초기 샌드박스 복원/IPC 실패 후 승인된 환경에서 기본 명령 재실행. 작업 영역 로드 경고는 있었지만 스타일 오류는 없다. Unity 컴파일 후 런타임 테스트 실행 성공.

## 남은 문제 / 검증 한계

- [ ] 실제 마우스 조작의 체감, 차징 자세와 발도 연결, 돌진 거리/후딜 밸런스 최종 확인. 현재 차징은 원본의 발도 전 초반 자세를 고정하며 GIF의 깊게 숙인 자세를 새로 제작한 것은 아니다.
- [ ] 이동 도중 피격 시 충돌 복원 추가 테스트는 MCP 응답 실패로 확인하지 못했다. 코드상 CancelCharge → Dash.Stop으로 복원한다.
- Unity MCP는 스크립트/플레이 리로드 후 일시적으로 연결이 끊어지거나 장시간 응답 지연이 발생했다.
- 기존 Unity AI 서비스 NoSubscription 오류는 별도 문제다. git diff --check는 Unity가 생성한 YAML 빈 값 뒤 공백을 보고하며 C# 공백 오류는 없다.
- 변경 사항은 미커밋이다.

## 다음 작업

1. 테스트 씬에서 우클릭 차징/해제와 기본 좌클릭 콤보를 실제 조작하여 체감을 확인한다.
2. 변경은 비교용 프리팹과 KatanaReferenceAC를 기준으로 한다. 기존 에이리언, 원본 MinSuScene, 외부 원본 에셋을 임의 변경하지 않는다.
