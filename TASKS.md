# 현재 작업 인계

## 현재 목표

- 기준: `Assets/01.Scenes/MinSu/KatanaReferenceTest.unity`, `Assets/03.Prefabs/Minsu/Katana_Reference.prefab`.
- 우클릭 차징 VFX를 기존/UNI 선택형으로 구성. 몸 번개와 기존 VFX 자산은 보존한다.
- 테스트는 사용자가 담당한다. 명시적 요청 없이 플레이/시뮬레이션/검증용 컴파일·빌드/스타일 검사를 실행하지 않는다.

## 완료 사항

- [x] PlayerChargeVFXController_s 추가. 카타나 차징 이벤트를 한 곳에서 받아 Original/Uni 중 선택한 효과만 실행한다. Inspector Style 기본값 Uni. 선택 변경 시 이전 효과를 정리하고 차징 중이면 새 효과를 시작한다.
- [x] PlayerChargeVFX_s는 컨트롤러가 있으면 자체 이벤트 구독을 하지 않고 컨트롤러의 호출을 받는다. 컨트롤러가 없는 기존 에이리언은 종전 동작을 유지한다.
- [x] PlayerUniChargeVFX_s 추가. 검 근처 Lightning Spear buildup과 지면 Onslaught buildup을 제어한다. Use Ground Onslaught 옵션, 검 위치 오프셋, 크기, 지면 레이어/높이/회전 오프셋, 청백색/청색 HDR 색, 종료 잔광 시간을 제공한다.
- [x] 차징 시작 시 buildup, 해제·취소 시 stop만 전달한다. 초기 이벤트 Manual 및 비활성 프리팹으로 자동 발사/폭발을 막는다. 비활성화/모드 변경 시 즉시 숨기며, 일반 종료 시 0.4초 후 숨긴다. 바닥 잔광은 마지막 지면 위치에 남긴다.
- [x] `Assets/03.Prefabs/Minsu/VFX/KatanaLightningSpearBuildup.prefab`, `KatanaOnslaughtBlue.prefab` 생성. UNI 원본과 기존 VFX 프리팹은 수정/삭제하지 않았다. 카타나 프리팹에 스크립트/참조를 연결했다. 몸 번개 및 검 궤적/명중 효과 코드는 변경하지 않았다.
- [x] manifest에 URP 17.3과 맞춘 `com.unity.visualeffectgraph: 17.3.0` 추가. 기존에는 VFX Graph 패키지가 manifest/lock에 없었다. 패키지 설치/그래프 마이그레이션 완료 여부는 이번 작업에서 검증하지 않았다.
- [x] 앞선 작업: 카타나 차징/발도/코드 돌진/원본 FBX 납도 곡선 복원, VFX 프리팹화, 공격당 0.05초 히트스톱, 차징 단계별 카메라 Noise 및 명중 Impulse 연결 완료.

## 남은 문제 / 검증 한계

- [ ] 이번 UNI 변경은 사용자 지침에 따라 플레이·시뮬레이션·컴파일/빌드·스타일 검사·시각 검증 모두 미실행. Unity에서 패키지 의존성 해결 및 임포트가 필요하며, 실제 그래프 크기/색/지면 회전은 사용자 확인 후 조정한다.
- [ ] Inspector: PlayerChargeVFXController_s의 Style을 Original로 바꾸면 기존 효과로 복귀. PlayerUniChargeVFX_s의 Use Ground Onslaught을 끄면 바닥만 제외.
- [ ] 기존 돌진 조기 정지 문제: CharacterController.minMoveDistance=0.001이 미세 잔여 이동을 무시하면 장애물로 오판하여 약 5.279m에서 멈출 수 있다. 이번 범위에서 미수정.
- [ ] 히트스톱과 향후 별도 timeScale 일시정지 시스템의 소유권 통합은 추후 필요.
- 변경 사항은 미커밋이다.

## 다음 작업

1. 사용자가 Unity 패키지 임포트 후 차징 효과를 직접 확인한다. 별도 테스트 요청 없이 Codex가 테스트하지 않는다.
2. UNI 크기/위치/잔광을 조정하거나 Style을 Original로 되돌릴 수 있다. 원본 UNI와 보관 중인 기존 VFX는 유지한다.
