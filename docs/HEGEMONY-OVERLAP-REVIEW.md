# Hegemony 모음집과 Honor의 중복·연동 검토

검토일: 2026-10-25. Honor Contracts 0.3.20 기준. 외부 설명과 로컬에 설치된 배포 파일을 구분해 대조했다. 이번 변경은 조사 문서뿐이며 연동 코드·호환 패치·새 의존성을 추가하지 않았다.

## 결론

Honor.Contracts의 봉신·영지·공납·외교·전쟁 계획은 Hegemony와 크게 겹치고, World Domination 2.0(WD2)과도 세계 군사·영지 운영 부분이 겹친다. Honor.Core/Society/Nobility/Diplomacy/Oaths의 개인 명예·강제 결투·사과·소문·대리 결투·맹세는 확인한 모음집 설명/가이드에서 직접 대체 기능을 찾지 못했다. 설명에 없다는 사실이 상대 코드 전체에 기능이 없다는 증명은 아니다.

기능 개수의 공통 분모나 상대 구현 전체를 확정하지 못했으므로 중복률을 숫자로 만들지 않는다. Honor의 계획과 이미 연결된 구현을 분리한다. Hegemony가 Honor와 같은 계약 계층/개인 봉신/의결/정당한 독립 규칙을 제공한다고 단정하지 않는다.

## 출처와 조사 범위

