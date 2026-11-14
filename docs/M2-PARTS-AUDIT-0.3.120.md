# M2.4 실제 조립 감사 — Preview 0.3.120 / 2026-11-14

## 같은 원 물품의 실제 전량 집기

새 WorkStoragePickups/StoragePickupObservation/WorkHaulPickupObservation을 WorkProgress의 실제 시작·정상 반환·출력·현재 승인·재산 호출·도시·최종 receipt·Scribe/load/명시 복구에 조립했다. 기존 int TryStartCarry(Thing,int,bool) 정상 실행을 관측하며 recovery가 호출하지 않는다. 기존 bool pickup 경로나 임의 바닥/다른 holder에 권한을 추가하지 않는다.

- 새 clinical16 standalone/v3/current Job grant, 같은 원 Pawn·원 지도·원 선반·원 neutral item ref가 필요하다. source는 원 호출 전 source-owned 비연결 선반 또는 이 새 pickup provenance가 있는 선반이다. 시작 전에 원 actual refs/정의/재료/수량/HP/품질/셀/회전/current membership·settings/calls를 확인한다. source 품목은 기본 Thing/ThingWithComps이고 현재 native 운반 보관함이 비어 있어야 한다.
- 지원 후보를 read-only preflight로 확인한 뒤에만 새 observer 시작을 만든다. 임의 ground/holder/권리 조합에는 새로운 Started를 만들지 않는다. body 전에 StoragePickupV18과 별도 원 StorageOriginsV12를 기록한다. native body 반환 뒤 independent BodyReturned를 먼저, native int 반환 수량 PickupTakenV18을 다음에 저장하고 나서 output을 검증한다.
- positive completion은 반환 수량이 원 전량과 정확히 같고 SAME original Thing ref가 SAME 원 Pawn의 exact native carry owner에 살아 있으며 수량/정의/재료/회전/HP/품질이 같아야 한다. 기록상의 Carried.Cell=-1은 held 상태의 표현이고 실제 위치 setter를 호출하지 않는다. Source shelf의 exact ordered membership만 해당 ID를 제외해야 하며 global survey Added/Removed는 둘 다 비어 있어야 한다. 집기는 파괴·재생성이나 새 ID의 생성으로 처리하지 않는다.
- WorkStorageProgressV11은 새 typed pickup의 정확한 before/after scope에만 membership 제거를 허용한다. generic Removed 추정이나 옛 이벤트 재해석을 하지 않는다. 첫 명시 original origin은 기존 version2 actual root에 연결되고 이후 actual SetFaction/독립 정상 반환/검증·도시/최종 receipt까지 common executor를 사용한다. 원 StorageV7/원 items·call approval prefix를 유지한다.
- 같은 원 물품은 native Pawn_CarryTracker/ThingOwner가 계속 sole deep-save owner이며 추가 refs는 reference-only다. 나중 native placement가 같은 ID를 되돌리는 범위도 연결했다. 과거 실제 faction call의 StorageCallV11 prefix는 그대로 검증하고 최신 현재 full-party consent로 이어간다. job/driver/예약을 재구성·재실행하지 않는다. 실제 pickup으로 생긴 reservation/owner 변화는 기존 current survey가 검토한다.
- partial/capped/실패·불명 output은 raw start/body/int result를 보존하고 자동 replay/원 수량 복원/물품 생성/소유자만으로 완료하지 않는다. 특히 뒤늦게 marker를 넣거나 새 clone을 원 물품으로 묶지 않는다. 이 source는 full-item path이며 partial split의 completion 권한이 아니다.

## API·보존·검사

현재1.7.5005 DLL SHA256 `82387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125` 일치. 새 metadata-read Pawn_CarryTracker 전체는 이전 carry 감사와 SHA256 `FCE281EE3C4E94365665EA897A5C016C668F0763E08B78305F819A9F32FCC386`로 일치한다. int path의 capacity clamp→SplitOff→native TryAdd→native reserve/resource update와 ThingOwner generic 전체 몸체를 읽었다. 전량 SplitOff는 같은 객체를 반환한다. Quality/Forbiddable PostSplitOff의 same-object 동작도 읽었다. source는 정확한 own observer만 허용하고 base/dispatch SplitOff/DeSpawn, owner TryAdd/accept/notify 및 comp callback 패치가 있으면 새 관측을 거부한다. unknown1.6 body는 실행 권한으로 추정하지 않는다.

원 계약·채무·실적·제삼자 권리·임상/의료·native player control은 유지한다. old null JSON/API/old kinds/source permissions는 유지하고 새 haul-pickup만 새 grant/typed before/result를 요구한다. 관측/복구는 TryStartCarry/SplitOff/Reserve/TryAdd/DeSpawn·health setter를 실행하지 않는다. game가 원래 수행한 정상 body를 관측한다.

규칙 검사: 전량 pickup→same-ID held state→actual shelf faction call→후속 같은 물품 placement→과거 call prefix 보존→최종 Applied ledger 저장/복원. partial count·global disappearance·replacement ID·미확인 output·뒤늦은 before·변조된/null 저장 자료를 거부한다. 원 refs가 로드 후 사라진 경우 native 보관함을 새로 만들지 않는다.

M2.4 전체 미완료. partial pickups/split/일반 ground/복잡 holder, pickup 뒤 임의 재입고·다른 count journal 조합, comp가 있는 merge/추가 수량 흐름, UFT/다른 반복 모드·전력/연결 설비, 역할/개인 권리/부모 follower·정치·후손 조립이 남았다. M1 10/10,M2 0/18,전체10/100 유지. 과거25–40%/3묶음 및 여섯 단계 비율은 이번에 재측정하지 않았다. Preview Steam/자동 예약 없음.
검사·설치: 양 타깃 전체 빌드 및 최종 native compiler 경고/오류0, 최종 Honor20093/Contracts6156/ENKO625, 양 runtime allowlist/패키지 검사 통과. `artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.120.zip` SHA256 `F1DE37F9A9EF10B08C6FAEBD21986DE6D69EB26F4B4B7195768F5AB8BB2EEEE4`. 이전 로컬 Preview는 `.local/contracts-preview-22010f9b04f143218ef57298f5dcd08e`에 백업 후 최종32파일 일치 설치했다. 활성화·언어 hash 불변. 다음: 별도의 새 before-source scope로 partial pickup/split·일반 ground 및 count/membership 복합 연속 작업을 연결한다. 기존 V18의 미확인/부분 native body를 다시 실행하거나 새 scope로 소급 승격하지 않는다. 시설·역할/개인 권리·부모 조립은 별도 실제 실행 근거가 필요하다.