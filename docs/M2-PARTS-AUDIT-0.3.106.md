# M2.4 — 원 기록 이후의 작업 진행과 현재 승인, Preview 0.3.106

2026-11-13. M2.4 전체 완료가 아니다. M1 10/10, M2 미완료 0/18, 기존 통합 점수 10/100을 유지한다.

## 실제 실행·저장·복구 연결

- `WorkProgress`(B06/B03): 원 standalone map25/clinical16의 모든 주민 authority/care/area와 모든 asset 호출이 양성 완료한 뒤, 원 도시 호출 직전에 별도 seal을 만든다. 원 이전 hash, census/call/care/area/asset 원문, tick, 원 물체 ID 집합과 현재 survey를 묶는다. 기존 임상·지도·호출 원문을 새 현재 상태로 교체하지 않는다. 관측 기능을 준비할 수 없는 경우 추가 seal을 만들지 않고 기존의 엄격한 원 인계 검사만 계속한다.
- `WorkProgressHooks`: 검토한 native Frame.CompleteConstruction, Pawn_JobTracker.EndCurrentJob(Succeeded), Toils_Recipe의 **전체** FinishRecipeAndStartStoringProduct initAction을 관측한다. Started → native body 정상 반환 → 검증된 현재 출력의 세 사실을 따로 저장한다. 제작 재료를 계산하려고 CalculateIngredients/ConsumeIngredients를 호출하지 않는다. 중첩 관측은 같은 동작의 이중 기록을 피한다. 관측 실패가 native 작업을 취소하거나 재실행하지 않는다.
- 프레임·자재의 소실과 새 완성물 ID, 제작 원 입력·소비·산출물, 원 작업 종료를 같은 원 Pawn/Job 참조 및 ID에 연결한다. 입력과 무관한 소실, 불확실한 시작, 검증되지 않은 출력은 현재 승인에 사용할 수 없다. 부분 stack 소비는 새 현재 survey에 남고 원 stack snapshot은 유지된다.
- `WorkProgressSurvey`: 같은 원 Map/Pawn/도시 roster와 실제 현재 물체/보관 구조, 임상·의료·영역·수술·장비·관계·욕구, 현재 작업/대기열, terrain/under-terrain/roof와 검토한 일반·목적지·physical·enroute 예약을 읽어 묶는다. 다른 지도/attack 예약 제한은 유지한다. 사라진 원 물체는 별도 양성 소비/완료 lineage가 있어야 한다. 미지원 현재 holder/comp/driver/설비는 보류한다.
- UI의 별도 진행 검토는 새 `WorkProgressV1` 행동 범위로 현재 원 source/target/연결 하위·제삼자·상위 당사자의 기존 실제 정부 결정을 요구한다. 현재 계약·채무·실적·외국 종주 권리를 그대로 묶는다. 옛 동의는 새 진행 허가가 아니다. 승인 후 현재 자료나 결정이 달라지면 새 검토가 필요하다.
- `RecoverWorkProgress`: 이미 양성 완료한 주민·재산 호출을 건너뛰고 **시작되지 않은 동일 원 도시 호출만** 실행한다. 이미 시작했으나 반환이 불확실한 도시는 재호출하지 않는다. 정상 반환과 현재 동의를 다시 확인한 뒤 독립 WorkCompletionV1 영수증을 남기고 Applied로 끝낸다. 도시 소유권만으로 완료하지 않는다.
- Scribe는 새 진행 원문을 별도 저장한다. 로딩은 원 seal/census/call/의료/area/asset 및 최종 current agreement/영수증을 검증한다. seal 뒤 자연 진행으로 옛 임상·지도 snapshot과 달라졌다는 이유만으로 새 증거를 덮어쓰지 않으며, 명시 복구에서 다시 현재 권한과 원 신원을 확인한다. 원 native 객체는 기존 소유자가 유일하게 deep-save한다.

## API 근거와 범위

설치 DLL 1.7.5005 rev1082, SHA256 `82387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125`, MVID `3523c04c-c298-43ae-9571-20caaef22acc`만 새 관측 실행을 허용한다. 원 제작 closure의 전체 initAction은 이 모듈의 token `06010955`/정확한 타입·메서드 이름과 함께 확인한다. native 메타데이터/본문만 읽었으며 원 소스 복사본은 게시하지 않는다. 미확인 1.6 본문 권한을 추정하지 않는다. 양 버전의 분리 빌드와 기존 경로는 유지한다.

기존 null JSON/포맷/권한을 확장하지 않는다. 새 seal 없는 과거의 진행 중단에 뒤늦게 관측 증거를 꾸미지 않는다. 외부 Harmony 패치가 있으면 새 seal/승인이 보류되며 정확한 자체 readonly observer만 기존 toil 검사에서 별도로 식별한다.

## 검사

순수 규칙 검사는 원 호출 미완료 seal 거부, 원문 변경·시간 역행, 불확실한 native 결과/출력, 소비 lineage, 현재 제삼자 거부·누락, 옛 동의 재사용, 도시 반환 누락, 저장 round-trip, 원 계약·채무·이전 보존을 다룬다. 최종 빌드·패키지·설치 결과는 STATUS의 0.3.106 항목에 기록한다.

## 남은 실제 구현 — 완료 아님

1. **더 이른 부분 시작**: 일부 주민만 반환했거나 care/area/asset 보존 확인 도중 자연 진행한 상태. 이번 seal은 모든 주민·재산 양성 완료 뒤만 허용한다. 각 원 객체/미완료 호출·출력을 개별 증거에 연결하는 별도 조립이 필요하다.
2. **넓은 native 작업·복합 물체**: 다른 driver, 진행 UFT/설비·전력/facility·작업장, 운반 merge/split로 원 ID가 소실되는 전체 경계, 사유·군사·수술 혼합 및 새 인물/지도 소실·재생성의 권리를 구현해야 한다. 현재 조사에서 지원하지 않는 값은 원문을 유지하고 보류한다.
3. **부모·후손 조립**: follower/정치/귀족/전시/후속 이전 및 M7 재사용·M2.10 전체 복합 복구에 새 동의/진행 영수증을 연결해야 한다. standalone 출처를 다른 부모 동의로 대신하지 않는다.

M2.4 남은 노력은 계획상 약40–55%, 큰 작업3묶음으로 추정한다. 체크리스트 완료율이나 실측 소요 시간이 아니다. 다른 6단계의 과거 잔여 추정값은 이번 변경으로 재측정하지 않았다. 공개 main 게시만 허용하며 Preview Steam 게시·반복 예약은 하지 않는다.
