# M2.4 실제 부품·조립 감사 — Preview 0.3.118 / 2026-11-14

## 1. 부분 소비 → 완전 소모 → 새 보관 물품

- 규칙 `WorkStorageRetirements.cs`와 실제 `StorageRetirementObservation.cs`를 기존 Begin/정상 body return/출력/현재 승인/실제 재산 호출/도시/최종 receipt/Scribe·로드·명시 복구에 연결했다.
- 최초 부분 소비의 원 StorageV7/물품 물리/실제 Thing refs/수량 이력은 그대로 둔다. 전환 전에 같은 원 선반·같은 원 물품의 직전 양성 수량 출력과 actual job.placedThings 전체 요구량을 `StorageRetirementsV14`에 기록한다. 원 선반의 모든 원 물품이 정확한 잔여량만큼 소비되는 범위다. 일부만 소실되고 다른 원 물품이 남는 일반 조합의 권한은 아니다.
- body 정상 반환을 기록한 뒤 원 Thing refs 자체의 Destroyed/비spawned/holdingOwner없음을 확인한다. 같은 ID 새 객체나 소유자만으로 소실을 추정하지 않는다. 전후 전체 map/known holder survey에서도 모든 원 입력이 실제 Removed여야 한다.
- 별도 `StorageBridgeV14`가 마지막 원 수량 이력과 첫 양성 membership 변화를 연결한다. 기존 StorageProgressV11/실제 snapshot refs를 사용해 빈 선반과 이후 실제 내려놓은 새 물품으로 이어간다. 현재 참조는 실제 slot 전부와 같아야 하며 과거 null 참조는 뒤의 양성 소실로만 설명한다.
- 실제 소속 호출 당시 사용한 count/membership prefix만 승인 검증에 사용한다. 이후 전환 때문에 과거 호출을 다시 실행하거나 승인을 다시 쓰지 않는다. 다음 인계/도시는 최신 상태에 대한 새 full-party 동의를 요구한다. 원 before/body 증거 없는 구 이벤트는 전환에 사용할 수 없다.

## 2. 같은 원 제작 주문의 제작 횟수 진행

- 규칙 `WorktableIterations.cs`, 실제 `WorktableIterationObservation.cs`, 기존 `WorktableEvidence`/실제 재산 controller를 조립했다. 대상은 원 지도에 기록된 기본 비연결 제작대의 정확한 native Bill_Production/RepeatCount이며 원 BillStack·Bill refs/순서를 그대로 쓴다. UFT/특수 병과·개인 노동/연결 시설·전력/연료의 권한은 추가하지 않는다.
- body 전에 원 또는 직전 양성 전체 제작대 hash, 같은 active Bill ID, 실제 repeatCount, 원 재산 호출 prefix, 별도 stable hash를 `WorktableIterationV15`에 기록한다. stable hash는 실제 active Bill의 repeatCount 하나만 제외하고 다른 모든 주문·설정/필터/참조·원 comp 구성을 포함한다. 원 Capture hash/WorktableV6를 정규화한 값으로 교체하지 않는다.
- 정상 반환 후 같은 원 Bill refs가 남고 stable hash는 같으며 실제 남은 횟수가 정확히 하나 줄었을 때만 별도 출력 hash/count를 저장한다. 다른 설정 변경·주문 교체·누락·순서 변경·횟수 증가/잘못된 감소·불명 반환은 raw를 보존하고 중단한다. 1→0 완료도 원 Bill을 삭제/재생성하지 않고 보존한다.
- 양성 `WorktableIterationsV15`를 기존 실제 재산 장부 또는 새 version4 root에 연결했다. 프레임 완공은 root 생성 전제조건이 아니다. 원 재산/호출/WorktableV6는 고정한다. 실제 검증은 최신 양성 table descriptor와 원 Bill refs를 함께 사용한다. `WorktableCallV15`는 원 실제 SetFaction 당시의 iteration prefix만 고정한다.
- 현재 full-party 승인→실제 SetFaction/정상 반환/검증→도시/최종 receipt→Scribe/로드/명시 복구에 같은 controller를 사용한다. Bill constructor/setter/Notify_IterationCompleted를 관측·복구 코드에서 호출하지 않는다. 다른 실제 Job의 후속 iteration에는 기존 새 현재 Job 승인/관측 경로가 필요하며 한 Job의 중복 반환을 재실행하지 않는다.

## 공통 보존·검사

원 계약·채무·실적·제삼자 권리·원 주민/의료/지도/native player control은 변경하지 않았다. old null JSON/API/old version1~3 root provenance는 유지하며 새 필드는 명시적인 실제 before-body 증거가 있을 때만 사용한다. native objects는 원 소유자가 deep-save하고 새 기록은 기존 raw journal과 reference-only lists를 사용한다. 불명 Started 호출/출력은 자동 replay·목표 소속만으로 완료·원 refs retarget으로 처리하지 않는다.

최종 규칙은 개별 최초/연속 진행 외에, 같은 이벤트의 재료 부분 소비+주문 횟수 진행 → 같은 다음 이벤트의 원 재료 완전 소모+주문 0회 완료 → 양쪽 과거 실제 호출 승인 보존 → 최종 Applied 원 장부의 저장/복원을 함께 검사한다. 완전 소모 후 새 물품 내려놓기도 별도 검사한다. before 증거 누락/뒤늦은 추가/변조·잘못된 감소/순서·unknown 호출·가짜 bridge를 거부한다.

현재 설치1.7.5005 DLL SHA256 `82387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125` 일치. 새로 읽은 native Bill_Production 전체 metadata는 기존 감사 파일과 SHA256 `26FC0CB4EA3215926AE8CFAC57EDF78BF73C5D76F90AB8DEE6741A492E25ECC4`로 일치하며 Notify_IterationCompleted/ExposeData와 기존 recipe/Thing 소실 경계를 재사용한다. 실제 code는 native getter/callback 미패치를 요구한다. unknown1.6 native body에 새 실행 권한을 추정하지 않는다.

## 완료 판정과 남은 작업

M2.4 전체 미완료. 실제 집기/운반 합치기와 수량 증가, 일부 원 물품만 남는 count+membership 혼합 변화, 새 보관 물품의 후속 수량 이력, UFT·다른 반복 모드/복합 제작 설정, 전력·연료·연결 설비, 특수 역할/개인 권리 및 부모 follower/정치/후손 조립이 남았다. 원 임의 진행 Job/예약을 재구성하거나 가드를 제거해서 완료하지 않는다. M1 10/10,M2 0/18,전체10/100 유지. 기존 M2.4 잔여25–40%/3묶음과 여섯 단계의 과거 비율은 이번 변경으로 실측 재산정하지 않았다. Preview Steam/자동 예약 없음.
검사·설치: 양 타깃 전체 빌드 및 native compiler 경고/오류0, 최종 Honor20093/Contracts6115/ENKO622, 두 runtime DLL allowlist/패키지 검사 통과. `artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.118.zip` SHA256 `6A15EB1DC49B1F2DB45CC7A946AAFB260E7725F3034C690E953CD81071E9E613`. 이전 로컬 Preview는 `.local/contracts-preview-ed3874a7a7284f9c95c32d32667e074e`에 백업한 뒤32파일 일치 설치했다. 활성화·언어 설정 hash 불변. 다음 착수: 실제 집기/합치기와 원 물품 일부가 남는 혼합 변화, 이어서 UFT/반복모드/연결 설비·역할/부모 조립. 이를 기존 before/body/호출 증거의 소급 확대나 unknown replay로 처리하지 않는다.