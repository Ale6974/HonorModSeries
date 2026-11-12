# M2.1~M2.3 부품 감사 — Preview0.3.102

2026-11-13. 이 문서는 부품의 지원 범위와 실제 조립 근거를 기록한다. M1 10/10, M2 0/18, 전체10/100의 기존 통합 점수는 유지한다. M2.3 전체 완료 판정은 하지 않는다.

## B01 — 원 주민 명부·신원

- 소유: `ResidentExecution.CaptureCore/CheckLocations/ValidateReferences`, `ResidentTransferCensus`, `ResidentEnrollment`.
- 입력/출력: 같은 원 Settlement/Pawn 참조와 전체 정렬 ID, 원 소속·임상 proof → 변경 전 원 census 및 각 주민의 독립 호출 기록. 원 도시에 없는 주민은 별도 현재 등록 승인 없이 추가하지 않는다.
- 저장: 원 Pawn 참조는 Reference, 임상/census/authority-call은 값으로 보관한다. 원 도시 및 전역 명부의 누락·중복을 확인하고 다른 이전/지도자 journal의 예약과 충돌을 거부한다. 후계 Pawn의 읽기 전용 참조도 pending 원본 GC 보호에 포함했다.
- 검사: `ResidentCensusTests`의 다른 도시 중복/누락/반복 ID, `ResidentEnrollmentTests`, `GovernedResidentTests`, `ResidentMappedTests`의 기존 원 범위. 새로운 개인 왕실 권리는 이 원 명부의 정확한 ID 집합과 별도로 짝을 맞춘다.
- 판정: 기존 지원 상태의 부품 계약 재검수. 원 객체 재생성/복잡 군사/새 임의 holder 전체 완료 아님.

## B02/B03 — 임상·의료와 독립 정상 반환

- 소유: `ResidentExecution.Apply/Verify/ValidateCensus`, `ResidentCareJournal`, `ResidentAreas`, 원 surgery/prisoner 기록, `ResidentTransferCensus.Begin/Returned/RequireRecoverable`.
- 입력/출력: 원 임상·의료·영역·수술 proof와 현재 행동 승인 → 각 주민 native 호출의 Started, 정상 반환, 별도 의료 출력 관측 및 복구 소비. 소속만으로 호출 성공을 추정하지 않는다.
- 중단: Started만 있는 호출은 다시 실행하지 않는다. 양성 정상 반환과 원 출력이 있어야 기존 의료/영역 복구를 이어간다. 원 자료를 후시점 설정으로 덮거나 임상 상태를 재설정하지 않는다.
- 저장/검사: Scribe 원 참조와 값 journal을 유지한다. `ResidentCensusTests`, `ResidentCareJournalTests`, `ResidentAreaTests`, `ResidentMobilityTests` 및 새 `PrivateRoyalRightsTests`의 주민 사이 저장/재로드가 검사 근거다.
- 판정: 기존 지원 조합 및 새 unloaded royal12 조립 검토. 일반 진행 작업/예약/복합 포로·노예·의료 조합 완료 아님.

## B05 — 원 개인 왕실 권리 보존

소유 파일: `RoyalRights.cs`, `RoyalRightsAdapter.cs`. 조립처: `RoyalTerritoryTransfers`, `CustodyContinuations`, `CustodyAgreements`, `ResidentExecution`, `TerritoryAdministration`.

### 실제 경로

새 UI 검토 → 같은 원 주민의 임상12(작위 보유 unloaded world 주민) 또는 기존 지원 임상 → 별도 RoyalRightsV2 원문/hash/수여 세력 명부 → 현재 작위 대표와 모든 수여·연결 제삼자 행동별 승인 → 원 census/care/독립 resident 호출 → 기존 native Pawn.SetFaction/자산/도시 호출 → 양성 반환 검증 → Scribe/명시적 Recover.

후속 목적지는 기존 Applied 원 기록을 바꾸지 않고 새 원 임상·권리·승인으로 `PreparePrivateRoyalContinuation`을 준비한다. source/target이 일반 세력이더라도 원 개인 권리 수여 세력이 귀족인 경우 그 현재 작위 승인을 요구한다.

### 보존하는 필드

- 원 titles의 faction/def/receivedTick/wasInherited/conceited와 tracker owner
- favor, highestTitles, 명시적 heirs 키/값(명시적 null 포함)
- permits의 원 faction/title/permit/lastUsedTick
- 지원 기본 Ability의 동일 ID/원 Pawn/def, cooldown/charges/lastCastTick 및 저장되는 idle verb 필드
- decree/의복·방 요구 설정, 해당 주민과 연결된 활성 개인 서약의 원 자료

원 Faction/후계 Pawn/Ability는 Reference로만 Scribe한다. 원 royalty tracker만 native deep-save를 소유한다. 복원 시 같은 원문과 원 참조 집합을 재검증하며 새 tracker/후계/능력을 만들거나 title/favor/허가를 쓰지 않는다.

### 경계

Native5005의 실제 RoyalTitle/FactionPermit/Pawn_RoyaltyTracker/Ability/VerbTracker/Verb/AbilityComp 저장·초기화 본문을 대조했다. 원 DLL SHA256 `82387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125`, MVID `3523c04c-c298-43ae-9571-20caaef22acc`만 새 경로를 허용한다. 다른 버전의 시그니처 일치만으로 권한을 넓히지 않는다.

Native 로드가 후계를 생성하거나 능력을 추가/제거하거나 하위 허가를 정리할 상태는 거부한다. 원 후계 dictionary 키, title이 부여한 정확한 능력 집합, 이미 정규화된 허가를 요구한다. 기본 Ability만 허용하고 상태 없는 native comp의 기본 Initialize/PostExposeData 및 idle native verb만 확인했다. 알려지지 않은 private comp 상태·진행 효과·목표/연속 시전은 보류한다.

옛 RoyalResidentsV1의 빈 권리 증거와 null JSON/Scope는 바꾸지 않았다. RoyalRightsV2는 새 action scope이며 옛 승인으로 승격하지 않는다.

### 남은 M2.3 작업

1. 지도 위 작위 주민의 clinical/area/map registry와 개인 권리 결합.
2. 미명시 후계·파생/상태 보유 능력에 대한 원본 보존 어댑터 및 native 로드 정규화 경계.
3. M3/M4의 포로·downed·지도자 조합과 귀환/후속 목적지 전체 통합 감사.

이 세 묶음이 남았으므로 M2.3은 **실제 실행·저장·복구 부분 조립**이다. 잔여 노력은 API 검토에 따라 약40~60%로만 추정하며 코드량이나 테스트 수로 완료율을 확정하지 않는다.

## 여섯 단계

이전 잔여 노력 추정31.5/80/95/90/100/70%는 역사적 수치다. 이번 새 부품 완료를 전체 기능 점수로 환산하지 않았으며 이를 최신 측정값으로 재사용하지 않는다. 현재 확정 점수는 M1 10/10, M2 미완료0/18, 1단계 전체10/100이다. 다른 단계의 구현은 이번 변경 범위 밖이다.

검사 결과: 양 타깃 전체 빌드(Honor20093/Contracts5704), 경고·오류0, EN/KO592; package allowlist 통과. 백업 contracts-preview-aebf97204770467e94b769b9b1fc33d4 후32개 파일 일치, ModsConfig/Prefs hash 불변. 산출물 SHA256 976AAE36C6A96805F3768E1FAF72498ED7339495C2B8B5DEF264E50158DD65F0.
