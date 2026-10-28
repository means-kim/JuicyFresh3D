# 현재 작업 인계

## 현재 목표

- 공용 Canvas 기반 몬스터 데미지 팝업 적용 및 사용자 가독성 확인.

- 체력바 감소 댐핑 및 주황색 피해 잔상 적용. 호버링 제외.

- 몬스터 청백색 오버레이 피격 플래시 적용 및 사용자 시각 확인.

- 기존 콤보 1~4타를 유지하면서 개별 베기 애니메이션 이벤트로 다단 명중 연결.

- 카타나 휘두르기·실제 명중·납도 SFX 연결 및 Inspector 조절.

- 기준: `Assets/01.Scenes/MinSu/KatanaReferenceTest.unity`, `Assets/03.Prefabs/Minsu/Katana_Reference.prefab`.
- 우클릭 차징 VFX를 Original/Uni/Both 선택형으로 구성. 현재 카타나는 Both로 설정. 몸 번개와 기존 VFX 자산은 보존한다.
- 테스트는 사용자가 담당한다. 명시적 요청 없이 플레이/시뮬레이션/검증용 컴파일·빌드/스타일 검사를 실행하지 않는다.

## 완료 사항

- [x] MonsterDamagePopup_s 및 DamagePopupCanvas_s/meta 추가, Monster_Maynard 연결. Health_s의 명중 위치/공격 종류 오버로드와 실제 피해량 이벤트를 검 명중에 연결했다. 사망 이벤트 전에 팝업을 요청해 처치 후에도 유지된다. 런타임 공용 Screen Space Overlay Canvas에서 최대 48개 재사용, 흰색/어두운 외곽선, 0.7초 상승·등장 확대·페이드, 대상별 좌우 4개 위치 순환. 중간 0.85/마지막 1.15/차징 1.3배 크기. 고정 월드 명중점을 화면에 재투영하므로 넉백에 따라가지 않으며 카메라 뒤/화면 밖은 숨긴다. 히트스톱 중에도 실제 시간으로 진행. 플레이·시각 확인·컴파일·빌드·스타일 검사는 미실행. 최초 프리팹 임시 문자열 편집은 자동 승인 검토에서 거절되어 적용되지 않았으며, 완성된 직렬화 블록으로 안전하게 연결했다.

- [x] HealthBar_s에 Health Smooth Time(0.06초), Damage Trail Delay(0.15초), Damage Trail Smooth Time(0.25초), Damage Trail Color(주황색) 추가. 기존 Fill 뒤에 잔상 Image를 런타임 생성하므로 기존 HealthBar_s 사용 체력바에 공통 적용된다. 실제 시간 기준 SmoothDamp 감소, 연속 피격 시 잔상 대기 갱신, 회복/초기화 시 즉시 동기화, 숫자 즉시 표시 및 사망 숨김/카메라 바라보기 유지. 호버링 없음. 플레이·시각 확인·컴파일·빌드·스타일 검사는 미실행.

- [x] MonsterHitFlash_s, URP 전용 MonsterHitFlash.shader/.mat 및 meta 추가. Monster_Maynard 프리팹의 몸 SkinnedMeshRenderer를 명시 연결. 최초 피격 때 동일 메시/본의 오버레이를 생성하고 이후 재사용하며 원본 재질과 락온 렌더링 레이어는 유지한다. 청백색/불투명도 0.7/0.08초, 실제 시간 기준 감쇠 및 재피격 타이머 갱신. 사망·비활성화 시 숨김, 제거 시 런타임 렌더러 정리. 깊이 검사 및 깊이 바이어스 적용, 그림자 패스 제외. 플레이·시각 확인·컴파일·빌드·스타일 검사는 미실행.

