# 모듈 구조 — 0.2.1

Contracts 개발 분기: `PoliticalWorld`는 영지/계약의 정치 ID를 native faction ID와 분리한 독립 모델이다. `Honor.Contracts.Preview`는 별도 `TestMods` 출력과 선택형 GameComponent/Window를 소유하며 모델 JSON만 Scribe에 저장한다. Core는 이를 참조하지 않는다. Preview의 가상 사건은 기존 Core Need/우호도·실물·점령 결과에 연결하지 않는다. 초기 Contracts 프로토콜과 새 모델의 통합, 실제 게임 어댑터는 후속 작업이다. 사용자 확정 요구사항은 CONTRACTS-DECISIONS.md를 기준으로 한다.

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

## 개발 중인 Contracts

`Honor.Contracts.Rules`는 세 범위의 계약 조항·서명·의무·분쟁·종료와 종속 그래프 검사를 담당하는 별도 엔진 독립 프로젝트입니다. 현재 어느 게임 DLL에서도 참조하지 않으며 Core/기존 Honor.Rules와 배포 관계를 바꾸지 않습니다. 향후 Contracts만 이 DLL을 포함합니다. 게임 객체의 권한/존재 확인, 저장, 자원 이전, 명예 결과는 별도 게임 어댑터의 책임입니다. [계약 설계](CONTRACTS-DESIGN.md).

`ContractOrigin`으로 평화 협상·항복 합의·점령 명령을 구별합니다. `SettlementConflict`는 정착지 하나의 공격/포위→확인된 항복 또는 점령→범위 검증→계약 발효를 관리합니다. 점령은 상대 서명 없이 군사적 체결로 표시하되 보호 의무를 유지합니다. `DefenseCalls`는 확인된 공격/포위 사건을 해당 계약의 보호 의무로 연결하며 실제 사건·지도·전투·퀘스트를 생성하거나 성공 판정하지 않습니다. 이 연결과 Scribe 복구는 후속 게임 모듈의 책임입니다.

`FreeCityRules`는 활성·분쟁 계약의 당사자와 정착지 범위에서 현재 자유도시 여부를 계산합니다. `CityRecognition`은 도시별 성장 인식 시점·가치·선택/협상 경위만 관리하며 계약을 서명하거나 별도 지위 플래그로 장부를 덮어쓰지 않습니다. 기지 가치와 현재 기준은 게임 어댑터의 입력이고, 실제 소유권·후보 외교·선택 편지/기즈모·Scribe 저장은 아직 연결하지 않았습니다. 정치 지위 조회 전에 계약 장부를 현재 틱까지 갱신해야 합니다.

`FactionHonorLedger`는 세력별 공적 사건의 고유 ID/순번/전후 값을, `ContractStability`는 계약별 양방향 신뢰·압박·반영 순번·통보 시각을 관리합니다. 현재는 게임과 연결하지 않은 규칙 객체입니다. 미래 Contracts GameComponent가 중앙 장부를 소유하고 사건을 시간/순번 순서로 각 직접 당사자 계약에 전달한 후 시간 누적을 진행해야 합니다. 낮은 명예와 신뢰의 결합이 이탈 압박을 가속하며, 실제 경고 전달·대응 기간 뒤의 SecessionReady는 AI 판단 입력입니다. 계약 종료·정착지 소속 변경·개인 Need 결과를 자동 호출하지 않습니다. 게임 사건의 귀책·증거 검증, Scribe 복원, 실제 독립과 연쇄 파급은 후속 구현입니다.

`RescuePledgeTerms`는 구원 보상으로 약속한 정착지 계약 문안과 사건/세 기한을 고정합니다. `RescuePledge`는 조건부 제안·수락·확인된 구원·이전 관계 정리·계약 체결을 관리하며 기존 DefenseCalls와 달리 구원 전 계약 의무를 만들지 않습니다. 자유도시 또는 실제 계약 당사자의 낮은 명예로 자격을 확인하고, 체결 때 전체 장부와 원 계약 기록을 다시 검사합니다. 권한·실제 전투 결과·독립 판정은 게임 어댑터의 책임이며 새 문안/계약을 중복 보상으로 생성하지 않아야 합니다. 실제 QuestPart/사건 생성기·전투·소속 변경·영한 UI·Scribe 연결은 아직 없습니다.
