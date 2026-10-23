# Honor.Nobility — 후속 구현 경계

Royalty와 Core를 의존합니다. 작위별 명예 민감도, 대리 결투 요청·대리인의 자격·결과 귀속을 소유합니다. `HonorApi.SensitivityModifiers`를 통해 민감도를 연결할 수 있습니다. 현재 작위 API·대리 결투 코드는 구현하지 않았으며 빈 설치 모드를 제공하지 않습니다.

실제 1.7 Royalty 작위 tracker·title seniority를 검증한 뒤 계수를 결정합니다. 대리인은 참가자의 ID와 요청자의 명예 귀속을 구분하고, 원 신청자의 이념을 유지해야 합니다.
