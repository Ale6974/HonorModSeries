# M2.4 — 기록된 선반의 연속 제작·내려놓기 변화 보존

2026-11-14 / Preview0.3.115. 기존 actual-resolution에 이미 포함된 비연결 선반의 내용물이 실제 작업으로 바뀌는 경로를 실행·저장·현재 동의·명시 복구·최종 도시 인계까지 연결했다. 전체 M2.4 완료 판정은 아니다.

## 원 보관 범위와 이후 변화를 분리

원 StorageV7 및 StorageContentsV7은 그대로 둔다. 새 StorageProgressV11은 순번·양성 작업 사건 번호·직전 범위 hash·새 현재 범위를 별도로 추가한다. 새 범위는 같은 선반 ID/설정/칸이어야 한다. 내용물에서 빠진 ID는 해당 사건의 실제 Removed, 새 ID는 실제 Added 또는 양성 haul-place의 원 입력으로 설명되어야 한다. 사건 후 실제 물체 집합에 없는 ID나 정책/칸 변경·범위 생략/재순서·중복을 허용하지 않는다.

원 프레임 때문에 이전 asset-call prefix에 빈 구간이 생긴 경우의 기존 actual-resolution을 사용한다. 여기서 새 actual-resolution 장부를 원인 없이 만들거나 아직 기록하지 않은 일반 선반의 과거 변화를 추정하지 않는다. 기존 source/target·평화·임상·각 party/권리·특수 역할/quest/holder·native 본문 조건은 유지한다.

## 실제 native 실행 경로

기존 BeginWorkObservation에서 같은 원 실제 선반·현재 내용물 참조/설정을 확인한다. 게임의 기존 recipe 완료 또는 TryDropCarriedThing이 실행되고 정상 본문 반환이 먼저 기록된 후, 전체 지도 전후 물체 ID/작업 출력을 검증한다. ObserveStorageProgress는 그 직후 같은 선반에서 읽은 실제 슬롯 내용물 변화만 새 범위로 결합한다.

native source/target affiliation, 같은 실제 선반 객체·물리/설정·같은 original refs 조건도 다시 확인한다. 각 새 snapshot의 물품은 같은 native Map의 실제 참조를 별도 reference-only wrapper에 저장한다. 기존 원 참조나 이전 snapshot 참조를 새 물체로 교체하지 않는다. 복수 선반의 변화는 모두 검증한 새 문서/참조 목록을 만든 뒤 함께 교체하며, 중간 실패는 원 시작/본문 반환과 기존 원문을 남긴다.

이 코드는 원 작업을 시작·다시 실행하거나 물품을 운반/소모/재생성하지 않는다. 실패한 내려놓기·본문 반환 없는 소실·입력/출력에 없는 이동·선반 자체의 소실/피해/정책 변경은 변화 기록의 허가가 아니다. 순수 규칙만 늘린 것이 아니라 실제 native 작업 종료 → current refs → 새 범위 → 현재 승인/인계 호출 및 마지막 도시 완료 경로에 조립했다.

## 과거 호출 승인과 현재 승인을 구분

실제 선반 소속 호출이 이미 정상 반환했으면 그 호출의 승인 원문/Started/Returned/Verified를 유지한다. StorageCallV11은 **그 호출 당시** 사용한 이력 prefix 길이만 기록한다. 후속 native 작업의 기록이 늘어도 과거 승인은 당시 prefix와만 대조한다. 옛 호출 승인 원문을 새 재고 기록으로 바꾸거나, 뒤의 새 기록을 과거 호출 권한으로 간주하지 않는다.

아직 시작하지 않은 호출은 최신 이력 전체가 현재 full-party ResolutionsV5에 들어 있어야 한다. 이미 반환한 호출은 다시 호출하지 않고 현재 실제 상태와 새 동의를 검토해 남은 도시 인계로 이어간다. 다음 호출/도시 단계의 RequireCurrentCustody는 최신 WorkProgress/Resolutions/현재 survey를 재계산하므로 이전 동의는 재사용할 수 없다. 물품에는 별도의 소속 변경/사유 권한을 부여하지 않는다. 원 계약·채무·제삼자 권리·주민/의료·플레이어 통제는 그대로다.

## Scribe·로딩·중단 복구

원 목록, 각 현재 범위와 직전 hash·양성 사건, 과거 call prefix marker를 raw 문서에 보존한다. 별도 WorkStorageReferenceV11 wrapper만 deep-save하며 실제 Thing은 reference-only다. native Map/holder가 유일한 실제 물품 저장 소유자다.

원/과거 snapshot의 물품 참조가 소실되었다면, 그 snapshot 뒤의 양성 사건이 해당 ID를 실제 Removed로 기록했을 때만 null 참조를 설명할 수 있다. 최신 snapshot은 같은 실제 슬롯 물체 참조 전부와 일치해야 한다. 누락/다른 객체·참조 모양/순서 변화는 원문을 남기고 보류하며 원 ID를 새 물체로 rebind하지 않는다. 최종 완료 때 추가 wrapper 참조만 해제하고 raw 이력을 유지한다.

실제 승인 화면은 설명 가능한 선반 변화 건수와 새 현재 동의/이전 호출 비반복 안내를 영한으로 표시한다. 화면/순수 모델만으로 완료하지 않는다. 원 호출·asset 전체 완료 조건 및 WorkCompletion receipt/Restore도 최신 이력을 검증한다.

## API 근거·검사·제한

설치5005 DLL SHA256 `82387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125`는 기존 검토와 동일하다. 새 writer는 추가하지 않았고 기존 검토한 StorageMapEvidence/slot·설정·실제 물품 읽기, recipe/양성 placement 관측과 actual SetFaction/도시 경로를 사용한다. 새 현재/과거 참조의 GetUniqueLoadID도 미패치를 확인한다. 미확인1.6 native 본문을 허용하지 않는다.

검사는 실제 규칙의 소비/생성→내려놓기 2단계 이력→현재 승인→최종 도시 receipt 저장 round-trip, 과거 call prefix의 유지/변조 거부, 첫 호출 전 새 이력 승인과 과거 동의 거부, unknown native call/실패한 placement/정책 변경/설명 없는 ID 변경/깨진 hash chain 거부, 옛 null JSON을 포함한다. 양 타깃 전체 빌드·최종 native 컴파일 경고/오류0, Honor20093/Contracts6069/ENKO618, 양 타깃 package allowlist와 백업 후 로컬32파일 일치·활성화/언어 hash 불변을 확인했다. 패키지 hash·백업 위치는 STAGE1-HANDOFF에 기록한다.

이번 범위는 이미 actual-resolution에 기록된 비연결 선반, 관측된 recipe/양성 placement다. 제작대 주문/반복 횟수 자체의 자연 변화는 기존 worktable proof를 덮어쓰지 않으며 별도 후속 프로토콜이 필요하다. 집기 단계 자체의 관측, 기록되지 않은 원 선반의 이전 변화·임의 이동, 반복 소실/재생성, 연결 그룹/전력·시설, 역할/개인 권리·부모 follower/정치·후손 조립은 남아 있다. 기존 최대128변화/선반과 원 장부3M·작업256사건 경계를 유지한다. M2.4 전체 미완료, 기존 잔여 노력 추정25–40%/3묶음은 이번 부분 확장만으로 재측정하지 않았다. M1 10/10,M2 0/18,전체10/100 유지. Preview Steam·자동 예약 없음.
