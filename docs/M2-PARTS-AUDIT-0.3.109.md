# M2.4 — 새 작업 관측 승인과 운반 합쳐짐의 실제 증거

2026-11-13 / Preview0.3.109. M2.4 전체 완료 판정이 아니다. 기존 standalone map25/clinical16/v3 실행 경로에 다음 부품을 실제 조립했다.

## 새 현재 작업 → 승인 → 같은 native 작업의 관측

- 현재 전체 당사자 승인에 `JobsV4`로 지원 작업의 실제 `ActiveWorkRow` 원문을 넣는다. 지원 대상은 기존 검토한 HaulToCell, 프레임 공급 HaulToContainer, ConstructFinishFrame, 기본 DoBill이다. 미지원 현재 작업은 새 관측 권한을 얻지 않는다. UI 확인문에도 현재 작업의 종료·자재 소비 관측을 함께 기록한다는 내용을 표시한다.
- Commit은 새 승인의 작업 원문과 실제 현재 작업을 다시 비교한 뒤, 원 Pawn/현재 Job/JobDef 참조, native startTick, 작업 원문 및 정확한 동의 ID·영수증 hash·등록 tick을 별도 `WorkJobObservationRecord`에 보관한다. 임의 작업을 자동 등록하거나 기존 grant를 덮지 않는다. 대기 작업들이 원문에 포함되어도 **현재 작업 하나만** 관측 참조를 얻으며, 다음 작업은 새 검토가 필요하다.
- 이후 실제 native 종료/제작/공사 관측은 준비 당시 원 작업뿐 아니라 이 새 승인에 연결된 동일 Job 참조·ID·정의·startTick도 인식한다. `GrantV4`가 원 사건에 연결되고 body return/검증 출력은 기존처럼 독립 저장한다. 작업 실행·재시작·이동·복제는 하지 않는다. 원 임상·지도·census/call/care/area/asset 원문도 교체하지 않는다.
- 규칙 `WorkJobObservations`는 정확한 현재 전체 동의, 원 transfer, 승인한 작업 원문과 grant를 검증한다. native 로딩/복구는 사건의 grant가 실제 archive 및 해당 동의의 JobsV4에 연결되는지 다시 확인한다. 잘못된 actor/job/영수증/원문/tick, 빠진 grant archive와 legacy 권한 확장은 거부한다.

## 운반 내려놓기·합쳐짐

- 검토한5005의 `Pawn_CarryTracker.TryDropCarriedThing` 두 정확한 overload를 readonly prefix/postfix/finalizer로 관측한다. 현재 같은 원 Pawn의 정확한 HaulToCell driver/Job과 새 관측 승인이 있어야 하며, 원 실제 carry 물체 하나를 입력으로 묶는다. 원 helper를 복구에서 호출하거나 `SplitOff`/`TryAbsorbStack`/배치를 직접 실행하지 않는다.
- `haul-place` 사건은 원 호출 Started, body 정상 반환과 **실제 bool 결과**, 그 뒤의 현재 물체 집합·수량/HP/위치·예약 survey를 별도 보관한다. 내려놓은 원 carry ID가 기존 더미에 합쳐져 사라지거나 native 분할로 새 ID가 생긴 경우, 정상 성공과 입력 범위에 맞는 양성 출력만 기존 lineage/새 현재 승인에 사용할 수 있다.
- 실패 반환도 원문에 남는다. false 반환으로 원 ID 소실/새 물체 발생을 완료 증거로 주장할 수 없다. 출력 조사 실패/예외는 Started/body 결과를 유지하고 미확인 상태로 남으며, native 호출을 재실행하지 않는다. 중첩 recipe/job-end 관측은 같은 동작을 이중 처리하지 않는다.
- 외부 carry method 패치, 비native placedAction, 알려지지 않은 배치/분할/합침 경계는 관측을 보류한다. 두 메서드 자체는 정확한 자체 observer만 허용한다. 기존 source/target/동일 지도·사람·원 물체·예약·제삼자 권리 조건도 유지한다.

## 저장·중단 복구·참조 수명

`WorkJobObservationsV4`는 자기 descriptor만 deep-save하고 Pawn/Job/JobDef는 reference/Def 저장한다. 원 native tracker가 유일한 Job deep-save 소유자다. 게임이 작업을 끝내거나 pool로 반환한 뒤에는 추가 Job 참조만 비우고 원 ID/작업 원문/동의/사건을 유지한다. 종료된 작업을 나중 작업으로 다시 연결하지 않는다. 최종 양성 완료에서는 추가 Pawn/Job 참조를 해제하고 증거는 남긴다. archive는256개/32M, 개별 원문/결정 크기도 제한하며 초과 시 원 기록을 삭제하지 않는다.

새 사건은 기존 `.108` 주민·의료·영역/재산/원 도시 실행, 현재 승인, 최종 WorkCompletion 영수증, Scribe/FinalizeInit/명시 Recover와 연결된다. 일반 원 상태 복구로 되돌아가도 새 관측 archive의 불일치를 건너뛰지 않는다. 옛 null JSON과 Begin/Approve 호출 형식은 유지하며, 새 grant가 없는 v1/v2에 새 운반 권한을 부여하지 않는다.

## API 근거·검사

설치 DLL5005 SHA256 `82387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125`/MVID `3523c04c-c298-43ae-9571-20caaef22acc`만 새 native 경계를 실행한다. 해당 DLL의 Job.Clear/ExposeData, **Verse.JobMaker**의 pool 재사용과 새 loadID 할당, carry의 두 bool 반환 메서드 및 ThingOwner의 실제 drop/split/재합침 흐름을 메타데이터·본문으로 확인했다. 타입 검색 실패는 근거로 사용하지 않았다. 게임 원문은 ignored .local에만 보관한다. 미확인1.6 본문 권한은 추가하지 않는다.

양 타깃 전체 빌드/최종 native 컴파일, Honor20093/Contracts5944, ENKO605 기준. 새 승인 누락·변조·다른 actor/job/tick·옛 scope 거부, 새 작업 공사 완료와 원 seal 불변, 저장 round-trip/최종 완료 연결, 운반 true/false 결과와 합쳐짐 ID·미확인 출력 거부를 검사했다. 실제 패키지/설치 결과는 STAGE1-HANDOFF의 .109에 기록한다.

## 남은 구현

1. 원 소유 프레임이 재산 호출 전에 자연 완공되어 **다른 source 소유 건물**이 된 경우의 별도 후계 재산 권한·새 호출/복구. 양성 작업 소비가 있어도 사라진 원 재산 호출을 반환한 것으로 꾸미지 않는다. 더 넓은 driver, 설비/전력/facility, holder 및 운반 조합도 남아 있다.
2. 개인·군사·왕실·노예·수술 역할의 작업 결합과 실제 각각의 권한/보존 부품 조립.
3. 부모 follower/정치·후손/M7 재사용·M2.10 복합 중단 복구 통합. 새 standalone 관측 승인으로 부모 실행 권한을 추정하지 않는다.

M2.4 미완료, 잔여 노력 계획 추정30–45%/큰3묶음. M1 10/10,M2 0/18,전체10/100 유지. 다른 여섯 단계의 과거 잔여 추정은 이번에 재측정하지 않았다. 공개 main 게시만 허용하며 Preview Steam·반복 예약은 하지 않는다.
