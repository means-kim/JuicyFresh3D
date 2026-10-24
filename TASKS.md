# 현재 작업 인계

## 현재 목표

- 테스트 씬: `Assets/01.Scenes/MinSu/KatanaReferenceTest.unity`.
- 프리팹: `Assets/03.Prefabs/Minsu/Katana_Reference.prefab`.
- 카타나 기본 콤보·차징 돌진의 실제 명중에 공격당 한 번, 기본 0.05초 히트스톱 적용.
- Cinemachine 차징 단계별 Noise와 실제 명중 시 공격당 한 번의 Impulse 연결.
- 테스트는 사용자가 직접 담당한다. 앞으로 Codex는 명시적인 테스트 지시가 있을 때만 해당 범위의 테스트·검증을 실행한다(AGENTS.md 반영).

## 완료 사항

- [x] CinemachineCamera와 LockOnCamera의 6D Shake/Impulse Listener(채널1, Gain1)를 MCP로 조회 확인. 두 카메라의 평상시 Amplitude Gain을 0으로 설정하고 테스트 씬 저장.
- [x] PlayerChargeCamera_s에 차징 1/2/3단계 강도 0.1/0.2/0.35 및 전환 시간 0.1초 추가(Inspector 조절). 기존 두 카메라 참조와 FOV 연출 유지. 해제·취소 시 강도0으로 감쇠, 컴포넌트 비활성화 시 즉시0.
- [x] SwordHitDetector_s에 부모 Impulse Source 자동 연결과 Hit Impulse Force(기본0.1) 추가. 실제 피해 성공 후 콤보 각 타격/차징 돌진 공격당 한 번 발생. Impulse 시간은 unscaled로 진행하여 히트스톱 중에도 재생.
- 이번 카메라 연결 변경은 사용자 지시에 따라 테스트·플레이 모드·검증용 컴파일·스타일 검사를 실행하지 않았다. 아래 검증 기록은 이전 히트스톱 작업의 결과다.
- [x] 기존 차징/발도/돌진, 납도 애니메이션 곡선 복원, VFX 프리팹 분리 완료. 차징 0.6/1.5초, 피해 30/50/80, 거리 6m.
- [x] 실제 사용자 입력으로 최대 차징/해제와 4단 콤보 확인. 종료 후 VFX·히트박스·충돌 무시 정리 확인. 적 손의 OnTriggerEnter→TryHit 경로로 돌진 피격 시 충돌 복원 확인. 증거: `Library/KatanaValidation/real-input-results.json`, `real-max-charge.png`.
- [x] HitStop_s 추가: 인스펙터 `_hitStopDuration` 기본 0.05초, WaitForSecondsRealtime 코루틴, 기존 배율 복원, 비활성화/제거 시 복구. 같은 공격 ID와 공격 종류를 기억해 정지 종료 후 다른 대상을 맞혀도 재발동하지 않는다. 이미 timeScale=0이면 정지를 새로 소유하지 않는다.
- [x] SwordHitDetector_s에서 TakeDamage 성공 후 연결. 콤보는 각 타격당 한 번, 카타나 돌진은 공격 전체에서 한 번. Katana_Reference 루트에 컴포넌트 연결. 기존 에이리언 프리팹과 원본 에셋 미변경.
- [x] Unity 컴파일 및 플레이 검증: 콤보 첫 명중 HP100→80/timeScale0, 약 0.056초 후 기존 0.5배속 복원. 같은 공격의 두 번째 대상 피해 적용/재정지 없음, 같은 대상 중복 피해 없음, 새 공격 재발동 확인.
- [x] 차징 돌진 SweepDash 경로: 첫 대상 피해30/timeScale0, 코루틴 종료 후 timeScale1, 두 번째 대상 피해30/timeScale1 유지. 비활성화/제거 시 0.3배속 복원, 기존 timeScale0 보존 확인. 테스트 대상 및 공격 구간을 제어한 플레이 검증이며 이번 히트스톱 변경 뒤 실제 마우스 체감 검증은 미실행.
- [x] 검증 종료 후 플레이 모드 종료, 테스트용 Application.runInBackground 설정 복원. 코드 2개(신규 HitStop_s 및 기존 SwordHitDetector_s), 신규 meta, 카타나 프리팹, TASKS.md 변경.
- [x] 지정 dotnet format `.sln` 명령은 파일 부재로 실패(현재 `.slnx` 존재). `.slnx` 대체 검사는 샌드박스 복원 실패, --no-restore는 MSBuild 파이프 권한 실패. 승인된 환경에서 `.slnx` 기본 검사 재실행: 종료 코드0, 작업 영역 로드 경고 있음. 지정 `.sln` 검사가 통과한 것은 아니다. Unity 콘솔에는 기존 NoSubscription 오류만 확인.

## 남은 문제 / 검증 한계

- [ ] 이전 실제 반복 돌진에서 약 5.279m 조기 정지 발견. CharacterController.minMoveDistance=0.001이 0.0005m 잔여 이동을 무시하면 PlayerDash_s가 장애물로 오판하는 경로를 비교 재현했다. 이번 히트스톱 작업에서는 수정하지 않았다.
- 히트스톱 도중 다른 시스템이 timeScale을 계속 0으로 설정하는 별도 일시정지 소유권은 구분할 수 없다. 현재 프로젝트에는 별도 timeScale 제어 코드가 없으며, 향후 일시정지 시스템 추가 시 통합 필요.
- `.vsconfig`, `JuicyFresh3D.slnx` 미추적 파일은 그대로 유지. 커밋하지 않음.

## 다음 작업

1. 사용자가 기본/락온 카메라의 단계별 차징 흔들림, 해제·취소 복귀, 실제 명중의 히트스톱+Impulse를 직접 확인한다. 강도는 PlayerChargeCamera_s의 단계별 Amplitude와 SwordHitDetector_s의 Hit Impulse Force에서 조절한다.
2. 돌진 미세 이동 오판은 수정 요청 시 처리하며, 테스트는 별도의 명시적인 지시가 있을 때만 실행한다.
