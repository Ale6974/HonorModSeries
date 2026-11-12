# 요구사항 상태 — 공개 0.2.1 / 호환성 0.2.2 / Contracts 개발판

## 0.3.100 — M2 부분 구현: 연합·작위 결정의 원 주민 등록과 연합 유휴 대기 집행

2026-11-12. 원 주민 등록을 일반 대표 전용에서 별도 현재 연합 의결/작위 지도자 결정으로 확장했다. 새 GovernmentDecisionV1은 원 charter/town/map/source/전체 Book hash/대표/원 명부 순서/선택 주민의 임상·의료 증거/시계에만 묶인다. ResidentRosterEnrollment/v1 scope는 진행 Added/Started/Verified를 바꿔도 같은 원 행동을 식별하되 다른 도시·인원·임상·계약·시계·행동으로는 재사용할 수 없다. native ReviewEnrollment는 실제 현재 연합 의석/대표·goodwill 또는 현재 대표 자신의 세력 작위/등급을 기존 검토된 NativeNpcContractAuthority에서 읽고 별도 결정으로 검토/확인한다. 각 List.Add 전과 최종 검증 전에 같은 현재 의석·대표·작위 결정을 다시 읽는다. 원 List/순서/같은 Pawn·임상·의료·별도 Started/Added/Verified와 reference-only Scribe·명시적 복구는 그대로다. 소속/건강/작업/병력 권한을 등록으로 추정하지 않는다.

대기 이전에도 별도 DeferredTownIdleBoundary/v1 결정과 원 charter/town/map/source/target/Book/원 인원·작업 관찰/기존 물리 journal ID prefix/시계를 저장한다. 적격 연합 도시는 실제 대기→현재 결정 재검토→native 유휴/예약 없음→별도의 새 등록 결정/전체 이전 검토→기존 실제 Apply를 실행한다. 대기 의결을 등록 의결로 재사용하지 않는다. 진행 job/driver/queue/예약은 native가 계속 처리/저장하며 종료/복사/재시작하지 않는다. 의석·대표·작위/결정/범위 변경은 원 기록을 보존하고 대기를 중단한다. Started는 자동 재시도하지 않고 독립 Applied 영수증을 검증한다. 귀족 source의 기존 소속 이전 제한은 유지하며, 무한히 대기시키지 않도록 native 지원되지 않는 source/target의 신규 대기 준비를 거부한다. 작위 결정의 실제 등록은 가능하지만 귀족 세력 전체 이전까지 완료했다고 부르지 않는다.

새 ResidentGovernment는 null이면 옛 일반 대표 Consent를 그대로 요구하고, nonnull이면 정확한 canonical 현재 scoped collective/royal 결정만 검증한다. 일반 대표를 꾸미거나 Royal/Union을 Ordinary로 다운캐스트하지 않는다. GovernmentDecisionV1은 optional null-omitted이며 옛0.3.96 enrollment/deferred 문서의 JSON bytes/기존 proof와 일반 전용 제한은 그대로 유지한다. 각 원 journal hash와 기존 Scribe/PostLoad/Finalize·명시적 중단 복구가 새 결정을 보존한다. 완료 기록은 원 역사적 결정의 scope를 검증하고 native 원 참조만 기존 방식으로 해제한다. unknown started append는 여전히 재실행하지 않는다.

GovTests는 연합 현재 의결→실제 연결한 append 규칙의 시작/정상 반환→일부 저장 복원→최종 Verified, 귀족 작위/대표, 대기와 등록 행동 간 재사용 거부→별도 wait 결정→Started→독립 실제 이전 영수증 링크와 복원을 검사한다. 의석/대표/작위/임상/의료/계약/목적지 변화, old/null 권한 확대, 불확실 append/Started 재실행, 이미 완료한 journal 재사용을 거부하고 옛 일반/null enrollment·wait JSON bytes 불변을 확인한다. 기존 mapped clinical/census·임상/수술/의료·실제 custody 회귀도 유지한다. 현재 설치5005 DLL SHA25682387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125가 감사본과 같음을 확인했으며 새 native API/unknown1.6 몸체 권한은 추가하지 않았다.

최종 검증: .local/m2-100-build16.log 및 .local/m2-100-final17.log 양 타깃 전체 빌드·규칙·XML/서식 검사 통과. Honor20,093/Contracts5,650(합25,743),영한589키/인자 일치,컴파일 경고·오류0. 패키지 artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.100.zip SHA256E2A195F82A08BF9EB791335ED3FB69C68F8B2CA2C45FE5081B8A43141515FB49. 기존 로컬 Preview는 .local/contracts-preview-c8c9e5b270d74132bc8ec9edc9913b80에 백업하고32파일 해시 일치·활성화/언어 불변으로 설치했다(.local/m2-100-install.log).

남은 범위: 소실·재생성 원 지도/주민/자산의 복합 복구, 귀족 source 소속 이전, 전쟁·개인/군사 권리·일반 진행 작업/예약의 소속 변경 너머 보존·커스텀 holder 및 미지원 역할 조합. 이들을 등록/대기 승인으로 허용하거나 기존 보호 조건을 지워 완료로 처리하지 않는다. M1 10/10,M2 전체 미완료0/18,전체10/100;1단계 잔여31.5%/8묶음,2~6단계 잔여80/95/90/100/70%,묶음5~7/6~8/5~7/6~8/4~6. Preview Steam 게시·외부 자동 예약 없음.

## 0.3.99 — M2 부분 구현: 다른 목적지의 현 종주와 두 구간 인계 복구

2026-11-12. TransitDestinationV1은 현재 새 종주의 소속이 원 target과 다를 때 원 인계를 중간 구간으로 마치는 별도 행동별 승인을 추가한다. 새 종주·모든 상위/연결 제삼자·원 source와 원 target 대표가 정확한 Book/payment/physical/foreign-rights/최종 목적지 범위에 동의해야 한다. 원 target이나 원 clinical/map/자산/호출 journal을 새 목적지로 덮어쓰지 않는다. 원 구간은 기존 실제 주민/자산/도시 호출·독립 정상 반환·명시적 복구로 완료한다. 불명확한 원 호출은 여전히 보류하고 target 소유만으로 완료하지 않는다.

원 구간의 독립 Applied/native 정상 반환을 확인한 뒤 창의 ‘현 외부 종주로의 별도 후속 인계 검토’에서 새 현재 계약·대표·임상·주민 census/의료/수술/영역·지도/재산 증거를 검토하고 확인한다. Native ReviewContinuation은 같은 원 도시 참조·마지막 실제 완료 이력·원 임상/자산/도시 완료 사실과 현재 새 NPC 계약을 확인하고, 기존 CaptureForTransfer/LoadedMapRecords.Capture/PrepareCareJournal/MapAssetRecords.Prepare를 이용한다. 새 현재 증거를 옛 의료 reset으로 쓰지 않으며 이미 체결된 현재 계약에 근거한다. 새 계약 조건을 임의 생성하거나 옛 계약을 재활성화/해약/재서명하지 않는다.

후속 TerritoryTransfer.PreviousTransferV1은 원 Applied ID/같은 town/baseline/이전 target=새 source/정상 시계를 별도로 저장한다. PrepareForeignContinuation은 원 Prepared/Destroyed/다른 도시·소유자 이력에 연결할 수 없다. 새 actual reference/census/care/asset/city 호출과 현재 CustodyAgreement를 모두 사전 staging하고 callback 없는 대입으로 연결한 뒤 기존 Recover를 실행한다. 새 승인은 이전 구간 완료를 다음 호출 권한으로 재사용하는 것이 아니며 각 구간에 독립 원본/권한이 있다. 기존 Scribe/PostLoad/Finalize/explicit recovery와 terminal archive에 두 구간을 보존하고, 후속 승인 marker가 없으면 옛 복구/취소 경로로 우회하지 못한다. 플레이어 native 소속/식민지 지휘·기존 계약/하위 권리·채무/납부 실적은 변경하지 않는다. 중단은 현재 Prepared 구간에서 복구하며 이미 완료된 첫 구간은 재실행하지 않는다.

옛0.3.97/98 null JSON/Scope/권한은 유지한다. Transit은 별도 TransitCustodyCompletion/v1이고 후속 record는 optional null-omitted PreviousTransferV1이다. 현재 설치5005 DLL SHA25682387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125가 직전 감사본과 같음을 확인했다. 새 native API/검토하지 않은1.6 몸체 권한은 추가하지 않았다. 기존 provider/quest/평화/마지막 source 도시/clinical·복잡 holder·진행 작업/예약 제한을 유지한다.

검사는 실제 원 signed root→부분 asset 반환→해약→다른 목적지 NPC 계약 서명→별도 transit 승인/현재 원 target 대표 포함→첫 구간 정상 완료→fresh 별도 원본/다른 target의 후속 준비→archive/물리 journal 저장 복원→독립 두 번째 asset prefix/정상 완료→전체 이력 복원을 연결한다. 원 구간 JSON 불변, 계약·하위 권리·납부/미납 불변, 첫 구간 미완료의 후속 준비 거부와 서로 다른 승인/목적지·호출 scope를 검사한다.

최종 검증: .local/m2-099-build16.log/최종 .local/m2-099-final17.log 전체 양 타깃 빌드·XML/영한 검사, .local/m2-099-final-rules.log 최종 규칙, .local/m2-099-final-native16.log/17.log 마지막 양 native 컴파일 통과. Honor20,093/Contracts5,624(합25,717),영한587키/인자 일치,최종 양 native 경고·오류0. 패키지 artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.99.zip SHA256E2ECDEA7F52D6A6C0D9F1585F7E947195AE9A6670AEB559B0454AB7F025A1C97. 기존 로컬 Preview를 .local/contracts-preview-541d99557349408cab83cd849824e84c에 백업하고32파일 해시 일치·활성화/언어 불변으로 설치했다(.local/m2-099-install.log).

아직 미완료: 소실/재생성된 원 지도·주민·자산의 복합 복구, 전쟁·개인/군사 권리·일반 진행 작업/예약 및 커스텀 holder 조합. 이번 후속 인계도 기존 지원 가능한 원 물리 상태와 현재 평화/마지막 기지 보존이 필요하며 원 객체를 만들거나 상태를 초기화하지 않는다. 두 구간 사이의 후속 검토는 별도 명시적 확인이며 자동 재계약/자동 강제 인계가 아니다. M1 10/10,M2 전체 미완료0/18,전체10/100;1단계 잔여31.5%/8묶음,2~6단계 잔여80/95/90/100/70%,묶음5~7/6~8/5~7/6~8/4~6. Preview Steam 게시/외부 예약 없음.

## 0.3.98 — M2 부분 구현: 현재 외부 종주 권리를 유지하는 원 인계 복구

2026-11-12. 새 ForeignRightsV1은 원 계약이 종료된 뒤 새 NPC 종주 계약이 유효한 경우를 별도로 처리한다. 원 외부 계약 ID·liege/subject·서명/조건/권리 전체 hash와 현재 실제 소속을 원 CustodyAgreement의 Book/payment/physical/census/map/호출 증거에 묶는다. 별도 행동명 ForeignMasterCustodyCompletion/v1에 현재 source/target·새 종주·전체 연결 제삼자와 상위 계약 당사자의 대표 결정 또는 해당 연합/귀족 결정을 검토한다. 가장 가까운 종주의 허가만으로 상위 종주의 권리를 우회하지 않는다. 현 상위 계층 전부가 같은 원 목적 세력에 있어야 하며 전체 4단계 한도와 기존 topology/현재 소유자 검증을 유지한다.

실제 CurrentCustodyWitness→검토/확인→기존 Recover→각 주민/자산 호출 전→도시 호출 전후에 새 권리 범위를 다시 검토한다. 이 별도 합의는 플레이어가 현재 그 세력의 봉신이라는 권한에 의존하지 않고, 현재 외부 종주와 모든 당사자가 승인한 같은 원 target만 인계한다. 플레이어 native 소속·식민지 통제는 변경하지 않으며 새 종주 계약·하위 계약을 해약/교체/재서명하지 않는다. 이미 정상 반환한 원 호출 prefix와 기존 원 임상/의료/수술/영역/재고·채무·납부 이력은 그대로 유지한다. 도중 계약/대표/공납이 변하면 옛 승인을 재사용하지 않고 새 전체 검토가 필요하다.

저장: optional null-omitted ForeignRightsV1을 기존 bounded128/32M 승인 archive에 포함한다. 외부 권리 없는0.3.97 문서의 직렬화 bytes/옛 Scope/hash는 그대로이며 null/옛 승인에 외부 종주 권한을 소급하지 않는다. 새 전체 권리와 별도 Scope는 archive proof에 포함하고 원 execution reference/latest marker·Scribe/PostLoad/Finalize·terminal 원 transfer와 연결한다. 반환이 불명확한 호출은 소유권만 보고 재실행/완료하지 않는다. 기존 물리 보존·평화·마지막 도시·provider/quest/배상/인원·자산 가드를 제거하지 않는다. 현재 DLL SHA25682387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125가 직전 검토본과 같음을 확인했다. 새 native API는 추가하지 않으며 검토5000/5005 범위와 미검토1.6 몸체 보류를 유지한다.

새 검사 흐름은 실제 원 이전 준비/일부 자산 정상 반환→상위 계약 해약→새 NPC 종주 서명→독립 현재 승인→저장/중단 복원→같은 원 이전 완료/terminal archive 복원이다. 유효 하위 계약·새 종주 계약·기존 미납 및 납부 실적을 그대로 검증한다. 상위 4단계 권리, 누락된 종주/상위 당사자, 다른 목적 세력, 승인 거부/철회, 무관한 계약 hash, 현재 대표/연합 scope/귀족 대표 변경, null 옛 권한 확대 및 legacy bytes 회귀를 검사한다.

최종 검증: .local/m2-098-build16.log 및 .local/m2-098-final17.log 양 타깃 전체 빌드·규칙·XML/양식 검사 통과. Honor20,093/Contracts5,614(합25,707),영한584키/인자 일치,양 타깃 컴파일 경고·오류0. 패키지 artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.98.zip SHA256B1680EA11129B7141274BA3E0AAA1CF891EA3866C95FBE5727B7D2830415AA8E. 기존 로컬 Preview를 .local/contracts-preview-6e35c7431beb4a70a252555a8cd10828에 백업하고32파일 해시 일치·활성화/언어 불변으로 설치했다(.local/m2-098-install.log).

범위: 새 종주의 실제 소속과 SAME 원 목적지가 일치하는 인계의 실제 실행·저장·중단 복구를 연결했다. 새 종주가 다른 목적지를 요구하는 경우의 별도 계약 재편/해약과 물리 목적지 변경은 원 임상/호출 journal을 바꿔 처리하지 않으며 보류한다. 소실·재생성된 원 지도/주민/물품, 미확인 정상 반환, 달라진 임상/재고, 개인/병력 권리·일반 진행 작업/예약·커스텀 holder의 전체 복합 복원도 남아 있다. M1 10/10,M2 전체 미완료0/18,전체10/100;1단계 잔여31.5%/8묶음,2~6단계 잔여80/95/90/100/70%,묶음5~7/6~8/5~7/6~8/4~6. 작업군 전체 점수와 실제 부분 실행 구현을 구분한다. Preview Steam 게시·외부 예약 없음.

## 0.3.97 — M2 부분 구현: 종료 계약·제삼자 현재 승인과 원 custody 중단 복구

2026-11-11. 새 CustodyAgreements는 원 Prepared/CityCallJournalV1 이전의 동일 ID·계약·source/target·원 임상/census·지도/재산 scope에 현재 계약 Book·공납 장부·전체 연결 당사자와 현재 대표의 별도 행동별 승인을 묶는다. 원 계약이 Released/TerritoryLost/Replaced/LawfullyEnded인 경우에도 기존 계약을 되살리거나 새 봉건 계약을 만들지 않는다. 옛 상위 당사자, 현재 활성 하위 계약과 연결 당사자 전체, 원 source와 target의 현 대표를 확인한다. 일반 대표 결정 및 연합의 해당 범위 투표/귀족의 현 작위 결정을 별도로 재검토한다. 새 외부 종주의 권한을 덮어쓰지 않으며 경쟁 제안/미결제 배송/불명확 호출/바뀐 물리 상태는 보류한다.

실제 도시·전체 이전 창의 새 현재 승인 검토/확인은 기존 Recover에 연결된다. 새 승인 경로는 원 source에서 아직 호출하지 않은 항목도 forward-only로 기존 ResidentExecution.Apply/MapAssetRecords.Apply/Settlement.SetFaction에 연결한다. 각 주민·자산 호출 전과 도시 호출 전후에 현재 Book/공납/대표·행동별 결정 및 원 참조를 재검토한다. 기존 원 의료/영역/수술·권한 호출/자산 호출의 독립 시작/정상 반환과 원 물리 ledger는 덮어쓰지 않는다. 원 positive prefix만 계속하며 unknown started call은 현재 target 소유여도 다시 호출하거나 완료하지 않는다. 승인은 원 객체·의료·재고를 복원하는 새 허가가 아니다.

새 bounded128/32M 승인 archive와 실행 reference의 optional marker를 Scribe하고 PostLoad/Finalize에 원 transfer·fingerprint·승인 hash·최근 marker·시계 및 계약 서명/조건·기존 채무/납부 실적을 대조한다. malformed raw를 유지한다. 원 physical Prepared/Applied 이력과 terminal 참조 해제 뒤에도 원 임상/지도 snapshot 연결을 보존한다. 현재 Book/공납 또는 대표가 바뀌면 옛 승인을 재사용하지 않고 전체 새 검토가 필요하다. 승인 기록은 옛 계약의 부활·채무 삭제·제삼자 소속/군사/재산 권한 확대를 하지 않는다. UI는 실제 당사자/세력/대표 이름과 영한 안내를 제공한다.

검사: 양 타깃 전체 빌드 및 규칙 검사를 수행한다. 새 CustodyAgreementTests는 실제 서명→원 loaded asset 이전 준비→일부 정상 반환→상위 계약 해약→현재 제삼자 승인→저장/복원→남은 원 호출/완료 흐름을 검사한다. 유효 하위 계약과 종료된 채무 계약의 이미 낸 공납/미납 잔액을 유지하며 누락/거부/바뀐 당사자·대표·범위, 옛 연합 투표 재사용, 귀족 작위 누락, 변조된 승인 archive, unknown 호출 재실행을 거부한다. native 몸체 허용 범위는 기존 검토5000/5005만 유지하고 미검토1.6 새 몸체는 보류한다. 게임/엔진 실행 없음, Preview Steam 게시/외부 예약 없음.

최종 검증: .local/m2-097-build16.log 전체1.6 빌드/규칙, .local/m2-097-final17.log 전체1.7 빌드/규칙, .local/m2-097-final-native16.log 마지막 native1.6 컴파일 통과. Honor20,093/Contracts5,592(합25,685),영한583키/인자 일치,최종 양 native 경고·오류0. 패키지 artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.97.zip SHA256CB9CCFB211E3F30194346B2B9FAB7FBC22B49FA16D0F896D7A0404828B9A6092. 기존 로컬 Preview는 .local/contracts-preview-9ad164421a0a4c30a87100e45613ff55에 백업하고32파일 해시 일치·활성화/언어 불변으로 설치했다(.local/m2-097-install.log).

한계와 다음 착수점: 이번 실제 복구는 SAME 원 도시/지도/주민/물체와 원 물리 상태가 검증되는 진행 중 custody에 대한 새 현재 승인이다. 소실·재생성된 지도/인물/물체, 달라진 임상·재고, unknown native 반환, 새 외부 종주가 있는 복합 재탈환의 전체 집행, 개인·병력 권리와 일반 진행 작업/예약의 소속 변경 너머 보존, 왕정/연합 새 등록 및 일반 커스텀 holder는 남아 있다. 원 데이터를 재생성/초기화하거나 보류 가드를 제거하지 않는다. M1 10/10,M2 미완료0/18,전체10/100;1단계 잔여31.5%/8묶음,2~6단계 잔여80/95/90/100/70%,묶음5~7/6~8/5~7/6~8/4~6. 부분 실행 구현과 작업군 전체 완료 점수를 구분한다.

## 0.3.96 — M2 부분 구현: 명부 밖 민간인 등록·작업/예약 보존 대기와 실제 이전

2026-11-11. 두 개의 새 실제 실행 경로를 연결했다. 이 변경으로 M2 전체를 완료한 것으로 판정하지 않는다.

1. ResidentEnrollment: 같은 원 도시 지도에 있지만 native previouslyGeneratedInhabitants에 없는 적격 source 민간인은 기존 현 일반 세력 대표의 NpcRealmDecision.Read/Consent와 원 active single-city charter·검증된 실제 소유자·원 전체 Book hash를 별도 등록 결정에 묶는다. UI가 대상 이름과 현 대표를 제시하고 확인을 받은 뒤 같은 원 native List에 같은 Pawn 참조만 append한다. 기존 roster 순서/참조, 원 Pawn/Map/source/ruler, 임상11 건강·장비·관계·필요·유전자/이념·영역 및 의료 설정을 보존한다. 소속/건강/장비/의료/영역/일자리를 변경하거나 Pawn·원 명부를 재생성하지 않는다. 각 append는 별도 Started/Added 정상 반환, 마지막 clinical 재검증은 별도 Verified로 저장한다. 모든 append가 반환돼도 Verified 없이는 완료/참조 해제/예약 해제를 하지 않는다. unknown started append나 바뀐 prefix/대표/Book/임상/참조는 원 raw와 참조를 보존하고 재실행하지 않는다. reference-only Scribe·PostLoad 구조·Finalize 원 ref/시계 검사와 명시적 복구를 연결했다. 등록 이후 새 전체 CT 검토는 기존 실제 map/clinical11/census/독립 소속 호출·의료/영역/수술/stock/자산/도시/전체 법적 후손 경로에 같은 Pawn들을 그대로 포함한다. 기존 옛 profile/census/명부 요건을 새 등록 권한으로 소급하지 않는다. 등록 자체는 별도 소속 이전 권한이 아니다. 왕정/연합·병력/Lord/진행 작업/소유 침대·특수 역할 등은 현재 새 등록의 범위가 아니다.

2. DeferredTownTransfer: 원 charter/town/map/source/target/현재 일반 대표·Book hash·기존 물리 ledger ID prefix·전체 기존 roster/지도의 source 인원 ID와 원 work ID/physical observation을 저장하는 명시적 대기 의도를 추가했다. 진행 job/driver/queue/예약은 게임이 계속 처리하고 저장하며 모드는 StopAll/EndCurrentJob/ClearReservation/StartJob, task 복제·재시작·원 job/driver 중복 deep-save를 하지 않는다. 250tick마다 원 범위를 검토하고, 실제 curJob/curDriver/대기열/이동이 없고 다섯 native 예약 manager가 비어 있으며 기존 전체 조건을 만족하는 시점에만 현재 임상·현재 대표·전체 stock/map/계약을 새로 검토한다. 새 민간인 등록이 필요하면 위 실제 등록을 먼저 완료하고 다시 전체 이전을 검토한 뒤 기존 Apply로 실제 이전한다. 대기 동안 자연히 진행한 건강·작업을 옛 의료 snapshot으로 되돌리지 않으며 원 work observation은 진단/범위 이력일 뿐 복원 권한이 아니다. 기존 native 이전의 원 clinical snapshot을 고치는 것도 아니다. 안전한 유휴 시점이 없는 작업을 강제로 중단하지 않으므로 이 경로는 진행 중 job을 faction-change 너머로 재구축하는 일반 실행기가 아니다.

대기 의도의 Waiting/Started/Applied/Stopped/Cancelled를 별도로 Scribe한다. current owner/contract/source 대표/원 인원/ledger scope가 달라지거나 도시/지도가 소실되면 원 증거를 남기고 대기를 중단한다. Started는 자동 재시도하지 않는다. 기존 등록·물리 이전 journal로 먼저 복구하고, 유일한 새로운 독립 Applied 영수증의 원 charter/town/source/target/시계가 맞을 때에만 별도 완료 링크를 만든다. 단지 target 소유라는 이유로 완료하지 않는다. 등록 뒤 전체 이전이 준비되지 못했다면 스케줄 의도만 중단하여 실제 등록/이전 장부를 직접 복구할 수 있게 하며, native 이전을 취소/완료했다고 기록하지 않는다. 기존 기록 전체가 끝나기 전에는 서로 다른 인물 실행이 동일 Pawn을 예약하지 못한다. 이 대기는 게임 내 동작이며 삭제된 외부 자동 예약을 다시 만든 것이 아니다.

설치 게임은1.7.5005 rev1082로 확인했다. Assembly-CSharp SHA25682387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125/MVID3523c04c-c298-43ae-9571-20caaef22acc. 현재 Pawn/MapPawns/JobTracker/PathFollower/MindState, dynamic components/settings/needs/relations, Thing/Building/Map/WorldObject/Settlement, 다섯 예약 manager, 건물/repair/meditation/tag 등록부·MapEvents·storage/group, Quest/QuestUtility/QuestManager·map mesh·style/quality·Casket/Crate/Hackable/Glower/Minified의 전체 metadata 소스를 기존 검토본과 대조해 동일함을 확인했다. 잘못 지정한 namespace/타입 및 임시 parser 실패 결과는 확인 근거로 사용하지 않았고 올바른 native 타입으로 다시 읽은 결과만 사용했다(.local/m2-096-current-*.cs 및5005-audited-*.cs). 현재 Job.GetUniqueLoadID는 저장 loadID만 읽고 JobQueue.Count는 원 목록 Count만 읽는다. JobTracker.ExposeData는 원 curJob/curDriver/jobQueue를 native deep-save하며 모드가 이 구조를 중복 저장하지 않는다. NativeTransferBodies는 검토한5005를 명시적으로 추가하며 새 work 관찰/대기 실행은5005만 허용한다. 새 등록은 기존 검토5000/5005 원 civic read 경계에서만 가능하다. 원4995 civic/4998 asset whitelist는 각자 유지한다. 미검토1.6 새 native 몸체는 보류하고1.6/net472·1.7/net10 분리 빌드와 기존 경로는 유지한다.

