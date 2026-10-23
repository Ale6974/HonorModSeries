# 모듈 구조와 단계

| 모듈 | 소유 기능 | 현재 단계 |
|---|---|---|
| Honor.Rules | 엔진 독립 명예 수치·참가 자격·결투 종료 규칙 | 구현·테스트 |
| Honor.Core | Need, 모욕 훅, 대기열·결투 상태기계, JobDriver, 안전, 응급처치, 소셜 기억, 설정·저장·API | MVP 구현 |
| Honor.Society | 사과, 목격·소문 전파, 가족 명예, 숙적 | 소스 경계·명세만 준비 |
| Honor.Nobility | Royalty 작위별 민감도, 대리인 자격·대리 결투 | 소스 경계·명세만 준비 |
| Honor.Diplomacy | 정착지 모욕, 월드맵 대표 결투 퀘스트 | 소스 경계·명세만 준비 |
| Honor.Oaths | 맹세, 의식, 칭호 | 소스 경계·명세만 준비 |

Core는 다른 확장 모듈에 의존하지 않습니다. 각 확장은 Core에만 의존하고 게임/DLC 타입은 실제 해당 버전 DLL 검증 후 사용합니다. 아직 동작하지 않는 빈 확장 모드를 플레이어에게 제공하지 않습니다.

Core에서 `Pending → Gathering → Fighting → Completed/Cancelled`를 관리합니다. 같은 맵에서는 한 결투만 진행하며 한 참가자에게 중복 의무를 만들지 않습니다. 참가자를 참조로, 상태·규칙·위치·제거 방어구·종료 처리 여부를 Scribe로 저장합니다. 종료 상태를 먼저 기록해 재진입이나 중복 호출이 명예 보상을 복제하지 않도록 합니다. 최근 결과는 한 게임일 유지하고 정리하며, 미해결 의무는 유지합니다.

`HonorApi.Change(pawn, delta, reason)`는 사건 변화를 적용합니다. `HonorApi.Need`로 Need를 조회합니다. `HonorApi.DuelEnded`는 종료 알림이고 확장 구독자 예외는 분리해서 기록합니다. `HonorApi.SensitivityModifiers`에 곱셈 함수를 등록할 수 있습니다. Nobility는 이 지점에 작위별 민감도를 추가할 예정입니다. 게임 객체 접근과 API 호출은 게임 스레드에서 수행합니다.

`DuelRuleExtension`은 `PreceptDef`의 DefModExtension입니다. 신청자에게 있는 교리만 읽습니다. 다른 모드가 충돌하는 교리를 여러 개 추가한 경우 defName 순서로 결정합니다. 이 결정은 결투 신청 시 고정해 진행 도중 이념이 변해도 규칙이 바뀌지 않습니다.

관계 평판은 `Thought_MemorySocial`과 바닐라 관계·우호도 시스템만 이용합니다. 독립 평판 통화·원장은 없습니다. 장래 보상 협상은 패자의 실제 자원·세력 권한·승자의 합리적 상한을 검증한 뒤 한 번만 처리해야 합니다.

개발 순서: (1) 현재 Core 루프와 저장 재개 검증 → (2) 의도적인 규칙 위반 판별·협상 요구와 결과 UI → (3) Society → (4) Nobility → (5) Diplomacy → (6) Oaths. 각 단계는 별도 플레이 점검과 호환성 확인 뒤 배포 범위를 넓힙니다.
