# M2.4 — 선반 설정·보관 물품과 일반 바닥 완공의 실제 인계

2026-11-14 / Preview0.3.112. B03/B06의 실제 후계/원 재산 실행기를 비연결 native 선반까지 확장했다. 전체 M2.4 완료는 아니다.

## 새 원 프레임 범위와 실제 완공

기존 ConstructionEvidence는 storageSettings가 있는 Frame을 거부했으므로, 결과 선반의 클래스만 허용해서는 실제 새 선반 경로가 실행될 수 없었다. 이번에는 새 원 NativeWorkFrame의 null-생략 StorageV7에 원 보관 설정 proof를 결합했다. 정확한 Building_Storage 대상·원 Frame·native StorageSettings/ThingFilter·null 원 settings.owner·storageGroup 없음과 기존 자재/작업/호출 조건을 확인한다. Blueprint_Storage가 실제로 소유자 없는 임시 설정을 만들고 native Frame이 이를 Scribe deep-save하는 경계를 metadata로 확인했다.

정상 native 완공 사건에서 원 Frame/자재 소실과 정확히 하나의 실제 source 선반을 관측한다. 원 정의·stuff·칸·회전·원 설정 proof를 비교하고, 실제 선반의 설정이 원 프레임 설정과 동일해야 별도 준비를 추가한다. 원 프레임 asset call은 계속 미시작이며 반환으로 꾸미지 않는다. native 완공을 대신 실행하거나 설정을 복사하는 코드는 추가하지 않았다. 옛 null Frame 범위는 새 보관 설정 허가가 되지 않는다.

## 실제 승인·호출·저장·중단 복구

새 WorkAssetResolution.StorageV7은 실제 선반 ID, 전체 설정/필터/우선순위/label/style proof, 정렬된 슬롯 칸과 전체 보관 물품 ID를 결합한다. 별도 wrapper의 StorageContentsV7은 실제 물품을 reference-only로 보관한다. native Map/Frame이 실제 물체·설정의 유일 deep-save 소유자다. 선반/설정/물품을 두 번 deep-save하지 않는다.

현재 전체 당사자 ResolutionsV5 승인 → 기존 주민/의료/영역 단계 → 다음 실제 선반의 동일 객체·설정·칸·전체 물품 참조 확인 → 기존 native SetFaction → 별도 Started/정상 Returned → 같은 보관 범위 확인 → Verified → 원 도시/최종 영수증으로 연결했다. 정상 반환 후 확인만 남으면 새 현재 승인 아래 재호출 없이 확인한다. Started만 있거나 원/실제 객체가 바뀌면 원문을 유지하며 replay하지 않는다.

프레임 소실 때문에 원 순차 호출에 빈 구간이 생긴 뒤의 변경 없는 **원 비연결 선반**도 별도 동일-object 호출로 이어간다. 원 map.Storage의 설정/칸/물품 원문을 그대로 사용하며, 물품 참조는 새 현재 탐색 결과가 아니라 원 LoadedMapRecord.Things에서 가져온다. 후계 선반은 양성 완공 직후의 실제 물품 참조를 별도로 동결한다. 호출 전후 및 로딩/명시 복구에서 실제 slotGroup의 전체 물품과 참조·순서를 비교한다.

내용물이 있다는 사실은 내용물 소속·사유 권한을 부여하지 않는다. 물품의 소속은 기존 original asset/현재 survey 권한을 그대로 따른다. 이 코드는 물품을 운반·분할·재포장·소속 변경하지 않는다. 기존 계약·채무·제삼자 권리·주민 건강/장비/관계/의료·플레이어 통제도 그대로 유지한다. 최종 완료는 모든 원 asset의 양성 반환 또는 별도 actual resolution을 요구하며, 별도 선반 장부/동의/최종 receipt도 원 transfer에 결합된다.

## 검증·호환성

새 저장 필드는 null일 때 생략한다. 옛 기본 건물/제작대 및 Frame null JSON/hash/API/승인 범위는 유지한다. 저장 범위는 cells<=4096/contents<=10000, 원/관측 물체 provenance, 정렬/유일성, 다른 원·별도 선반과 칸/물품 중복, 원 shelf scope의 정확한 동일성을 검증한다. 다른 선반 그룹의 구성원을 일부만 인계할 권한은 만들지 않는다.

설치5005 DLL SHA256 `82387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125`가 이전 검토 값과 같다. 현재 Blueprint_Storage.MakeSolidThing, Frame.CompleteConstruction/ExposeData, Building_Storage의 상속·ExposeData, StorageSettings 생성자/ExposeData/CopyFrom/owner 흐름을 metadata로 확인했다. native recovery writer는 기존 SetFaction뿐이다. 새 원 storage scope는 CopyFrom/priority/notify/filter copy 경계의 미패치도 확인한다. 검토된5005만 새 native 경로를 실행하고, 미확인1.6 본문 권한은 추가하지 않는다. 게임 원문은 ignored .local에만 보관한다.