최종 검사: .local/m2-096-release16.log의 전체 규칙/양식 검사와 .local/m2-096-final-native16.log의 마지막 native1.6 컴파일, .local/m2-096-final-native17.log의 최종1.7 전체 빌드/검사가 통과했다. Honor20,093/Contracts5,567(합25,660),영한579키/서식 인자 일치,마지막 양 native 컴파일 경고·오류0. ResidentEnrollmentTests는 실제로 연결한 등록의 source/대표/Book/원 명부 순서·중복·임상/의료·개별 시작/정상 반환 prefix·마지막 Verified/불확실 append 거부·기존 mapped census 연결을 검사한다. DeferredTownTransferTests는 원 work observation·인원/계약/대표 변화·Waiting 취소·Started 재실행 거부·별도 실제 Applied 영수증/old receipt/다른source/target/계약/시계 거부와 저장 복원을 검사한다. 새 명부/대기 인원 조회는 raw native MapPawns.pawnsSpawned와 원 map owner/타입/참조만 직접 검토하며, 임의 holder를 거치는 recursive AllPawns를 먼저 호출하지 않는다. 전체 known holder 검토는 기존 실제 map 실행 경로에서 그대로 한다.

패키지 artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.96.zip SHA256F1FB84AB5A28C047F83D5F49A74F020C4F5B37C117EE53184A5F17DDACFB2CB3. 기존 로컬 Preview를 .local/contracts-preview-0ac62c08a0504acb99723df403d72199에 백업하고32파일 해시 일치·활성화/언어 불변으로 설치했다(.local/m2-096-install.log). 새 안내 docs/M2-RESIDENT-TRANSFER.md와 영한 UI를 제공한다. Preview Steam 게시 없음. 다음 실제 착수점은 진행 중 physical custody와 원 계약 종료/제삼자 권리 변경이 겹친 복합 소실·귀환을 새 행동별 현재 권한과 원 per-call/clinical/asset 이력에 묶는 실행 경로다. 현재 보류 가드를 제거하거나 원 임상·재고를 초기화하지 않는다.

남은 실제 구현: 복합 영지/지도 소실·재탈환 뒤 원 계약이 종료되거나 제삼자/개별 권리가 달라진 진행 중 physical custody를 새 현재 권한으로 안전하게 이어가는 전체 경로, 일반 진행 job/예약의 소속 변경 너머 직접 보존, 왕정/연합의 별도 새 등록 mandate, 일반 커스텀 holder 및 미지원 역할 조합이다. 원 객체를 만들거나 old contract/unknown calls를 재해석해 완료하지 않는다. M1 10/10,M2 전체 미완료0/18,전체10/100,1단계 잔여31.5%/8묶음,2~6단계 잔여80/95/90/100/70%,묶음5~7/6~8/5~7/6~8/4~6. 이 점수는 작업군 전체 완료 기준이며 실제 부분 구현이 없다는 뜻이 아니다. Preview Steam 게시·외부 자동 예약 없음.


## 0.3.95 — M2 부분 구현: 소유 중첩 보관 계층·포장 물체 자체의 실제 이전

2026-11-11. 새 OwnedHolderForestV1=1/StockFormat20은 기존 원 Map/모든 Thing/holder/slot/내용물 증거와 각 물체 자신의 source 권한을 묶어, 정확한 기본 소유 중첩 Casket/Crate와 소유 MinifiedThing 자체를 실제 기존 SetFaction·독립 자산별 시작/정상 반환·도시 호출·reference-only Scribe·명시적 중단 복구에 연결한다. 새21은 같은 범위와 기존 mapped clinical11 주민을 함께 처리한다. 원 도시 전체 후손의 법적 검토/의료·census/영역/수술/원 Pawn 참조와 물리 journal도 같은20/21을 사용한다. 옛3~19/null bytes·native 보류 조건은 새 권한으로 확대하지 않는다.

각 보관함·포장·내부 재산은 자기 원 소유자를 검토하며 부모의 권한으로 자식의 private/foreign 소유권을 추정하지 않는다. 원 positive normal-return prefix만 target 소유를 source 원 증거로 정규화하며 unknown started 호출은 target 소유여도 재실행/완료하지 않는다. 원 건강·품질/style·stored known/signal/hack/glow 설정·모든 원 slot과 부모/child holdingOwner·참조를 유지한다. 열기/CanOpen/lazy hackable 초기화/InnerThing setter/SplitOff/꺼내기/넣기/재포장/운반/재생성/체력 수리·중복 native deep-save는 하지 않는다. 원 경로에서 새 보관 계층이 안쪽에서 선택된 경우 바깥쪽 물체도 다시 새 경계로 검토한다. quality/style의 실제 comp 목록과 native cached refs를 대조하고 바뀌거나 알 수 없는 상태는 원 raw를 유지하며 보류한다.

현재 DLL SHA256D55AA8E47FA3C5463CA85B0ECCD51F195B4BBCD04F56DA3AF21C2F6688A81254가 직전과 같음을 확인했다. metadata로 읽은 현재 RimWorld.MinifiedThing에는 SetFaction override가 없고 Thing.SetFaction을 상속한다. 원 GetDirectlyHeldThings/get_InnerThing은 원 container 및 slot0만 읽으며 setter/SplitOff는 물품을 이동·파괴할 수 있으므로 호출하지 않는다. ExposeData는 원 innerContainer의 native deep-save를 이미 담당하므로 모드 장부에서는 같은 원 객체의 reference만 남긴다. QualityUtility.TryGetQuality가 package.InnerThing의 compQuality를 읽으므로 전체 원 자식 계층 검토 이후 알려진 unpatched getter/quality dispatch와 실제 comp cache를 확인한다. 기존5000/6c268a95-d1a8-4d6f-ad81-e5dcf6949c0e body 경계만 새 native 실행을 허용하며 미검토1.6 몸체는 보류한다. 양 버전 빌드/기존 경로는 유지한다.

최종 검사: .local/m2-095-release16.log/release17.log 양 타깃 전체 빌드 통과(Honor20,093/Contracts5,511,영한561키/인자 일치,컴파일 경고·오류0). OwnedHolderForestTests는 실제 계층 ID/슬롯·빈/중첩 원 범위·각 자산의 원 source·독립 부분 호출/unknown 재실행 거부·옛 scope/marker 확대 거부·설정 변경 거부·20/21 Prepared/Applied/Cancelled 복원을 검사한다. 기존 OwnedContainer/repair/civic/전체 lawful follower 회귀도 통과했고 LawfulFollowerMigrationTests에21의 원 clinical census/별도 asset calls/전체 법적·물리 복원을 추가했다. 패키지 artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.95.zip SHA25644851A7F1790D013B67371C21597393ECDD73EED307DB74758E1696A36C9C54B. 기존 로컬 Preview는 .local/contracts-preview-279354a99ca64b298d238e9b39f7d0a6에 백업한 뒤32파일 해시 일치·활성화/언어 불변으로 설치했다(.local/m2-095-install.log). Preview Steam 게시 없음. 다음 실제 착수점은 진행 작업/예약·개별 권리,원 도시 명부와 별칭이 없는 지도 시민의 원 권한/임상/호출/Scribe 복구,일반 커스텀 holder와 복합 소실·재탈환이다.

M1 10/10,M2 전체 미완료0/18,전체10/100,1단계 잔여31.5%/8묶음,2~6단계 잔여80/95/90/100/70%,묶음5~7/6~8/5~7/6~8/4~6. 진행 작업/예약·개별 권리/병력,원 명부 alias 없는 시민,일반 커스텀 holder 및 복합 소실·귀환은 실제 구현이 더 필요하다. source/current representative/마지막 도시/provider/quest/평화/채무/제삼자 권리 가드는 유지하며 미지원 상태를 삭제·초기화하지 않는다. 자동 예약/Preview Steam 게시 없음.


## 0.3.94 — M2 부분 구현: 소유 상자·보관함의 실제 이전과 원 내용물 보존

2026-11-11. 새 OwnedContainersV1=1/StockFormat16은 검토한 원 세력 소유의 정확한 기본 Building_Casket/Building_Crate를 실제 기존 Building.SetFaction·개별 자산 시작/정상 반환 journal·도시 호출·Scribe·명시적 복구에 연결한다. 새17은 같은 범위와 기존 적격 mapped clinical11 주민의 동시 이전이다. 기존 원 주민 census/의료/영역/수술/개별 호출과 자산별 호출은 독립적으로 유지하며, 주민→자산→도시 순서와 원 모든 Map/Pawn/Thing 참조 검사를 그대로 사용한다. 옛3~15/null bytes·중립 Capture·옛 권한은 확대하지 않는다.

CaptureOwned는 별도 native 경로다. 원 ThingOwner<Thing>/Owner/각 child.holdingOwner, 모든 원 슬롯/물품/중첩 forest와 contentsKnown/openedSignal/기존 hack·glower 저장 설정, 원 HP/품질/style/원 최대 내구도·repair 등록을 대조한다. 내용물은 기존 적격 중립 물품만 지원하며 부모의 소유권으로 내용물의 private/foreign 권한을 추정하지 않는다. 소유 컨테이너는 ROOT만 허용한다. 원 저장 물품을 열기·꺼내기·재포장·운반·재생성·소속 변경하지 않으며 보관함/물품/owner를 중복 deep-save하지 않는다. 기본 Styleable/Quality/Hackable/Glower 외 컴포넌트와 진행 해킹 lastUser/autohack은 보류한다. 원 comp/def/parent와 native style/quality cache도 검토한다. 원 asset prefix의 정상 반환 없이 target 소유만으로 성공을 추정하지 않고 unknown started 호출은 재실행하지 않는다. 슬롯·내부 물품·해킹 설정·원 참조가 달라지면 원 기록을 보존하고 보류한다. 손상 건물/선반/whole owned group 및 적격 주민과의 조합은 같은 별도 marker를 사용한다.

추가18/19: HeldAssetsV1=1은 원 검토 보관 forest 안의 세력 소유 기본 Thing/ThingWithComps/Building을 각각 실제 SetFaction 대상에 포함한다. 원 ROOT 또는 HELD 위치·부모/순서·전체 물품 참조와 해당 물품 자신의 source 권한을 묶으며,19는 별도 clinical11 주민과 함께 처리한다. 옛16/17 이하에는 held 권한을 소급하지 않는다. 원 held 물체는 같은 container에 계속 남고 호출은 배치·꺼내기·재포장 없이 기존 unspawned Thing/Building.SetFaction을 사용한다. 현재 native unspawned Building.SetFaction은 지도 건물 registry 분기를 실행하지 않으며 Thing.SetFaction의 quest tag 경계는 기존 무quest/patch 가드로 검토한다. 건강도·원 품질/style은 그대로이며 새 내부 물품의 normal-return도 원 ordered asset prefix에 따로 남긴다. 소유 held Casket/Crate·Minified holder 자체/커스텀 holder·개인/foreign 재산은 아직 보류한다. 지도상 객체의 faction 변경을 근거로 held 호출을 완료하지 않는다. 기본 held 소유 물체만 지원하고 일반 holder 전체 완성은 아니다.

현재 설치 DLL SHA256D55AA8E47FA3C5463CA85B0ECCD51F195B4BBCD04F56DA3AF21C2F6688A81254는 직전과 같다. 현재 Casket/Crate/Hackable/Glower 전체 metadata 소스가 기존 검토 파일과 동일함을 확인했다(.local/m2-094-*.cs). Casket/Crate에는 SetFaction override가 없고 검토된 Building.SetFaction을 상속한다. 그 호출은 보관함 Open/CanOpen/EjectContents/CompSignal/내용물 변경을 실행하지 않는다. CanOpen의 lazy hackable 초기화는 호출하지 않는다. 새 소유 컨테이너 native 실행은 검토한5000/MVID6c268a95-d1a8-4d6f-ad81-e5dcf6949c0e만 허용한다.1.6/net472의 고정 참조 빌드와 기존 경로는 유지하며 미검토 native 몸체를 허용하지 않는다. source/current representative/마지막 도시/provider/quest/평화/채무/제삼자 권리·무진행/무예약 가드는 그대로다.

최종 검사: .local/m2-094-final16.log/final17.log 양 타깃 빌드 통과(Honor20,093/Contracts5,478,영한560키/인자 일치,컴파일 경고·오류0). OwnedContainerTests는 원 다중 슬롯/중첩 물품/빈 보관함/별도 held 재산,원 호출 부분 prefix/unknown 재실행 거부,marker 누락·변조/옛 scope 확대 거부,설정·순서 변경 거부,Prepared/Applied/Cancelled 복원과 mapped 동시 범위를 검사한다. LawfulFollowerMigrationTests는16~19 경로의 전체 법적 후손/별도 clinical census/물리 장부/원 map 복원을 검사한다. 패키지 artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.94.zip SHA256C0916DF3265DB1B20709B16EA98271BD640557887A43A033A2E16923BB209A90. 기존 로컬 Preview를 .local/contracts-preview-a36f494fc654454e8f7e54f633e9f4de에 백업하고32파일 해시 일치·활성화/언어 불변으로 설치했다(.local/m2-094-install.log). Preview Steam 게시 없음. 다음 착수점: 원 진행 작업/예약·개별 권리 및 비별칭 시민의 실제 이전 권한/중단 복구,소유 nested holder/일반 커스텀 holder,복합 자산·도시 소실/귀환의 원 provenance 유지 경로. 미지원 상태를 삭제·초기화하지 않는다.

M1 10/10,M2 전체 미완료0/18,전체10/100.1단계 잔여 노력31.5%/8묶음,2~6단계80/95/90/100/70%,묶음5~7/6~8/5~7/6~8/4~6 유지. 진행 작업/예약/병력/개인 권리,원 명부 alias 없는 시민,일반 커스텀·소유 nested holder,복합 소실/재탈환은 남아 있다. 자동 예약과 Preview Steam 게시는 없다.


최종 검사: .local/m2-084-build16.log와 build17.log의 양 타깃 전체 빌드 통과, 최종1.6 규칙 재검사 finalrules16.log와1.7은 각각 Honor20,093/Contracts5,199, 영한543개 문구, 컴파일 경고/오류0. 형식6 Prepared/Applied/Cancelled·변경 설정·누락/중복/범위 밖 멤버·옛 형식 확대 거부·원 멤버 순서·주민 보유 정당한 독립의 원 그룹/물리 journal roundtrip 검사 포함. 패키지 artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.84.zip SHA256CB92960E8DD4A03A74031C56BA598A3E726919BA947AA5A7330097614DF11DB3. 기존 로컬 Preview 백업 .local/contracts-preview-3b0431f3ae69440cba0ebe8e3d361655 후 설치,32파일 해시 일치·ModsConfig/Prefs 불변(.local/m2-084-install.log). Preview Steam 게시 없음.

## 0.3.93 — M2 부분 구현: 손상 자산·소유 선반·유휴 시민과 자산의 동시 이전

2026-11-09. 세 개의 새 원본 범위를 실제 native 이전·저장·명시적 중단 복구·전체 정당한 독립 후손 검토에 연결했다. MapAssetTransfers의 원 source/target/map proof·개별 시작/정상 반환 prefix와 ResidentExecution의 독립 census/의료/영역/수술/원 Pawn 참조를 유지한다. 원 물체·주민을 이동/재생성하지 않고 내구도·재고·관계·설정·채무를 초기화하지 않는다. 옛3~12/null JSON bytes·형식·권한은 그대로 유지하며 새 범위를 옛 기록에 소급하지 않는다.

- 형식13: AssetRepairV1=1과 MapAsset.Repair의 원 최대 내구도/수리 가능 여부를 원 물리 HP와 묶는다. native repairable list와 set을 소유 세력별로 직접 읽고 실제 현재 소유자만 정확히 등록되었는지 검증한다. 잘못된/중복/불일치 등록은 고치지 않는다. Building.SetFaction의 원 정상 해제/재등록으로 target의 수리 대상이 되며 원 HP와 최대치/수리 설정이 같아야 한다. RepairableBuildings/Contains 같은 캐시를 생성하는 getter를 준비에 호출하지 않고 수리·치료·목록 재작성도 하지 않는다. 수리 목록은 native unsaved 파생 등록이며 저장된 작업 우선순위나 진행 작업의 복원을 주장하지 않는다.
- 형식14: OwnedStorageV1=1로 정확한 Building_Storage의 소속 이전을 허용한다. CaptureOwned는 별도 경로이며 옛 중립 Capture는 계속 중립만 허용한다. 원 shelf settings/filter/priority/style/quality·slot cells·ALL ground goods와 원 StorageGroup/member 순서/shared settings/manager 참조를 보존한다. 그룹에 소유 자산이 있으면 모든 원 구성원의 property authority가 필요하다. 소유권 혼합 그룹은 보류한다. 새 owned shelf에서만 기본 Styleable+Quality를 지원하고 실제 comp 목록/def/parent·native compQuality/compStyleable cache 참조와 unpatched quality read를 확인한다. 그룹 재구성·물품 운반·filter/style/quality 설정·설정 복사·중복 deep-save는 없다. 손상 선반은 같은13 repair 증거와 함께14에 묶인다.
- 형식15: CivicAssetsV1=1로 원 city roster와 같은 Pawn alias를 가진 기존 적격 유휴 지도 시민과 원 자산을 함께 이전한다. 원 clinical11/map bindings·주민별 정상 반환/의료/영역 기록과 원 자산별 정상 반환은 별도로 유지한다. 같은 전체 Map/모든 원 주민·방문자·물체·설정이 검토 범위에 있어야 하며 이를 실제 원 참조와 대조한다. 주민을 먼저 처리하고 자산을 처리한 뒤 모두 확인해야 기존 도시 호출과 완료가 가능하다. 명시적 복구도 이미 확인한 주민/자산을 재실행하지 않고 원 미처리 prefix만 진행한다. map10/12~14의 옛 guard는 새15 권한으로 확대하지 않는다. Visitor의 원 소속/의료/영역은 변경하지 않으며 현재 Faction/Host와 목적지의 상호 평화도 필요하다.

실행 제한은 유지한다. 지도 시민은 원 roster alias·비지도자/비노예/비포로·무진행/무이동/무예약·무할당 재산/수술 지시 등 기존 조건을 만족해야 한다. 자산은 검토된 native 기본 물체/건물/선반만 지원하고 별도 lord/활성 또는 제안·생성 중 quest/지정·설치 청사진/건물 추적 scope/알 수 없는 event subscriber와 private/foreign/held/복잡 자산은 보류한다. 모든 로딩 지도의 다섯 예약 manager가 비어 있어야 한다. unknown started call은 target 소유라도 재실행/완료하지 않는다. 복합 자산/도시 소실은 아직 해결하지 않으며 원 참조·raw를 보존한다. 진행 작업·병력/노예·원 명부 alias 없는 시민·임의/소유 다중 슬롯 보관함과 더 넓은 조합은 미완료다.

설치 게임1.7.5000 rev1275/DLL SHA256D55AA8E47FA3C5463CA85B0ECCD51F195B4BBCD04F56DA3AF21C2F6688A81254/MVID6c268a95-d1a8-4d6f-ad81-e5dcf6949c0e를 metadata로 확인했다. Thing/Building/원 건물·repair·meditation/tag 등록부/MapEvents/Building_Storage/StorageGroup, 다섯 예약 manager/quest/signal/Map/MapDrawer/Styleable/QualityUtility와 기존 검토본의 전체 텍스트를 대조했다. Pawn 전체/MapPawns/mind/jobs 및 component/player settings/needs/relations도 기존 검토본과 동일함을 확인했다. 현재 PathFollower.StopDead의 비동기 경로 정리를 추가로 확인하여 새5000 civic 경로는 원 curPath와 curPathRequest가 모두 null이고 disposal dispatch가 미패치인 경우만 허용한다. native의 통상 AI cache reset을 일반 작업 이력 복원으로 부르지 않는다. 자산 whitelist는 기존4998+새5000, civic whitelist는 기존4995+새5000으로 구분한다. 미검토1.6 native 몸체는 보류하며1.6/net472 분리 빌드와 기존 경로를 유지한다.

검사: .local/m2-093-release16.log/release17.log 양 타깃 전체 빌드 통과,각 Honor20,093/Contracts5,433·영한558문구·컴파일 경고/오류0. 새 MapRepairAssetTests/OwnedShelfTests/CivicAssetTests 및 LawfulFollowerMigrationTests가 원 HP/repair 등록, source/target prefix·unknown call 거부, shelf/group/settings/goods/quality, 원 시민 census와 형식13~15, Prepared/Applied/Cancelled 저장·전체 법적 후손 복원을 검사한다. 패키지 artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.93.zip SHA2566F4CB99317046E0C1F53C64E6F01445835D820298104FBBBF9554B5A8A897E08. 기존 로컬 Preview를 .local/contracts-preview-1088147c6a654307b5fa6d529993c777에 백업 후 설치,32파일 해시 일치·활성화/언어 구성 불변(.local/m2-093-install.log). Preview Steam 게시 없음.

M1 10/10,M2 전체 미완료0/18,전체10/100,1단계 잔여31.5%/8묶음,2~6단계 잔여80/95/90/100/70%,묶음5~7/6~8/5~7/6~8/4~6 유지. 다음 착수점은 원 owned Casket/Crate/held asset의 별도 holder·권리·dispatch와 active work/reservation·원 alias 없는 시민의 원 roster authority, 복합 소실/후속 재탈환 복구다. 기존 조건을 삭제하거나 실패 원본을 정리해 M2 완료로 만들지 않는다. 자동 예약 없음.

## 0.3.92 — M2 부분 구현: 원 세력 소유 고정 자산의 실제 소속 이전·저장·복구

2026-11-05. 새 LoadedMapScope.OwnedAssets/StockFormat12와 MapAssetTransfers의 별도 원 자산/호출 journal을 실제 Thing.SetFaction/Building.SetFaction·도시 이전·정당한 독립 전체 후손·Scribe·명시적 복구에 연결했다. 원 faction-owned root 물체 ID/소유자/클래스·기본 물리 metadata/색상·스타일·precept·원 설정을 원 Map/ALL Thing 참조와 묶는다. 같은 물체를 유지하고 이동·재생성·despawn/spawn·원 faction raw field 쓰기·건강/재고 수리·중복 deep-save를 하지 않는다. 새 자산 scope는 원 source faction의 자산만 받으며 다른 세력/개인 권리를 추정하지 않는다. 옛3~11/null JSON bytes와 실행 권한은 확대하지 않는다.

각 자산 호출 시작을 먼저 기록하고 같은 Game의 native 정상 반환과 정확한 target 소속을 확인한 뒤 반환 사실을 별도 기록한다. 실제 준비·모든 자산 호출 전후·도시 호출 전후·복구는 전체 원 map/stock/grid/설정과 실제 원 Thing 참조를 재검토한다. 정상 반환한 원 prefix만 target 소유로 대조하고 다음 unstarted 자산만 실행한다. started/unreturned는 실제 target 소유라도 성공을 추정하거나 재호출하지 않는다. 도시 호출은 모든 원 자산 반환 뒤에만 허용하며 Applied 이력도 전체 완료 journal과 원 scope/source/target을 짝짓는다. 취소는 자산/시민/도시 호출 전의 무변경 범위만 허용한다. 원 정치/계약·공납·의무·제삼자 권리·source 마지막 정착지·전체 후손·정상 도시 반환 검사를 유지하고, 기존 저장 세계 주민의 임상/census/의료/영역/수술 기록도 별도로 그대로 집행한다. 복합 자산/도시 소실은 완료로 처리하지 않으며 Destroyed에서 새 자산의 원 참조/호출 raw를 자동 해제하지 않는다.

현재 엄격한 실행 범위: 지도 위 인물이 없는 원 NPC 도시, 별도 lord/작업 예약/지정/설치 청사진·건물 추적 scope/활성 또는 제안 퀘스트·생성 중 퀘스트가 없어야 한다. 정확한 기본 Thing/ThingWithComps/Building root만 받고, 건물은 손상 없는 비자연물·비상단 집결 지점, 기본 CompStyleable만 지원한다. registered source precept/style/paint, def/실제 comp 목록, native virtual dispatch·unpatched callback·빈 map event subscriber를 확인한다. 원 NPC 건물 등록 목록/repairable list+set/원 map owner를 검증하고 native가 통상 등록·캐시 무효화·mesh dirty만 수행하게 한다. private/foreign property, 소유 선반/다중 슬롯 보관함/held assets/복잡 comp·손상 수리 상태/진행 작업·예약·집단/지도 시민 동시 이전은 미완료다. 중립 선반/그룹/구역/원 보관함 보존은 기존 별도 경로를 유지한다.

설치 게임이 1.7.4998 rev1299로 갱신된 것을 확인했다. DLL SHA256 FE9B615CF5918955AB750F9D627FB0082FD23C556BDFDEE410F88857C67A081B, MVID bfbab472-0f01-4378-acdf-3bd92e3a67f8. 현재 Thing/Building.SetFaction, Quest.State/expiry/manager/signal, Map.GetChildHolders/direct owner, MapEvents, 다섯 예약 manager, 원 건물/repair/meditation/tag registry, MapDrawer dirty/section 및 CompStyleable getter/save 경계를 metadata로 대조했다. 새 자산 실행은 이 MVID만 허용한다. 기존 map10/profile11 지도 시민은 별도 옛 audited MVID20545414-6845-4afb-b0ca-63bce8627a4c 제한을 그대로 유지하며 새 게임 몸체의 시민 실행 권한을 자동 확대하지 않는다.1.6/net472 컴파일/기존 경로는 유지하되 미검토1.6 native 자산 실행은 보류한다.