- [x] PlayerComboAnimation_s에 BeginComboSlash(int step), BeginFinalComboSlash(int step), EndComboSlash(int step) 추가. int는 콤보 타수 1~4. Katana_Reference의 Use Slash Events 활성화, 전용 공격 클립에 1/2/3/4회 판정 이벤트 연결. 기존 정규화 판정 구간을 각 횟수로 균등 분할하고 중간 구간 사이 1/60초 간격을 둔 초기 배치이며 실제 칼 움직임 일치는 미확인. Animation 창에서 조절 필요. 마지막 End 이벤트 후 기존 체인 시점에 다음 타로 연결한다. 종료/취소 시 닫고 비활성 옵션은 기존 시간 기반 판정을 유지한다.
- [x] SwordHitDetector_s가 콤보 타수와 별도 베기 ID를 사용해 중복 판정/명중음/히트스톱/카메라 충격을 베기당 한 번 처리. 시작 이벤트에서 현재 BoxCollider 겹침을 조회해 짧은 구간도 시작 위치에서 판정한다. 중간 베기 넉백 0.35배, 히트스톱/카메라 충격 0.5배, 마지막 베기 기존 강도. Slash Damage Multipliers(X/Y/Z/W=1~4타)는 기본 모두 1로 기존 피해를 매 베기에 적용하므로 전부 명중하면 총 피해가 증가한다. 차징 판정·기존 휘두르기/납도 이벤트·애니메이션 곡선/GUID는 유지. 플레이·컴파일·빌드·스타일 검사는 미실행.

- [x] MonsterKnockback_s 및 meta 추가, Monster_Maynard 프리팹 연결. 검 명중 성공 후 방향/타수/차징 단계를 전달한다. 일반타 0.15m/0.12초, 4타 0.6m/0.2초, 차징 1~3단계 0.45/0.75/1.1m 및 0.18/0.24/0.28초. 카타나 차징은 저장된 돌진 방향 사용. 감속 이동, NavMesh 경계 및 몸 폭의 장애물 검사, 약한 재피격의 강한 넉백 덮어쓰기 방지 적용. Hit 상태에서만 이동하고 경직은 필요 시 넉백 시간까지 연장한다. 히트스톱 중 정지, 사망/비활성화/Hit 종료 시 취소. 기존 사용자 경직 설정 0.8초 유지. 플레이·컴파일·빌드·스타일 검사는 미실행. 다음 작업: 사용자가 콤보 사거리, 벽/경계 정지, 차징 관통 후 방향, 연속 피격 감각 확인 후 Inspector 조절.

- [x] MonsterHitStun_s 및 meta 추가: Damaged 이벤트와 경직 타이머를 분리. MonsterState_s는 Hit 진입/복귀와 상태 전환을 담당하며 기존 내부 HitState_s를 제거했다. Monster_Maynard 프리팹에 연결하고 Hit Stun Duration 기본값 0.3초 적용. 재피격 시 남은 시간과 설정값 중 큰 값으로 갱신하고 Hit 애니메이션을 다시 재생한다. Time.deltaTime으로 히트스톱 중 경직 타이머도 정지하며 사망/비활성화 시 정리한다. 넉백은 추가하지 않았다. 플레이·컴파일·빌드·스타일 검사는 미실행. 사용자 확인: 피격 시 이동/공격 중단, 경직 만료 복귀, 재피격 및 사망 전환.

- [x] 납도 트레일 추가: PlayerLightningVFX_s의 BeginKatanaSheatheTrail/EndKatanaSheatheTrail 이벤트를 일반 공격 1~4 및 KatanaChargeDash 전용 클립의 기존 칼 돌리기/검집 삽입 SFX 시점에 추가했다. 기존 이벤트·곡선·GUID는 유지한다. 기존 3줄/평활화/20분할을 재사용하며 Sheathe Trail Width Multiplier(0.65), Sheathe Trail Lifetime(0.18초)로 조절한다. 공격 판정은 변경하지 않았다. 시작 시 기록 초기화, 정상 종료 후 잔상 소멸, 상태 전환/취소 시 생성 중단, 비활성화/행동 차단 시 정리를 적용했다. 플레이·시각 확인·컴파일·빌드·스타일 검사는 미실행. 사용자가 Animation 창에서 시작/종료 이벤트의 실제 시점을 확인·조절한다.

- [x] 검 궤적 3줄의 Hermite 보간 전에 위치 기록의 가중 평균 평활화 추가. Inspector의 Trail Path Smoothing(기본 0.75), Trail Smoothing Passes(기본 3)로 조절한다. 양 끝 위치와 원본 기록은 보존하고 매 프레임 다시 계산해 누적 수축을 방지한다. 사용자가 변경한 20분할과 기존 크기·굵기·색·수명·공격 전환 초기화는 유지한다. 주 선의 지그재그 제거 및 보조선 흔들림 축소 유지. 플레이·시각 확인·컴파일·빌드·스타일 검사는 미실행. 다음 확인은 사용자 플레이에서 꺾임 완화와 궤적이 안쪽으로 좁아지는 정도 확인.

