# M2.4 — 주민·의료 인계 후 일부 재산이 남은 복구 경계

2026-11-13 / Preview0.3.107. M2.4 전체 또는 일부 주민·의료 복구 완료를 주장하지 않는다.

## 실제 연결

- 새 WorkProgress **version2**는 standalone25의 주민 authority/care/area가 모두 양성 완료하고 원 map/clinical 검사가 일치한 뒤, 재산 일괄 호출 **전에** seal을 저장한다. 기존 version1은 여전히 모든 재산 완료를 요구한다. 과거 증거를 version2로 승격하지 않는다.
- seal의 원 asset-call 원문은 바꾸지 않는다. 현재 같은 원 MapAssetTransfers 장부는 원 순서의 양성 반환 prefix로만 전진할 수 있다. unknown started·반환 철회·잘못된 map/source/target은 거부한다. 현재 survey에 현재 asset-call 원문과 실제 원 source/target 물체를 함께 묶어, 일부 재산만 인계된 뒤 작업·자재가 자연 진행해도 별도 현재 검토가 가능하다.
- 새 현재 전체 당사자 동의의 실제 Recover는 다음 **동일한 원 기본 재산 하나**를 대상으로 한다. 원 Thing/ThingWithComps/Building 참조·정의·stuff·수량·HP·칸·회전·설정·기존 source 권한과 검토한 native 호출/등록/예약 경계를 재확인한다. Frame/작업대/보관함/특수 holder는 이 새 부분 호출 권한에 포함하지 않는다. 원 인물·의료·작업을 다시 호출하거나 재생성하지 않는다.
- 원 AssetJournal에 Started를 먼저 기록하고 실제 SetFaction을 한 번 호출한다. 정상 반환을 독립 기록한 뒤 원 물리 상태·설정을 다시 검증해야 별도 WorkAssetVerifiedV2 사실을 남긴다. native 반환만 있거나 target 소유권만 있는 상태는 보존 확인을 대신하지 못한다. 전체 원 지도 검사를 통과한 기존 정상 일괄 경로도 같은 원 항목의 보존 확인을 기록한다.
- 한 재산의 인계 후 현재 physical survey/asset journal이 바뀌므로 옛 동의를 재사용하지 않는다. 다음 재산 또는 마지막 동일 원 도시 호출에는 새 현재 동의가 필요하다. 기존 .106 city 복구/최종 WorkCompletionV1 영수증으로 조립한다.
- Scribe는 원 seal과 전진한 원 asset journal 및 독립 보존 확인 ID를 따로 저장한다. 로딩은 원문·순서·정상 반환·보존 확인의 연결을 검사하고, 불확실한 호출은 재실행하지 않는다. 완료 시에는 전체 재산 반환과 보존 확인을 요구한다. 원 임상·계약·채무·제삼자 권리·native player 통제를 유지한다.

## 검사와 제한

양 버전 전체 빌드 및 최종 native 컴파일, Honor20093/Contracts5904, EN/KO602 검사 기준. 새 규칙 검사는 version1의 부분 seal 금지 유지, version2의 부분 prefix, complete 거부, unknown call 거부, 반환 철회 거부, 새 현재 승인 원문을 포함한다. 설치 DLL5005 SHA는 .106과 동일한 검토 모듈만 사용한다. 미확인1.6 실행 권한은 추가하지 않는다. 소스/메타데이터·규칙·빌드·패키지만 검사한다.

**남은 작업**: 일부 주민 authority만 반환한 상태, 의료/area 출력만 기록되고 복원이 미완료인 상태는 아직 이번 seal에 포함되지 않는다. 각 원 주민의 호출·의료·area 상태를 독립적으로 연결하고, 완료한 주민의 새 현재 진행과 미완료 원 주민의 실제 호출 권한을 분리해야 한다. 원 임상 증거를 현재 값으로 교체하거나 원 전체 검사를 건너뛰는 방식은 사용하지 않는다. 특수 재산·운반 merge/split·넓은 작업·부모 follower/정치·후손 조립도 남아 있다.

M1 10/10,M2 0/18,전체10/100 유지. M2.4 잔여 노력40–55%/큰3묶음의 계획 추정 범위는 이번 작은 부분 조립으로 임의 축소하지 않는다. 다른 여섯 단계의 과거 잔여 추정은 새 측정값이 아니다. 공개 main 게시만 허용; Preview Steam 게시·예약 없음. 패키지·설치 결과는 STAGE1-HANDOFF의0.3.107에 기록한다.