검사: .local/m2-092-final16.log/final17.log 양 타깃 전체 빌드 통과(Honor20,093/Contracts5,371,영한555문구). MapAssetTests는 원 소유자·범위·순서/반환 prefix·unknown call/재실행 거부·변경 scope 거부·Prepared/Applied/Cancelled 저장 복원을 검사한다. LawfulFollowerMigrationTests는 원 자산12+별도 mixed10 세계 시민과 전체 법적 후손/이전·저장 복원을 확인한다. 마지막 손실 참조 유지/UI 반영 소스도 .local/m2-092-release16.log/release17.log 경고/오류0,영한555키/서식 인자 동일. 패키지 artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.92.zip SHA25644F7F2D3816ED95F3534CD79437C15BFCF567DF4F5957CCA0D58267486279511. 최초 교체 전 원0.3.91백업 .local/contracts-preview-195af69c85b84d8689ab78bf54c18800 유지. 최종 재설치 백업 .local/contracts-preview-451572a8754f417c939f1d425544c300,32파일 해시 일치·활성화/언어 불변(.local/m2-092-releaseinstall.log). Preview Steam 게시 없음.

M1 10/10,M2 전체 미완료0/18,전체10/100,1단계 잔여31.5%/8묶음,2~6단계 잔여80/95/90/100/70%,묶음5~7/6~8/5~7/6~8/4~6 유지. 다음 착수점은 실제 원 repair 상태를 보존하는 손상 자산과 소유 저장/holder의 별도 callback·개별 권리,4998 시민 호출 몸체의 별도 검증 및 지도 시민/자산 동시 이전·진행 작업/예약의 원 저장·복구다. 도시 소실/재탈환과 미지원 상태를 초기화해 완료로 만들지 않는다. 자동 예약 없음.

## 0.3.91 — M2 부분 구현: 방문자만 있는 지도의 실제 도시 이전·원 참조 보존

2026-11-05. 새 StockFormat11/ReadOnlyBystandersV1=1은 NativeResidents 없이 지원되는 원 방문자만 있는 지도를 실제 기존 도시 SetFaction·정당한 독립 전체 후손 이전·저장·명시적 중단 복구에 연결한다. 방문자는 시민 소속 이전/의료/영역 호출의 대상이 아니다. 별도 원 Visitors proof와 Pawn/Lord/인벤토리/무기/의복 원 참조를 reference-only Scribe하고 준비·도시 호출 전후·복구에서 원 객체와 소속/신체/장비/관계/작업 ID/부대 membership을 대조한다. 원 장비 holder의 정확한 타입(ThingOwner<Thing>/ThingOwner<ThingWithComps>/ThingOwner<Apparel>), Owner와 각 child holdingOwner를 확인한다. 방문자 최대128,개별 장비256,지도 물체/방문자/원 장비 합계10,000과 전역 ID 중복 금지를 적용한다. 인물/장비/부대를 이동·재생성·재소속시키지 않는다.

옛 map3~10/null JSON bytes와 호출 권한은 확대하지 않는다. 옛 CaptureResidents 빈 시민/방문자-only 호출은 계속 거부하며 새 CaptureBystanders만 새 marker11을 만든다. marker 누락/알 수 없는 값/원 시민과의 혼합/중복·범위 초과/원 인물·부대·장비 교체/상태 변화는 원본을 유지하며 보류한다. 방문자의 실제 Faction/Host는 목적지와 상호 평화여야 하며 원 지도의 중립 물체·재고·grid·zone·holder 제한, source 마지막 정착지·provider·quest·계약·채무·제삼자 권리를 그대로 검사한다. 원 도시 호출 시작/정상 반환은 기존 독립 CityCall journal로 보존하며, target 소속이나 visitor proof만으로 미확인 호출을 재실행/완료하지 않는다. 원 저장 세계 시민은 별도 기존 임상7~10/census/의료/영역/수술 기록을 유지하며 방문자 proof로 시민 권한을 만들지 않는다. 기존 map10/profile11의 MVID 제한도 유지한다.

검사: .local/m2-091-final16.log/final17.log 양 타깃 전체 빌드 통과(Honor20,093/Contracts5,338,영한553문구). MapBystanderTests는 옛 null/typed scope 엄격성·marker/중복/범위·변경 후 원 기록 불변·Prepared/Applied/Cancelled 복원·미확인 호출 거부를 검사한다. LawfulFollowerMigrationTests는 새 방문자11+원 mixed10 세계 시민의 정당한 독립 전체 후손·도시 이전/저장 복원을 확인한다. 마지막 native holder 원 타입 검사 반영도 .local/m2-091-native16.log/native17.log 경고/오류0. 패키지 artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.91.zip SHA2565CFE6FB838090100752EB61116B174F53DCA7EC8F74BBFD6726F110A72D1BC27. 기존 로컬 Preview를 .local/contracts-preview-f8e884de08a543ac8640945d7bf7dee6에 백업 후32파일 해시 일치·활성화/언어 불변으로 설치(.local/m2-091-install.log). Preview Steam 게시 없음.

M2 전체 미완료: 소유 물체의 별도 실제 소속 변경/권한·개별 호출/복구,진행 작업/예약/집단 상태·포로/노예·원 명부 별칭이 없는 시민의 소속 이전,일반 커스텀 holder와 더 넓은 조합이 남는다. 다음 실제 착수점은 소유 물체 이전의 원 소유자/권리 범위와 Thing.SetFaction의 지도 registry·quest/event 경계를 함께 묶는 별도 원 자산/호출 journal이다. 단순 guard 제거나 기존 중립 증거로 소유권을 추정하지 않는다. M1 10/10,M2 0/18,전체10/100,1단계 잔여31.5%/8묶음,2~6단계 잔여80/95/90/100/70%,묶음5~7/6~8/5~7/6~8/4~6 유지. 자동 예약 없음.

## 0.3.90 — M2 부분 구현: 지도 위 원 시민의 실제 소속 이전·저장·복구

최종 검사: .local/m2-090-final16.log/final17.log 양 타깃 전체 빌드 통과(Honor20,093/Contracts5,312, 영한552문구). 마지막 native 경계/MVID 반영 소스도 .local/m2-090-final-ship16.log/final-ship17.log 컴파일 경고/오류0. 패키지 artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.90.zip SHA256 C705C2AA0C735A57A444A53BF8D50CC35A476F8B057D89DC6A2D8CD5510CD760. 기존 로컬 Preview를 .local/contracts-preview-2440f98bac4c411493dc5c6cee64125e에 백업 후 설치,32파일 해시 일치·활성화/언어 구성 불변(.local/m2-090-install.log). Preview Steam 게시 없음.

2026-11-04. 새 map 형식10/임상 profile11로 같은 원 도시의 previouslyGeneratedInhabitants에 정확히 같은 Pawn 참조로 남아 있는 유휴 지도 시민을 실제 기존 Pawn.SetFaction 경로에 연결했다. 새 NativeResidents/MapBindings는 원 map ID·cell·rotation·임상/영역 hash를 묶으며 원 NativeMap/MapResidents 참조를 Scribe한다. 세계 명부와 지도 등록의 같은 원본을 별칭으로 보존하고 MapPawns의 실제 map owner/spawned list/소속별 원 목록을 양방향 대조한다. 원 지도/주민을 재생성·이동·despawn/spawn하거나 소속 raw field·건강·장비·관계·유전자/이념을 재작성하지 않는다.

실제 준비는 새 census/authority-call/medical/area/수술 원 기록을 동일 map10과 짝짓는다. 개별 호출 시작은 먼저 저장하고 같은 Game의 정상 반환과 의료/영역 출력을 별도 관찰한다. 부분 처리 후 원 Prepared reference만 조회해 원 임상/영역 증거를 정규화하며 현재 Target 소속만으로 성공을 추정하지 않는다. 원 scope·직전 ref·호출·의료 관찰이 없거나 시작 뒤 정상 반환이 없으면 재실행하지 않는다. 완료 전 모든 원 증거/의료/영역 소비와 map/도시 정상 반환을 요구하고 완료 이력의 문서와 원천을 남긴 뒤 추가 참조만 해제한다. Load/FinalizeInit·명시적 복구·정당한 독립 전체 후손/council/물리 이력도 같은11/10 증거를 사용한다. 옛 map3~9/profile1~10/null bytes·조건·권한은 확대하지 않는다.

엄격한 현재 범위: 원 도시 명부와 원 지도에 동시에 있는 적격 NPC 인간형 비포로/비노예/비지도자·무작위 부대 소속이 없는 시민이다. 현재/예약/대기/우선 작업·이동·지정 침대/재산·의료 수술 지시·추가 host/quest/거래 역할·비어 있지 않은 thinkData/명령 의도는 보류한다. **모든 로딩 지도의 다섯 예약 manager에 실제 예약이 없고, 해당 지도에 designation이 없어야 한다.** 원 NPC 임상 상태와 위치를 보존하며 native가 검증된 faction-change의 통상 AI reset/캐시 등록 알림을 수행한다. 일반 진행 작업/AI 이력을 복원하는 실행이 아니며 그 범위를 완료했다고 부르지 않는다. 등록부/추적기의 원 owner와 정확한 native 클래스/필드·IThingHolder interface dispatch·패치/이벤트 경계를 검사하고 모르는 callback/layout은 보류한다. 상태를 지울 수 있는 PriorityWork.IsPrioritized와 faction-list GetPawnList 조회를 준비에 호출하지 않는다.

외부 인물은 별도 Visitors 원 참조/읽기 전용 proof로 분리한다. 지원되는 인간형 방문자는 소속/의료/영역/신체/장비/관계/유전자/신앙/수술/작업 ID/지도 위치와 기존 lord membership이 같은지 대조하며 어떤 이전/복구 호출도 적용하지 않는다. 방문자의 실제 소속/host도 목적지와 상호 평화여야 한다. 포로/노예/작위·특수 상태/커스텀 holder 등 미지원 방문자는 전체 작업을 보류한다. 이것은 방문자·병력·재산의 권한이나 소속을 넘겨받는 기능이 아니다.

설치 Version.txt1.7.4995 rev309/DLL SHA25656399B6144D9F1695F9C53D054DC82972F7AB35E68D1A19A7129F2F5860CCD21의 Pawn.SetFaction/StopAll/CleanupCurrentJob/MapPawns/MapEvents/다섯 예약 manager/PriorityWork 및 Settlement의 명부 Reference Scribe를 대조했다.새 지도 시민 실행은 검토한 DLL MVID20545414-6845-4afb-b0ca-63bce8627a4c만 허용한다.1.6 고정 참조/net472 컴파일과 기존 경로는 유지하되,새11의 미검토1.6/다른 런타임 몸체는 보류하며 시그니처만으로 쓰기 권한을 가정하지 않는다.

M2 전체는 미완료다. 원 시민 별칭이 없는 지도 인물, 진행 작업/예약/집단·포로/노예·소유 물체의 별도 소속별 권한·개별 호출/복구, 일반 커스텀 보관함과 더 넓은 조합이 남아 있다. M1 10/10,M2 0/18,전체10/100,1단계 잔여31.5%/8묶음,2~6단계 잔여80/95/90/100/70%,묶음5~7/6~8/5~7/6~8/4~6 유지. 자동 예약/Preview Steam 게시 없음.

## 0.3.89 — M2 부분 구현: 원 다중 슬롯 보관함과 중첩 숲의 실제 보존

최종 검사: .local/m2-089-build16.log/build17.log 양 타깃 전체 빌드 통과,각 Honor20,093/Contracts5,284개 검사·영한550개 문구 일치·컴파일 경고/오류0. ContainerMapTests는 다중 슬롯/중첩 포장·빈 보관함·legacy 슬롯0 엄격성·누락/중복/순서/범위/타입·Prepared/Applied/Cancelled 저장 복원·옛 형식 확대 거부를 검사한다. LawfulFollowerMigrationTests는 원 mixed10 주민/보관함9와 원 전체 정치/물리 이력을 함께 복원한다. 패키지 artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.89.zip SHA2565CA57B8020BABC4A8530446FCFEC38E2C4F7493CD2232BF3F9C59ED9A66C5AE0. 백업 .local/contracts-preview-2a01917f2b7e4da2a2a14ec2d67423a0 후 로컬 설치 완료,32파일 해시 일치·ModsConfig/Prefs 불변(.local/m2-089-install.log). Preview Steam 게시 없음. **M2 전체 미완료**: map/live roster alias와 원 예약/designation/지도 등록·개별 native-call/정상 반환·소유 주체별 자산 권한/원본 보존을 실제 집행과 복구에 연결하는 큰 경로가 남는다. 보관함9는 기본 중립 Casket/Crate 보존의 실제 실행이며 임의 커스텀 holder나 주민/재산 소속 변경의 완료가 아니다.

2026-11-02. 새 지도 형식9의 Containers 기록으로 정확한 기본 Building_Casket/Building_Crate의 원 컨테이너·순서 있는 다중 슬롯·중첩 포장/보관함을 실제 도시 이전·원 주민/의료 기록·정당한 독립 전체 후손 검토·Scribe·명시적 중단 복구에 연결했다. 원 Map과 바깥/안쪽 Thing 참조만 보존하고 native parent town 소속 변경만 호출한다. 보관함/내용물의 Open/CanOpen/EjectContents/TryAcceptThing/꺼내기/넣기/재포장/복제/재생성/소속 변경을 호출하지 않는다.

원 known contents/개봉 signal과 정확한 ThingOwner 기본 타입/Owner/각 자식 holdingOwner, 전체 정수 슬롯 순서·개수·원 물체/부모 ID를 대조한다. 최대 전체10,000물체·1,000보관함·8단계 원 forest를 묶는다. 새 scope에서만 다중 슬롯을 허용하며 기존 MinifiedThing은 여전히 슬롯0 하나다. 옛3~8/null bytes와 단일 포장 규칙은 확대하지 않는다. 누락·중복·잘못된 부모·슬롯 누락/순서 변경·순환·깊이 초과·원 내용물/설정 변화는 원본을 보존하며 보류한다.

상자에서만 정확한 기본 CompHackable의 저장 필드 schema/값을 읽으며 lastUser가 있거나 autohack 진행 상태면 보류한다. 기본 CompGlower의 실제 저장 glowOn/color override만 읽고 비저장 radius/cache는 저장 권한으로 기록하지 않는다. Hack/빛/개봉/알림 getter·setter를 호출하지 않는다. 주민·시체·소유 보관함/물품·알 수 없는 holder/component·임의 커스텀 상자는 아직 지원하지 않는다. 같은 원 참조·doc·city-call 정상 반환·주민 census/medical/surgery/area·물리/법적 이력이 복구까지 짝지어야 한다. 영한 검토/보류 안내를 추가했다.

동시에 설치1.7 Pawn.SetFaction/Thing.SetFaction/Building_Casket/Building_Crate/CompHackable/CompGlower와 reservation manager 코드를 확인했다(.local/m2-089-*). 지도 위 Pawn.SetFaction은 명부 재등록·목적지 예약 해제·designation 제거·auto-slaughter/attack target cache 알림을 실행한다. Thing 소속 변경도 quest/지도 이벤트를 실행한다. 이것을 기존 neutral scope/세계 주민 동의로 승인했다고 해석하거나 관련 가드를 제거하지 않는다. 지도 위 주민/소유 물체 경로는 미완료이며,원 map/saved roster alias·실제 map 등록·원 예약/designation/원 소속별 권리·개별 정상 반환/중단 journal을 별도로 실제 실행에 연결해야 한다.

M2 전체 미완료. M1 10/10,M2 0/18,전체10/100,1단계 잔여31.5%/8묶음,2~6단계 잔여80/95/90/100/70%,묶음5~7/6~8/5~7/6~8/4~6을 유지한다. 자동 예약/Preview Steam 게시 없음.

## 0.3.88 — M2 부분 구현: 쓰러진 일반 주민·포로 혼합 명부의 실제 형식 선택

최종 검사: .local/m2-088-build16.log/build17.log 양 타깃 전체 빌드 통과,각 Honor20,093/Contracts5,263개 검사·영한548개 문구 일치·컴파일 경고/오류0. 실제 CaptureForTransfer가 사용하는 임상 형식 선택의8가지 조합과 기존 mixed10/area/census/medical/authority/법적/물리 저장 검사를 통과했다. 자동 선택의 확대는 개별 대상의 기존 부적격 조건을 허용하지 않으며,옛 임상 증거의 profile 변경을 허용하지 않는다. 패키지 artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.88.zip SHA2568E142BC4B95BE2769B9FD0B28296C0624DC76972CF16367C442EEDCA80071B52. 백업 .local/contracts-preview-49ce95c8dac44a20bdd1f7eb73bf4499 후 로컬 설치 완료,32파일 해시 일치·ModsConfig/Prefs 불변(.local/m2-088-install.log). Preview Steam 게시 없음. M2 전체 완료 아님: live map resident의 명부/실제 map 소속/AI·의료·맵 목록 및 소유 물체의 별도 동의·개별 호출/참조/정상 반환·중단 복구 경로가 아직 없다. 기존 neutral scope를 병력/재산 권한으로 바꾸거나 가드를 제거해 완료 처리하지 않는다.

2026-11-02. 도시 전체 원 명부에서 영역 제한/쓰러짐/포로 존재를 함께 평가해 새 임상 형식을 선택하는 경로를 실제 CaptureForTransfer에 연결했다. 영역 제한이 없어도 쓰러진 비포로 주민과 지원 포로가 함께 있으면10을 사용한다. 기존에는8을 선택해 포로 검사를 통과할 수 없었다.7(일반/기존 포로),8(비포로 쓰러짐),9(비포로 영역),10(지원 혼합)의 자동 선택을 명확히 분리했다.

선택 함수는 허가/현재 동의/원 census/의료 초기화 증거가 아니다. 기존 개별 원 신원·건강·장비·관계·신분·이념·유전자·명부/도시/대상 검사와 독립 호출/의료/영역/수술/포로 journal을 그대로 적용한다. 쓰러진 포로·비어 있지 않은 포로 영역·지도 위 주민·소유 재산·노예/지도자/진행 작업 등 기존 제한은 유지한다. 원 임상10을 준비·실제 호출·저장/중단 복구·전체 정당한 독립 검토에 동일하게 재사용하며 옛 형식 기록은 확대하거나 재작성하지 않는다. 빈 영역에는 쓰기 권한이 생기지 않는다. 검토 화면의 영한 혼합 명부 안내에도 쓰러짐 조합을 반영했다.

M2 전체는 여전히 미완료다. 지도 위 주민·소유 물체·임의 보관함 및 폭넓은 복합 상태의 별도 실제 권한·보존·호출/중단 경로가 남아 있다. M1 10/10,M2 0/18,전체10/100,1단계 잔여31.5%/8묶음,2~6단계 잔여80/95/90/100/70%,묶음5~7/6~8/5~7/6~8/4~6을 유지한다. 자동 예약/Preview Steam 게시 없음.

## 0.3.87 — M2 부분 구현: 영역 제한 주민과 포로의 혼합 이전

최종 검사: .local/m2-087-final16.log/final17.log 양 타깃 전체 빌드 통과,각 Honor20,093/Contracts5,255개 검사·영한548개 문구 일치·컴파일 경고/오류0. 혼합 profile10 census/원 clinical-area hash·옛9 증거 대체 거부·빈 포로 영역의 무복구권 완료·원 호출/의료 journal·정당한 독립의 원 혼합 scope/물리 이력 복원을 검사했다. 최초 검사는 기존 미지원10 경계 테스트가 새 지원10을 거부해 실패했으며,미지원 경계11과 신규10 원본/9분리 검사를 갖춘 뒤 최종 전체 검사를 다시 통과했다. 패키지 artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.87.zip SHA256EEB656D0358B0FB98CCFD4B39C44EE8EB40973B8B3C608FFD4827895EB795041. 백업 .local/contracts-preview-d5380f10c6324c7fab687cfe7595daef 후 로컬 설치 완료,32파일 해시 일치·ModsConfig/Prefs 불변(.local/m2-087-install.log). Preview Steam 게시 없음. M2 미완료: 지도 위 주민·소유 물체/임의 보관함·광범위 주민/중단 조합의 원본 보존과 실제 실행 경로가 남는다.

2026-11-02. 새 resident profile10은 기존에 각각 지원되는 일반 주민의 원 영역 제한과 영역 제한이 없는 포로가 같은 도시에 있을 때의 실제 이전·저장·중단 복구를 연결한다. 옛 profile9의 포로 거부와1~9 bytes/권한은 그대로 둔다. 새 BindMixedAreas는 원 profile9 임상/영역 증거를 별도10 hash로 감싸고 원 census·각 주민 authority-call·의료·영역·수술/포로 journal·도시/물리/법적 전체 후손 기록을 짝지으며, 원 Pawn/Map/Area 참조만 저장한다.

포로는 기존 profile7 임상·신앙·유전자·모집/작업/상태 증거와 원 개인 소속을 유지하며 기존 검증된 NPC 관리 세력 변경 경로만 사용한다. 포로의 원 영역은 비어 있어야 하고 쓰러진 포로·소유 재산·노예·지도자·임무·지도 위 주민 등 기존 제한을 제거하지 않는다. 일반 주민 영역은 정상 반환 뒤 실제 관찰된 초기화만 한 번 복구한다. 포로의 빈 영역은 빈 정상 출력/완료 사실만 기록하며 BeginRestore 권한을 만들지 않는다. 원 임상/기록 변화나 미확인 호출은 원본을 유지하며 보류한다.

설치1.7 Pawn_GuestTracker.SetGuestStatus의 기존 포로 소속 유지·관리 세력/의료 초기화 읽기 경계를 다시 확인했다(.local/m2-087-guest.cs). 구금/신분 전환이나 원 영역 권한을 추정하지 않으며 재실행하지 않는다. 새 정당한 독립 주민 census도10을 명시적으로 짝지어 같은 원본으로 재검토한다. 영한 검토 화면에 혼합 명부 보존 범위를 추가했다.

M2 전체 완료는 아니다. 지도 위 주민·소유 건물·임의 보관함·더 넓은 주민/복합 상태의 실제 경로가 남아 있다. M1 10/10,M2 0/18,전체10/100,1단계 잔여31.5%/8묶음 및2~6단계 잔여80/95/90/100/70%,묶음5~7/6~8/5~7/6~8/4~6을 유지한다. 자동 예약과 Preview Steam 게시 없음.

## 0.3.86 — M2 부분 구현: 원 재배 구역과 혼합 구역 보존

최종 검사: .local/m2-086-build16.log/build17.log 양 타깃 전체 빌드 통과,각 Honor20,093/Contracts5,244개 검사·영한547개 문구 일치·컴파일 경고/오류0. 원 재배/저장/공유 선반 조합,형식8 Prepared/Applied/Cancelled 저장 복원,변경 설정·중복/중첩/밖 칸·옛 형식 확대 거부 및 주민 동반 정당한 독립의 원 scope/물리 journal 검사를 포함한다. 설치1.7 Zone_Growing.ExposeData/PlantDefToGrow와 기존 Zone/ZoneManager의 저장/읽기 경계를 확인했다(.local/m2-086-growing.cs);1.6은 고정 참조/net472와 실제 경로의 필드/타입/패치 검사다. 패키지 artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.86.zip SHA256058763453CFC51AD53357E635681E62AE52E1A48F2D89A28098511AB62376216. 백업 .local/contracts-preview-552b600697f0406e883444024b20fdfa 후 로컬 설치 완료,32파일 해시 일치·ModsConfig/Prefs 불변(.local/m2-086-install.log). Preview Steam 게시 없음. 다음 실제 미완료는 지도 위 주민/소유 건물/임의 보관함/커스텀 구역과 복합 상태의 안전한 이전 경로다.

2026-11-02. 새 지도 형식8의 null-omitted Growing 기록으로 원 재배 구역을 저장 구역·공유 선반·원 주민/의료 기록과 함께 실제 도시 이전·정당한 독립 전체 후손 검토·저장/명시적 중단 복구에 연결했다. 원 Zone_Growing 참조만 별도 Scribe하며 원 작물 설정(null 포함)·파종/수확 허용·이름/기본 이름·색/숨김·칸 집합과 실제 ZoneManager 등록/조회 grid를 대조한다. 작물 선택·파종·수확·구역 생성/삭제/재등록·칸/필터/지도 수정은 하지 않는다.

설치1.7의 PlantDefToGrow getter는 null일 때 작물을 자동 선택하며 오염/Cells 조회도 실행한다. 원 private plantDefToGrow 저장 필드의 정확한 타입과 등록된 Def 참조를 읽고 getter/setter/작물 초기화를 호출하지 않는다. Zone.Cells Shuffle도 피한다. 기본 저장/재배 구역만 허용하고 양쪽 칸·선반·구역 ID 중첩/누락/교체·설정 변화·커스텀 구역은 원본을 유지하며 보류한다. 칸 집합은 순서를 섞는 native 동작과 구별해 canonicalize하고 원 구역/물체 참조를 유지한다.

기존3~7/null bytes와 허용 범위는 확장하지 않는다. 같은 원 city-call 시작/정상 반환·주민 census/임상/의료/영역/수술·재고/계약/채무·물리/후손 journal에 형식8을 짝지으며, 미확인 시작 호출은 반복하거나 목적 소속만 보고 완료하지 않는다. 원 미완료 참조는 복구까지 유지하고 양의 완료 이력은 문서를 보존하며 추가 참조만 해제한다. 영한 구역 수/보류 안내를 추가했다.

M2 전체는 미완료다. 지도 위 주민·소유 건물·커스텀 구역·임의 보관함·복잡 작업/중단 조합이 남았다. M1 10/10,M2 0/18,전체10/100,1단계 잔여31.5%/8묶음,2~6단계 잔여80/95/90/100/70%,묶음5~7/6~8/5~7/6~8/4~6은 유지한다. 자동 예약/Preview Steam 게시 없음.

## 0.3.85 — M2 부분 구현: 원 저장 구역 보존 이전

