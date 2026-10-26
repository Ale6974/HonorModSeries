# 1단계 예약 개발 인수인계 — 0.3.35 / 2026-10-26

honor-1은30분 반복/ACTIVE다. 중복 예약/동시 집행 없이 main·AGENTS·확정 결정·STATUS를 먼저 읽는다.1단계는 미완료다.

이번은0.3.34의 별도 개인 affiliation 호출이 ScopedNative 밖에 있던 문제를 수정했다. native callback이 같은 record 복구에 재진입해 아직 원 call이 끝나지 않은 상태를 확정할 수 있었으므로 모든 native source/affiliation/legacy combined와 return receipt를 공통 LeaderNativeCallScope로 묶었다. Ready는 active scope 동안 집행/복구를 거절한다. Affiliation은 재진입만 막고 후임 생성 no-redress/no-new-relations 권한을 얻지 않는다. finally는 임시 권한만 해제하고 exception/pending 원본·JSON·동의·기존 정상 반환/관측·의료1회 규칙은 그대로다.

## 다음 착수점: 전체 합병 실제 coordinator

0.3.32의 whole-faction 정치 제안/확인만 있고 합병 실행은 없다. 설치1.7의 PawnUtility.GetFactionLeaderFaction은 defeated faction도 leader pointer를 검사한다. FactionManager.Remove는 temporary만 지원하고 남은 pawn에 SetFaction(null)을 호출한다. SettlementDefeatUtility는 파괴·전투 보상/편지이므로 평화 합병에 사용하지 않는다. TryGenerateNewLeader는 native 내부에서 leader=null 뒤 새 후임을 생성한다. source 마지막 지도자를 평범하게 SetFaction하면 계속 새 source 후임이 생길 수 있다. 분석 원문은 ignored .local/merger-*.cs에만 있으며 Git/배포 금지다. 거대한 merger-Faction.il은 출력하지 않는다.

전체 정치 동의/별도 개인 동의·모든 실제 source 도시/world object/관련 pawn·장부/채무/제삼자 권리를 exact scope로 연결하고, source office 종료와 원본 주민/재고 이전·등록된 역사적 source 정체성·중단 복구를 함께 처리해야 한다. 임의 leader pointer/defeated/temporary 쓰기나 source를 own administration으로 속여 마지막 도시 가드를 면제하지 않는다. 검증된 별도 종료 경계가 없으면 해당 분기는 보류하고 다른 독립 작업을 진행한다. 아직 native vacancy hook/transpiler나 새 merger row/마지막 base 예외는 전혀 추가하지 않았다.

다른 큰 잔여: 실제 owed protection의 고의 거부 증거와 정당한 일방 독립, 미지원 주민/노예·로딩 맵/복잡 stock·지도자 수술/역할/작위, 귀족/집단 의결·복잡 이적, 상위/집행 중·재진입/중단/점령·재탈환 소실과 전체 후손/복구다. 개인 split succession이 끝난 source actor 경계는 지원하지만 affiliation Applying의 불확실 source actor를 다시 호출하지 않는다. Hegemony/WD2는 선택형 권위 adapter 없이 우회하지 않는다.

## 검사·게시·설치

1.6은 pinned1.6.4871/net472,1.7은 설치1.7.4985/net10.0이다. 코드·규칙·영한·패키지만 검사하고 게임/엔진은 시작하지 않는다. 양 전체 빌드 대상별 Contracts3886 + Honor/runtime20093 =23979개, 공용 EN/KO421키와 FactionDef12필드다. 패키지/설치·해시는 TESTING 최신 기록을 따른다. 검증 소스는 공개 main, 로컬 설치는 게임 없는 상태에서 백업 후 진행하고 활성화/언어/Steam은 유지한다. 질문은 가능한 독립 작업 뒤 모으고 없는 답변으로 동의를 꾸미지 않는다.

잔여 노력 추정:1단계35%(7~9묶음),2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6). 이번은 호출 안전성 수정이며 비율을 낮추지 않는다. 확정 명세의 실제 실행·저장/복구·영한 안내·양 타깃 기준을 충족할 때만 완료 보고/반복 종료한다.
