# M2.4 — 주민별 부분 호출·의료·영역의 현재 승인 복구

2026-11-13 / Preview0.3.108. 이번 범위는 standalone map25/clinical16의 알려진 양성 부분 반환이다. M2.4 전체 완료가 아니다.

## 실제 진입·실행

`ResidentExecution.Apply`가 원 주민 native 호출의 정상 반환, 의료 출력 및 영역 출력을 기록한 직후 `SealClinicalReturn`을 호출한다. 새 **WorkProgress version3** seal은 원 census/임상·authority/care/area 원문과 원 asset journal, 같은 원 지도·사람·물체 ID 및 현재 survey를 보관한다. 첫 주민만 반환하고 의료/영역 복원이 남았거나 나머지 주민이 아직 source인 경계를 저장할 수 있다. 원 seal을 나중의 현재 임상 자료로 바꾸지 않는다. 알려지지 않은 호출이나 새 seal 없는 옛 중단에 과거 관측을 만들어 넣지 않는다.

실제 UI의 별도 현재 승인 → `RecoverWorkProgress` → `ResumeClinicalOriginal`이 다음 한 단계를 실행한다.

1. 원 주민의 authority/care 출력이 정상 반환한 경우, **미소비된 원 의료 출력만** 기존 `ResidentCareJournal`의 MayRestore/Consume 또는 SealUnchanged로 복원·확인한다. 현재 의료 정책이 관측된 출력과 다르면 덮어쓰지 않는다. 건강·부상·장비를 복원하지 않는다.
2. 의료 완료 후 원 영역이 미완료라면 기존 `ResidentAreaRecords.Restore`로 동일 원 Map/Area 참조의 검증된 빈 출력/정확한 복원 prefix만 이어 붙인다. Applying/Consumed는 원 journal에 남고, 영역·격자 재생성이나 다른 제한 덮어쓰기는 하지 않는다.
3. 앞선 주민의 처리가 끝나면 다음 **미시작 원 주민**의 현재 임상을 새 readonly 임시 capture로 검토한다. 현재 승인에는 주민 ID와 완전한 새 임상 proof `PendingV3`를 저장한다. 새 `WorkResidentStart`는 이 정확한 proof·동의 ID·tick을 별도 Scribe archive에 보관하고, 기존 원 authority/care/area에는 각각 Started를 기록한 뒤 실제 native 소속 호출을 한 번 실행한다. 지원 작업은 기존 검토된 private native work-preserving 호출로 유지하고, 깨끗한 idle만 기존 native 호출을 사용한다. 미시작 주민의 의료 정책·원 영역 자체가 달라졌거나 미지원 작업/이동이 있으면 보류한다.
4. 정상 반환과 의료/영역 출력은 후속 보존 확인보다 먼저 독립 기록한다. 호출이 Started만 있거나 의료 출력이 불명확하면 재호출·소유권으로 완료하지 않는다. 다음 단계는 변경된 survey/임상 journal과 현재 모든 당사자에 대한 **새 동의**를 요구한다.

모든 주민의 호출·의료·영역 완료 후 기존 .107 재산 단계와 .106 원 도시 단계로 실제 조립한다. 원 계약·채무·제삼자 권리와 native player 통제는 유지한다. 의원/귀족 등은 기존 실제 권한 판단을 그대로 사용하며 도시 대표 동의로 개인 권리를 추정하지 않는다.

## 저장·복구·완료 증거

- `WorkProgress.ClinicalAdvance`는 원 census·ID·OriginalCare·영역 원본을 고정하고 호출/출력/복원 상태의 전진만 허용한다. 이미 관측한 의료 출력·영역 Cleared 사실의 변경, 반환·복원 철회와 불확실한 호출은 거부한다. 초기 seal에는 적어도 하나의 양성 원 반환이 있어야 한다.
- 원 임상/census/map/call 원문과 별개로 현재 주민의 승인 proof 및 시작 사실을 archive/Scribe한다. 로딩과 현재 실행은 같은 주민·원 지도·roster, source/target, 원 journal, 동의 PendingV3 및 시작 tick/범위를 연결한다. native 객체는 원 소유자가 유일하게 deep-save한다.
- 원 native 도시 명부의 순서는 비교용 ID 정렬 명부와 분리한다. v3 seal의 RosterV3 ID 순서와 reference-only Scribe가 같은 원 Pawn 집합/원 순서를 확인한다. native 명부를 정렬·교체하지 않으며 완료 후 추가 참조만 해제한다. 인계 재산이 없는 주민 전용 지도도 지원하고 재산 호출을 꾸며 넣지 않는다.
- 최종 `WorkCompletionReceipt.ClinicalV3`에 실제 완료한 authority/care/area 원문을 넣는다. 규칙 Confirm 및 Restore가 전체 주민·의료·영역과 재산의 양성 완료를 요구하고, native 로딩은 이 영수증이 실제 별도 journal과 정확히 같은지도 검증한다. 원 임상 proof를 현재 값으로 교체하거나 완료한 주민을 재호출하지 않는다.
- 새 source 임상은 원 임상·원 area.Previous를 수정하지 않는 별도 readonly capture다. 현재 건강·나이·욕구·관계·지원 작업/이동 등의 변화는 명시적으로 승인된 새 proof에 기록한다. 이전 proof가 새 호출의 권한으로 둔갑하지 않는다.

## 검사와 남은 범위

양 타깃 전체 빌드·최종 native 컴파일, 규칙 Honor20093/Contracts5921, EN/KO604 기준. 혼합 두 주민 fixture(첫 주민 의료/영역 미완료 + 다음 주민 미시작), 현재 승인 proof, unknown 시작 거부, 출력 변경 거부, 전진·완료 및 최종 영수증 round-trip/완료 누락 거부를 추가했다. API 실행 권한은 기존 검토한5005 DLL/MVID만 유지하며 미확인1.6 본문 권한은 추가하지 않는다. 소스/규칙/메타데이터/빌드/패키지만 검사한다.

남은 M2.4: 새 관측 seal 없는 과거 중단·불확실한 출력, 미지원 driver/특수 설비·holder/운반 merge/split, 새로 시작된 작업들의 후속 소비 lineage 확대, 왕실·군사·노예·수술 혼합 및 부모 follower/정치·후손 조립. 일부는 의도적으로 거부해야 하는 상태이며 가드 제거로 완료 처리하지 않는다. 주민 의료 정책/원 영역 변경에는 별도 정책 권한이 필요하다. 새 v3 범위를 기존 v1/v2에 소급하지 않는다.

M1 10/10,M2 0/18,전체10/100 유지. M2.4 잔여 노력은 계획 추정35–50%/큰3묶음(관측·작업 조합 확대 / 특수 역할·권리 / 부모·후손 통합)이며 체크리스트 완료율이나 실측 시간이 아니다. 다른 여섯 단계의 과거 잔여 추정은 재측정하지 않았다. 공개 main만 게시; Preview Steam·자동 예약 없음. 패키지/설치 결과는 STAGE1-HANDOFF에 기록한다.
