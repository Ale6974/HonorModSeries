# 1단계 예약 개발 인수인계 — 0.3.37 / 2026-10-26

honor-1은30분 반복/ACTIVE다. 중복 예약/동시 집행 없이 main·AGENTS·확정 결정·STATUS를 먼저 읽는다.1단계는 미완료다.

이번은 새 도시 주민 실행/관측 기본값을 profile7로 올려 정확한 native 기본 Gene를 가진 주민/적격 포로의 실제 소속/host 변경과 의료·수술·진행 확인/도시 완료·명시 복구에 원 genetics 지문을 연결했다. ResidentGenes는 original Gene/pawn/등록 def/ID/목록 순서/내생·이식·raw private xenotype/name/icon/hybrid·passionPreAdd/같은 tracker의 acyclic override를 읽기만 한다. 원 Pawn tracker의 Deep/Reference saving에 맡기며 이중 저장/재생성/추가/제거/재발현은 없다. special/custom Gene subclass, unknown Harmony dispatch/getter, 손상·바뀐 원본을 보류한다. cachedGenes nonnull이면 실제 xeno→endo 참조 순서를 확인하고 관찰 중 getter로 새 캐시를 생성·수리하지 않는다. null-cache의 정상 native materialization은 허용한다. 옛 profile1~6 지문/저장된 profile 복구·5인수 Compose API를 보존했다. 일반/포로의 현재 cold recovery도 stored profile을 사용한다. 지도자 둘은 계속 고정 profile5이므로 genetic leader 지원으로 부르지 않는다.

0.3.36의 미배정 월드 지도자 개인 합류는 UnassignedSource=true/SeparateSuccession=true로 그대로 유지한다. 원 계약 도시를 former residence로 꾸미지 않고 현재 world/보존 등록 명부를 모두 확인한다. 실제 후임/같은 actor 소속·의료/목적지 추가·원 명부 불변을 검증하며 unknown roster/새 배정·원본 변화는 보류한다. 기존 JSON bool false 생략/assigned-source semantics도 유지한다.

0.3.35의 모든 native source/affiliation/legacy combined와 return receipt를 묶는 LeaderNativeCallScope, Ready 재진입 거절, Affiliation의 후임 생성 권한 없음, finally 임시 권한만 해제·exception/pending 원본 보존은 유지한다. 기존 지원 profile5/빈 수술/원 세력2도시 이상·own administration/상호 동맹/비로딩·GC/redress·책임/채무 가드도 그대로다.

## 다음 착수점: 전체 합병 실제 coordinator

0.3.32의 whole-faction 정치 제안/확인만 있고 합병 실행은 없다. 설치1.7의 PawnUtility.GetFactionLeaderFaction은 defeated faction도 leader pointer를 검사한다. FactionManager.Remove는 temporary만 지원하고 남은 pawn에 SetFaction(null)을 호출한다. SettlementDefeatUtility는 파괴·전투 보상/편지이므로 평화 합병에 사용하지 않는다. TryGenerateNewLeader는 native 내부에서 leader=null 뒤 새 후임을 생성한다. source 마지막 지도자를 평범하게 SetFaction하면 계속 새 source 후임이 생길 수 있다. 분석 원문은 ignored .local/merger-*.cs에만 있으며 Git/배포 금지다. 거대한 merger-Faction.il은 출력하지 않는다.

전체 정치 동의/별도 개인 동의·모든 실제 source 도시/world object/관련 pawn·장부/채무/제삼자 권리를 exact scope로 연결하고, source office 종료와 원본 주민/재고 이전·등록된 역사적 source 정체성·중단 복구를 함께 처리해야 한다. 임의 leader pointer/defeated/temporary 쓰기나 source를 own administration으로 속여 마지막 도시 가드를 면제하지 않는다. 검증된 별도 종료 경계가 없으면 해당 분기는 보류하고 다른 독립 작업을 진행한다. 아직 native vacancy hook/transpiler나 새 merger row/마지막 base 예외는 전혀 추가하지 않았다.

개인 합류의 미배정 지도자 분기는 준비됐지만 합병에서 일반 개인 합류 API의 source2도시 가드를 면제해서 재사용하지 않는다. 별도 coordinator가 정치 전체 동의·개인 동의·모든 실제 자산을 묶고, source가 끝나는 경우의 native office 종료 경계를 별도로 검증해야 한다. 초기 구현 범위를 좁힐 경우에도 실제 source pawn/world object 전체를 확인하며 남겨 둔 주민/포로/제삼자 권리를 삭제·무시하지 않는다. 완료를 못 하는 native 중간 상태만 남기는 사용자 집행 버튼을 추가하지 않는다.

다른 큰 잔여: 실제 owed protection의 고의 거부 증거와 정당한 일방 독립, 미지원 주민/노예·로딩 맵/복잡 stock·지도자 수술/역할/작위, 귀족/집단 의결·복잡 이적, 상위/집행 중·재진입/중단/점령·재탈환 소실과 전체 후손/복구다. 개인 split succession이 끝난 source actor 경계는 지원하지만 affiliation Applying의 불확실 source actor를 다시 호출하지 않는다. Hegemony/WD2는 선택형 권위 adapter 없이 우회하지 않는다.

## 검사·게시·설치

1.6은 pinned1.6.4871/net472,1.7은 설치1.7.4985/net10.0이다. 코드·규칙·영한·패키지만 검사하고 게임/엔진은 시작하지 않는다. 양 전체 빌드 대상별 Contracts3962 + Honor/runtime20093 =24055개, 공용 EN/KO423키와 FactionDef12필드다. 최종 native cache/getter/크기 가드 뒤 양 Preview를 재빌드했다. 패키지/설치·해시는 TESTING 최신 기록을 따른다. 검증 소스는 공개 main, 로컬 설치는 게임 없는 상태에서 백업 후 진행하고 활성화/언어/Steam은 유지한다. 질문은 가능한 독립 작업 뒤 모으고 없는 답변으로 동의를 꾸미지 않는다.

잔여 노력 추정:1단계35%(7~9묶음),2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6). 이번은 기본 유전자 주민 보존의 실제 지원 범위 확장이지만 전체 합병 등 큰 묶음 완료는 아니므로 비율을 낮추지 않는다. 확정 명세의 실제 실행·저장/복구·영한 안내·양 타깃 기준을 충족할 때만 완료 보고/반복 종료한다.
