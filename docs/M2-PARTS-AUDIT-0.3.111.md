# M2.4 — 실제 후계·남은 원 제작대의 주문 보존 인계

2026-11-14 / Preview0.3.111. B03/B06의 별도 실제 재산 인계를 일반 제작대로 확장했다. 전체 M2.4 완료 판정은 아니다.

## 실제 실행 경로

원 source Frame의 양성 native 완공 사건에서 정확히 한 개의 실제 `Building_WorkTable` 또는 `Building_WorkTable_HeatPush`를 관측한다. 기존 정의·stuff·위치·회전·source 권한·원 프레임 미시작 호출 조건을 유지한다. 실제 새 제작대의 원 configuration/physical과 별도 `WorktableV6`(ID, 전체 주문 설정 hash, 순서 있는 Bill ID)를 후계 장부에 결합한다. 실제 Bill 참조는 별도 wrapper의 `WorktableBillsV6`에 reference-only로 보관한다. native BillStack이 유일한 Bill deep-save 소유자다.

현재 전체 당사자 `ResolutionsV5` 승인 → 원 주민/의료/영역 단계 → 다음 실제 제작대의 호출 전 검토 → 기존 native SetFaction → 독립 Started/정상 Returned → 호출 후 동일 물체/주문 참조·설정 확인 → Verified → 기존 원 도시 인계/최종 영수증 순서로 실행한다. 복구에서는 동일 실제 제작대와 같은 주문 참조/순서·필터·제작 설정·금지 설정을 확인한다. 새 주문을 만들거나 필터를 복사하거나 제작/작업을 재시작하지 않는다. 이미 Returned인 호출은 재호출 없이 보존 확인만 가능하며, Started만 있는 호출은 계속 보류한다.

원 프레임 소실로 동결된 옛 순차 호출에 빈 구간이 생긴 뒤의 **변경 없는 원 일반 제작대**도 같은 별도 실행기로 연결한다. `AddOriginal`은 원 지도에 기록한 WorktablesV1을 그대로 복사하며, 실제 호출 **전과 후** 모두 원 주문 설정과 동일 참조를 확인한다. 현재 값을 원 설정으로 덮어쓰지 않는다. 원 프레임 호출·원 임상/재고/재산·계약/채무·제삼자 권리·플레이어 통제는 그대로 유지한다.

## 저장·승인·호환성

WorktableV6는 null일 때 JSON에 기록하지 않는다. 옛 .110 기본 건물 scope/hash/동의와 호출 의미를 바꾸지 않는다. 새로운 제작대 scope를 가진 항목은 주문 ID·유일성·용량·configuration hash를 검증하며, 옛 기본 건물 동의로 새 제작대 범위를 인계할 수 없다. 역직렬화된 Bill ID 배열도 canonical 문서로 비교해 같은 현재 동의가 load 후 참조 배열 불일치 때문에 거짓 거부되지 않게 했다.

원 별도 ledger·정상 반환·승인 archive와 실제 Thing/Bill 참조를 Scribe/로딩/명시 복구/최종 영수증에 연결한다. 완료 후 추가 Bill/Thing 참조만 해제하고 원문은 유지한다. 원 프레임을 새 제작대로 rebind하지 않고 원 asset call 반환을 꾸미지 않는다.

## 지원 경계와 API 근거

기존 `WorktableEvidence`를 재사용한다. 정확한 native 두 클래스·BillStack/제작 주문, 검토된 기본 comp만 허용한다. 전력·연료·고장·mote component, 연결 시설, 개인 제한/특수 주문·bound UFT·임의 파생 제작대는 별도 프로토콜이 없어 보류한다. 일반 제작대의 명시적 독립 재산 경계만 활성화하며, 옛 capture의 작업자/권한 조건은 그대로다.

설치5005 DLL SHA256 `82387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125`가 기존 검토와 같다. 현재 Building_WorkTable과 BillStack 전체 metadata 본문을 다시 추출해 .105 검토 파일과 동일 hash를 확인했다. 제작대는 Building에서 SetFaction을 상속하며 BillStack은 native ExposeData로 deep-save된다. 새 writer는 기존 검토된 SetFaction뿐이다. 새로운 1.6 본문 권한은 추가하지 않았고 미확인 native 경로는 보류한다. 게임 원문은 ignored .local 밖에 게시하지 않는다.

## 검사·미완료

새 fixture는 제작대 후계/원 제작대 remainder의 실제 규칙 흐름, 주문 범위 변조·옛 동의·미검증/불확실 반환 거부, 동결 원 호출 불변, canonical 배열 승인, 최종 영수증 round-trip을 검사한다. 양 타깃 전체 빌드 및 최종 native 컴파일 경고/오류0, Honor20093/Contracts5975/ENKO607, 패키지 allowlist와 백업 후 로컬32파일 일치·활성화/언어 hash 불변을 확인했다. 패키지 hash·백업 위치는 STAGE1-HANDOFF에 기록한다.

M2.4 전체는 미완료다. terrain·저장/전력/연결 시설 등 특수 후계와 연속 소실·변형, 더 넓은 작업/특수 역할·개인 권리, 부모 follower/정치·후손 조립이 남았다. 이번 확장은 이 전체의 완료나 다른 마일스톤의 통합을 대신하지 않는다. 잔여 노력 추정25–40%/큰3묶음은 좁은 확장만으로 재산정하지 않는다. M1 10/10,M2 0/18,전체10/100 유지. 공개 main만 게시하며 Preview Steam·자동 예약 없음.
