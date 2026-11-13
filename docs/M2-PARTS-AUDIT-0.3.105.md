# M2.4 — 실제 작업 보존 인계, Preview 0.3.105

2026-11-13. M2.4 전체 완료 판정이 아니다. M1 10/10, M2 미완료 0/18, 기존 통합 점수 10/100을 유지한다. 부품 작성량·빌드 통과를 통합 완료 점수로 바꾸지 않는다.

## 실제 연결한 부품과 진입점

- **B06 / ResidentActiveWork**: 새 map25 / clinical16 / ActiveWorkV1에서 정확한 native `JobDriver_HaulToCell`, `JobDriver_HaulToContainer`의 프레임 공급, `JobDriver_ConstructFinishFrame`, `JobDriver_DoBill`의 지원 기본 제작을 다룬다. 원 현재 Job/driver/안정된 toil index/남은 시간/제작량, 순서 있는 대기열(최대32), 원 제공자 ThinkTree/키, 이동의 native 저장 필드, 작업 우선순위 및 priority/thinkData 원문을 묶는다. 기존 rest23/24와 임상1~15의 권한·바이트는 바꾸지 않는다. 휴식과 다른 미지원 driver를 임의로 섞지 않는다.
- **B03 / NativeWorkFactionCall**: 실제 `ResidentExecution.Apply`의 새16 분기에서 호출한다. Harmony ReversePatcher가 검토한 native Pawn.SetFaction의 별도 private 복사본을 만들고 정확히 두 호출 경계만 교체한다. 원 전역 Pawn/Job 메서드를 패치하거나 raw faction 필드를 쓰지 않는다. 승인된 같은 Game/Pawn/Map/source/target/원 작업/대기열/예약 문맥에서만 ClearMind의 작업·이동 정리를 생략한다. 나머지 native 등록·소속·관계·의료 reset 처리는 기존 원 임상/care/area journal에 그대로 연결한다. 변환 경계 수가 각각1이 아니거나 외부 패치가 있으면 native 시작 전에 거부한다.
- **예약**: 같은 원 ReservationManager의 물품/칸/공유/다중/대기 작업 claim, physical ingredient claim 및 원 frame enroute list/lookup/count를 native 저장 구조 그대로 유지한다. 각 claimant가 실제 원 도시 시민이며 원 현재/대기 Job과 대상이 맞아야 한다. 다른 지도와 attack reservation은 기존 제한을 유지한다. 제삼자·병력·미지원 작업의 예약을 묵시적으로 승인하지 않는다.
- **목적지 색인**: PawnDestinationReservationManager의 동일한 원 reservation 객체를 원 claimant의 순서대로 target faction 색인으로 옮긴다. 기존 source/target set과 다른 claimant는 유지한다. 필요한 새 빈 cache set/목록은 먼저 할당하고, 콜백 없는 주 스레드 필드 교체에서 원 행을 이동한다. Reserve/Release로 행을 재생성하거나 job을 새로 예약하지 않는다. native 호출 시작 증거가 이 변경 전체를 감싸고, 불확실한 시작은 재실행하지 않는다. 실제 살아 있는 원 Job/driver/toils/path/queue/cargo/claim 객체는 호출 전후 같은 참조와 상태인지 따로 확인한다. runtime identity 비교 값은 저장 권한이나 load 증거로 쓰지 않는다.
- **B07 / ConstructionEvidence**: 원 Frame의 workDone, build definition, 색/금지 상태, 원 resourceContainer/Owner/슬롯/자재 객체를 보존한다. 자재는 검토한 기본 중립 물품만 허용한다. 별도 WorkFramesV1과 원 참조만 Scribe하며 Frame이 유일하게 자재를 deep-save한다. 열기·꺼내기·소비·완공·재생성은 인계 중 하지 않는다. storageGroup/settings가 있는 프레임은 별도 보존 프로토콜 전까지 보류한다.
- **WorktableEvidence**: 원 기본 Building_WorkTable 및 검토한 정확한 Building_WorkTable_HeatPush의 동일 BillStack/정렬된 bill 소유 관계와 원 production bill 객체, recipe/worker/fixed filter, 반복·보관·작명·범위·재료 필터·진행량을 WorktablesV1/원 참조에 묶는다. Bill이나 필터를 새로 만들거나 setter로 복사하지 않는다. native 저장이 재료 허용 목록을 바꿀 상태는 사전에 거부한다. source-owned table과 원 재산별 native 호출 journal을 사용한다. 설치 XML의 CraftingSpot/ButcherSpot에 있는 native report component와 연결 시설이 없는 affected-by-facilities component도 별도로 검토했다. 전력·연료·고장·연결 시설·사유 노동 제한·미완성 제작물은 아직 별도 범위가 필요하다. 설치 XML의 석재 절단 작업대가 HeatPush 하위 클래스임을 확인해 해당 native 무상태 UsedThisTick 본문과 상속 소속 호출을 따로 검토했다. 체력을 사용하지 않는 CraftingSpot/ButcherSpot의 native HP=-1은 기존 물체 증거가 거부하므로 아직 보류한다. 가짜 체력값이나 상태 초기화로 통과시키지 않는다.
- **운반 화물**: 원 Pawn_CarryTracker/정확한 ThingOwner/원 slot0의 기본 중립 물품을 원 건강/장비 자료와 별개로 저장 증거에 묶고 reference-only Scribe한다. native carry tracker가 유일한 deep-save 소유자다. 원 자재·화물·지도 물품·주민·장비 ID의 중복과 범위 초과도 거부한다. 소유권이 있는/사유/복합 화물은 이 기본 범위에 포함시키지 않는다.