최종 검사: .local/m2-085-build16.log/build17.log 양 타깃 전체 빌드 통과,각 Honor20,093/Contracts5,226개 검사·영한545개 문구 일치·컴파일 경고/오류0. StockpileMapTests는 원 설정/칸/물품·Prepared/Applied/Cancelled 복원·옛 형식 확대 거부·변경/중첩/누락/중복 증거 보류·원 공유 선반과의 조합을 검사하며,LawfulFollowerMigrationTests는 원 주민/census/공유 그룹/저장 구역을 동일 법적·물리 journal로 묶어 복원한다. 설치1.7 Zone_Stockpile/Zone/ZoneManager 및 기존 SlotGroup 저장·읽기 코드를 확인했고,1.6은 고정 참조/net472와 실행 전 shape/patch 검사 경로다. 패키지 artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.85.zip SHA25601ED6DF9C369FF2C5AAFDD06C3ED77772146A44F3EE3B1396DFCE378D942663C. 백업 .local/contracts-preview-c73e9b7f78ac4409b4fa118c22675f2e 후 로컬 설치 완료,32파일 해시 일치·ModsConfig/Prefs 불변(.local/m2-085-install.log). Preview Steam 게시 없음.

새 지도 형식7과 null-omitted Stockpiles 기록을 실제 로딩 도시 이전·정당한 독립의 전체 주민 동반 검토·Scribe·명시적 중단 복구에 연결했다. 원 등록된 Zone_Stockpile 참조를 Map/Thing/공유 그룹 원 참조와 함께 보존하고 원 설정·우선순위·이름·기본 이름·색·숨김 상태·칸 집합·저장 물품을 대조한다. 구역과 물품을 별도 deep-save·복제·재생성하거나 구역 등록/칸/필터/운반을 변경하지 않는다.

설치1.7의 Zone.Cells는 원 칸 목록을 Shuffle하는 getter다. 그 호출을 피하고 원 저장 backing list를 읽으며, native가 순서를 섞을 수 있는 칸 목록은 집합으로 canonicalize한다. ZoneManager의 전체 원 등록 목록과 ZoneAt 조회 grid를 양방향으로 대조하고 재구축하지 않는다. 공용 설정 읽기는 기존 선반과 같은 정확한 StorageSettings/ThingFilter owner와 저장 필드 증거를 사용한다. 저장 구역끼리 또는 선반 칸과의 중첩·누락/교체/중복/다른 지도·미지원 구역/구성원·변경 설정/물품은 원 기록을 유지하며 보류한다. 이번 범위의 모든 등록 구역은 정확한 기본 stockpile이어야 하며 재배 구역 등 다른 구역은 아직 지원하지 않는다.

기존3~6/null JSON bytes·strict generic Prepare·원 주민 임상/의료/수술/영역·도시 호출 정상 반환·재고/계약/채무/물리/전체 후손 기록을 보존한다. 미완료 원 구역 참조도 저장/복구하고, 정상 반환 없는 시작 호출은 목적 소속만 보고 완료하거나 다시 호출하지 않는다. 완료 기록은 원 문서를 유지하며 추가 참조만 해제한다. 영한 검토 화면에 저장 구역 수와 지원 범위를 표시한다.

M2 전체는 미완료다. 지도 위 주민·소유 건물·재배/커스텀 구역·임의 보관함·복잡 작업과 폭넓은 중단 조합이 남아 있다. M1 10/10·M2 0/18·전체10/100,1단계 잔여31.5%/8묶음 및2~6단계 잔여80/95/90/100/70%,묶음5~7/6~8/5~7/6~8/4~6을 유지한다. 자동 예약과 Preview Steam 게시를 재개하지 않는다.

## 0.3.83 — M2 부분 구현: 독립 중립 선반의 설정·보관 칸·물품 보존

## 0.3.84 — M2 부분 구현: 중립 선반의 공유 저장 그룹 보존

M1 완료 감사의 다섯 고정 조건과 현재 코드를 대조했으며 M1 10/10을 유지한다. 이번 변경은 M2의 실제 로딩 도시 이전·저장·복구 경로를 확장한다.

새 지도 형식6의 원 Groups에는 등록된 원 공유 그룹 ID, 공용 필터·우선순위·저장된 이름의 hash와 전체 선반 구성원의 원 순서를 기록한다. 각 선반 자체의 비활성 개별 설정·스타일·칸·원 물품도 기존 Storage 기록으로 별도 보존한다. 원 Map/Thing 참조에 원 StorageGroup 참조를 추가로 Scribe하며 중복 deep-save하지 않는다. 그룹 재생성·가입·해제·설정 복사·운반·물품 소속 변경 없이 기존 월드 도시 소속 변경만 실행한다.

그룹과 manager의 실제 기본 타입/원 지도/등록/ID를 검사하고 모든 구성원이 같은 원 지도 범위의 중립 선반이며 양방향 멤버십이 정확해야 한다. 미확인 구성원·원본 교체·공용/개별 설정 변경·다른 지도·누락된 참조는 원 기록을 유지하고 보류한다. 기존 도시 호출 정상 반환, 주민 원 임상·의료·수술·영역, 물리 장부와 정당한 독립의 전체 후손 검토에 동일 scope를 연결한다. 시작 후 정상 반환이 없는 호출을 반복하거나 목적 소속만 보고 완료하지 않는다. 옛 형식3~5/null 기록은 새 그룹 권한으로 확대하지 않는다. 영한 검토 화면에 공유 그룹 수와 보존 범위를 표시한다.

M2 전체 완료는 아니다. 지도 위 주민·소유 건물·저장 구역·임의 보관함·복잡 작업과 더 넓은 중단 조합은 남아 있다. M1 10/10·M2 0/18·전체10/100, 1단계 잔여31.5%/8묶음 및 2~6단계 잔여80/95/90/100/70%, 묶음5~7/6~8/5~7/6~8/4~6을 유지한다. 자동 예약은 삭제 상태다.

일반 선반은 내부 보관함으로 물품을 옮기지 않고 지도 칸의 물품을 관리한다. 새 지도 형식5로 독립된 중립 Building_Storage의 실제 원 우선순위·필터·스타일·보관 칸과 원 물품을 일반 이전·정당한 독립 이전·저장/중단 복구에 연결했다. 원 선반/물품/지도 참조를 그대로 유지하며 선반이나 설정을 별도로 중복 저장하지 않는다. 공유 저장 그룹·소유 선반·미지원 보관 주체는 계속 보류한다.

원 필터의 저장되는 허용 정의/특수 필터·범위·루트, 우선순위·이름, 등록된 스타일/신앙 참조를 읽기만 한다. 저장되지 않는 표시용 cache를 원 권한으로 기록하지 않으며 필터·우선순위·스타일 setter나 설정 복사/변경 알림을 호출하지 않는다. 실제 SlotGroup의 원 부모·칸·물품을 대조하고 변경·누락·중복·다른 칸/선반·겹침을 발견하면 원 기록을 보존하고 보류한다. 운반·재배치·그룹 생성/가입이나 물체 소속 변경은 하지 않는다.

지도 전체 원 증거와 도시 호출 정상 반환, 주민 원 의료/수술/영역 기록과 물리 이력에 같은 선반 범위를 짝지었다. 옛 형식3/4/null 기록을 선반 권한으로 확장하지 않는다. 검토 화면에 독립 중립 선반 수와 영한 안내를 추가했다.

M2 전체 완료는 아니다. 지도 위 주민·소유 건물·공유 저장 그룹·임의 보관함과 복잡 작업/중단 조합은 남아 있다. M1 10/10·M2 0/18·전체10/100과 1단계 잔여31.5%/8묶음 및 2~6단계 잔여80/95/90/100/70%, 묶음5~7/6~8/5~7/6~8/4~6은 유지한다. 최종 검사/설치는 최신 인수인계를 따른다.

## 0.3.82 — M2 부분 구현: 로딩 지도 포장 물품의 실제 보존 이전

중립 로딩 지도에서 지원되는 포장 물품과 그 내용물을 원 위치·보관함에 그대로 유지하는 실제 이전·저장·복구를 연결했다. 새 지도 형식4는 원 부모/보관 순서와 모든 원 물체 신원·수량·건강도·재료/품질, 8단계/전체10,000개 이하 계층을 묶는다. 기존 형식3 및 옛 null 기록은 자동 확대하지 않는다. 원 지도와 바깥/안쪽 물체 참조를 모두 보존하며 복사·포장 해제·재포장·꺼내기·재생성은 하지 않는다.

현재 설치 코드의 MinifiedThing은 원 ThingOwner를 직접 반환하고 한 내용물만 보관한다. 정확한 기본 타입과 원 container.Owner/child.holdingOwner 관계를 확인한다. 빈/중복/잘못된 부모·내용물 교체·순환·과도한 깊이는 원본을 보존하며 거부한다. 내용물 타입을 먼저 확인한 뒤 포장 품질 정보를 읽어 미지원 주민/물체의 getter를 승인 없이 먼저 따라가지 않는다. 알려진 홀더만 검증한 후 지도 주민 재귀 조회를 수행하며, 다른 보관 주체나 지도 component/ship holder는 호출하지 않고 보류한다.

1.7에서는 지도에 놓인 물체도 map.spawnedThings를 holdingOwner로 가질 수 있음을 설치 코드에서 확인했다. 무조건 null로 가정하지 않고 원 지도 container와 일치하는지 검사한다. 옛 버전의 null root 관계도 그대로 지원한다. 원 지도/내용물과 거래 재고·주민 원 기록을 일반 이전 및 정당한 독립의 전체 검토/물리 이력에 짝지으며, 기존 도시 호출의 정상 반환과 원 증거가 없으면 완료하거나 반복 호출하지 않는다. 지도 위 주민·소유 물체·임의 보관함·복잡 작업·미확인 외부 권위는 계속 보류한다.

M2 전체 완료는 아니다. 지도 위 주민·소유 건물 및 더 넓은 복합 상태/중단 처리가 남아 M1 10/10·M2 0/18·전체10/100, 1단계 잔여31.5%/8묶음과 2~6단계 잔여80/95/90/100/70%, 묶음5~7/6~8/5~7/6~8/4~6을 유지한다. 최종 검사·패키지·설치 결과는 최신 인수인계에 기록한다.

## 0.3.81 — M2 부분 구현: 중립 로딩 지도의 실제 이전·보존·복구

병력과 소유권 정리가 필요 없는 중립 로딩 지도에 새 실제 이전 경로를 연결했다. 지도 위 주민·소유 물체·도착 중 운송·지정 침대·보관함/중첩 물품·복잡/커스텀 물체가 없어야 하며 양 세력은 평화 상태여야 한다. 원 Map과 물체는 참조만 보존하고 지형/아래 지형/지붕, 원 물체 전체의 신원·위치·수량·건강도·재료/품질, 기존 거래 재고 증거를 canonical scope로 묶는다. 지도 위 주민은 지원하지 않으며, 지도 밖에 저장된 적격 월드 주민은 기존 임상/의료/수술/영역 기록과 함께 지원한다.

지도용 StockFormat3과 별도 LoadedMap 원 기록·도시 호출 journal을 요구하는 새 PrepareLoaded 경로다. 옛 준비 API는 로딩 지도를 계속 거부하고 과거 null 기록을 확장하지 않는다. 실제 월드 정착지의 소속만 변경하며 지도 제거·재생성·물체 소속 변경·지형 수정·원 주민 생성은 하지 않는다. 원 지도/물체 참조와 보존 증거를 호출 전후 대조하고, 저장된 미완료 기록은 같은 원본으로만 명시적 복구한다. 정상 반환 없는 호출은 목적 소속만 보고 성공을 추정하거나 다시 호출하지 않는다. 지도나 내용물이 달라지면 원 기록을 유지하고 보류한다.

정당한 독립/연합 가입에서도 최초 전체 후손 검토의 원 지도 scope를 주민·물리 journal과 함께 묶었다. 주민의 지도 scope와 부모 이전 원본의 짝을 복원/실행/종료까지 검증한다. 물리 이력4M 한도를 넘는 준비는 원 장부를 바꾸기 전에 거부한다. 공납/채무·하위 계약·원 서명·마지막 기지·native 플레이어 통제와 기존 provider/quest 가드는 유지한다. 검토 화면에 원 중립 물체 수와 영한 지원 범위를 표시했다.

설치1.7 DLL의 WorldObject.SetFaction은 허용되는 월드 물체의 factionInt만 변경하며 Settlement/MapParent는 해당 메서드를 별도 override하지 않는 것을 다시 확인했다. 원 지도 물체의 소속 변경 권한으로 해석하지 않는다. 거래 재고는 생성하는 getter 대신 기존 GetDirectlyHeldThings와 NextRestockTick을 읽으며, 실제 null 컨테이너/시계-1도 기존 정확한 stock 형식으로 기록한다. 별도의 가짜 빈 재고나 방문 이력은 만들지 않는다.

M2 전체 완료는 아니다. 지도 위 주민·소유 건물·복잡 작업/보관 상태가 있는 로딩 도시와 더 넓은 중단·소실 조합은 남아 있다. M1 10/10·M2 0/18·전체10/100, 1단계 잔여31.5%/8묶음 및 2~6단계 잔여80/95/90/100/70%, 묶음5~7/6~8/5~7/6~8/4~6을 유지한다. 최종 검사·설치는 최신 인수인계를 따른다.

## 0.3.80 — M2 부분 구현: 주민의 원 영역 제한을 실제 이전·저장·복구

새 profile9로 적격 비포로 월드 주민의 저장 영역 제한을 보존한다. 원 Pawn·지도·영역은 참조만 저장하며 중복 저장·재생성하지 않는다. 원 건강/상태/의료·주민 명부 증거와 영역 기록을 묶어 일반 영지 이전과 정당한 독립 이전의 실제 실행/저장/중단 복구에 연결했다. 기존 profile1~8은 변경하지 않으며 주인 지정·포로/노예·지도자·로딩 도시 등 다른 제한은 유지한다.

설치 DLL의 Pawn_PlayerSettings.Notify_FactionChanged는 의료 초기화와 allowedAreas.Clear를 수행한다. 원 영역 기록은 소속 호출 시작과 함께 시작하며, 같은 Game에서 정상 반환·실제 목적 소속이 확인된 뒤 관찰한 초기화에만 복구 권한을 준다. 원 임상 증거도 다시 대조한다. 복구 시작을 먼저 기록하고 원 dictionary에 원 지도/영역 참조만 추가한다. 일부 추가 후 중단되면 실제 내용이 정확한 원 순서의 일부인지 대조한 뒤 누락된 부분만 이어가며, 모두 일치한 뒤 권한을 소진한다. 기존 다른 설정을 지우거나 영역/지도 칸·소유권을 바꾸지 않는다.

지도·영역 참조 소실, 다른 주민 참조, 알 수 없는 호출/초기화, 임상 상태 변화 또는 다른 영역 설정이 끼면 원 기록을 보존하고 중단한다. 저장 복원과 완료/참조 해제 때도 원 주민/census·원 임상/영역 proof·호출/의료 기록을 짝지어 검증한다. 이미 소진한 초기화 권한으로 나중의 사용자 설정을 덮어쓰지 않는다. 영한 지원/보류 안내를 갱신했다.

M2 전체 완료는 아니다. 로딩 도시와 나머지 미지원 상태/복합 중단 경로는 남아 있다. M1 10/10·M2 0/18·전체10/100과 1단계 잔여31.5%/8묶음을 유지한다. 2~6단계 잔여80/95/90/100/70%, 묶음5~7/6~8/5~7/6~8/4~6도 변경하지 않는다. 최종 검사/설치는 최신 인수인계를 따른다.

## 0.3.79 — M2 부분 구현: 쓰러진 비포로 주민의 원 상태 보존 이전

미로딩 영지에서 다른 적격 조건을 충족한 쓰러진 비포로 월드 주민을 위한 새 임상 profile8을 실제 일반 이전·정당한 독립 이전·저장/중단 복구 경로에 연결했다. 기존 profile7의 건강·장비·관계·욕구·이념·유전자 증거에 원 행동 상태와 쓰러짐 여부를 추가로 묶는다. 소속 변경 후 같은 증거를 대조하며 치료·건강 수정·재생성·강제 기립을 하지 않는다. 기존 profile1~7의 지문과 쓰러진 주민 거부는 그대로다.

새 임상 형식은 원 census와 별도 호출 시작/정상 반환·의료 journal이 있어야 실행한다. 불확실한 호출을 다시 실행하지 않으며 의료 변화도 원 정상 반환에서 관찰된 범위만 복구한다. 쓰러짐·건강·상태가 달라지면 원본을 보존하고 보류한다. 쓰러진 포로, 지도자·노예·정신 이상·진행 작업·보관 중 주민·로딩 지도와 기타 미지원 상태의 제한은 유지한다. 영한 안내에 새 지원 범위를 표시했다.

현재 설치1.7 Assembly-CSharp.dll SHA25656399B6144D9F1695F9C53D054DC82972F7AB35E68D1A19A7129F2F5860CCD21의 Pawn.SetFaction/ClearMind_NewTemp/Downed 코드를 다시 읽었다. Downed는 health.Downed를 읽고, 소속 변경은 의료/행동 초기화 경계를 지나지만 여기서 건강 상태를 직접 고치는 API를 사용하지 않는다. 기본 소속·행동·쓰러짐 getter 패치가 있으면 새 경로를 보류한다. 1.6은 설치 실행 증명이 아닌 고정 참조 API/분리 빌드 기준이다.

M2 전체 완료는 아니다. 로딩 도시와 나머지 미지원 주민의 안전한 실제 이전·조합 검사까지 남아 고정 점수 M1 10/10·M2 0/18·전체10/100과 1단계 잔여31.5%/8묶음을 유지한다. 2~6단계 잔여80/95/90/100/70%, 묶음5~7/6~8/5~7/6~8/4~6도 그대로다. 최종 검사·배포 결과는 최신 인수인계를 따른다.

## 0.3.78 — M2 부분 구현: 주민 보유 영지의 정당한 독립·연합 가입

새 주민 보유 독립 경로를 실제 실행에 연결했다. 원 전체 후손 검토와 각 주민 보유 도시의 원 명부·의료 설정·profile7·출발/목적 세력을 별도 LawfulResidents 문자열로 고정한다. 기존 주민 없는 API는 그대로 엄격하며 과거 기록에 권한을 보충하지 않는다. 최초 검토와 최종 교체 직전 재검토에서 원 거부·계약·평의회·전체 명부가 같아야 한다.

각 도시를 실제 이전할 때 원 census와 새 실행 기록이 일치해야 한다. 원 호출/의료 journal과 도시 호출 정상 반환을 정치 이력 및 물리 장부에 짝지어 저장·복원하며 일부 이전 후 명시적 복구에도 동일한 원 증거와 현재 연합 동의를 요구한다. 처리하지 않은 도시의 주민·의료 상태가 바뀌면 새 값으로 조용히 대체하지 않는다. 완료한 최초 실제 이전 이후의 별도 후속 이전을 옛 권한으로 해석하지 않는다. 원 Pawn·수술/포로 custody·재고·채무·내부 계약·플레이어 native 통제·마지막 기지 제한은 유지한다. 영한 확인/불가 안내를 실제 지원 범위에 맞췄다.

M2 전체 완료는 아니다. 비로딩 도시의 기존 지원 주민에 대한 연결이며 로딩 도시·미지원 주민의 안전한 실제 이전과 더 넓은 조합은 남아 있다. 고정 점수 M1 10/10·M2 0/18·전체10/100, 1단계 잔여31.5%/8묶음은 유지한다. 2~6단계 잔여80/95/90/100/70%, 묶음5~7/6~8/5~7/6~8/4~6도 그대로다. 최종 검사/설치 결과는 최신 인수인계에 기록한다.

## 0.3.77 — M2 부분 구현: 원 주민 명부와 호출·의료 증거의 실제 저장/복구

주민이 있는 일반 비로딩 도시의 새 이전은 원 도시·출발/목적 세력·보존 프로필·전체 주민 순서와 의료 설정을 census로 고정하고, 이전 장부의 별도 proof와 짝지어 Scribe 저장한다. 같은 주민의 다른 도시 중복/누락 및 다른 이전·지도자 이동 작업의 예약을 거부한다. 원 Pawn 참조와 기존 신원·건강·장비·관계·신분 보존 검사를 유지한다.

주민별 native 호출 시작/같은 Game에서의 정상 반환을 의료 기록과 별도로 저장한다. 정상 반환과 실제 목적 소속을 확인한 직후 의료 출력도 기록하며, 뒤의 보존 검사 실패가 이미 확인한 호출 반환 사실을 지우지 않는다. 재개는 미시작·반환 확인·의료 복구 대기·검증 완료를 구분한다. 호출/의료 반환이 불확실하면 재호출이나 의료 출력 추정을 하지 않고 원본을 유지한다. 완료 장부와 참조 해제는 모든 주민의 원 호출·의료 복구 완료를 요구한다. 화면에 영한 진행 집계를 추가했다. 옛 null 형식은 보충하거나 새 권한으로 재해석하지 않는다.

M2 전체 완료는 아니다. 주민 보유 정당한 독립/전체 후손 정치 이전에 원 의료·호출 증거를 연결하는 경로와 로딩 도시/미지원 주민의 실제 보존 이전은 남아 있다. M3의 넓은 수술·포로·노예 조합도 별도다. M1 10/10, M2 0/18, 전체 잔여 작업 점수10/100과 1단계 잔여31.5%/8묶음을 유지한다. 2~6단계 잔여80/95/90/100/70%, 묶음5~7/6~8/5~7/6~8/4~6도 그대로다.

검증: 양 타깃 전체 빌드, 버전별 Honor20,093개·Contracts5,053개 규칙 검사, 영한535개 문구 검사. 배포 결과는 최신 인수인계를 따른다.

## 0.3.76 — M1 완료: 개별 실제 대표를 사용하는 다중 소유자 후계 복구

고정 M1 체크리스트5개를 코드·실제 적용 경로·저장/복구 규칙·양 타깃 빌드·패키지 기준으로 완료 처리한다. [완료 근거](M1-COMPLETION-AUDIT.md)에 각 항목의 실행 파일과 검사 범위를 정리했다. M1은10/10점, 합의한 잔여 작업군 기준10/100점이다. 기존 환산식의 전체1단계 참고 진척은68.5%, 잔여 노력 추정은31.5%이며 정밀 공수 측정은 아니다.

마지막 미연결이던 다중 실제 소유자의 소실 복구에는 단순한 제한 제거 대신 후손별 실제 대표 서명 map을 넣었다. 원 전체 생존 후손과 NPC 상위 당사자 각각의 실제 소유자·원 이전 증거·현재 대표 수락·상호 평화와 정부 결정을 묶는다. 새 계약은 해당 당사자의 대표만 서명하고 교체하지 않는 내부 계약은 원 서명·조건·채무를 그대로 유지한다. 단일/복합 소실과 직접 반환·옛 반환 사실의 별도 현재 정리에서도 같은 map과 정부 archive를 재계산·짝 검사한다. 원 종료 계약이나 옛 반환 permission을 재작성하지 않는다.

새 필드는 null-omitted 문자열로 옛 저장 형식/일반 단일 대표 경로를 유지한다. 최종 native 재검토 후 원 Game/Book을 대조하고 모델을 함께 교체한다. 미완료 물리 인계·권한/증거 누락·provider/quest 제한은 유지한다. 확인 화면에 각 영지·실제 세력·현재 대표와 영한 안내를 표시한다.

M2~M9의8개 작업군은 아직 완료하지 않았다. 주민/의료·수술/포로/노예·지도자 이동·전체 독립/가입·전시/복합 이적·전체 합병과 그 물리 중단 조합은 고정된 각 작업군에 남는다.2~6단계 잔여 노력 추정80/95/90/100/70%와 큰 묶음5~7/6~8/5~7/6~8/4~6은 변경하지 않았다. 자동 예약은 삭제 상태를 유지한다.

검증:1.6/net472·1.7/net10 전체 빌드와 버전별 Honor20,093개·Contracts5,022개, 영한533개 문구 검사 통과. 최종 원 Game/Book/미완료 인계 guard도 양 타깃 재컴파일했다. 게임 종료 후0.3.76 로컬 설치도 완료했다. 기존 Preview는 백업했고32개 설치 파일의 해시 일치 및 활성화/언어 설정 해시 불변을 확인했다.

## 0.3.75 — 플레이어 당사자 해약·재협상의 정부별 현재 권한

연합·귀족 상대의 실제 합의 해약과 조건 재협상에 각각 별도 현재 결정을 연결했다. 해약은 원 전체 Book·관측·이전/공납 장부·대표·시각을 묶어 원 결과를 재계산하고, 실제 양쪽 해약 서명과 공납 마감을 함께 적용한다.128건/32M Scribe 이력과 root/proof 짝, 원 종료 계약·제삼자 조건/서명·원 영수증·채무 계정·이전 prefix를 복원 때 대조한다. 일반 세력으로 위장하거나 기존 일반 권한 API를 느슨하게 만들지 않았다.

재협상은 기존 Amendment 링크와 archive에 null-omitted 범위 관측·전체 공납 장부·정부 결정의 canonical 문자열을 추가한다. 실제 경제 수락과 정부 결정은 같은 현재 우호도를 사용해야 하며, 옛 해약·새 계약 서명·최종 감사 기록을 기존 경로로 저장/복원한다. 플레이어가 봉신인 경우도 원 scoped 재협상 사건을 다시 계산한 뒤 동일한 실제 종주 아래의 계약 포인터와 무이동 정치 journal을 함께 갱신한다. 기존 채무·납부·서명·후손 계약·native 플레이어 통제를 보존한다. 도시/주민 이전이나 새로운 가입·전쟁 권한은 만들지 않는다.

M1 전체 완료 심사는 아직 보류다. 확인된 다음 통합 범위는 **소실 범위의 생존 후손들이 서로 다른 실제 소유 세력에 있는 경우**다. 현재 TownSuccession은 생존 영지가 원 소실 세력과 같은 실제 세력이어야 한다. 이 제한을 제거하지 않고 원 이전 증거와 각 제삼자 현재 수락을 묶는 복구 경로가 필요하다. 고정 M1 체크리스트의 하위/제삼자 권리·소속 불일치 통합 조건에 해당한다. M1 점수0/10, 여섯 단계 잔여 노력의 기존 대략 추정35/80/95/90/100/70%, 큰 묶음7~9/5~7/6~8/5~7/6~8/4~6을 유지한다.