- [x] 차징 오디오 연결: virtual_vibes-electric-spark-zap-423424를 차징 시작부터 해제/취소까지 반복 재생(기본 볼륨 0.4). 최대 단계 3 진입 시 Cinematic_Impact_Hard_01을 차징당 한 번 재생(0.75). 별도 AudioSource로 완료음과 루프를 분리하고, 최대 충전 후에도 누르고 있으면 루프를 유지한다. 비활성화 시 모두 정지. 프로필의 Charge Loop/Complete Clip 및 Volume에서 조절한다. 사용자 명중음 선택/납도 타이밍은 유지. 플레이·청취·컴파일·스타일 검사는 미실행.

- [x] KatanaAudio_s / KatanaSoundProfile_s 및 KatanaSoundProfile.asset 추가. 휘두르기·명중·납도 클립 배열과 개별 볼륨, 캐릭터 전체 Volume / Spatial Blend / 선택적 AudioMixerGroup Output 지원. 명중음은 Attack 1~4 Hit Clip/Volume 및 Charge Hit Clip/Volume으로 분리했다. 초기값은 모두 기존 첫 명중 클립과 볼륨을 복사했으며 각 슬롯에서 고정 재생한다. 빈 슬롯은 무음이다. HitConfirmed가 피해 적용 전 저장한 타수를 전달해 공격별로 선택한다. 공격 1~4는 Whoosh_02 / Combo_01 / Combo_02 / Combo_05로 고정하고 차징 돌진은 1타를 공유한다. 칼 돌리기는 Combo_02, 검집 삽입 클립은 사용자가 변경한 현재 참조를 유지한다. 원본 SFX는 미수정.
- [x] Katana_Reference 프리팹에 연결(테스트 씬 인스턴스가 상속). 일반 공격 1~4와 차징 돌진 전용 클립에 PlayKatanaSwing / PlayKatanaSheathe 이벤트만 추가. 기존 곡선 보존. 공격 이벤트는 기존 4%를 유지하고 납도는 PlayKatanaSheatheTurn / PlayKatanaSheathe로 분리했다. 납도 이벤트 타이밍은 사용자가 직접 수정한 현재 값을 유지한다. 이번 명중음 분리 작업에서는 애니메이션 파일을 수정하지 않았다. 삽입음 재생 시 회전음 종료. Animation 창에서 이벤트 위치 조절 가능.
- [x] SwordHitDetector_s에 실제 TakeDamage 성공 후 HitConfirmed 알림 추가. 카타나 오디오만 구독하고 공격당 명중음 한 번 재생. 동작/명중 AudioSource를 런타임에 분리 생성하고 공격 종료·취소 시 동작음 정지. 전역 매니저와 Mixer 자산은 아직 만들지 않았으며 Output 미지정 시 기본 출력 사용.

- [x] PlayerLightningVFX_s의 차징 몸 전기 항목에 Body Size / Body Spread 추가(기본값 각각 1). Size는 골반·가슴 중간점을 중심으로 몸 전기 배치를 확대하고, Spread는 몸 전기의 지그재그 퍼짐만 조절한다. 기존 굵기 계산·칼날 번개·궤적·명중 효과는 유지했다. 사용자 지침에 따라 이번 변경의 테스트/컴파일/스타일 검사는 미실행.

