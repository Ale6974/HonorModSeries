# M2.4 실제 조립 감사 — Preview 0.3.121 / 2026-11-14

## 실제 부분 집기·분할·원 잔량 보존

WorkStorageSplits/StorageSplitObservation은 새로운 before-body StorageSplitV19를 사용한다. 원 선반의 같은 original Thing refs/전체 physical before와 actual current Job의 명시적 부분 요청(0보다 크고 원 수량보다 작음)/원 calls를 기록한다. V18 전량 요청을 변경하거나 V18의 이미 실행된 부분·미확인 출력을 재해석하지 않는다.

actual TryStartCarry normal BodyReturned와 PickupTakenV18 int result를 독립적으로 기록한 뒤, 0보다 크고 요청 이하인 partial quantity만 검증한다. 원 Thing은 같은 refs로 원 shelf에 남고 그 수량만 taken만큼 감소해야 한다. 다른 원 물품/정의/재료/HP/품질/셀/회전은 그대로다. actual Pawn native carry owner에는 original refs와 다른 native-created Thing이 있어야 한다. 새 ID는 original seal/과거 양성 Added와 겹치지 않으며 같은 정의/재료/HP/품질/회전과 taken 수량을 가져야 한다. 전후 global survey는 원 ID Removed 없이 새 carried ID 하나만 Added여야 한다.

StorageSplitsV19/version6 actual root는 원 V7/원 physical proof와 구 calls를 유지하고 각 양성 잔량을 별도로 보존한다. 현재 native 검증은 마지막 양성 잔량과 same original refs를 비교한다. 새 cargo의 실제 refs는 별도 SplitCargoV19 reference-only 목록으로 Scribe한다. native carry/map가 sole deep-save owner다. 나중 positive Removed가 없는 missing cargo refs를 교체/생성하지 않는다. 같은 source의 다음 partial pickup도 직전 양성 출력에서 이어져야 한다.

actual current full-party consent→실제 original shelf SetFaction/독립 정상 반환/검증→도시/최종 receipt→Scribe/load/명시 복구를 existing controller에 연결했다. StorageSplitCallV19는 actual faction call 당시 split history prefix만 고정한다. 나중 quantity change로 old approval/call을 다시 쓰거나 replay하지 않는다. original contract/debt/performance/third-party rights/native player control/주민 의료는 유지한다.

## 조립·검사

기존 V18 preflight에 새 partial-request branch를 분리했고 기본 Thing/ThingWithComps, Quality/Forbiddable native comp callback, empty native carry owner, 원 비연결 source-owned shelf/current Job scope만 대상으로 한다. source/root가 다른 count/merge/membership protocol이면 새로운 Started를 만들지 않는다. raw before/current full-party decisions/실제 객체를 추정하지 않는다. zero/failed/unknown output은 raw native start/body/count를 보존하고 완료하지 않는다.

규칙 검사: 첫 partial pickup, 원 잔량의 다음 partial pickup, 정확한 새 ID·수량·원 physical 유지, 모르는/재사용한 ID·잘못된 양·뒤늦은 before·unknown call 거부, old null JSON 보존, 과거 actual call approval prefix, 최종 Applied 원 장부 저장/복원. 같은 원 calls portfolio에서 source partial pickup→새 native cargo→다른 original shelf의 true whole merge→원 source/destination 각각 actual faction call→최종 Applied 저장/복원 통합도 검사했다.

현재 installed1.7.5005 DLL SHA256 `82387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125` 및 .120 carry/ThingOwner/Thing split body audit를 재사용한다. source는 own exact native pickup observer 외 외부 patch를 거부한다. 관측/복구에서 SplitOff/ThingMaker/TryStartCarry/TryAdd/Reserve/HP·수량 setter를 실행하지 않는다. 새 native Thing은 게임이 만들고 actual identity/output을 확인할 뿐이다. unknown1.6 body에는 새 권한을 추정하지 않는다.

## 미완료

M2.4 전체 미완료. 일반 ground/복잡 holder, partial에서 whole 전환 및 same-source 재합치기/다른 count·membership protocol 조합, comp merge, UFT/다른 반복 모드·전력/연결 시설, 역할/개인 권리·부모 follower/정치/후손 조립이 남았다. 이번 source의 범위는 기존 milestone 전체로 축소하지 않는다. M1 10/10,M2 0/18,전체10/100 유지. 과거 잔여25–40%/3묶음과 여섯 단계 비율은 새 측정값이 아니다. Preview Steam/자동 예약 없음.
검사·설치: 양 타깃 전체 빌드/native compiler 경고/오류0, 최종 Honor20093/Contracts6172/ENKO626, 두 runtime allowlist/패키지 검사 통과. `artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.121.zip` SHA256 `55A9C2416CB4C37D773E71EF03136F12DAFF4803760A0A63BA411F5516BD464D`. 이전 로컬 Preview는 `.local/contracts-preview-e7aaa9b7664745c9b445e7f400d51150`에 백업 후 최종32파일 일치 설치, 활성화·언어 hash 불변. 다음은 새 explicit before scope에 의한 partial→whole/재합치기·ground 및 복합 stock/시설/역할/부모 조립이며, old unknown outputs/native calls를 replay하거나 original snapshots를 새 현재 값으로 교체하지 않는다.