검증:1.6/net472·1.7/net10 전체 빌드, 버전별 Honor20,093개·Contracts4,970개 검사와 영한532개 문구 검사를 통과했다. 실제 플레이어 계약 포인터/정치 journal의 재협상 저장 복원도 검사했다.0.3.75 패키징과 기존 로컬 Preview 백업/32파일 교체를 완료했으며 활성화·언어 설정 해시는 유지했다.

## 0.3.74 — 집단·귀족 영지 후계와 옛 반환 사실의 별도 현재 정리

연합/귀족 세력의 영지 소실 후계 복구를 실제 검토·적용·저장·복원에 연결했다. 실제 현 대표/작위/구성 도시 정책을 원 전체 복구 계획·원 Book·소실 archive·현재 상위 당사자·전체 관측과 묶는다. 원 계획을 다시 계산하고 결과 root의 GovernmentProof와 별도128건/32M Scribe archive를 짝지어 검사한다. 일반 세력의 기존 Consent를 느슨하게 만들지 않는다.

범위 확대/합병 전에 받은 반환 증거는 원래의 도시 반환 사실로 그대로 보존한다. 현재 전체 범위의 새 수락, 원 반환/소실 이력, 실제 현 소유자, 반환된 모든 영지의 원 양의 증거를 별도 HistoricalReturn 정리 기록에 묶어 생존 후손 계약을 실제 재연결한다. 기존 반환 API는 여전히 확대 범위를 승인하지 못하며, 결과에도 옛 반환 permission 대신 별도 HistoricalReturnProof를 기록한다. 반환 증거를 재작성하거나 native 호출을 반복하지 않는다. 중간 소속 변경 시도·무효화·원 증거/원 장부 누락·미완료 인계는 거부한다. 끝난 원 계약과 기존 채무·서명·제삼자 권리는 유지한다.

확대된 하위 소실, 나중에 상위 범위에 편입된 기존 반환, 연합/귀족 결정과 역사적 반환 정리의 동시 저장·복원 및 별도 marker 누락 거부를 검사했다. 원래 반환 사실을 더 넓은 계약 권한으로 바꾸는 방식이 아니다.

이번 두 소실 경로는 연결했지만 M1 전체 완료 감사에서는 플레이어가 당사자인 합의 해약 및 조건 재협상의 비일반 세력 권한이 아직 일반 세력 전용으로 남아 있다. LiveCharters.ReviewRelease와 Preview/CharterAmendments의 별도 scoped 실행·저장 결정을 이어야 한다. M1 점수0/10을 유지한다. 여섯 단계 잔여 노력의 기존 대략 추정35/80/95/90/100/70%, 큰 묶음7~9/5~7/6~8/5~7/6~8/4~6은 아직 재산정하지 않았다.

검증:1.6/net472·1.7/net10 전체 빌드, 버전별 Honor20,093개·Contracts4,922개 검사와 영한531개 문구 검사를 통과했다.0.3.74를 패키징하고 기존 로컬 Preview를 백업한 뒤32파일을 교체했다. 활성화·언어 설정 해시는 유지했다.

## 0.3.73 — 초기 제안 권한·원 인물 해약·영지별 반환 복구

초기 영지 제안의 NPC 수락과 최종 플레이어 서명에 각각 연합/귀족 정부 결정을 연결했다. 조건·전체 원 Book·관측·납부 장부·대표·시각과 행동을 해시에 묶고 실제 수락 결과를 다시 계산해 저장/복원한다.128건/32M archive와 별도 Scribe marker를 짝짓고, 최종 결정은 실제 최종 서명 audit의 세력·대표·시각과도 일치해야 한다. 현재 결정을 교체 직전에 다시 읽으며 제안 당시 수락으로 나중의 최종 서명을 대신하지 않는다. 옛 일반 제안의 경제 정책과 기존 기록은 유지한다.

퇴임·이주한 연합/귀족 인물은 자신의 원 개인 서약만 별도 정책3/4로 해약한다. 일반 세력으로 위장하지 않고 현재 실제 세력 유형과 등록 여부를 따로 기록한다. 같은 세력의 퇴임 인물은 현직이 아니어야 하며 원 인물의 성인·의식·구금/노예 제외·현재 관계·quest/provider 조건을 유지한다. 이주 시 원 Realm은 보존하고 CurrentRealm과 실제 Faction 참조를 별도로 저장한다. 본인의 해약에 귀족 표나 세력 전체의 동의를 만들지 않으며 적용 직전 원 인물 결정을 다시 확인한다.

상위·하위의 개별 반환 및 나중에 합쳐진 소실 범위에서도 각 영지의 정상 반환 증거를 따로 받아 생존 후손을 실제 재연결한다. 호출 전 당시 전체 소실 범위를 묶고, 반환된 영지마다 원 receipt·scope digest·현재 containing head를 결과에 짝지어 복원한다. 상위가 아직 점령 중이고 하위만 반환된 경우도 처리한다. 소실된 옛 계약은 끝난 채 유지하며 생존 후손의 원 조건·제삼자 권리와 채무를 보존한다. 영토/주민/재고를 재생성하거나 native 호출을 재실행하지 않는다.

M1 전체는 아직 미완료다. 집단/귀족 세력의 전체 영지 소실 후계 권한 어댑터와 범위 확대 전에 이미 받은 단일 반환 증거의 후속 정리가 남는다. 옛 증거를 새로운 범위에 재사용하는 방식으로 완료 처리하지 않는다. M1 점수0/10과 여섯 단계 잔여 노력의 기존 대략 추정35/80/95/90/100/70%, 큰 묶음7~9/5~7/6~8/5~7/6~8/4~6을 유지한다.

검증:1.6/net472·1.7/net10 전체 빌드, 버전별 Honor20,093개·Contracts4,888개 검사, 영한530개 문구 검사를 통과했다. 최종 native 복원 시각 검사도 양 타깃 재컴파일했다.0.3.73 패키징과 기존 로컬 Preview 백업/32파일 교체를 완료했으며 활성화·언어 설정 해시는 유지했다.

## 0.3.72 — 개인 서약의 집단·귀족 생성·승계·해약

연합 대표와 유효 작위 지도자에 대한 양방향 개인 봉신 서약을 실제 생성·현재 당사자 해약·명시적/250tick 후계 승계에 연결했다. 영지용 수락을 재사용하지 않고, 개인 사건의 행동·원 당사자·세력·시각·역할과 전체 원 개인 서약 이력을 별도 해시로 묶는다. 기존 실제 사망 증거·후계 지정·최대4단계·원 Pawn/Faction 참조 조건을 유지하며, 교체 직전 현재 대표와 권한 결정을 다시 대조한다. 복원은 각 원 이력 prefix에 대해 해시를 다시 계산하고, mutable 표 배열을 복사해 외부 수정이 저장 상태를 바꾸지 못하게 한다.

옛 일반 지도자 이벤트는 null-omitted 형식 그대로 남는다. 현직을 잃었거나 이주한 비일반 세력 인물의 별도 개인 해약 정책은 아직 확장하지 않았다. 영지 초기 제안 권한 및 합쳐진 소실/여러 개별 반환의 복구도 남아 M1 전체는 미완료다. M1 점수0/10, 여섯 단계 잔여 노력의 기존 추정35/80/95/90/100/70%를 유지한다.

## 0.3.71 — 계약별 집단·귀족 결정과 소속 분쟁·복합 귀환 복구

NPC 계약 해약과 재봉신의 실제 확인/적용 경로에 별도 정부 결정을 연결했다. 자유도시연합은 실제 구성 도시 전체의 표를 사용하는 명시적 mod 정책v1(플레이어 우호도50 이상)을, 귀족 세력은 실제 현 지도자의 해당 세력 내 유효 작위·서열에 근거한 지도자 결정을 기록한다. 일반 지도자로 위장하거나 귀족 표를 만들어내지 않는다. 결정은 행동 종류·정확한 원 계약/전체 하위 범위·장부·대표·시각의 해시에만 유효하며 다른 계약·전쟁·가입·도시 이동에 재사용할 수 없다. 별도 null-omitted 자료와 원 archive의 해시/짝 검사로 저장·복원한다.

이전 이력으로 설명되지 않는 현재 소속도 계약 해약에서 제한적으로 정리한다. 원 소속 주장자와 실제 현 통제자, 모든 하위 당사자의 현재 수락 및 상호 평화를 요구한다. 원 소실 기록이나 파괴 결과가 있으면 이 경로로 대체하지 않는다. 계약 검토 안에서만 신원을 대조하며 원 세력 기준·실제 관측·원 조건·서명·채무·물리 장부는 바꾸지 않는다. 영토 점령의 적법성을 인정하거나 도시를 이동시키는 처리가 아니다.

상위 영지의 적대 소실 뒤 하위 영지가 추가 소실되고, 그 뒤 상위 영지가 정상 반환된 경우에도 실제 계약 재연결을 지원한다. 반환 호출 전 원 전체 소실 범위를 별도로 묶고 정상 반환 후에만 증거를 기록한다. 현재 전체 범위가 정확히 일치해야 기존 cascade 실행을 허용하며, 소실된 하위 계약은 종료 상태로 남겨 생존 후손만 새 수락으로 연결한다. 옛 단일 반환 증거를 나중에 생긴 소실로 확대하지 않는다. 재진입·불확실 호출 재실행·소유권/건강 복원 권한은 추가하지 않는다.

보류 해약 요청의 원 계약이 실제 소실로 종료된 경우에는 원 요청 Book과 양의 소실 기록을 짝지어 별도 종료 사유(phase3)를 저장할 수 있다. 합의 해약 증거나 NPC 서명으로 바꾸지 않는다. 복원 때 원 소실 이력과 현재 Book을 함께 확인하며, 이후 하위 재연결에도 원 기록이 유지되어야 한다.

M1은 아직 미완료다. 개인 서약/초기 제안의 집단·귀족 경로, 합쳐진 소실 범위·여러 하위 영지의 개별 반환·변경된 원 범위의 재협상이 남는다. 고정 M1 체크리스트 전체가 끝나지 않아 점수0/10과 잔여 작업 점수0/100을 유지한다. 여섯 단계 잔여 노력의 기존 대략 추정은35/80/95/90/100/70%, 큰 묶음7~9/5~7/6~8/5~7/6~8/4~6이다. 자동 예약은 삭제 상태를 유지한다.

## 0.3.70 — NPC 간 계약 종료·하위 권리 보존·보류 요청 복원

NPC 계약 관리 화면에 **NPC 간 상위 계약 합의 해약**을 연결했다. 옛 종주와 봉신의 현재 일반 NPC 수락, 원 전체 하위 계약 각각의 유지 수락과 실제 소속 증거를 확인한다. 해당 계약 하나의 들어오는 연결만 종료하고 하위 계약의 조건·서명·순서와 다른 채무 계정·납부·물리 이전 이력을 그대로 보존한다. 상호 적대하는 실제 세력, 집단/왕족/provider 권한과 확인되지 않은 소속을 이 수락으로 대신하지 않는다. 도시·주민·물품·native 세력·외교는 쓰지 않으며 독립 선포나 연합 가입을 실행하지 않는다.

원 Book·전체 관측·당사자/하위 수락·이전/공납 장부와 결과를 128건/32M 제한의 종료 archive에 저장하고 별도 Scribe root/proof 목록과 짝지었다. 복원 때 원 결과를 재구성·해시 대조하고 현재 Book의 원 종료·제삼자 조건·서명, 기존 이전/채무/영수증 및 시각을 검사한다. 전체 Book 교체도 기존 종료 이력을 훼손하면 거부한다. 오류 시 원 자료를 보존한다. NPC 간 계약의 기록은 일반 플레이어 합의 해약이나 도시 이동 기록으로 소급하지 않는다.

물리 인계나 납부가 미완료인 동안에는 **종료 요청만 저장**할 수 있다. 이는 NPC 동의나 계약 종료가 아니다. 32건/32M의 요청 이력에 정확한 원 Book·이전·납부 상태를 보관한다. 기존 도시 이전 확인/복구 화면에서 원 작업을 처리한 뒤, 원 Book이 같고 원 Prepared 이외의 기존 결과가 그대로 유지되며 모든 미완료 인계가 해결됐을 때 현재 NPC 수락을 다시 받아 종료한다. 완료 요청과 실제 종료 기록은 서로의 proof·Root·Before·시각으로 짝지어 저장/복원한다. 불확실한 호출은 이 경로에서 재실행하지 않는다. 원 범위나 계약이 달라지면 재개를 거부하고 요청을 보존하며 요청만 취소할 수 있다. 소실/파괴 결과를 성공한 종료나 새로운 동의로 바꾸지 않는다.

M1의 일반 NPC 간 종료와 보류 요청 복구를 실제 실행에 추가했지만 **M1 전체는 미완료**다. 집단/귀족 권한, 원 증거 없는 실제 소속 변경의 권리 정리, 원 Book을 바꾼 복합 소실/귀환 뒤의 종료 재협상이 남는다. M1 0/10·잔여 작업 점수0/100과 여섯 단계 잔여 노력35/80/95/90/100/70%, 큰 묶음7~9/5~7/6~8/5~7/6~8/4~6의 기존 대략 추정을 유지한다.

## 0.3.69 — 서로 다른 실제 세력의 당사자·하위 영지 계약 재배치

NPC 계약 관리 화면의 기존 재배치 실행을 확장했다. 옛 종주·봉신·새 종주의 도시가 서로 다른 일반 NPC 세력에 있어도, 각 도시의 원 소속 또는 완료된 이전 이력으로 실제 소속이 증명되고 각 현재 대표가 수락하면 진행한다. 새 종주는 플레이어의 현재 실제 종주 세력에 있어야 한다. 기존 NPC 간 계약의 해약은 옛 종주와 봉신의 현재 대표 수락을 각각 받고, 새 계약은 새 종주와 봉신의 실제 대표를 구분해 서명한다. 원 서명·조건은 덮어쓰지 않는다.

하위 영지 전체 중 다른 실제 세력에 있는 도시는 별도의 RetainedRealms 수락을 받는다. 이는 **원 계약·도시·주민·재고를 그대로 두는 수락**으로, 도시 이전이나 다른 세력을 대신하는 권한이 아니다. 원 전체 계약 장부·현재 관측·기존 물리 이전/공납 이력과 모든 정확한 당사자/하위 목록을 원 재배치 기록에 묶는다. 모든 관련 실제 세력이 서로 비적대해야 하며 각 세력의 현재 일반 권한·우호도·quest/provider 제한은 기존대로 검토한다.

확인 직전에 현재 수락·전체 소속·원 장부와 동료 기록을 다시 검토하여 최초 확인 stamp와 같아야만 Book과 재배치 archive 복사본을 함께 적용한다. 이후 저장 복원에서 원 결정을 재구성·해시 대조하고 이전/현재 계약 링크, 원 하위 조건·서명·채무 및 이전/공납 이력을 함께 검증한다. 실패한 원 자료는 유지하고 추가 변경을 막는다. Parties/RetainedRealms=null인 옛 JSON·해시·API 의미는 유지한다. 이 작업은 실제 계약 재배치이며 native 소유권·주민·의료·물품·외교를 쓰지 않는다.

**M1 전체는 아직 미완료**다. 진행 중인 영지 소실/불확실한 물리 호출이 겹친 이적·종료, 집단/귀족 권한과 원 증거 없는 소속 변경을 포함한 전체 계약 정리·복구는 남아 있다. 이번 수락을 그런 권한으로 확장하거나 옛 이전을 재실행하지 않는다. M1 0/10·잔여 마일스톤0/100 유지. 여섯 단계 잔여 노력35/80/95/90/100/70%, 큰 묶음7~9/5~7/6~8/5~7/6~8/4~6은 기존 대략 추정이며 이번 검사 개수로 다시 환산하지 않는다.

## 0.3.68 — NPC 개인 봉신·양쪽 승계·퇴임자 해약과 사망 증거 복구

주민의 개인 봉신 서약 화면에서 일반 NPC 지도자를 **개인 봉신으로 수락**하는 실제 경로를 추가했다. NPC 세력이나 도시를 플레이어에게 넘기는 동의로 해석하지 않는다. 플레이어가 주군인 서약에서도 해당 플레이어가 후계를 지정하고, NPC 봉신은 실제 native 지도자 선정 결과로 승계한다. 양쪽 모두 사망하면 NPC 쪽 실제 후임을 먼저 확인한 뒤 플레이어 쪽 지정 후계를 연결한다. 원 인물·역할·서약 ID·전체 이력은 유지한다.

이제 원 Pawn의 실제 사망을 관측하면 그 당사자와 상대방·원 서약·시각을 DeathObserved로 먼저 저장한다. 새 승계는 이 긍정적 사망 기록을 요구한다. 이후 원 사망자 참조가 없어져도 원 기록과 현재 후임을 검토하여 같은 서약을 승계할 수 있다. 인물이 없다는 사실만으로 사망을 추정하지 않으며 알려진 같은 ID의 생존/부활 인물이 있으면 승계하지 않는다. 옛 0.3.67 이력의 null/false 생략 바이트와 기존 승계 의미는 유지한다. 복원 때 새 사망 표식이 증거와 맞지 않거나 장부 시각이 현재 게임보다 앞서면 원 자료를 보존하고 변경을 중지한다.

개인 서약의 **합의 해약**도 실제 화면·수락·저장에 연결했다. 현재 일반 NPC 대표라면 기존 권한 정책을, 같은 일반 세력에 남은 생존 퇴임자라면 자기 개인 서약만 종료하는 개인 수락 정책v1을 사용한다. 후자는 실제 성인·행동 가능·비구금/노예·quest 비예약·현직 없음·우호도50/상호 비적대·provider 없음 등을 확인하며 새 지도자가 옛 지도자의 서명을 대신하지 않는다. 종료 사유와 시각은 새 이력으로 남고 원 하위 개인 서약·다른 계약·영지 장부·채무는 변경하지 않는다. 해약 후 새 서약을 맺더라도 옛 이력은 별도로 유지한다.

다른 일반 세력으로 이적한 원 인물도 개인 수락 정책v2로 자기 서약을 합의 해약할 수 있다. 원 계약의 Realm은 바꾸지 않고 별도 CurrentRealm과 실제 현재 Faction 참조를 저장·대조한다. 원 인물의 현재 행동 가능·성인·비예약·현재 세력 우호도50/상호 비적대 등을 확인하며 현재 세력이나 도시를 대신 처분하지 않는다. **M1 전체는 미완료**이며 집단/복합 NPC 권한과 중첩된 실제 영지 소속 불일치의 전체 복구·권리 보존은 남아 있다. 개인 공납·복무·보호의 본격 집행은 원 계획의 2/5단계 작업이며 이번 M1 기본 서약 구현으로 완료됐다고 계산하지 않는다. M1 0/10, 잔여 마일스톤0/100; 여섯 단계 잔여 노력35/80/95/90/100/70%, 큰 묶음7~9/5~7/6~8/5~7/6~8/4~6 유지.

## 0.3.67 — 개인 봉신 서약의 실제 생성·지정 후계·저장 승계

Contracts Preview의 주민 명령에 **개인 봉신 서약** 화면을 추가했다. 현재 결정 가능한 일반 NPC 지도자에게 기존 일반 권한 정책(우호도 50 이상·상호 비적대·독립 권한)에 따라 개인 충성 서약을 생성한다. 플레이어는 성인 자유 주민을 후계자로 지정하며 최초 인물·원 세력·수락 근거·지정 변경·승계를 같은 append-only 서약 장부에 유지한다. 최대 4단계/순환/중복 개인 소속을 검사한다. Pawn·Faction은 native 원 참조로 별도 Scribe하고 장부는 버전 1 JSON으로 저장한다. 불러올 때 전체 이력과 참조 집합을 대조하며 손상된 원 자료는 보존하고 추가 변경을 중지한다.

250틱마다 사망한 원 봉신의 지정 후계와 사망한 원 주군의 실제 native 후임을 확인하여 같은 서약에 승계를 추가한다. 원 주군과 봉신이 모두 사망한 경우 실제 주군 후임을 먼저 연결하고 지정 봉신 후계로 이어간다. 현재 NPC 권한·원 사망 인물·지정 후계가 확인되지 않으면 자동 변경하지 않는다. 주민/지정 후계 화면의 명시적 재검토도 같은 저장 장부와 검사 경로를 사용한다. 확인창 이후 다른 서약 변경이 발생하면 오래된 확인을 거부한다. 이미 반영된 승계를 재실행하거나 새 Pawn을 생성하지 않는다.

이 초기 개인 서약은 충성·상속 기록의 실제 경로다. **개인 서약의 공납·복무·보호 임무 집행과 자체 미납·실적은 아직 연결하지 않았다.** 기존 영지 계약·공납·실적·채무는 건드리지 않으며 개인 채무로 복사하지 않는다. 귀족/연합 집단 권한, NPC 개인 봉신 전체, 생존 주군의 면직/다른 세력 이적, 원 인물 참조가 소실된 경우의 추가 사망 증명 및 임의 도시 소속 복구는 미완료다. 따라서 M1 전체 완료가 아니며 M1 0/10, 잔여 마일스톤 0/100을 유지한다. 여섯 단계 잔여 노력35/80/95/90/100/70%, 큰 묶음7~9/5~7/6~8/5~7/6~8/4~6 유지.

## 0.3.63 — 하위 영지를 동반하는 정당한 독립의 초기 실제 집행

계약상 보호의 고의 거부로 정당한 독립을 선택할 때, 저장된 주민이 없는 직속 영지와 그 아래 영지 전체가 원 종주 세력에 있고 원 세력과 자유도시연합이 서로 적대하지 않는 범위의 실제 연합 가입·도시 이전을 연결했다. 원 전체 계약·거부·정치·이전 장부와 모든 하위 영지의 원 소유자·재고 증거를 검토에 묶고, 실제 연합 대표와 구성 도시의 수락 후 원 계약 종료·공납 마감·정치 journal을 함께 적용한다. 이후 기존 도시별 native 이전·공유 외교·명시적 중단 복구를 이용한다. 종주의 해약 서명을 만들거나 플레이어 native 세력을 바꾸지 않는다.

각 새 도시 이전은 원 검토에 있던 영지·원 세력·연합 목적지와 원 재고 증거에만 허용한다. 옛 이전 이력은 순서와 결과를 그대로 유지하며, 새 결과는 그 뒤에 별도로 붙인다. 진행 중에는 원래의 전체 하위 목록을 유지하고 이미 확인된 기존 결과를 재실행하지 않는다. 각 단계에서 현재 연합 의결과 기존 도시·재고·원 세력의 마지막 정착지 보존 조건을 확인한다. 미완료 이전을 복구할 때도 원 법적 출처와 전체 journal을 먼저 확인한다. native 집행 콜백의 재진입과 집행 중 별도 도시 이전은 차단한다.

이 범위는 원 거부 당시의 계약 장부·정치 이력이 변하지 않았고 원 계약이 재협상된 계보가 아닌 경우로 제한된다. 적대하는 원 세력에서의 이전, 추가 전쟁 수락, 진행 중 영지 소실·재탈환과 그 복구는 아직 지원하지 않는다. 저장된 주민이 있는 영지는 의료 설정 복구의 별도 증거가 구현되기 전까지 보류하며 다른 기존 제한도 유지한다. **전체 정당한 독립·전체 합병과 1단계는 아직 미완료**이며 여섯 단계 잔여 노력35/80/95/90/100/70%, 큰 묶음7~9/5~7/6~8/5~7/6~8/4~6을 유지한다.

## 0.3.62 — 완료된 영지 이전 이력을 보존하는 정당한 독립

하위 NPC 영지가 없는 정당한 독립·연합 가입에서 완료·취소·파괴로 이미 정리된 영지 이전 이력이 있어도 진행할 수 있도록 실제 검토·집행·복구 경로를 확장했다. 원 전체 이전 장부와 각 이력의 소유자·결과를 그대로 보존하며, 이번 가입에서 도시·주민·재고를 이전하거나 과거 이전을 다시 실행하지 않는다. 아직 준비 중인 이전은 진행할 수 없다.

새 기록은 기존 이력이 있는 별도 형식을 명시하고 원 전체 장부와 해시를 최초 후손 검토·정치 journal·보호 거부/계약 종료 증거에 묶는다. 집행과 중단 복구 중 현재 이전 장부가 조금이라도 달라지면 보류한다. 완료된 과거 독립 기록은 이후 정상적인 새 이전을 영구적으로 막지 않는다. 기존 이력 없는 기록은 옛 형식과 API·바이트를 유지하며 새 권한을 소급 부여하지 않는다.

하위 NPC 영지의 실제 이동·원 소유자 증명, 재협상/변경된 원 계약 계보, 추가 전쟁 수락은 여전히 미완료다. 이번 제한된 실제 집행 분기는 1단계 전체나 독립 작업 묶음의 완료가 아니다. 여섯 단계 잔여 노력35/80/95/90/100/70%, 큰 묶음7~9/5~7/6~8/5~7/6~8/4~6을 유지한다.

## 0.3.61 — 하위 영지가 없는 정당한 독립·연합 가입의 실제 집행

검증된 계약상 보호 고의 거부를 근거로 플레이어가 명시적으로 독립을 선택하고, 현재 자유도시연합의 실제 대표·구성 도시 표결에서 수락하면 가입하는 초기 실행 경로를 연결했다. 원 계약 종료·공납 마감·거부 증거·연합 의결·원 정치 명령과 빈 전체 후손 범위를 복사본에서 함께 검증한 뒤 원 장부와 정치 journal을 묶어 적용한다. 이후 기존 공유 외교 집행과 명시적 중단 복구를 사용한다. 플레이어의 native 세력과 주민 통제는 그대로 유지한다.