- [x] PlayerChargeVFXController_s 추가. 카타나 차징 이벤트를 한 곳에서 받아 Original/Uni/Both를 지원한다. Inspector Style 기본값과 카타나 프리팹을 Both로 설정했다. Both는 기존 Converge·StageFlash와 UNI 빌드업/Onslaught을 함께 시작하고, 기존 효과에 진행률·단계 변경도 전달한다. 해제·취소 시 양쪽을 종료한다. 기존 enum 값 0/1은 보존했다. 선택 변경 시 이전 효과를 정리하고 차징 중이면 새 효과를 시작한다.
- [x] PlayerChargeVFX_s는 컨트롤러가 있으면 자체 이벤트 구독을 하지 않고 컨트롤러의 호출을 받는다. 컨트롤러가 없는 기존 에이리언은 종전 동작을 유지한다.
- [x] PlayerUniChargeVFX_s 추가. 검 근처 Lightning Spear buildup과 지면 Onslaught buildup을 제어한다. Use Ground Onslaught 옵션, 검 위치 오프셋, 크기, 지면 레이어/높이/회전 오프셋, 청백색/청색 HDR 색, 종료 잔광 시간을 제공한다.
- [x] 차징 시작 시 buildup, 해제·취소 시 stop만 전달한다. 초기 이벤트 Manual 및 비활성 프리팹으로 자동 발사/폭발을 막는다. 비활성화/모드 변경 시 즉시 숨기며, 일반 종료 시 0.4초 후 숨긴다. 바닥 잔광은 마지막 지면 위치에 남긴다.
- [x] `Assets/03.Prefabs/Minsu/VFX/KatanaLightningSpearBuildup.prefab`, `KatanaOnslaughtBlue.prefab` 생성. UNI 원본과 기존 VFX 프리팹은 수정/삭제하지 않았다. 카타나 프리팹에 스크립트/참조를 연결했다. 몸 번개 및 검 궤적/명중 효과 코드는 변경하지 않았다.
- [x] manifest에 URP 17.3과 맞춘 `com.unity.visualeffectgraph: 17.3.0` 추가. 기존에는 VFX Graph 패키지가 manifest/lock에 없었다. 패키지 설치/그래프 마이그레이션 완료 여부는 이번 작업에서 검증하지 않았다.
- [x] 앞선 작업: 카타나 차징/발도/코드 돌진/원본 FBX 납도 곡선 복원, VFX 프리팹화, 공격당 0.05초 히트스톱, 차징 단계별 카메라 Noise 및 명중 Impulse 연결 완료.

## 남은 문제 / 검증 한계

- [ ] 이번 오디오 연결은 플레이·청취·컴파일·빌드·스타일 검사 미실행. 사용자가 클립 선택/음량과 특히 납도 이벤트 위치를 직접 조정해야 한다. 공격/납도 클립은 사용자가 지정한 파일로 연결했으며 청취 평가하지 않았다.

- [ ] UNI 연결 및 Both 추가는 사용자 지침에 따라 플레이·시뮬레이션·컴파일/빌드·스타일 검사·시각 검증 모두 미실행. Unity에서 패키지 의존성 해결 및 임포트가 필요하며, 실제 그래프 크기/색/지면 회전은 사용자 확인 후 조정한다.
- [ ] Inspector: PlayerChargeVFXController_s의 Style을 Original로 바꾸면 기존 효과로 복귀. PlayerUniChargeVFX_s의 Use Ground Onslaught을 끄면 바닥만 제외.
- [x] 돌진 조기 정지 대응: Katana_Reference 프리팹의 CharacterController.m_MinMoveDistance를 0.001에서 0으로 변경했다. 미세 잔여 이동이 무시되어 장애물로 오판하는 원인을 제거하도록 설정했다. 실제 돌진 거리 확인은 사용자 진행 예정이며 플레이·컴파일·빌드·스타일 검사는 미실행.
- [ ] 히트스톱과 향후 별도 timeScale 일시정지 시스템의 소유권 통합은 추후 필요.
- 변경 사항은 미커밋이다.

## 다음 작업

- 피격 플래시: 사용자가 명중/다단 명중, 히트스톱 중 소멸, 락온 동시 표시, 벽 뒤 가림을 확인하고 MonsterHitFlash_s의 Color/Opacity/Duration 조절.

- 사용자가 KatanaSoundProfile.asset의 클립/볼륨 및 전용 클립의 이벤트 타이밍을 확인한다. 이후 차징 루프와 발도/임팩트 추가 여부를 결정한다.

1. 사용자가 Both 모드의 중첩 차징 효과를 직접 확인한다. 기존 크기·밝기·방출량은 유지했으며 필요 시 조정한다. 별도 테스트 요청 없이 Codex가 테스트하지 않는다.
2. UNI 크기/위치/잔광을 조정하거나 Style을 Original로 되돌릴 수 있다. 원본 UNI와 보관 중인 기존 VFX는 유지한다.
