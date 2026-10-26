# 1단계 예약 개발 인수인계 — 0.3.32 / 2026-10-26

honor-1은30분 반복/ACTIVE다. 새 예약/대화나 재설정 없이 main·AGENTS·확정 결정·STATUS를 읽고 실제 남은 통합을 이어간다. 예약 신호가 중간에 추가되어도 앞 작업을 중복 실행/빌드/설치하지 않는다.

이번에는 설치 DLL의 전체 합병/지도자 경계를 확인하고 별도 whole-faction POLITICAL PROPOSAL → 현재 일반 대표 NPC 응답 → 별도 플레이어 확인 → 저장 이력을 실제 도시 UI에 연결했다. 기존 도시 서명과 구분하며 실제 전체 source 도시의 기존 관리 계약, 모든 native world object 및 소속/관리·노예/추가 세력 관련 pawn ID, 원 book/transfer·양방향 실제 외교를 scope에 묶는다. 명시 모드 NPC 규칙 v1은 우호도75·현재 관리청과 상호 동맹·기존 대외 전쟁 일치다. 연합/귀족·집단 권위/known provider 및 보류/부분 해약/현재 touching offer는 제외한다. 미응답3일, 과거 NPC 응답/대표 불변, current-Game final player confirmation 시 전체 scope를 다시 읽는다. Pawn 참조를 pin하지 않고 bounded JSON raw를 보존하며 잘못된 proposal 자료는 비집행 제안 subsystem만 막는다.

**전체 합병 집행은 아직 미구현이다.** 이번 동의 준비를 합병 완료·주민/지도자 합류·승계·자산 보존/이전으로 부르지 않는다. 기존 마지막 도시 가드는 그대로이고 도시/주민/세력/외교/물품·채무·제삼자 권리는 바꾸지 않는다. NPC 지도자의 정치 대표 응답은 개인 합류 동의가 아니다.

## 다음 착수점

1단계의 실제 합병 coordinator/원본 보존/중단 복구를 계속하되, **지도자 개인 합류·존속 원 세력의 native 승계**가 선결 경계다. 설치 Pawn.SetFaction은 현재 지도자인지 확인해 Notify_LeaderLost → TryGenerateNewLeader를 실제 faction 변경 전 호출한다. TryGenerateNewLeader는 기존 map member 선택 또는 새 Pawn 생성 경로라, 예외/원 Pawn 아직 source·source leader 이미 교체/불확실 call을 분리해 기록해야 한다. 무작정 재시도하거나 원 leader를 raw-write로 복구하지 않는다. 기존 ResidentExecution의 일반 leader 제외를 제거하지 말고 별도 실제 개인 동의/저장 원본/현재 source·destination 및 GC retention/의료·수술/원 roster·stock proof와 단계별 복구를 묶는 전용 경로가 필요하다. 안전한 존속 source의 개인 합류부터 연결할 수 있다. 정치적 전체 합병 동의를 개인 동의로 전용하지 않는다.

일반 WorldObject.SetFaction은 객체 owner만 바꾸며 전체 faction membership/leader 처리를 하지 않는다. FactionManager의 해당 자동 제거는 temporary만 대상으로 하므로 source의 마지막 도시 이전을 세력 전체 해산이라고 해석하지 않는다. source가 끝나는 합병에서는 native 지도자 생성/이탈 처리와 모든 실제 pawn·world object/권리 보존을 추가로 확인해야 한다. 미확인 consent/권한·native 종료를 꾸미거나 새 지도자가 생겼다는 이유로 기존 ID를 삭제하지 않는다. 이번 확인한 game 본문은 ignored .local의 merger-*.cs에만 있고 배포하지 않는다.

나머지 큰 잔여는 정당한 일방 독립의 실제 보호 의무/고의 거부 근거, 미지원 주민/노예·로딩 맵/복잡 stock, 집단 전시 의결/복잡 이적, 상위/집행 중·재진입/중단/점령·재탈환 소실과 전체 후손/복구 통합이다. Hegemony/WD2는 선택형 권위 adapter 없이 우회하지 않는다. 질문은 가능한 독립 작업 뒤 모아 요청하며 없는 답변을 동의로 쓰지 않는다.

## 검사·설치·게시

1.6은 pinned1.6.4871/net472,1.7은 설치1.7.4985/net10.0이다. 게임을 시작하지 않고 코드·규칙·저장/복구·영한 안내·패키지를 검사한다. 로컬은 게임이 없을 때 백업 후 설치하고 활성화/언어는 유지한다. 검증 소스는 공개 main에 게시하며 게임/외부 DLL·로그/설정·도구/세이브는 제외한다. Steam은 기존 제한을 유지한다.

최종 규칙은 대상별 Contracts3801 + Honor/runtime20093 = 23894, EN/KO406개 키다. 양 전체 빌드와 마지막 회귀 뒤 양 Contracts 호스트·최신1.7 Preview를 확인했다. 패키지/설치 결과는 TESTING을 따른다.

잔여 노력 추정은1단계35%(7~9묶음),2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6)다. 이번은 합병 동의 준비의 부분 작업이라 추정은 유지한다. 확정 명세의 실행·저장/복구·영한 안내·양 타깃 검사를 모두 충족할 때만1단계 완료로 보고하고 반복을 종료한다.