- [사용자가 지정한 모음집](https://steamcommunity.com/sharedfiles/filedetails/?id=3825718677): Mods with Hegemony Integration, 조회 시 9개 항목. 함께 설치해야 하는 필수 의존성 목록과는 다르다.
- [Hegemony](https://steamcommunity.com/sharedfiles/filedetails/?id=3825320105): Workshop 작성자 설명, 배포된 About.xml/LoadFolders.xml, MODDER_GUIDE.md, Docs/UserGuide.md/DetailedReference.md.
- [World Domination 2.0](https://steamcommunity.com/sharedfiles/filedetails/?id=3680501610): Workshop 작성자 설명과 로컬 About.xml.
- [Combat Extended](https://steamcommunity.com/sharedfiles/filedetails/?id=2890901044), [Vanilla Psycasts Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2842502659), [Ignorance Is Bliss](https://steamcommunity.com/sharedfiles/filedetails/?id=2554423472), [RimMarket](https://steamcommunity.com/sharedfiles/filedetails/?id=3817820660), [Vanilla Traits Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2296404655), [Alpha Memes](https://steamcommunity.com/sharedfiles/filedetails/?id=2661356814), [Vanilla Ideology Expanded - Memes and Structures](https://steamcommunity.com/sharedfiles/filedetails/?id=2636329500): Workshop 작성자 설명; CE/VPE/IIB/RimMarket은 로컬 메타데이터도 확인했다.
- Hegemony 배포 가이드는 beta 0.138.0 / API 5 / 2026-10-24로 표기한다. 1.7 배포 DLL의 public HegemonyApi를 정적으로 확인해 ApiVersion=5 및 아래 사용 후보의 존재를 확인했다. 모든 내부 동작이나 1.6 DLL 전체를 검토한 것은 아니다.
- 조사한 1.7 Hegemony DLL SHA256: `A6C0993BC73FF30D61EAC95E40E55C6EAA67E05084A995978FBDDD5F930FCFDA`.
- Steam 본문은 일반 HTTP 조회로 확인했다. 웹 열기 도구의 접근 실패를 기능 부재로 해석하지 않았다. 원문/역컴파일 결과/제삼자 DLL은 조사용 무시 경로에만 두고 Git에 포함하지 않는다.

## 아홉 모드의 중복과 주의점

| 모드 | Honor와 기능 중복 | 주요 판단 |
|---|---|---|
| Hegemony | Contracts 높음, 기존 다섯 모듈 낮음 | 봉신 개발·세금·물자 배송·신뢰/불만·조약·전쟁·정복/해방·반란·실제/추상 인구 관리. 같은 도시/외교의 이중 작성자가 되는 것이 핵심 위험 |
| World Domination 2.0 | Contracts의 전쟁/영지 단계 높음 | 실재 월드 상단·습격·확장·도로·생산 전초기지·수동 방어·NPC 외교. 정복/파괴/군사 사건 연결이 필요 |
| Combat Extended | 기능 중복 낮음, Core 결투의 기술 위험 높음 | 사격·근접·탄약/장비·피해 처리 변경. 같은 무기 Def/재료/품질/내구도만으로 같은 탄약·장전 상태를 보장할 수 없음 |
| Vanilla Psycasts Expanded | 낮음 | 초능력 경로·능력과 자동시전. 결투 중 초능력/소환/지속 효과가 별도 공격 경로가 될 수 있어 규칙·억제 범위를 별도 검토해야 함 |
| Ignorance Is Bliss | 낮음 | 기술 수준에 따른 외부 세력 사건/대상 제한. Honor의 외교 결투·계약·구원 대상 생성도 상대 제한을 존중하는 정책이 필요 |
| RimMarket | 경제·운송 부분 일부 | 시장·흥정·시장 평판·무역 노선·실제 배송/매복 구원. 시장 평판은 Honor의 개인/세력 명예와 별개. 공납과 판매·기부의 중복 정산 방지 필요 |
| Vanilla Traits Expanded | 낮음 | 특성 확장. Honor 특성의 의미/상충 및 Hegemony 지도자 효과 매핑 후보 |
| Alpha Memes | 낮음 | 신념·교리·구조 확장. 결투 규칙의 신청자 이념 우선과 특수 이념 콜백을 대조할 필요 |
| Vanilla Ideology Expanded | 낮음 | 밈·교리·역할·의식. Honor 맹세/의식과 주제 일부 중복하나 같은 정치 계약 체계는 확인되지 않음 |

## Contracts의 기능별 비교

| 기능 | Hegemony에서 확인한 범위 | Honor 현재/계획과 차이 |
|---|---|---|
| 봉신·영지 | 봉신 도시 개발, 구매/정복/해방, 지도자·시장, 반란 | Honor는 4단계 계약 계층·연합·개별 권리/채무·조건부 종주 후보를 다룸. 일부 이전은 제한된 주민/재고만 지원 |
| 공납·경제 | 식량/자재/번영 세금, 실물 물류, 공급/무기 계약 | Honor는 새 공납 조건 편집·NPC 응답·별도 서명·선택형 실제 납부를 연결. NPC 상납/징수관은 남음 |
| 협상·외교 | 자원/조약 조건 협상, 신뢰·불만·세력 강도, 사절, AI 협상 | Honor는 명예/의무/관계에 따른 영향력·체제별 표결 계획. 현재 단순 신규 제안 응답은 포괄 협상 AI가 아님 |
| 전쟁·점령 | 실제 원정·보강·정복/해방·시뮬레이션/관전 | Honor의 월드 턴·포위/항복·구원→봉신 확정은 대부분 추가 연결 필요 |
| 독립·반란 | 행복도/문화/노예 봉기·내전·선동 | Honor는 계약보호의 고의 거부 1회에 대한 행사형 정당한 독립, 기타 위반의 통지/시정, 채무 보존을 계획 |
| 명예·신뢰 | 협정 이행 신뢰, 불만, 우호도, Royalty 관련 서비스 | Honor 개인 Need·단계 하한 및 향후 비소비성 세력 명예와 동일하다고 볼 수 없음. 원인·책임·기준을 분리해야 함 |
| 개인 봉신·용병단 | 외부 실제 병력 지원 API는 확인 | Honor의 개인 서약/승계, 고용 시작금·최종 정산·연장·몸값 분담·배신 책임의 대체 기능은 확인되지 않음 |

## 코드에서 확인한 주요 충돌 경계

1. **도시 소유권**: Honor TerritoryAdministration/UnionPolitics는 native Settlement.SetFaction 경로로 이전한다. Hegemony API 가이드는 외부 호출자가 먼저 SetFaction한 뒤 이전 소유자를 추정시키지 말라고 명시한다. Hegemony 인구/저장고/정치 소속/여행/전쟁 기록까지 반영해야 하므로 단순 native 성공은 통합 성공이 아니다.
2. **플레이어 대리 세력**: 양쪽 모두 플레이어의 본래 소유권을 유지하며 별도 정치 대표를 다룬다. Honor 관리청/연합과 Hegemony realm proxy를 서로 다른 종주로 중복 해석하지 않도록 canonical identity 정책이 필요하다.
3. **공유 외교**: Honor는 TryAffectGoodwillWith/SetRelationDirect/Notify_GoodwillSituationsChanged를 감시·공유한다. Hegemony/WD2의 자체 조약·전쟁 장부는 native 우호도 변화만으로 완결되지 않는다. 중복 알림·재동기화·조약 외 전쟁 상태 위험은 추론이며 재현된 결함으로 기록하지 않는다.
4. **저장된 주민/재고**: Honor의 native saved inhabitants/stock 검사는 Hegemony의 별도 인구·물자 저장소까지 검증하지 않는다. 본래 목록이 비었다고 해당 도시가 비어 있다고 간주해서는 안 된다. Hegemony 인구에는 추상 주민과 실제 이름 있는 주민이 혼재할 수 있다.
5. **점령/도시 소실**: WD2/Hegemony가 도시를 대체·이전·해방할 수 있다. Honor의 제한된 leaf-town Destroy 후 종료 처리만으로 계층 전체의 정복/권리/채무를 판정할 수 없다.
6. **공납/운송**: 같은 흐름을 Hegemony 세금, Honor 공납, RimMarket 판매로 여러 번 계상하지 않도록 원인/계약/영수증 ID가 필요하다. 완료 확인 전 이행·명예를 주면 안 된다.
7. **설정·순서**: 로딩 순서 변경만으로 서로 다른 소유권/전쟁/경제 장부의 충돌은 해결되지 않는다. Hegemony가 WD2에 제공하는 연동이 Honor까지 연동한다는 뜻도 아니다.

## 실제로 존재하는 연동 기반

Hegemony public API 5에서 정적으로 확인한 진입점:

- `GetOverlord`, `IsPlayerVassal`: 실제 대표 관계 조회 후보.
- `SubscribeToEvents`, `ReadEvents`: 소유권·전쟁·원정 등 커밋 사건과 저장된 시퀀스 기반 재조회.
- `RegisterVeto`: 제공되는 입장 단계의 거부 사유. 이미 비용을 낸 여행 취소나 무제한 정치 규칙 등록은 아님.
- `TryDeclareWar`, `TryMakePeace`: Hegemony 장부/기존 외교 제한을 거치는 쓰기.
- `TryTransferSettlement`: native 소유권 변경과 Hegemony 기록 조정을 담당. 완료/미로딩/정지/비전투/유효 owner 및 종주 표현·수도/마지막 도시 제한이 있다.
- `TryGetOperationStatus`: 같은 provider/request ID로 불확실한 결과를 복구. false가 무변경을 뜻하지 않음.
- `TryContributeExternalForce`: 실제 현장 상단의 적격 성인 전투원/장비/식량을 기존 전투에 지원. 추상 병력값이나 원격 파견, 별도 정복 권한은 제공하지 않음.

가이드상 이벤트 저널은 최근 1024개, 전쟁/화평/소유권 영수증은 공통 4096개 한도다. 소비자가 cursor/request ID를 저장하고 누락 시 권위 상태를 재조회해야 한다. 콜백 중 변경 API 재진입은 허용되지 않는다. 이 API가 Honor의 명세상 모든 병력/봉신/독립 경로를 해결하지는 않는다.

## 의존성·버전

- Hegemony 로컬 About: Harmony + Jaeger's Utility Framework 필수. 1.6/1.7. 구 FT1 package와 명시적 비호환.
- WD2 로컬 About: Harmony + Vanilla Expanded Framework 필수. 1.6/1.7. 구 WD와 Visit Settlements 명시적 비호환.
- VPE 로컬 About: Harmony + Royalty + Vanilla Expanded Framework. 1.6/1.7 포함.
- RimMarket 로컬 About: Harmony. 1.6/1.7.
- **설치된 CE와 IIB의 About은 1.6만 지원하고 1.7을 선언하지 않는다.** IIB는 1.4/1.5/1.6 표기. 모음집에 1.7 태그가 있다는 이유로 모든 항목이 1.7을 지원한다고 볼 수 없다. 다른 배포/후속 업데이트 전체의 부재를 단정하지 않는다.
- 모음집의 9개를 Hegemony나 Honor의 일괄 필수 의존성으로 추가하지 않는다. 향후 별도 연동 모드를 만든다면 정확한 provider와 framework만 의존성으로 명시하고 다른 연동은 선택형으로 둔다.

## 제안하는 방향 — 아직 설계 변경으로 확정하지 않음

- Honor의 개인 명예/결투/사회/맹세와 계약상 의무·정당성·권리·승계는 독립 모듈로 유지할 가치가 있다.
- 독립 Contracts의 승인된 최소 흐름은 유지하되, 세계 경제·군대·인구 시뮬레이션을 Hegemony와 같은 규모로 더 확장하는 우선순위는 재검토할 가치가 있다.
- 선택형 Hegemony 연동에서는 Hegemony가 도시/경제/병력/전쟁의 실제 실행 주체, Honor가 계약/명예/위반·권리의 기록 주체가 되도록 명시적인 단일 작성자 경계를 설계한다.
- 연동 경로는 공개 API/버전 검사·동일 요청 재시도·사건 중복 방지·저장 후 재조정부터 구현하고, 불충분한 경로는 이유를 표시하며 보류한다. 원래 native 이전 코드로 조용히 되돌아가지 않는다.
- 이 검토만으로 개발 로드맵 폐기·필수 의존성 변경·상대 모드 코드 복사·호환 보장을 실행하지 않는다. 현재 Honor에는 Hegemony/WD2 전용 어댑터가 없다.

이번 검토로 단계별 구현 잔여 추정은 변경하지 않는다: 1단계 약50%(7~9묶음), 2단계 약85%(5~7), 3단계 약95%(6~8), 4단계 약90%(5~7), 5단계 약100%(6~8), 6단계 약70%(4~6). 외부 실행 제공자를 채택할 경우 연결 작업은 줄거나 늘 수 있으나, 실제 경계와 채택 범위를 정하기 전 할인하지 않는다.