이 경로는 **하위 NPC 영지와 과거 물리적 영지 이전이 모두 없고, 연합 가입에 새 선전포고가 필요하지 않은 경우**로 제한된다. 원 거부 당시의 계약·정치 이력이 유지되고 재협상 계보가 없어야 한다. 현재 연합·대표·실제 표결과 기존 quest/provider 제한을 다시 검토한다. 원 계약의 최초 서명·기존 채무·납부 기록은 보존하고 종주의 해약 서명을 만들지 않는다. 원 거부·종료 표식·정치 실행 기록·원 공납 계정과 영수증을 저장과 복구 때 서로 대조한다. 기록이 누락·변조되거나 미완료 적용을 완료로 가장하면 원 자료를 유지하고 변경을 차단한다.

**전체 정당한 독립은 아직 미완료다.** 하위 영지의 전체 이전·기존 물리 이전 이력·재협상 계보·추가 전쟁 수락과 그 중단 복구가 남는다. 전체 합병·귀족 집단 권한·미지원 주민/지도자/지도/재고·상위/진행 중/재진입 소실도 별도 남은 작업이다. 이번 제한된 실제 집행 분기를 전체 작업 묶음 완료로 계산하지 않으며 여섯 단계 잔여 노력35/80/95/90/100/70%, 큰 묶음7~9/5~7/6~8/5~7/6~8/4~6은 유지한다.

## 0.3.60 — 정당한 일방 종료의 복사본 준비 구조 (집행 미연결)

원래 기록된 계약상 보호 고의 거부를 검증하고, 원 계약과 공납 장부의 복사본에서만 일방 종료·마감을 준비하는 LawfulExitPreparation을 추가했다. 새 LawfullyEnded 단계는 합의 해약과 구분하며 NPC/플레이어 해약 서명을 만들지 않는다. 원 조건·양측 최초 서명·하위 계약·납부 영수증·이전 기록은 유지한다. 미납 기존 회차는 계속 채무로 남고 종료 시각 이후 공납은 새로 발생하지 않는다. 기존 합의 해약/이전 API는 이 새 단계를 이전 권한으로 인정하지 않는다.

첫 준비 경로는 동일한 미해약·미재협상 원 계약, 원 거부 당시의 전체 계약/정치 이력, 현재 native player/종주 주체, 미확정 지원/예약 물품/진행 중 이전 없음, 전체 연결 영지의 현재 관측을 요구한다. 원 관측도 snapshot에 보존하고 확인 시각·최신 관측·모든 원 장부를 재검토한다. 저장형 준비 결과는 원 입력/원 거부/계약·공납 출력의 해시와 정확한 재구성으로 검증하며 별도 실제 거부 이력과 짝이 맞아야 한다. 구조상 맞는 종료 표식만으로는 증거가 되지 않는다.

**현재는 준비 구조와 규칙·저장 검사만 완료했다. 실제 게임에서 계약을 일방 종료하거나 독립시키는 버튼/집행기는 연결하지 않았다.** 연합의 현재 의결·기존 전쟁 수락, 원 전체 후손 검토, 별도의 적법한 이전 출처 증명, 실제 소속·전체 영지 이전·외교와 여러 장부의 원자적 적용/중단 복구를 함께 연결해야 한다. 이 준비 결과를 실제 게임 원장에 단독 적용해서는 안 된다. 재협상 계보 등 넓은 종료 조건도 아직 보류한다. NPC 동의로 위장하거나 옛 해약 API를 우회하지 않는다.

1단계 전체와 독립 기능은 미완료다. 남은 노력35/80/95/90/100/70%, 큰 묶음7~9/5~7/6~8/5~7/6~8/4~6 유지. 이번 준비 구조를 독립 기능이나 작업 묶음의 완료로 계산하지 않는다.

## 0.3.59 — 계약상 보호 요청에 대한 명시적 NPC 거부

실제 보호 약정이 있는 플레이어 영지의 공동 적 공격 요청에 새 NPC 응답을 연결했다. 일반 종주의 현재 결정 가능한 대표가 비열함(Honor_Vile) 특성을 가진 경우, native 지원 대상/발동 조건과 현재 map·세력·points·우호 지원 전략을 사용한 실제 combat group 구성 가능 조건을 모두 확인한 뒤 배치를 고의 거부한다. 이는 모드의 명시된 ordinary NPC 응답 정책v1이며, 낮은 명예나 지원 불능을 위반으로 추정하는 판정이 아니다. 병력은 생성·파견하지 않으며, 능력 검사 중 변경/예외는 거부 증거가 되지 않는다. 검사 후 원 요청·원장·공유 정치 상태·대표·실제 공격/인물 목록을 새로 대조한 뒤에만 결정을 기록한다.

원 약정·원 요청 해시·현재 결정권자·특성/정책·확인 points·시각을 별도 IntentionallyRefused 결과로 저장하고 영한 편지/보호 이력에 표시한다. 원 native 호출의 Applying/실패/미확인/생성/도착 결과를 거부로 재분류하지 않는다. Refuse는 새 요청에만 가능하며 이미 시작된 불확실 호출은 변환·재실행할 수 없다. 원 계약 조건/서명/채무·하위 계약·소속·외교는 바꾸지 않는다. 같은 출처의 일일 요청 제한도 적용한다. 옛 enum 값/생성자/Refusal=null 생략 바이트를 유지하고 변경·혼합된 거부/호출 증거는 저장 복원 때 거부한다.

정당한 독립의 근거가 될 개별 거부 사건의 실제 응답·보존 경로이며, 독립권 행사/일방 계약 종료·연합 수락/전체 후손 이전·복구는 아직 미구현이다. 거부 1회로 자동 독립·선전포고·불명예/채무 초기화를 적용하지 않는다. 지원 실패·지원 불능·중단·패전과는 계속 구분한다. 집단/왕족/provider 결정은 개인 정책으로 대신하지 않는다.

현재 집중1단계 미완료. 남은 노력35/80/95/90/100/70%, 큰 묶음7~9/5~7/6~8/5~7/6~8/4~6 유지. 다음 실제 큰 작업은 검증된 원 거부 사건을 사용하는 명시적 독립권 행사와 일방 종료·연합/전쟁 수락·전체 후손/채무 이전의 원자적 집행 및 복구다. 전체 합병 native 생명주기·귀족 집단 권한·미지원 보존/소실 통합도 남는다.

## 0.3.58 — 수술 지시를 보존하는 지도자 개인 합류·복구

새 지도자 개인 합류는 지원되는 미착수 native Bill_Medical 원본을 별도 승계·소속 변경·명시적 복구에 연결한다. 동의 당시의 정렬된 지시/ID/레시피/신체 부위/필터·설정 snapshot을 새 optional SurgeryOrders에 저장하고 profile7의 원 신원 지문과 함께 대조한다. 최종 실행은 새로 검토한 원본을 사용한다. 실제 직위 승계를 별도로 기록한 뒤, 소속 변경 호출 전에 원 지시를 별도 보관하고 native stack만 비운다. 확인된 변경 뒤에는 같은 Bill 참조·ID·순서를 원 pawn stack에 재부착하고 나서 의료 설정·목적지 추가·출발지 제거·완료로 진행한다. 지시의 복제·재발행·의료 상태 수정은 하지 않는다.

중단된 분리 승계 기록은 원본 보관 상태를 다시 확인해 아직 시작하지 않은 소속 변경만 진행한다. 이미 시작했으나 결과가 불확실한 호출은 재실행하지 않는다. 양쪽 동시 보관·누락·순서/부위/설정 변경과 외부 holder는 거부하며 원 자료를 유지한다. 분리된 지시는 기존 ResidentSurgeryRecord의 deep-save/원 신체 부위 링크 복원을 사용하고, 붙어 있는 지시는 pawn의 native 저장만 사용한다. 원본 재부착 검증 전에는 완료나 참조 해제가 불가능하다. Prepared 취소는 원 지시가 pawn에 붙어 있는 상태에서만 가능하다. 옛 null SurgeryOrders와 생성자/지문/저장 바이트는 유지하며 옛 기록에 새 권한을 채우지 않는다. 새 형식은 profile7와 별도 승계가 필수다.

시작된 수술·소모 약품/특수 재료·커스텀 Bill·퀘스트/왕족/노예/로딩 맵 등은 기존 검사로 보류한다. 출발 세력 존속·현재 권한·원 계약/채무/명부/재고·한 번의 의료 설정 확인·후임의 no-redress/새 관계 제한도 유지한다. 전체 합병이나 도시 소유 이전을 추가한 것은 아니다. 설치1.7 DLL의 SetFaction이 surgeryBills.Clear를 호출함과 BillStack의 원 Bills/Clear를 재확인했다. 새 경로는 두 접근의 외부 Harmony 패치도 거부한다.1.6은 참조 시그니처 기준으로 분리 빌드한다.

현재 집중1단계 미완료. 잔여 노력 추정35/80/95/90/100/70%, 큰 묶음7~9/5~7/6~8/5~7/6~8/4~6 유지. 전체 합병 집행/복구·정당한 일방 독립·귀족 집단 권한/복잡 전시 이적·미지원 주민/지도자/맵/재고와 상위/진행 중/재진입 소실이 남는다.

## 0.3.57 — 적재 교역 동물이 있는 도시의 지도자 전출·복구

같은 세력 내 직위 유지 전출이 양쪽 도시의 기존 stock format2를 받아들이도록 실제 계획·복원 경로를 연결했다. 이는0.3.38부터 사용하는 native 교역 동물/일반 적재 물품 보존 검사와 같은 증거다. 기존 동물 신원·소속·건강/장비/상태, 동일 inventory의 물품 ID/정의/재질/수량/HP/품질과 원 unload/not-for-sale/unpacked 설정을 그대로 대조한다. 실제 전출은 같은 지도자 참조의 목적지 추가→출발지 제거→검증만 수행하며 재고·동물·화물은 이동하거나 생성하지 않는다.

이전에는 실제 보존 가능한 format2를 Capture가 반환해도 전출 계획의 형식 범위가0~1이라 막혔다. 형식2를 허용하되 원 stock proof와 source/destination format을 계속 저장·검증하고, 수량/설정/형식 변경이나 unknown format3 이상은 거부한다. 기존0/1 지문·저장 형식과 지도자 stored profile/명부/현재 직위·권한/의료/계약 보존 가드는 유지한다. 일반 아이템 이외 중첩 컨테이너·시체·사람/미지원 동물은 기존 검사를 우회하지 않으며, 개인 합류·도시 소유 이전·합병 권한으로 확장하지 않는다. 새 native API/hook은 없다.

현재 집중1단계 미완료. 잔여 노력 추정35/80/95/90/100/70%, 큰 묶음7~9/5~7/6~8/5~7/6~8/4~6 유지. 전체 합병 집행/복구·정당한 독립권·귀족 집단 권한·복잡 전시 이적 및 미지원 주민/지도자/로딩 맵/재고·상위/진행 중/재진입 소실은 남는다.


## 0.3.56 — 기본 유전자 지도자의 개인 합류·직위 유지 전출

새 지도자 개인 합류와 같은 세력 내 안전한 거처 이전은 기존 주민 profile7의 검사를 재사용해 지원되는 native 기본 Gene 원본도 검증한다. ID/순서·내생/이식 분류·원 저장 유전형/이름/아이콘/혼합 여부·passionPreAdd·같은 트래커 억제 관계를 기존 신원/건강/장비/관계/신앙/의료 증거에 묶는다. 실제 별도 직위 승계→소속 적용→한 번의 의료 설정 확인→같은 인물의 명부 이동 및 같은 세력 전출/명시적 복구 경로에 연결했다. 유전자 생성·제거·재발현이나 옛 상태 복원으로 검사를 통과시키지 않는다.

새 기록만 explicit ResidentProfile7을 저장한다. 이전의 생략된0 표시는 계속 profile5이며, 원 생성자·Capture/Read APIs·null/default JSON과 지문을 유지한다. 복구의 모든 actor 읽기는 저장된 형식을 사용하고 형식 혼용·알 수 없는 값·새 유전 합류의 옛 combined 호출 사용을 거부한다. 전출 profile도 그 원 proof에 포함한다. 원본 유전자나 명부/의료/권한이 달라지면 원 기록을 남긴 채 보류한다. 개인 합류는 출발 세력 존속·별도 동의·분리 승계/소속 journal과 기존 no-redress/새 관계 제외 범위를 유지한다.

1.7.4990 설치 DLL의 원 xenotype/cachedGenes 필드와 캐시가 xenogenes→endogenes 원 참조로 구성되는 것을 다시 확인했고,1.6.4871 참조의 같은 필드/공개 접근 시그니처를 확인했다. 새 native hook은 없다. 특수/커스텀 Gene 클래스·외국/손상 링크·미확인 패치는 기존 검사로 보류하며, 수술 지시·노예·왕족 작위·로딩 맵·마지막 출발 도시 등 미지원 지도자 상태를 새 형식으로 우회하지 않는다. 전체 합병·도시 소유 이전·이동/도착 보장을 추가하지 않는다.

현재 집중1단계 미완료. 잔여 노력 추정35/80/95/90/100/70%, 큰 묶음7~9/5~7/6~8/5~7/6~8/4~6 유지. 전체 합병 native 집행/복구·정당한 독립권 행사·귀족 집단 권한·복잡 전시 이적 및 미지원 주민/지도자/로딩 맵/재고·상위/진행 중/재진입 소실은 남는다.


## 0.3.55 — 일반 봉건 가입의 현재 통치자 동의·승계 재검토

새 일반 봉건 가입은 이미 서명된 실제 플레이어 계약과 현재 일반 세력의 등록된 유일·생존·결정 가능한 통치자, 우호도50/상호 비적대 및 기존 provider/퀘스트 제한을 확인한다. 서명만으로 현재 소속 적용 동의를 추정하지 않고 별도의 Feudal 동의를 원 전체 후손/이전 검토와 함께 저장한 후 실제 정치 상태·공유 외교·전체 후손 이전 경로를 진행한다. 귀족·연합 등 집단 체제는 개인 통치자로 대신 의결하지 않는다.

실제 평시·전시 소속 적용과 중단 복구에 연결했다. 원 계약/통치자 동의/전체 이전 목록·자료를 보존하고, 후임이나 변경된 현재 권한은 별도 명시적 재검토 뒤 최대64회 이력으로 덧붙인다. 각 새 도시 이전과 외교 반영 전후, 각 별도 전시 선언 전후의 현재 일반 통치자를 확인한다. 기존 전쟁 수락/확인된 결과는 별개로 보존하며, 일반 가입 동의만으로 전쟁을 만들거나 확인된 선언을 재실행하지 않는다. 권한 확인 불능을 고의 보호 거부·불명예·독립권으로 취급하지 않는다.

신규 Feudal/FeudalReaffirmations는 null 생략하며 옛 null 저장/생성자/Prepare APIs를 유지하고 과거 결정을 소급 채우지 않는다. 새 Feudal 기록은 원 FollowerReview와 실제 계약/명령/native player/목적지·전쟁 역할을 함께 검증하며 복구/소실 시간·계보에 묶는다. 부분 이전과 후속 경제 재협상 뒤에도 원 동의·승계 이력은 bounded 정치 history에 남는다.7M 안전 한도/크기 실패 시 원 모델 보존과 기존 주민/건강/의료/재고/마지막 도시 가드는 유지한다. 새 native hook/API나 임의 승계/원 서명 재작성은 없다.

현재 집중1단계 미완료이며 잔여 노력 추정1단계35%(7~9묶음),2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6)는 유지한다. 전체 합병 집행/복구·정당한 일방 독립·귀족 집단 권한·복잡 전시 이적 및 미지원 주민/지도자/로딩 맵/재고·진행 중 상위/재진입 소실이 남는다.


## 0.3.54 — 가입 당시 전체 후손 검토의 저장·복구 연결

새 평시·전시 연합 가입은 확정 당시의 전체 계약 장부, 실제 이전 장부와 전체 후손 관찰/이전 계획을 원 검토 문자열과 해시로 정치 journal에 함께 보존한다. 실제 가입 준비 경로에서 현재 확인한 계획을 저장한 뒤에만 정치 상태를 적용한다. 복원과 중단 후 재개는 이 원 장부/후손 검토를 다시 계산해 원 준비 시각·목적지·native player·물리 장부 기준 해시·전체 후손 및 실제 incoming 목록이 같은지 확인한다. 현재 소유 상태나 일부 생존/미이전 도시로 원래 계획을 추정하지 않는다.

일부 도시가 이미 이전된 뒤에도 원 입력은 그대로 보존하며, 현재 상태의 안전한 개별 이전·복구는 기존 별도 증거와 재검토를 따른다. 이 저장은 권한·검토 범위 메타데이터이며 원 주민/건강/의료/재고를 다시 쓰거나 소유권 실행을 재호출하는 권한이 아니다. 기존 원 계약 조건·채무·서명·심층 하위와 실제 이전 영수증을 유지한다. 소실 정치 계보 검사도 원 FollowerReview를 바꿀 수 없도록 묶었다.

새 null-omitted FollowerReview와 별도 PrepareReviewedAdmission을 사용하므로 옛 생성자/Prepare/PreparePeace·null 저장 바이트는 유지한다. 옛 기록에 없는 원 자료를 현재 상태로 채우지 않는다. 원 자료나 전체 journal이7M 안전 한도를 넘으면 원 모델을 바꾸기 전에 거부하며 삭제/축약/가드 해제로 완료를 만들지 않는다. 영한 화면에서 전체 원 계획을 보존하는 동작을 안내한다. 새 native API·hook·외부 모드 권한 우회나 자산 보존 예외는 없다.

현재 집중1단계 미완료이며 잔여 노력 추정1단계35%(7~9묶음),2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6)를 유지한다. 전체 합병 집행/복구·정당한 일방 독립·귀족 집단 권한·복잡 전시 이적과 미지원 주민/지도자/로딩 맵/재고·진행 중 상위/재진입 소실은 남는다. 이번 원 검토 보존으로 그 실행들을 완료로 부르지 않는다.


## 0.3.53 — 전시 연합 가입과 동반 이전의 평의회 범위 통합

새 전시 연합 가입은 원래의 실제 동반 이전 목록을 별도 IncomingCharters에 묶는다. 기존 회원 표와 아직 이전 중인 도시를 구분하여, 최초 Moves와 같은 목록 및 실제 Applied 이전 영수증·같은 계약/현재 소유·준비 시각이 맞는 도시에만 완료 전 표 제외를 허용한다. 가입 완료 뒤에는 정상 회원 도시로 집계한다. 평시와 전시는 같은 실제 이전 증거 검사와 명부 구성 함수를 사용한다.

실제 전쟁 수락→native 적대 반영→공유 외교→전체 후손 도시 이전→중단 복구 경로에 연결했다. 현재 유일한 결정 가능한 연합 대표·실제 평의회와 같은 전쟁 범위를 명시적으로 재검토하며, 각 새 도시 이전 전후 및 외교 반영 때 판단이 달라지면 이미 확인한 결과를 남기고 중단한다. 복구는 원 수락/전쟁 결과/동반 목록을 바꾸지 않고 기존 bounded 재수락 이력에 덧붙인다. 확인된 선전포고를 다시 호출하거나 나중 화평을 선전포고로 되돌리지 않는다. 새 범위가 있는 전시 가입 중 자동 플레이어·관리청 외교 공유는 명시적 공유 범위 밖에서 정지한다.

새 속성은 null 생략하며 옛 생성자·공개 Review/Prepare API·기존 JSON/전쟁 증거 바이트를 유지한다. 기존 null 저장에 동반 이전 권한을 소급 추정하지 않으며, 봉건 개인 동의를 연합 표 제외 권한으로 바꾸지 않는다. 새 범위는 최초 준비/복원/재수락 시 전체 후손 목록 및 원 전쟁과 함께 검증한다. 준비·재수락의7M 크기 한도로 기존8M 정치 저장 안에 후속 결과·소실 기록 공간을 남기고 실패 때 원 모델을 보존한다. 새 native hook/API, 외부 제공자 우회, 마지막 도시/주민/재고 가드 예외는 없다.

현재 집중1단계 미완료. 잔여 노력 추정1단계35%(7~9묶음),2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6)는 유지한다. 전체 합병 집행/복구·정당한 일방 독립·귀족 집단 권한·복잡 전시 이적과 미지원 주민/지도자/로딩 맵/재고·진행 중 상위/재진입 소실은 남는다. 이번 전시 가입 통합으로 다른 잔여를 완료로 부르지 않는다.


## 0.3.52 — 평시 연합 가입의 실제 평의회 수락·집행·복구

평시 자유도시연합 가입에 현재 실제 회원 도시마다 기본1표·과반수 수락을 적용하고, 해당 수락을 실제 가입 정치 journal에 저장한다. NPC 기본 판단은 회원 연합 우호도50, 회원 도시가 없는 창립 연합은 현 창립 대표 -50이며, 기여/규모 가중치나 개별 도시 성격은 아직 추정하지 않는다. 현재 등록된 연합/유일한 결정 가능한 대표·퀘스트/외부 제공자·상호 비적대·실제 회원 도시와 기존 전쟁 충돌을 재검사한다. 평시 수락으로 새 전쟁을 부담하거나 기존 전쟁을 없애지 않는다. 전시 수락은 기존 별도 경로다.

실제 가입·공유 외교·전체 후손 이전 경로에 연결했다. 각 새로운 도시 이전 전후와 외교 반영 후에 원 수락 또는 마지막 명시적 재검토와 같은 대표/평의회 판단인지 검사하고, 달라지면 부분 결과를 보존한 채 중단한다. 원래 동반 이전 목록을 가입 수락에 함께 묶고 실제 Applied 이전 영수증·현재 소유·원래 준비 시각/계약이 일치하는 도시에만 가입 완료 전 표 제외를 허용한다. 단순 소유 변경이나 Prepared/옛 이전으로 회원 표를 빼지 않는다. 완료 후 다음 가입부터는 해당 도시도 회원 표를 갖는다. 기존 단일 도시 보존/회복 경로는 원 물리 journal만 정리하며 정치 가입을 자동 완료하거나 새로운 평의회 동의를 만들지 않는다.

중단 후 현재 대표·실제 평의회를 다시 검토하고 별도 수락을 최대64회 덧붙인다. 원 수락·모든 기존 채무/하위 조건·이전/소실 기록을 보존하며 동일한 재검토는 공간을 재소비하지 않는다. 크기 초과는 원 모델을 바꾸기 전에 거부한다. 과거 완료/취소 수락도 기존 bounded 정치 history에 정확한 명령 계보와 함께 보존한다. 미완료 평시 가입 중 자동 플레이어/관리청 외교 공유는 명시적 조정 범위 밖에서 정지한다. 직접 소속/주민/재고/돌봄/정신 상태 가드를 완화하지 않는다.

새 Peace/PeaceReaffirmations 속성은 null 생략하며 기존 생성자·Prepare API와 옛 JSON 바이트를 유지한다. 이미 있던 null 평시 기록을 소급 표결로 채우지 않는다. 소실 증거의 시간·정치 계보 검사에도 새 기록을 묶고 손상 원본은 보존한다. 이는 평시 연합 가입의 실행 기반이며 합병·정당한 일방 독립·귀족 집단 의결이나 가중 정치 시스템 전체 완성이 아니다. 현재1단계 미완료, 잔여 노력 추정35/80/95/90/100/70%, 큰 묶음7~9/5~7/6~8/5~7/6~8/4~6 유지.


## 0.3.51 — 현재 NPC 권한으로 합의 해약·공납 마감 원자 적용

기존 합의 해약 버튼의 살아 있는 지도자/우호도20 검사와 서명 선기록을 교체했다. 이제 실제 등록된 일반 NPC의 현재 결정 가능한 유일 대표, 우호도50 이상·상호 비적대 및 기존 provider/집단 체제 제외 규칙을 적용한다. 검토와 최종 확인 사이 대표·장부 전체·공납/이전 기록·실제 존재/소유 권한이 달라지면 적용하지 않는다. 영어/한국어 확인창에서 기존 채무/후손 보존과 이후 별도 정치 전환을 안내한다.

실제 원 장부를 복제하여 플레이어/NPC 합의 해약과 활성 실물 공납 계정의 종료를 함께 준비하고, 기존 협상·최종 서명·소실/복귀·보호 기록과 검증한 후 두 모델을 교체한다. 검토 취소/거절/마감 실패는 원 서명을 일부 남기지 않는다. 기존 한쪽 플레이어 해약 시각은 보존해 완료할 수 있으며, 옛 저장을 소급 재판정하거나 현재 NPC 결정으로 과거 서명을 대체하지 않는다. 주민/장비/재고/소속/외교 쓰기나 새로운 저장 schema/통화를 추가하지 않았다. NPC 하위 전체 이탈은 기존 별도 경로로 처리하며 이 버튼으로 우회하지 않는다.

이는 기존 실제 종료 경로의 권한·일관성 보완이며 정당한 일방 독립·합병·집단 의결의 완료가 아니다. 현재 집중1단계는 미완료다. 잔여 노력 추정은 1단계35%(7~9묶음),2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6)를 유지한다. 다음 큰 작업은 실제 독립권 판정/행사·연합/후손 이주, 전체 합병 집행/복구, 집단 권한·미지원 주민/로딩 도시/복잡 재고 및 중첩 소실이다.


## 0.3.50 — 같은 종주 아래 플레이어 봉신계약 재협상

실제 외교 공유가 적용된 활성·미보류 플레이어 봉신계약에서, 같은 현 일반 NPC 종주와 경제 조건을 재협상하는 실행 경로를 추가했다. 실제 같은 당사자 공납 이력과 새로운 NPC 조건 수락을 재검사한다. 옛 계약의 실제 합의 해약·새 NPC/플레이어 서명, 정치 장부의 새 계약 참조, 완료 상태의 같은 세력 정치 명령/후손 검토와 최종 동의 기록을 복사본에서 함께 준비한 뒤 원자적으로 교체한다. 기존 NPC 도시 재협상도 유지한다.