순수 fixture는 원 Frame storage 허가 누락·잘못된 hash·잘못된 슬롯/물품·중복/겹침·옛 동의·재해시한 원 설정 변조를 거부하고, 실제 후계와 보관 물품이 있는 원 선반의 별도 호출/반환/검증/마지막 receipt round-trip을 검사한다. 양 타깃 전체 빌드 및 최종1.7 native 컴파일 경고/오류0, Honor20093/Contracts6010/ENKO608, 양 타깃 패키지 allowlist, 백업 후 로컬32파일 일치 및 활성화/언어 hash 불변을 확인했다. 패키지 hash·백업 위치는 STAGE1-HANDOFF에 기록한다.

## 일반 바닥 완공 — 건물 호출을 만들지 않는 별도 수락

새 NativeWorkFrame.TerrainV8은 원 프레임의 등록된 지형 정의 이름을 명시한다. 같은 원 Frame/자재의 양성 native 완공, 새 Thing 추가 없음, 정확한 원 칸의 실제 일반 terrain/under-terrain/roof를 관측해 별도 WorkTerrainAcceptance를 기록한다. 원 Frame 호출은 여전히 미실행이며 지형용 가짜 Thing ID나 건물 SetFaction 반환을 만들지 않는다.

실제 ReviewWorkProgress의 전체 현재 승인에는 이 원문도 ResolutionsV5로 포함된다. 같은 Map·native terrain/roof owner·같은 실제 칸/정의/아래 지형/지붕과 미패치 읽기 경계를 다시 확인한 뒤 **별도 수락**만 장부에 기록한다. 이 단계는 원 프레임 공사가 현재 도시의 지형으로 남아 있음을 승인하는 행동이다. 새로운 지형 생성/SetTerrain/복구/채색/native 건물 소속 호출을 실행하지 않는다. 실제 권한 이전은 기존 마지막 도시 SetFaction 호출로 수행한다.

원 Frame tag 없는 옛 scope·완공되지 않은 사건·Thing이 추가된 다른 공사·다른 칸/지형·옛 동의는 거부한다. terrain 항목은 native Started/Returned/Verified/Actual/Asset 필드를 가질 수 없고, 별도 Accepted/Approval만 갖는다. 일반 재산 항목과 섞인 장부에서도 원 asset 전부가 원 반환·실제 객체 인계 또는 승인된 지형 완공으로 설명돼야 도시/최종 receipt가 완료된다. 원 호출 장부는 동결된 채 유지한다.

지형의 별도 승인/정의/칸/원 사건/전체 동의 archive를 Scribe·로딩·명시 복구·최종 receipt에 연결했다. 이 항목에는 deep-save할 새 객체가 없다. 이전 승인으로 만든 Accepted 이력도 원 전체 현재 승인에 결합해 검증한다. 같은 실제 지형이 변하거나 사라지면 원문을 남기고 보류하며, 다시 SetTerrain하여 과거 상태로 되돌리지 않는다.

현재5005 TerrainGrid/RoofGrid/Frame metadata를 읽어 실제 배열/owner/getter와 완공 분기를 확인했다. 새 경로는 표준 비기초·비임시·비다리 지형이며, foundation/temp/color 배열의 해당 칸이 비어 있어야 한다. 겹층/채색 지형·필스 소실 등 추가 출력은 일반 바닥 범위로 추정하지 않는다. native 게임 원문은 ignored .local에만 둔다. fixture는 바닥-only resolution과 남은 원 건물을 합친 전체 실행/동의/원 호출 불변/저장 receipt round-trip 및 가짜 native 호출·재수락·옛 scope 거부를 확인한다.

## 미완료

겹층/채색·임시/다리 등 복합 terrain, 연결 선반 그룹·전력/연료/시설 등 다른 특수 후계, 후계 자체의 연속 소실/변형, 더 넓은 작업·역할/개인 권리와 부모 follower/정치·후손 조립은 남아 있다. 선반 물품의 자연 소비·교체도 원 참조를 새 것으로 바꾸어 완료하지 않는다. 이번 실행 확장은 전체 완료 판정을 대신하지 않는다. M2.4 잔여 노력 추정25–40%/큰3묶음은 이번 부분 확장만으로 재산정하지 않았다. M1 10/10,M2 0/18,전체10/100 유지. Preview Steam·자동 예약 없음.