## 실제 저장·중단 복구

진입은 기존 도시 관리 UI의 현재 계약/대표 검토 → 원 map25/clinical16 준비 → 기존 resident/care/area 호출 → 재산별 호출 → 도시 호출 → 확인이다. 원 임상·재고·계약·채무·하위 권리·native player 통제는 기존 journal과 함께 유지한다.

주민 native 본문이 정상 반환하면 **즉시** 원 AuthorityJournal에 반환을 남기고 현재 원 객체의 의료 output과 area output도 기록한 뒤 작업 보존을 확인한다. 뒤의 확인이 실패해도 그 호출을 다시 실행하지 않는다. 원 작업과 자료가 그대로인 양성 부분 반환 상태는 기존 명시 Recover가 저장된 원 참조/호출/care/area/자산/도시 증거로 전진한다. 미반환 Started, 달라진 작업/건강/자재, 소실/재생성 객체, 불명확한 결과는 원문을 유지하고 보류한다. target 소유권만으로 완료하지 않는다.

**ActiveWorkAbort / StandaloneActiveWorkV1**은 UI에서 새로 준비한 단독25에만 허용한다. 주민/care/area/재산/도시 변경이 하나도 시작되지 않았다는 별도 증거를 갖춘 경우 현재 작업이 자연 진행/종료했어도 준비만 취소하고 원 이력을 UnstartedActiveWorkAbortV1으로 보관한다. 현재 작업·건강·예약·소속·계약·채무를 쓰지 않는다. 취소 후 새 실행에는 새 검토가 필요하다. 새 미실행 단독 준비는 작업 진행 차이만으로 전체 장부의 restore 실패를 만들지 않는다. 기존24의 중단 권한을25로 소급하지 않으며, 부모 스케줄러·정치·귀족·후손·후속 custody 조합에는 이 UI 출처를 꾸미지 않는다.

## API 근거와 검사 경계

설치 1.7.5005 DLL SHA256 `82387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125`, MVID `3523c04c-c298-43ae-9571-20caaef22acc`의 Pawn.SetFaction/ClearMind, guest/components, 네 driver와 Job/JobQueue/QueuedJob/JobDriver/PathFollower/PriorityWork/WorkSettings, 네 예약 manager와 ThingCountTracker, Frame 및 frame generator, Building_WorkTable/Bill/BillStack/ThingFilter, carry/forbidden/시설·표시 component 본문을 대조했다. 실패한 타입 조회나 컴파일 오류는 API 근거가 아니다. 역컴파일 원문은 ignored .local에만 두고 배포하지 않는다.