현재 종주와 실제 하위 영지 소속이 모두 같고 이동할 도시가 하나도 없어야 한다. 직속·간접 NPC 후손 전체를 검토하고 원 계약·채무 근거·서명·하위 조건·이전/공납 영수증·전쟁 이력을 보존한다. 기존 전쟁 수락은 정확한 이전 명령 계보로 별도 보존하며 새 선전포고나 외교 공유 재적용을 호출하지 않는다. 도시/주민/재고/플레이어 native 소속이나 지휘권은 쓰지 않는다. 영지 이전이 필요하거나 기존 정치 전환/실물 납부가 보류이면 별도 경로에서 먼저 해결한다.

새 player-subject 협상에만 Payments 증거를 보존하고, 옛 NPC 도시 협상은 null JSON/생성자/해시를 유지한다. 별도 128건/32M player renewal slot과 book/amendment/최종 서명/실제 정치 명령·journal·기존 war history를 함께 검증한다. 누락/변조는 원본을 보존하고 변경을 막으며 옛 계약 참조로 자동 수리하지 않는다. 새 계약 실물 공납과 별도 보호 합의는 새로 검토해야 함을 확인 전에 안내한다. 과거 보호/공납 기록은 남는다.

추가로 기존 주민·재고 점검과 재협상 화면이 같은 CA_Open/CA_Title 번역 키를 쓰던 충돌을 고쳤다. 재협상은 CN_ 전용 키를 쓰고 양 언어 중복 키가 있으면 빌드를 실패하도록 했다. 기존 점검 화면의 의미를 바꾸지 않는다.

현재 집중1단계는 미완료이며 잔여 노력 추정1단계35%(7~9묶음),2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6)는 유지한다. 전체 합병·정당한 독립권/연합 편입·집단/복잡 전시 이적·미지원 이전/소실의 큰 잔여는 이번 경제 재협상으로 완료되지 않는다.


## 0.3.49 — 저장된 지원군 생성 반환의 명시적 정리

기본 지원군 집단 생성이 정상 반환하고 정확한 새 인원이 이미 원 맵/세력으로 도착한 경우, 그 증거를 전체 지원 호출 반환과 별도로 즉시 저장한다. 뒤의 편지·행동/지휘 배정 등에서 호출이 중단되면 이 원 증거를 보존한다. 사용자가 현재의 같은 ID 전원·원래 맵·정확한 등록 세력을 다시 검토하고 확인할 때만 별도 GenerationAcknowledged 상태로 정리할 수 있다. 과거 계약이 합법적으로 종료된 뒤에도 같은 역사 사실을 검토할 수 있다. 인원·건강·장비·명령·소속·계약/채무는 변경하지 않는다.

이 정리는 전체 지원 호출의 정상 종료, 전투 준비/승리 또는 보호 의무의 완전 이행 증거가 아니다. ReturnedTick은 계속 비워 두고 실제 생성 시각과 나중 확인 시각을 구분한다. 원 호출은 재실행하지 않고 같은 요청 ID도 재사용하지 않는다. 새 지원 요청의 보류만 해제하며 원 요청 시각 기준의 종주별 하루 쿼터와 모든 과거 이력은 유지한다. 이후 새 요청은 기존 별도 현재 동의/권한·공유 외교·공통 적·지원 가능성 검사를 모두 거친다.

당시 저장한 생성 증거가 없는 기존 불명 호출은 나중 군대로 채우지 않는다. 인원 누락/사망/다른 맵·세력, 변경된 검토/저장 범위 또는 동일 native 생성 반환이 반복 관찰된 경우는 계속 보류한다. 반복 관찰 표시는 원 긍정 증거를 지우지 않고 별도로 보존해 재로딩 후에도 사용을 막는다. 새 null 속성은 생략하고 옛 JSON·enum0~3·생성자를 유지한다. 생성/최종 반환 두 인원 목록을 퇴출 없이 저장하기 위해 전체32M/시작 전16M 여유로 크기를 분리 검증한다.

현재 집중1단계는 미완료다. 잔여 노력 추정1단계35%(7~9묶음),2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6)는 유지한다. 전체 합병·정당한 독립권 판정/행사·집단/복잡 전시 이적·미지원 이전/소실 및 광범위 경제/보호 의무 통합이 남는다. 이번 부분 결과 정리를 고의 거부·불명예·독립권으로 해석하지 않는다.


## 0.3.48 — 보호 합의와 실제 동맹 지원 요청

활성 플레이어 봉신계약의 공유 소속/외교를 유지한 상태에서, 현 일반 NPC 종주와 별도의 보호 합의를 맺는 창을 추가했다. 상호 동맹·우호도75의 명시적 NPC 수락 규칙이며 기존 경제 조건/서명·채무·후손 계약을 바꾸지 않는다. 실제 플레이어 본거지의 활성 공격이 기본 지원 기준을 넘고 모든 공격자가 종주의 공통 적일 때, 사용자가 검토/확인해 기본 RaidFriendly 지원을 요청한다. 기본 위협 점수·기본 전투 집단·도보 진입/지원 AI를 사용하며 추가 지급금은 없다. 종주별 하루 한 번이고 새 계약을 맺어도 이전 요청 간격은 초기화되지 않는다.

요청을 Applying으로 저장한 뒤에만 native CanFireNow/TryExecute를 호출한다. 모든 원 맵/월드/임시 주민 ID와 정확한 기본 집단 생성 반환에 담긴 새 지원군의 실제 도착을 대조하고 정상 반환·도착 확인·false·도착 미확인·중단 불명을 구분한다. 불명 호출은 저장 후 재실행하지 않으며 새 지원 요청도 막는다. 정상 반환 없는 현재 군대만으로 복구/성공을 꾸미지 않는다. 지원군 도착은 전투 승리나 보호 의무의 완전 이행 증거가 아니다. 지원 불가/실패/중단은 고의 거부·불명예·정당한 독립권을 만들지 않는다. 자동 출병·상위 종주 전파·고의 거부 판정/독립 집행은 여전히 남는다.

정확한 원 계약/대표 결정/공유 소속/외교/맵/공격자/이전 주민과 현재 보호 기록을 최종 확인한다. 기본 지원군 생성 범위에만 기존 월드 주민 redress와 새 관계 생성을 막고 정치 변경·자동 외교 경로를 동결한 뒤 finally에서 범위를 해제한다. 원 주민·건강·장비·관계·재고를 다시 생성하거나 이전하지 않는다. 별도 bounded 보호 slot은 손상 원본을 보존하고 보호 경로만 차단한다. 과거 합의·실제 요청/반환 이력을 퇴출하지 않는다. Hegemony/WD2 및 집단/귀족 권한은 기존 제한을 따른다.

현재 집중1단계는 미완료다. 정당한 독립을 위한 실제 의무/지원의 선행 연결이며 잔여 노력 추정은1단계35%(7~9묶음),2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6)로 유지한다. 합병·독립권 판정/행사·집단/복잡 전시 이적·미지원 이전/상위 소실, 징수/NPC 공납·광범위 보호 이행/정산 등 큰 묶음은 남는다.


## 0.3.47 — 재협상한 영지의 소유권 복귀 연결

실제 이전 완료 후 동일 당사자가 계약을 재협상한 영지가 적대 세력에 소유권을 잃었다가 돌아오면, 새 계약 번호로 과거 이전 영수증을 바꾸지 않는다. 원래 완료 이전부터 각 실제 합의 해약/새 서명까지의 협상 기록을 따라 같은 도시·당사자·실제 소유 세력이 유지됐는지 검증한다. 각 협상 당시 장부에 원 이전이 동일한 순서와 내용으로 존재하고 이미 완료되어 있어야 한다.

정상 native 복귀 관찰 전후에 정확한 현재 이전/협상 기록을 묶고, 별도 현재 대표 동의·전체 후손 승계 확인에서도 재검사한다. 새 return 증거의 optional Amendments는 null 생략하며 옛 생성자·API·JSON/고정 해시를 유지한다. 복원 시 원 소실·협상 slot·복귀/승계 증거를 함께 검증하고 원본을 보존한다. 실제 이전/주민/재고를 재실행하거나 옛 계약을 부활시키지 않으며, 돌아온 영지의 새 계약은 기존 별도 제안/서명 절차를 따른다. 단순히 같은 도시라는 이유나 관련 없는 계약/변경된 영수증은 권한이 아니다.

현재 집중1단계는 미완료다. 잔여 노력 추정은1단계35%(7~9묶음),2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6)로 유지한다. 전체 합병·정당한 일방 독립·집단 의결/복잡 전시 이적·미지원 주민/노예/지도자/로딩 맵과 나머지 상위/중첩 소실·복구가 남는다.


## 0.3.46 — 같은 당사자 도시 계약 재협상

플레이어가 종주인 NPC 도시의 활성 계약에 재협상 창을 연결했다. 현 일반 NPC 대표가 기존 계약의 합의 해약과 새 경제 조건에 동의해야 하며, 마지막 확인에서 현재 Game·대표·도시·장부·이전/공납 기록을 다시 검사한다. 원 계약의 실제 상호 해약과 새 계약의 양측 서명을 복사본에서 원자적으로 확정한다. 과거 조건/서명/채무 근거, 모든 하위 계약과 소유권 이력은 보존한다. 새 계약의 실물 공납은 기존 별도 활성화 절차를 따른다.

별도 128건/32M 문자 협상 archive와 새 계약의 Amendment 연결·최종 동의 audit를 저장하고 함께 검증한다. 변조·누락·중복·오래된 확인·부분 해약·예약된 실물 이전/지급은 거절한다. 기존 JSON의 null 필드는 생략한다. 플레이어가 봉신인 계약, NPC 내부 계약, 집단/귀족/제공자 대표의 동의, 소속 변경·주민/물품 이전·자동 NPC 공납은 이번 경로에 포함하지 않는다. 다른 root 계약으로 바뀐 뒤 과거 소유권 복귀 증거를 확장하는 작업도 남는다.

현재 집중은 1단계이며 미완료다. 잔여 노력 추정은 1단계35%(7~9묶음), 2단계80%(5~7), 3단계95%(6~8), 4단계90%(5~7), 5단계100%(6~8), 6단계70%(4~6)를 유지한다. 전체 합병·정당한 일방 독립·집단 의결/복잡 전시 이적·미지원 주민/노예/지도자/로딩 맵·나머지 상위/중첩 소실과 복구가 주요 기초 잔여다.


Contracts **0.3.45**는 이전 완료 후 실제 소유자가 계약의 기준 세력과 달라진 상위 영지의 소유권 복귀도 연결한다. 같은 root 계약의 소실 전 실제 이전 완료 기록을 보존하고, native 관찰 전후와 최종 동의에서 현재 장부가 정확히 일치해야 한다. 실제 소유자와 기준 identity를 분리해 후손 승계·새 계약으로 이어지며 옛 조건/서명·채무 근거·소실/이전 이력을 고치지 않는다. 옛 영수증을 새 계약/다른 세력/소실 후 기록으로 대체하거나 원 주민·재고를 되살리지 않는다. 기존 모드의 JSON/hash/API는 유지하고 새 scope에만 추가 증거를 요구한다. 현재1단계 미완료, 잔여 노력 추정35%(7~9묶음),2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6)는 유지한다.

Contracts **0.3.44**는 분리된 상위 영지의 적대 소유권 상실 뒤 원래 NPC 소유권이 정상 native 반환으로 돌아온 경우를 별도 기록한다. 원 계약을 부활시키지 않고 현 NPC 동의·원래 상위·전체 생존 하위를 다시 검토해 하위 계약을 재연결한 뒤, 돌아온 도시에는 기존 새 제안/서명 경로로 새 계약을 체결한다. 원 조건/서명·채무 근거·소실 시각·하위/승계 이력은 유지한다. 복귀 뒤 새 소유권 변경 시도가 있으면 불확실한 결과에도 이전 증거를 재사용하지 않는다. 관찰/승계 기록은 별도 저장 후 교차 확인하며, 역사 원본을 보존한다. 이전 정규화·중첩/추가 소실·집단/제공자·일반 재탈환의 나머지는 남는다. 현재 집중1단계 미완료, 잔여 노력 추정35%(7~9묶음),2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6)를 유지한다.

Contracts **0.3.43**은 일부 말단 영지 소실을 정리한 뒤 추가 소실이 발생해도, 각 원 관찰의 계약/명령 기록과 실제 확정 순서를 보존해 남은 정리를 이어간다. 이전 관찰·새 관찰을 어느 쪽부터 정리해도 정확한 과거 명령별 book을 재구성하고 부분 저장 복원·원 정치 전환 완료로 연결한다. 모든 이전 소실이 끝난 뒤의 새 기간은 그 자체 원본에서 시작해 옛 book을 덮어쓰지 않는다. 실제 증거 없는 과거 사건·상위/보존 집행·변경된 실물/권한/전쟁·관련 없는 계약 변경은 허용하지 않는다. 현재 집중1단계는 미완료다. 잔여 노력 추정35%(7~9묶음),2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6)를 유지한다.

Contracts **0.3.42**는 적용 후 중단된 정치 소속 전환에서 같은 원본 범위의 여러 빈 말단 도시가 실제 파괴된 경우를 연결했다. 각 소실은 별도 증거로 유지하고 사용자가 하나씩 정리할 때 현재 book/명령 기록을 재검토한다. 원 관찰 당시 명령과 실제 확정 직전 명령을 분리해 같은 tick의 정리 순서도 보존하며, 부분 정리 후 저장 복원·마지막 생존 영지 이전으로 이어진다. 이전/소실을 혼동하거나 원 세력·주민·물품을 쓰지 않는다. 상위/주민/보존 집행·다른 관찰 범위/재진입/재탈환은 남는다. 현재 집중1단계는 미완료다. 잔여 노력 추정은1단계35%(7~9묶음),2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6)로 유지한다.

Contracts **0.3.41**은 개별 이전이 보류된 빈 비로딩 상위 도시의 실제 파괴를 원본 증거로 기록하고, 명시적 정리에서 상위 계약·이전 보류·후손 소실 archive를 함께 확정한다. 관찰/실제 소실/나중 정리 시각을 구분하며 저장 재로딩 시 세 기록의 일치가 필수다. 같은 실제 일반 NPC 세력의 생존 전체 후손·원래 상위 주체를 다시 확인하고 별도 승계 검토를 연다. 하위 조건·서명·채무·깊은 계약과 native 소유권은 보존한다. 전체 정치 전환 중 상위 소실, 주민/물품·중첩/재진입·재탈환 등은 남는다. 현재 집중은1단계이며 잔여 노력 추정35%(7~9묶음),2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6)로 유지한다. 세부 큰 잔여는 ROADMAP을 따른다.

Contracts **0.3.40**은 NPC 영지 소실 후 승계에서 실제 NPC 소유 세력과 플레이어 정치적 소속을 분리했다. 계약만 체결하고 아직 물리적으로 이전하지 않은 NPC 도시의 생존 하위도, 기록된 원 NPC 세력의 현 동의와 검증된 원래 상위로 실제 재연결한다. player 상위 당사자와 NPC chief를 구분하고 원 조건/서명/채무·깊은 하위·소실 이력을 보존한다. native 도시/주민/재고·플레이어 소속/외교를 변경하지 않는다.

현재 집중 **1단계**, 잔여 노력 추정 **35%(7~9묶음)**: 전체 합병·정당한 독립·미지원 보존/로딩 맵·집단 의결·진행/재진입/재탈환 및 나머지 소실/복구. **2단계80%(5~7)** 징수관/NPC 공납·경제/보호 의무, **3단계95%(6~8)** 턴 전쟁·포위/점령·구원, **4단계90%(5~7)** 명예/신뢰·표결/분쟁, **5단계100%(6~8)** 용병단·개인 서약/승계, **6단계70%(4~6)** 저장/호환성·통합 배포. 소실 복구의 실제 지원 분기 확장이며 큰 묶음 완료로 세지 않는다.1단계 미완료로 반복은 유지한다.

Contracts **0.3.39**는 NPC 계층 재편에서 연합/귀족/제공자 체제의 지도자를 곧바로 승인자로 취급하던 경로를 막고, 현재 일반 대표의 유일 직위·등록·가용성/우호도·실제 final consent를 book 원자 교체에 연결했다. 현 Game에 review/window/component를 묶어 다른 저장의 오래된 검토/컴포넌트가 계약을 바꾸지 못하게 했다. 영지 소실 후 같은 realm 승계에도 대표 가용성을 재검사하며 원 저장 계약·서명·채무/하위 이력을 고치지 않는다.

현재 집중 **1단계**, 잔여 노력 추정 **35%(7~9묶음)**: 전체 합병·정당한 독립·미지원 보존/로딩 맵·집단 의결·나머지 소실/복구. **2단계80%(5~7)** 징수관/NPC 공납·경제/보호 의무, **3단계95%(6~8)** 턴 전쟁·포위/점령·구원, **4단계90%(5~7)** 명예/신뢰·표결/분쟁, **5단계100%(6~8)** 용병단·개인 서약/승계, **6단계70%(4~6)** 저장/호환성·통합 배포. 이번은 부당한 새 동의/다른 Game 재사용을 막는 정확성 수정이며 집단 정부나1단계 완료로 세지 않는다. 반복은 유지한다.

Contracts **0.3.38**은 일반 물자를 가진 거래 동물도 실제 도시 이전·저장 복구에 연결했다. 같은 동물·원 컨테이너·원 물품과 하역/판매 제외/포장 해제 설정을 유지하고 별도로 이중 저장하거나 물자를 이동·재생성하지 않는다. 기존 일반/빈 동물 재고 지문은 유지하며 cargo의 누락/중복·다른 보관자·설정 변화는 완료/미변경 취소를 막는다.

현재 집중 **1단계**, 잔여 노력 추정 **35%(7~9묶음)**: 전체 합병 집행·정당한 독립·미지원 주민/노예·지도자·로딩 맵/복잡 재고·집단 의결·나머지 소실/복구. **2단계80%(5~7)** 징수관/NPC 공납·경제/보호 의무, **3단계95%(6~8)** 턴 전쟁·포위/점령·구원, **4단계90%(5~7)** 명예/신뢰·표결/분쟁, **5단계100%(6~8)** 용병단·개인 서약/승계, **6단계70%(4~6)** 저장/호환성·통합 배포. 복잡 재고의 한 실제 분기이며 큰 묶음 전체 완료로 세지 않는다.1단계 미완료로 반복은 유지한다.

Contracts **0.3.37**은 지원되는 기본 Gene를 가진 주민을 실제 도시 이전·적격 포로 관리 세력 변경과 명시 복구 경로에 연결했다. 새 profile7이 원 유전자 ID/순서·내생/이식 분류·저장 유전형/이름/아이콘·열정/억제 관계를 확인하며 유전자 생성/삭제/재발현이나 이중 저장은 하지 않는다. 특수 subclass·손상/미확인 상태는 보류한다. 기존 프로필1~6/저장된 복구 의미·5인수 공개 API와 원 주민/건강/장비/관계·계약/채무는 유지한다.

현재 집중 **1단계**, 잔여 노력 추정 **35%(7~9묶음)**: 전체 합병 집행·정당한 독립·미지원 주민/노예·지도자·로딩 맵/복잡 재고·집단 의결·나머지 소실/복구. **2단계80%(5~7)** 징수관/NPC 공납·경제/보호 의무, **3단계95%(6~8)** 턴 전쟁·포위/점령·구원, **4단계90%(5~7)** 명예/신뢰·표결/분쟁, **5단계100%(6~8)** 용병단·개인 서약/승계, **6단계70%(4~6)** 저장/호환성·통합 배포. 주민 보존의 실제 지원 범위 확장이며 큰 묶음 전체 완료로 세지 않는다.1단계 미완료로 반복은 유지한다.

Contracts **0.3.36**은 도시 명부에 배정되지 않은 일반 월드 지도자의 별도 개인 합류를 실제 native 후임 선정·소속 변경·의료 확인·목적지 명부 추가/명시 복구까지 연결했다. 현재 도시와 보존된 등록 과거 도시를 모두 확인하고, 검토한 원 도시를 가짜 거주지로 만들거나 존재하지 않은 원 배정을 제거하지 않는다. 새로운 외부 배정/알 수 없는 명부/원본 변화는 보류한다. 구버전 JSON·기존 assigned-source 흐름과 도시/계약/채무는 유지한다.

현재 집중 **1단계**, 잔여 노력 추정 **35%(7~9묶음)**: 전체 합병 집행·정당한 독립·미지원 주민/노예·로딩 맵/복잡 재고·집단 의결·나머지 소실/복구. **2단계80%(5~7)** 징수관/NPC 공납·경제/보호 의무, **3단계95%(6~8)** 턴 전쟁·포위/점령·구원, **4단계90%(5~7)** 명예/신뢰·표결/분쟁, **5단계100%(6~8)** 용병단·개인 서약/승계, **6단계70%(4~6)** 저장/호환성·통합 배포. 제한된 개인 합류의 실제 적용 범위 확장이며 큰 잔여 묶음을 완료로 세지 않는다.1단계 미완료로 반복은 유지한다.

Contracts **0.3.35**는 개인 합류의 별도 native 소속 변경에서 빠져 있던 callback 재진입 차단을 보완했다. 사임·승계/소속 변경/구버전 combined 목적을 분리하고 정상 반환 기록까지 보호한다. 소속 변경은 후임 생성 hook 권한을 얻지 않으며 예외는 저장 의도/원본을 지우거나 성공으로 바꾸지 않는다. 전체 합병 native 종료 API도 조사했지만 안전한 실행 coordinator는 아직 없고 마지막 도시 가드를 유지한다.

현재 집중 **1단계**, 잔여 노력 추정은 **35%(7~9묶음)**: 전체 합병 집행·정당한 독립·미지원 주민/노예·로딩 맵/복잡 재고·집단 의결·나머지 소실/복구. **2단계80%(5~7)** 징수관/NPC 공납·경제/보호 의무, **3단계95%(6~8)** 턴 전쟁·포위/점령·구원, **4단계90%(5~7)** 명예/신뢰·표결/분쟁, **5단계100%(6~8)** 용병단·개인 서약/승계, **6단계70%(4~6)** 저장/호환성·통합 배포. 이번 안전성 보완으로 비율을 낮추지 않는다.1단계 미완료로 반복을 유지한다.

Contracts **0.3.34**는 새 개인 합류의 **native 사임/승계 → 별도 source 후임 기록 → 아직 시작하지 않은 개인 소속 변경 → 기존 의료/배정 복구**를 분리했다. 후임 선정 완료 후 actor가 아직 source인 중단을 같은 증거로 이어갈 수 있으며 source 사임을 다시 호출하지 않는다. source/affiliation의 정상 반환·현재 관측/시각을 각각 보존하고 구버전 근거 없는 combined 상태를 새 증거로 채우지 않는다. pending original pawn의 GC뿐 아니라 native 재사용/재장비도 막고 단계 안내를 추가했다.

**1단계35%(7~9묶음)** 추정을 유지한다. 이번은 개인 합류 중단의 추가 실제 실행 분기이며 전체 합병·정당한 독립·미지원 주민/노예·맵/stock/지도자·집단 의결·나머지 소실/복구는 남았다. **2단계80%(5~7)** 징수/NPC 공납·보호 의무, **3단계95%(6~8)** 턴 전쟁·포위/점령·구원, **4단계90%(5~7)** 명예/신뢰·표결/분쟁, **5단계100%(6~8)** 용병단·서약/승계, **6단계70%(4~6)** 저장/호환성·통합 배포. 현재 집중은1단계이며 미완료로 반복은 유지한다. 아래0.3.33 이하는 이전 기록이다.

Contracts **0.3.33**은 별도 개인 동의 뒤 **지원 저장 지도자의 실제 native 소속 변경·원 세력 후임 선정 → 원 지문/의료 확인 → 동일 Pawn의 목적지 추가/출발지 제거 → 명시 복구/완료**를 연결했다. 후임 생성 중 기존 월드 Pawn 재생성/재장비·새 관계 생성을 배제하고 실제 native office를 확인한다. 불확실 call은 재실행하지 않으며 원 actor/관측 증거를 보존한다. 의료 초기화 복원 권한1회·double-reference 복구·미변경 준비 취소·GC 보호·교차 작업/자동 공유 동결과 영한 UI를 연결했다. 원 도시 owner/계약/채무·플레이어 native 통제는 바꾸지 않는다.

범위는 원 세력2도시 이상·own administration·일반 저장 지도자·빈 수술/지원 profile5/비로딩 도시·지원 stock이다. source 종료 합병·특수 지도자/수술/맵·복잡 stock·일부 불확실 중단은 남아 **1단계35%(7~9묶음)** 추정을 유지한다. 전체 합병·정당한 독립·미지원 보존·집단 의결·나머지 소실/복구가 핵심 잔여다. **2단계80%(5~7)** 징수관/NPC 공납·보호 의무, **3단계95%(6~8)** 턴 전쟁·포위/점령·구원, **4단계90%(5~7)** 명예/신뢰·표결/분쟁, **5단계100%(6~8)** 용병단·개인 서약/승계, **6단계70%(4~6)** 저장/호환성·통합 배포.1단계 미완료로 반복은 유지한다. 아래0.3.32 이하는 이전 기록이다.

Contracts **0.3.32**는 전체 합병의 설치 API를 확인하고, 기존 도시 서명과 구분되는 **세력 전체 정치 제안 → 실제 현재 일반 대표에 대한 명시 NPC 응답 → 별도 플레이어 확인 → 저장 이력**을 연결했다. 원래 전체 source 도시/관련 주민·world object·장부/외교를 묶고, 바뀐 대표/전체 범위는 과거 응답을 덮지 않고 새 검토가 필요하다. 동의/거절·3일 응답 기한·양 타깃 영한 안내를 추가했다. **실제 전체 합병/지도자 합류·승계/자산 이전은 아직 미구현이며 마지막 도시 가드는 유지한다.** 이번은 동의 준비의 부분 진척으로 합병 완료나1단계 완료로 부르지 않는다.

