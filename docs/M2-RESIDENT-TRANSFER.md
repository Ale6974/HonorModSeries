# M2 주민 등록·대기 이전 — Preview0.3.96

현재는 M2 부분 구현이다. 원 계약이 종료되거나 권리가 바뀐 복합 소실·귀환과 진행 중인 작업 자체를 소속 변경 너머로 직접 재구축하는 일반 경로는 미완료다.

## 명부 밖 민간인

도시 관리 화면의 ‘명부 밖 민간인의 별도 주민 등록 검토’에서 현재 일반 세력 대표와 대상 이름을 확인한다. 대표가 유효한 독립 일반 세력 결정권자이며 원 도시·현재 계약·검증된 소유자·인물 참조와 임상 상태가 맞아야 한다. 군대/Lord·특수 역할·미확인 private/quest 상태·진행 작업/예약은 자동으로 민간인으로 간주하지 않는다. 왕정/연합의 별도 새 등록 승인도 아직 구현하지 않았다.

확인하면 기존 도시 명부의 순서와 원 주민을 그대로 두고 같은 원 Pawn 참조만 추가한다. 인물의 소속·건강·장비·의료·영역·작업을 바꾸지 않는다. 그 후 기존 ‘이전 검토’와 확인으로 현재 전체 임상/명부/지도/재산/계약을 다시 검토하고 기존 실제 이전 경로를 실행한다. 등록 결정 자체가 소속 변경이나 옛 의료 초기화 복구 권한은 아니다.

중단되면 ‘원 민간인 등록 중단 복구’를 사용한다. 원 대표/Book/참조/명부 prefix/임상 상태가 달라졌거나 append가 시작됐지만 정상 반환이 없으면 다시 추가하거나 새 자료로 덮어쓰지 않는다. 모든 추가가 반환돼도 마지막 임상 Verified가 없으면 완료하지 않는다. 원 기록과 참조는 유지한다.

## 작업·예약을 유지하는 대기 이전

‘진행 작업·예약 종료 후 도시 이전 대기’는 현재 원 도시/지도/대상 세력·대표·계약/물리 장부 prefix·인원 범위를 고정한다. 확인 후 게임이 진행하는 작업·작업 실행기·대기열·예약은 원래 게임 저장 경로에 남으며, 모드는 강제 종료·예약 정리·진행도 복사·재시작을 하지 않는다.

안전한 유휴·무예약 시점을 관찰하면 현재 상태를 새로 검토하고 필요한 별도 주민 등록과 기존 전체 이전을 실행한다. 이때의 임상 snapshot은 새로운 실제 실행의 원본이며 대기 시작 때의 건강이나 작업으로 되돌리는 자료가 아니다. 계속 작업이 있는 경우 대기는 끝나지 않을 수 있다. 이를 해결하려고 강제로 일을 중단하거나 AI를 정지시키지 않는다.

- 실행 전 Waiting은 도시를 바꾸지 않고 취소할 수 있다.
- 계약/대표/소유자/인원/지도 등이 달라지면 증거를 보존하고 대기를 중단한다.
- Started는 자동 재시도하지 않는다. 원 등록/물리 이전 기록을 먼저 복구한다.
- ‘원 이전 완료 영수증 연결’은 유일한 독립 Applied 기록의 원 계약/도시/source/target/시계를 확인한다. 현재 도시 소유권만으로는 완료하지 않는다.
- 아직 물리 실행 기록이 생기지 않았다면 스케줄 의도만 중단할 수 있다. 실제 등록/이전 장부를 취소하거나 완료한 것으로 기록하지 않는다.
- 복합 소실·재탈환으로 원 계약이나 제삼자 권리가 달라진 경우는 아직 새 권한/전체 custody 복구가 필요하다. 원 인물·물체·계약을 재생성하거나 옛 미확인 호출을 반복하지 않는다.

영어/한국어는 게임 언어를 따른다. 1.6/net472와1.7/net10 패키지는 분리한다. 새 work 관찰/대기는 검토한1.7.5005 몸체에서만 실행하며, 미확인 native 몸체를 참조 시그니처만으로 허용하지 않는다. 로컬 설치는 활성화/언어 설정을 변경하지 않는다. Preview Steam 게시는 수행하지 않는다.

## English

The town window offers a separate explicit enrollment review for eligible unlisted civilians. It records the current ordinary representative, original roster and people, clinical/care evidence, each append start/normal return and final verification. It appends the same original Pawn references; it does not change faction, health, equipment or work. A fresh complete town migration review is still required afterward.

Deferred transfers keep native work and reservations running and natively saved. At an observed safe idle boundary they perform fresh authority, registration and full migration reviews before calling the existing executor. A busy town may keep waiting indefinitely; no work is forcibly cancelled. Started execution is never automatically retried. Recover original journals and link only their independent positive applied migration receipt. Changed scope/loss stops waiting with evidence retained. Compound loss/recapture and generic in-flight job continuation remain unfinished.


## 종료 계약의 원 이전 완료 승인 — 0.3.97

도시 이력 또는 전체 이전 창에서 ‘현재 승인’ 검토를 선택한다. 현재 당사자·세력 대표와 원 이전 범위를 검토하고 확인하면 같은 원 주민·재산·도시의 남은 이전을 기존 실행 경로로 진행한다. 해약된 원 계약을 다시 활성화하지 않으며 기존 하위 계약·채무·공납 실적은 유지한다. 연합/귀족은 이번 범위의 현재 결정이 필요하다.

중단 후에는 같은 원 이력에서 복구한다. 이미 정상 반환한 호출은 반복하지 않고, 반환 여부가 불분명하거나 원 대상이 바뀌면 보류한다. 계약·공납·대표가 바뀌었다면 현재 승인 전체를 다시 검토한다. 도시/지도/주민/물체 소실이나 재생성, 원 임상/재고 변화는 이 승인으로 초기화·재생성하지 않는다.

## Current custody completion — English

The town history and full transfer window offer a separate current approval review. Review the actual parties, current representatives and original transfer scope, then confirm to finish the same original residents, assets and town through the existing executor. An ended charter stays ended; subordinate contracts, debt and payment evidence remain. Collective/royal parties require a fresh decision scoped to this action.

Recover through the same original history after interruption. Positive normal returns are not replayed; uncertain calls or changed original objects remain blocked. Changed contracts, payments or representatives require a fresh complete approval. This does not recreate lost maps, people or items, reset clinical/stock evidence, or complete every compound loss/recapture case.