새 실행은 이 검토한5005 모듈에만 허용한다. 1.6/net472와1.7/net10을 각각 컴파일하며 미검토1.6 본문을 허용하지 않는다. 새 작업 안내는 기본 영어/한국어597키의 native 언어 경로를 사용하고 사용자 언어/활성화 설정을 변경하지 않는다.

ActiveWorkTests는 원 다중 Job/대기열/진행값/다른 제공자 범위, 기본 table/bill·frame/resource 문서, 원 census/의료·호출의 부분 저장/반환/전진, unknown-call 재시작 거부, 별도 미실행 취소/이력·원 계약 불변을 검사한다. native 객체 실행의 증명으로 순수 자료 검사를 바꾸어 부르지 않는다. 검사는 코드·규칙·참조 빌드·메타데이터·XML·패키지로 한정한다.

## 완료하지 않은 M2.4와 다음 착수

1. **이미 시작한 뒤 자연 진행/종료·자재 소비가 생긴 복구**: 원 snapshot을 현재 값으로 덮지 않고, 원 양성 호출/보존 관측에 연결되는 새 진행·완료 사실과 현재 승인을 별도 append하는 실행·저장 경로가 필요하다. Job pooling, frame 완공으로 바뀐 객체, UFT·제작 결과의 실제 계보도 구분해야 한다. unknown call은 여전히 재실행 금지다.
2. **더 넓은 작업·설비·혼합 상태**: 다른 driver/휴식 혼합, 소유·복합 운반 화물, 미완성 제작물과 bound bill/ingredients, 전력·연료·시설 링크·무체력 제작 지점, storage frame, 외부 claimant/다른 지도·병력·수술 조합. 읽기 가드를 지워 지원한다고 부르지 않는다.
3. **부모 조립과 전체 중단 경계**: 기존 lawful follower는 여전히 임상7~11을 받는다. 이 새16을 기존 승인으로 끼워 넣지 않는다. 별도 원 부모/현재 전체 당사자 승인/일괄 진행·후손/정치·전시 조합을 연결하고 각각 저장·명시 복구를 검증해야 한다. M2.5/M3/M7/M2.10 소유 작업과 조립 책임을 유지한다.

다음은 1번의 양성 완료 관측·append-only 진행 증거 설계와 실제 native 완료/저장 훅부터 작업한다. **이번 네 driver와 네 종류 예약을 연결했어도 M2.4 전체는 미완료**다. 남은 M2.4 노력은 계획 추정 약45~60%, 큰 묶음3개다. 완료 체크리스트 비율이나 실측 시간이 아니며 수량을 맞춰 범위를 줄이지 않는다.

여섯 단계의 과거 추정 잔여31.5/80/95/90/100/70%는 다른 단계의 새 측정값이 아니다. 이번에 구현한 것은 1단계의 M2.4 부분이며 2~6단계는 변경하지 않았다. 기존 통합 점수는 M1 10/10, M2 0/18, 전체10/100이다.

최종 검사·패키지·설치 결과는 아래에 기록한다. Preview Steam 게시와 자동 예약은 없다.

최종 결과: 양 타깃 전체 빌드 및 최종 native 참조 컴파일 경고/오류0; Honor20093 / Contracts5864; EN/KO597 및 placeholder 일치. 패키지 runtime/DLL allowlist 통과. 백업 contracts-preview-8e667df7d1e843ce860ca8365e87a016 후 로컬32개 파일 일치, 활성화/언어 설정 해시 불변. ZIP SHA256 DCF5760A1EE838A5C9B58B9C3AD5A5A5CD5F46E4C5E5CC9F3B26126CD6E0E02E. 설치 게임 DLL SHA는 위 검토값과 동일하다.
메타데이터에서 원 Pawn.SetFaction의 mind 정리1개/목적지 해제1개 경계도 확인했다. 최종 컴파일된 private copy에 reverse-patch transpiler local function이 포함됨을 확인했다. 이 구조 검사는 native 본문의 실제 실행 증거와 구분한다.