현재 집중 **1단계**, 잔여 추정 **35%(7~9묶음)**: 전체 합병 집행·정당한 독립·개인 합류/승계·미지원 보존/집단 의결·나머지 소실/복구. **2단계80%(5~7)** 징수관/NPC 공납·경제/보호 의무, **3단계95%(6~8)** 턴 전쟁·포위/점령·구원, **4단계90%(5~7)** 명예/신뢰 사건·표결/분쟁, **5단계100%(6~8)** 용병단·서약/승계, **6단계70%(4~6)** 저장/호환성·언어/설정·통합 배포. 반복 예약은 유지한다. 아래0.3.31 이하는 이전 기록이다.

Contracts **0.3.31**은 **적용 후 보류된 정치 전환에서 아직 이전하지 않은/이미 이전 완료한 빈 NPC 말단 도시의 실제 파괴 → 별도 양의 사건 기록 → pending 정치 창의 명시 검토 → 원 이전 이력을 바꾸지 않는 계약 종료·독립 정치 소실 영수증 → 생존 전체 재검토/계속**을 연결했다. 가짜 이전을 만들거나 Applied를 파괴로 덮지 않는다. 원 전체 Followers·조건/서명/채무·검증된 실제 owner/과거 결과/전쟁 동의를 보존한다. 기존 Prepared 손실과 양 순서로 섞여도 원 증거·receipt prefix를 함께 검사하며, 미정리 중 자동 player/proxy 외교 공유를 멈춘다. Native 독립 사건은 그대로 진행되므로 계속하기에서 새 외교를 재검토한다.

현재 집중 **1단계**의 잔여 추정은 **35%(7~9묶음)** 유지다. 전체 합병·정당한 일방 독립·지도자 개인 합류/승계·미지원 주민/노예·로딩 맵/복잡 재고·집단 전시 의결·상위/집행 중/재진입 소실·재탈환과 전체 복구 통합은 남았다. **2단계80%(5~7)** 징수관/NPC 공납/경제·보호 의무, **3단계95%(6~8)** 턴 전쟁/포위·점령/구원, **4단계90%(5~7)** 명예/신뢰 사건·표결/분쟁, **5단계100%(6~8)** 용병단/개인 서약·승계, **6단계70%(4~6)** 저장/호환성·언어/설정·통합 배포.1단계는 미완료이며 반복은 유지한다. 아래0.3.30 이하는 이전 기록이다.

Contracts **0.3.30**은 **적용 후 보류된 정치 소속 전환 중 빈 NPC 말단 봉신의 실제 파괴 → 정확한 정치/도시 원본에 묶은 증거 → 같은 보류 창의 명시 검토/정리 → 별도 정치 소실 영수증 → 생존 봉신 전체 재검토/계속**을 연결했다. 원래 전체 Followers는 불변이다. 소실 도시는 Destroyed로 남기고 성공 이전으로 부르지 않으며 완료 안내도 구분한다. 원 권리·조건/서명/채무·전쟁 동의/이미 확인한 적대 결과/재수락 이력과 이전 부분 결과를 보존한다. 미정리 소실 중 재개를 막고 네 journal 사본을 함께 검증/교체하며 로드 때 상호 증거를 대조한다.

현재 집중은 **1단계**, 잔여 추정은 **35%(7~9묶음)** 유지다. 전체 합병·정당한 일방 독립·지도자 개인 합류/승계·미지원 주민/노예·로딩 맵/복잡 재고·집단 전시 의결·상위/집행 중/재진입 소실·재탈환과 나머지 복구 통합은 남았다. **2단계80%(5~7):** 징수관/NPC 공납/경제·보호 의무. **3단계95%(6~8):** 턴 전쟁/포위·점령/구원. **4단계90%(5~7):** 명예/신뢰 사건·표결/분쟁. **5단계100%(6~8):** 용병단/개인 서약·승계. **6단계70%(4~6):** 저장/호환성·언어/설정·통합 배포. 작업 묶음은 고정 시간이나 대화 횟수가 아니다.1단계는 아직 미완료이며 반복은 유지한다. 아래0.3.29 이하는 이전 기록이다.

Contracts **0.3.29**는 **멈춘 개별 이전의 빈 NPC 말단 도시에서 정상 실제 파괴 → 원래 book/transfer/빈 stock 상태에 묶은 별도 증거 → 사용자 재검토/명시 확정 → 영지 계약 종료·별도 Destroyed 이전 결과**를 연결했다. 관측만으로 진행 장부를 바꾸지 않으며, 성공/취소/합의 해약/불명예를 만들어 내지 않는다. 원 조건/서명/채무/이력과 실제 source/target 소실 owner를 유지한다. 양의 저장 증거 없이 참조 누락을 파괴로 보지 않으며, 되살아난 ID/달라진 상태/원본/부분 완료는 보류한다. 소실 identity의 owner 정규화·재이전과 일반 복구 우회를 막는다. native 주민·재고·소속 변이는 추가하지 않았다.

범위가 개별·빈 말단·idle Prepared로 한정되어 **1단계 잔여35%(7~9묶음)**를 유지한다. 주민/물품 custody·정치/하위 전체 해약/지도자/납부 조정·자신의 native 집행 중 재진입/중단·상위 소실·적대 점령/재탈환과 합병/독립/승계·미지원 보존 등은 남았다. 다른 단계는2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6)다. 다음 착수점은 STAGE1-HANDOFF를 따른다. 아래0.3.28 이하는 이전 기록이다.

Contracts **0.3.28**는 **하위 소실 이후의 실제 상위 소실 → 옛 미정리 범위 불변 참조 → 공통 원 그래프 확인 → 현재 최상위에서 전체 생존 계약 검토/재연결**을 연결했다. 둘 이상의 이전 범위와 같은 tick의 사건도 실제 원본으로 판별하고, 옛 ID/Before/순서/소실 근거와 이후의 로컬 순서를 보존한다. 완료 영수증을 인수·재작성하지 않으며 cycle/다중 인수/누락/부분 완료를 거절한다. 기존 일반 관측 API의 상위 가드는 유지하고 별도 검증 경로에서만 범위를 인수한다. native/UI/저장 경로까지 연결했으며 세력 합병이나 native 소유권/주민/물품 변이는 아니다.

소실 처리 묶음의 추가 분기이므로 **1단계 잔여35%(7~9묶음)** 추정은 유지한다. 진행/재진입/중단 소실·재탈환·합병·정당한 독립·지도자 합류/승계·미지원 보존/집단 의결 등은 남았다. 다른 단계는2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6)다. 다음 착수점은 STAGE1-HANDOFF를 따른다. 아래0.3.27 이하는 이전 기록이다.

Contracts **0.3.27**는 **상위 영지 소실 이후의 순차 하위 소실 관측 → 원래 관리 범위/정확한 사건 순서 저장 → 현재 대표의 전체 범위 검토 → 생존 전방 계약 일괄 재연결**을 연결했다. 끊긴 active chain 뒤의 깊은 생존자도 포함하며 실제 기록한 상실 조상만 건너뛴다. 원 조건/서명/채무·미손실 내부 관계를 보존하고 각 소실의 자기 교체 내역/공동 정리 영수증을 함께 저장한다. 중간 실패에서 일부 형제만 바꾸지 않으며, 생존자가0인 범위도 유효한 상위/대표와 별도 확정 후 정리할 수 있다. 이 관측/정리에서 native 소유권·주민·재고를 쓰지 않는다.

실제 순방향 중첩 소실 경로의 통합을 반영해 **1단계 잔여40%→약35%(7~9묶음)**로 재평가한다. 역순 상위 소실·진행/재진입 중 손실·후속 재탈환, 합병·정당한 독립·지도자 합류/승계·미지원 주민/로딩 맵·집단 의결 등은 남아 있다. 2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6)는 유지한다. 아래0.3.26 이하 단락은 이전 기록이다. 다음 착수점은 STAGE1-HANDOFF를 따른다.

Contracts **0.3.26**는 실제 활성 플레이어 봉건 계약 이후 **일반 종주 지도자의 별도 기존 전쟁 수락 → native 외교 → 공유 외교/직·간접 후손 전체 이전 → 중단 복구**를 연결했다. 평의회와 지도자 권한을 명시적으로 구분해 서로 바꿔 쓸 수 없으며, 이전 지도자 수락·새 지도자 재확인·기존 계약/채무/후손/도시 가드와 Hegemony/WD2 보류를 유지한다. 군사·보호 의무나 귀족 의결을 만들어 내지 않는다. 최종 native 콜백 뒤 대표를 다시 확인하고, 진행 중 전쟁 intent는 자동 goodwill hook만으로 소리 없이 재개되지 않도록 했다.

일반 지도자 편입만 추가 연결했으므로 **1단계 잔여40%(7~9묶음)** 추정을 유지한다. 귀족/집단 전시 의결·복잡 이적, 합병·정당한 독립·지도자 개인 합류/승계·미지원 주민/로딩 맵·중첩/진행 중 소실은 남았다. 2~6단계는80%(5~7),95%(6~8),90%(5~7),100%(6~8),70%(4~6)다. 다음 착수점은 [1단계 인수인계](STAGE1-HANDOFF.md)를 따른다. 아래0.3.25 이하 단락은 이전 구현 기록이다.

Contracts **0.3.25**는 1단계의 **전시 연합 수락 → native 적대 확인 → 공유 외교/전체 후손 이전 → 명시적 중단 복구**를 연결했다. 실제 도시 기초표/창설 대표의 별도 NPC 수락을 요구하며, 기존 동맹·quest/cap·도시/주민/원 소속/마지막 기지 보호를 우회하지 않는다. 원 동의·현재 새 검토·대상별 적용 결과·이전 전쟁 수락 이력을 저장하고 불확실한 선언이나 뒤늦은 화평을 자동 재선포하지 않는다. 일반 봉건 전시 가입과 Hegemony/WD2 전용 전쟁 장부 연동은 남아 있다.

추가로 이미 서로 적대하던 등록 NPC 사이의 **정상 완료한 실제 도시 소속 변경**을 관측해 기존 영지 계약만 종료하고, 상위 도시의 파괴/소유권 상실 근거를 구분해 생존 하위 재연결에 전달한다. 주민·재고·실제 소유권을 이 관측자가 쓰거나 새 정복 계약/전투 증거를 만들지 않는다. 미정리 상위 소실 동안 다른 정치/초기 구성/새 이전/새 실물 전달을 동결해 후손 누락을 막는다. 같은 기록의 재연결 검토와 기존 플레이어 제안 거절은 유지한다.

**1단계는 아직 완료가 아니다. 잔여 노력 약45%→약40%(7~9묶음)**: 합병·정당한 일방 독립·지도자 개인 합류/승계·미지원 주민/노예·로딩 맵/복잡 재고·중첩/진행 중 소실/후속 재탈환과 전체 복구 통합이 남았다. 다른 단계는2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6)를 유지한다. 단순 검사 개수/버전 증가로 완료를 계산하지 않는다. 아래0.3.24 단락은 이전 구현 기록이다.

Contracts **0.3.24**는 **1단계의 상위 영지 소실·하위 계약 정리**를 연결했다. 정상 완료한 native 파괴의 전후 상태·원래 계약/계층·소속을 확인한 제한된 상위 NPC 도시의 소실 근거를 저장한다. 우선 그 도시의 영지 계약만 종료하고 하위 계약은 그대로 보류한다. 별도 검토/확정에서 같은 실제 세력의 현재 유효 대표와 원래의 살아 있는 상위 주체를 확인해 직속 하위 계약을 새 계약으로 연결한다. 옛 계약은 Replaced 이력으로 남고 조건/원 서명/대표/후계 연결을 보존하며, 더 아래 계약과 주민·재고·실제 소속·개인 서약은 바꾸지 않는다. 단일 장부/근거 보관소 교체로 부분 형제 편입을 막고 소실·정리 이력을 별도 저장한다. 기존 말단 소실 경로와 JSON 기본 바이트를 유지한다. 연합/귀족 의결·다른 세력·달라진/해약 진행 중 하위 구조·중첩 소실·점령·진행 중 파괴/이전 복구는 이 경로로 해결하지 않는다. **1단계 잔여 추정은 약50%→약45%(7~9묶음)**로 재평가하며 다른 단계는 유지한다. 이번 변경은 공납 확장이 아니다.

Contracts **0.3.23**은 실제 계약 동의·공납·이력을 연결했다. 활성화한 실물 납부 계정과 검증 영수증만 사용해 계약별/같은 당사자 누적 실적(유예 내/지연 납부, 유예 중, 미납 연체, 전달 확인 대기)을 표시한다. 종료된 계약의 기존 채무는 보존하되 이후 회차를 만들지 않으며, 다른 상대의 실적은 직접 섞지 않는다. 같은 상대의 실제 연체가 있으면 새 계약 동의를 보류하는 공개 NPC 규칙을 수동 제안/자동 제안/최종 서명에 연결했다. 최종 서명은 현재 대표·응답·채무·소유권/위상을 다시 확인하고 사본에서 계약과 최종 대표/조건/시각 이력을 함께 확정한다. 과거 대표를 새로 추정하거나 기존 서명을 고쳐 쓰지 않는다. 저장별 설정/일일 일괄 알림/회차별 중복 방지를 가진 실제 공납 기한 편지를 추가했다. 전달 확인 대기는 미납 경고에서 제외하며, 연체만으로 고의·불명예·제재·독립권을 부여하지 않는다. 1단계는 약50%(7~9묶음)를 유지하고 2단계 잔여는 공납 실적·협상·알림 연결로 약85%→약80%(5~7묶음)로 재평가했다. 승계·합병·일반 전시 가입·정당한 독립의 실제 집행은 여전히 남아 있다.

Contracts **0.3.22**는 초기 NPC 세계 재편을 선택하지 않아도 공동 자유도시연합을 별도로 준비하고, 기존 가입/후손 이동 검토로 이어갈 수 있게 했다. 준비는 기존 NPC 도시·계약·채무·플레이어 소속을 변경하지 않으며 실제 가입은 별도 확정한다. 활성/한쪽만 해약한 종주계약·예약 납부·진행 중 전환은 준비를 막는다. 가입 전에 플레이어와 목적지의 실제 외부 관계를 함께 검사하고, 목적지가 공유하지 않는 기존 적대를 가입만으로 지우지 않도록 보류 이유를 영한으로 표시한다. 전환 중 재검사와 검토 토큰의 플레이어 관계 포함으로 오래된 확인/중간 콜백 변화를 보강했다. 이는 전쟁 인수 표결·일반 전시 가입을 구현한 것이 아니다. 독립 실행 유지/선택형 Hegemony 연동 방향과 1단계 잔여 약50%(7~9묶음)는 유지한다.

Contracts **0.3.21**은 1단계의 실제 NPC 종주계약 제안 흐름을 추가했다. 새 게임/기존 저장에서 별도 수신 설정을 저장하고, 7일마다 최대 1건·세력별 30일 재접촉 제한·3일 응답 기한을 적용한다. NPC가 먼저 서명한 조건부 후보를 기존 계약 목록에 기록하고 영한 편지로 알린다. 플레이어의 명시적 최종 서명 전에는 기존 계약/후손/채무·소속·외교·재고를 바꾸지 않는다. 등록과 NPC 응답은 사본에서 준비해 선택된 상대만 일괄 반영한다. 독립 실행 유지 + 선택형 Hegemony 연동을 사용자 결정으로 확정했으며 아직 외부 어댑터나 추가 필수 의존성을 만들지 않았다. 1단계 잔여 추정은 약50%/7~9묶음을 유지한다.

Contracts 0.3.20에서는 **1단계 계약·정치 구조를 진행 중**이다. 새 계약의 공납 가치/주기/유예/배송 공제 초안·역할별 NPC 응답·별도 서명·실제 청구 연결과 실패한 제안의 원자적 등록과 기존 계약 중 조건부 종주 후보 비교·발효 시 이중 종주 차단을 추가했다. 기존 계약·채무·영수증을 유지한다. 연합 가입·합의 해약 후 복귀·새 종주 소속과 전체 NPC 후손 이동을 통합 검토하고 단계별 의도를 저장한다. 미적용 준비 취소·부분 적용 후 재검토/재개·기존 채무 보존을 지원한다. 하위 전체 합의 해약·연합 이주와 도시별 중단 복구를 유지하고, 제한된 일반 저장 주민의 실제 소속 변경·의료 등급 복원·참조 보호/복구와 시작하지 않은 표준 수술 지시 원본 보관/복원, 기본 모집 상담 기록·등록된 개종 목표와 일반 이념을 가진 적격 포로까지 원 소속/신분/신앙을 유지하는 관리 세력 변경·관찰한 저항도/의지 초기화의 일회 복원·중단 복구, 적격 일반 이념 신도의 신앙/확신도/교리·이력 대조와 실제 파괴된 적격 말단 영지 계약 종료를 추가했다. 기존 미등록 일반 NPC 세력의 초기 내부 계층, 같은 공동 세력의 하위 계약 재배치/플레이어 직속 복귀, 제한된 거래 동물 보존, 실제 합의 해약한 옛 종주에 한정한 적대 출발지 이전을 추가했다. 소속/손님 상태 이념 알림의 누락 검사를 보완하고 주요 보류 사유를 도시별 영한으로 안내한다. 별도 원 소속·직위 유지 협의로 적격 지도자의 저장 주민 참조를 원 세력의 다른 미로딩 도시로 옮기는 실제 참조 집행/복구를 추가했다. 이는 실제 여행·향후 맵 등장이나 개인 합류/승계 보장이 아니다. 주민 기준 기록은 이전 승인이나 완전 보존 증거가 아니다. **로딩된 맵·진행/자원 결박/특수 수술/미지원 포로/노예/지도자 개인 합류·승계/이념 역할·미지원 교리·특수 주민·인간/복잡 재고 이전, 일반 전시 이적·정당한 일방 독립·상위/점령/진행 중 영지 소실 정리는 미완료**다. 전체 [6단계 진행표](CONTRACTS-ROADMAP.md)를 기준으로 개발하며 실제 게임 실행은 보류한다. 기존 모음집 공개 완료는 사용자 보고다.

실제 세계 연결: 계약 장부·선택형 플레이어 공유 외교·실물 공납 직접 전달과 제한된 NPC 소속 집행을 작성했다. `TerritoryAdministrationLedger`/`TerritoryTransferLedger`는 별도 검토·확정 후 실제 NPC faction을 변경하며, 미로딩/적격 일반 주민·물품 재고 보존/잔여 기지·외교 자격을 검사한다. 징수관·NPC 상납·보호 퀘스트는 미구현이다. 코드/빌드 검사만 수행했고 실제 게임 실행/가격 대조/실물 보존/엔진 저장 복구 증거는 없다. [범위와 사용 경로](CONTRACTS-PREVIEW.md).

Contracts 신규 개발: [확정 명세](CONTRACTS-DECISIONS.md), [개발 미리보기](CONTRACTS-PREVIEW.md). 정치 주체와 게임 세력을 분리한 `PoliticalWorld`에 4단계 계층·고정 가치 공납·구원 파견·미이행 시정/독립의 연결 흐름을 추가했다. 별도 선택형 TestMod에 영한 화면과 Scribe 스냅샷 연결을 구현했고 설치 DLL로 컴파일했다. **이는 가상 모델이며 실제 공납/점령/가입/전투 기능이 아니다.** 게임 실행 검증은 사용자 요청에 따라 보류. 아래의 Contracts 초기 규칙 설명은 새 모델의 완성 목록이 아니다.

다섯 모듈은 실제 코드를 포함한 공개 실험판입니다. 전체 초기 명세 완성을 뜻하지 않습니다. 구체적인 수치·사용법은 README, 의존성은 DEPENDENCIES를 기준으로 합니다.

| 모듈 | 구현한 범위 | 지속성 |
|---|---|---|
| Core | 명예 Need·단계 하한, 모욕 강제 결투, 세 종료 규칙, 신청자 이념 우선, 제외 설정, 동일 무기·방어구·결투장·안전·즉시 처치·특성·기억·API | Need, 결투 상태, 장비 참조, principal/대리인, 설정 |
| Society | 당사자 사과 제안/수락, 현장 목격과 지연 정착지 소문, 살아 있는 혈연 명예, 반복 모욕 숙적 | 쌍별 모욕 횟수·마지막 시각·사과 제안, 소문 시각·이미 들은 사람 |
| Nobility | Royalty 최고 작위 seniority 배율, 대리인 지정, 본인과 대리인 보상 분리 | Core 결투의 principal 참조 |
| Diplomacy | 플레이어가 시작하는 비적대 정착지 모욕 퀘스트, 실제 월드 장소·상단 입장·대표·동일 장비·실제 전투·은/우호도·만료 | 퀘스트/장소/NPC 참조, 기한·종료 상태, 세력별 대기 기간 |
| Oaths | 목격자 2명의 짧은 의식, 5일 승리/평화 맹세, 위반·성공 기억과 명예, 의례적 칭호 | 월드/맵 이동을 견디는 GameComponent 맹세 기록 |

## 부분 구현

- 항복은 통증/다운 기준입니다. 별도 항복 대화는 없습니다.
- 동일 무기는 Def·재료·품질·내구도 비교입니다. 스킬·유전자·임플란트까지 같게 만들지 않습니다.
- 방어구 판별은 바닐라 Armor 태그/카테고리 기준입니다. 즉시 처치는 무약물 현장 안정화이며 상처를 회복시키거나 의사를 이동시키지 않습니다.
- 비사망전 사망은 불명예로 처리하는 훅이 있으나 고의·사고·치료 방치·종료 후 보복을 완전히 구분하지 않습니다.
- 기본 검/단검/리볼버와 선택 비폭발 탄환 사격을 지원합니다. 특수 Verb·폭발·마법·CE 호환성을 주장하지 않습니다.
- Society 소문은 하루 뒤 같은 맵 정착민에게 한 번 전파합니다. 월드 전역의 점진적인 소문 네트워크는 아닙니다. 가족은 바닐라 혈연을 사용하며 가문 조직을 새로 만들지 않습니다.
- Nobility 대리인은 같은 맵·같은 세력의 적격 정착민입니다. 대리인 협상/고용 비용은 없습니다.
- Diplomacy는 플레이어가 정착지 버튼으로 시작하는 제한된 퀘스트입니다. 무작위 모욕 사건·적대 세력 협상·승자의 자유 보상 요구는 없습니다. 기본 상단 도착 메뉴와 실제 맵 생성을 사용하며, 자동화 검사는 직접 맵 생성 후 전투를 검증했습니다. 장거리 상단 이동·화면 조작은 별도 수동 검증 대상입니다.
- Oaths의 칭호는 기즈모에 표시하는 의례 호칭이며 바닐라 작위/능력치가 아닙니다. 맹세 종류는 두 가지입니다. 의식은 전용 600틱 Job이며 Ideology의 Ritual 시스템과 통합하지 않았습니다.

## 남은 개발

세력·개인 봉신과 외국 정착지 계약은 별도 Honor.Contracts 확장으로 개발 중입니다. 현재 선택형 로컬 개발판에 실제 계약 장부·저장·일부 소속 이전·선택형 실물 공납·납부 실적과 영한 UI를 연결했습니다. 보호 퀘스트·개인 봉신/승계·완전한 NPC 경제/군사·세력 명예 사건 연결과 일반 독립/전시/합병 집행은 남아 있습니다. Contracts Workshop 항목은 아직 없습니다. 현재 범위는 문서 상단과 [단계별 범위](CONTRACTS-ROADMAP.md)를 기준으로 합니다.

추가 요청한 정착지 공격·포위 후 항복/점령 계약과 봉신 피습·포위 방어 요청도 규칙 계층에 추가했습니다. 항복 합의와 점령 명령의 경위·서명을 구분하고, 정착지별 범위와 중복 체결을 검사합니다. 실제 패배 처리 보존 패치·포위 진행·사건 생성기·구원 퀘스트는 미구현입니다. 세력 전체 강화는 한 정착지 점령과 별도 범위로 남깁니다.

자유도시(적용되는 유효 봉건계약 없음)와 플레이어 도시의 기지 가치 기준 도달 후 종주/독립 선택도 규칙 계층에 추가했습니다. 기준 제안은 100,000이며 입력값으로 변경 가능합니다. 자동 선택은 없고, 후보 선택은 계약 서명이 아니며, 활성·분쟁 계약을 독립 선언으로 지우지 않습니다. 실제 세계 표시·가치 관측·편지·후보 외교 검증·설정 화면·저장과 전용 NPC 도시 생성은 미구현입니다.

추가 요청에 따라 세력 명예·계약별 신뢰·영지 이탈 압박을 도입했습니다. 검증 사건 입력에 따른 명예, 종주/종속 양방향 신뢰, 저명예·불신의 시간 누적, 경고 전달 후 대응 기간과 느린 회복은 엔진 독립 규칙으로 구현했습니다. 개인 Need는 유지하며 전 주민을 연대 처벌하지 않습니다. 실제 공적 사건 평가/자동 라우팅·게임 UI·Scribe 저장·이탈 AI·독립/반란과 세력 소속 변경은 미구현입니다.

일부 자유도시와 명예가 낮은 종주/소속 세력의 영지가 “구원하면 봉신이 되겠다”고 제안하는 RescuePledge 규칙도 구현했습니다. 고정 문안·수락/구원/확정 기한·검증된 구원·기존 권리 정리·중복 방지를 관리합니다. 미션 생성기·실제 구원 전투·지방 대표 권한·세력 분리·외교/명예 결과·영한 UI·Scribe 저장은 미구현입니다.

일반 결투 승자의 합리적 은·물품·우호도·합류 요구 및 권한/상한 검증, 세밀한 고의 살해 판별, 추가 명예 단계 행동/무드, 고급 이념 의식과 가문·소문 네트워크, UI 수동 플레이·장기 정착지·대규모 모드 조합 검증.

검증 대상은 정확히 1.7.4985 unstable 설치본입니다. 이후 게임/Harmony 변경에는 재검증이 필요합니다. 모드 제거·DLC 제거를 진행 중인 저장 게임에서 안전하다고 보장하지 않습니다.

0.2.1 공개 전 검토에서 발견한 재현 문제와 수정은 [RELEASE-REVIEW](RELEASE-REVIEW.md)를 참조하세요.
