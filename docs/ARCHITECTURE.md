# 모듈 구조 — 0.2.1

Core는 다른 부속 모드에 의존하지 않습니다. 부속 모드는 Core/Rules/Harmony API만 참조하며 서로 어셈블리를 참조하지 않습니다. Honor.Rules.dll은 Core에만 포함하고 각 부속 모드에는 자신의 DLL만 배포합니다. Nobility는 Royalty DLC가 필요합니다.

| 모듈 | 소유 데이터와 연결 |
|---|---|
| Honor.Rules | 엔진 독립 명예 수치·자격·종료 규칙 |
| Honor.Core | Need, Harmony 모욕/피해 훅, MapComponent 결투, 전용 JobDriver, 설정, principal/대리인 참조 |
| Honor.Society | MapComponent 쌍별 모욕/사과와 소문 목록; 바닐라 Thought_MemorySocial와 혈연 사용 |
| Honor.Nobility | 최고 작위의 seniority에 따른 SensitivityModifiers, Core.AssignProxy |
| Honor.Diplomacy | QuestNode + QuestPart + MapParent, 대표 Pawn 참조; GameComponent 세력 대기 기간; 합의된 대표 결투 타격만 바닐라 외교 공격에서 제외 |
| Honor.Oaths | GameComponent 맹세 목록, 전용 의식 JobDriver, Core 사건 구독 |

Core 상태는 Pending → Gathering → Fighting → Completed/Cancelled입니다. 종료를 먼저 기록하고 처치·보상·이벤트를 호출해 재진입으로 복제하지 않습니다. Diplomacy도 Resolved를 먼저 기록해 은/우호도/퀘스트 결과를 한 번만 반영합니다. pending 의무와 확장 상태는 Scribe로 저장합니다.

확장 API: HonorApi.Need/Change, DuelEnded, InsultOccurred, DishonorOccurred, SensitivityModifiers, AdditionalParticipantValidators, PendingDelayTicks. 구독자 예외는 Core가 분리해 기록합니다. Diplomacy는 지정된 장소의 살아 있는 대표만 추가 자격으로 인정하며 모든 방문객을 허용하지 않습니다. Society/Nobility는 자동 준비 지연을 최소 600틱으로 올립니다. 수동 TryBegin은 즉시 가능합니다.

DuelRecord.Beneficiary는 실제 전투자에서 귀족 본인으로 결과를 매핑합니다. AssignProxy는 pending 상태에서만 허용하고 최초 신청 시 고정한 Mode를 바꾸지 않습니다. Involves는 전투자와 본인 모두를 바쁜 참가자로 취급합니다.

DuelRuleExtension은 PreceptDef 확장입니다. 최초 신청자 교리만 읽고 충돌 시 defName 순서로 고릅니다. 별도 평판 화폐는 없고 바닐라 관계 기억/혈연/세력 우호도를 사용합니다.

모든 게임 객체 접근은 게임 스레드에서 수행합니다. 실제 게임/DLC 타입은 설치 DLL로 검증하며 게임 DLL·역컴파일본은 배포하지 않습니다. 계획과 출시 범위는 STATUS, 증거는 TESTING을 참조하세요.
