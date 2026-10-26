# 1단계 예약 개발 인수인계 — 0.3.46 / 2026-10-26

최신 설치0.3.46: 기존0.3.45 백업 `.local/contracts-preview-41447ac65a1243b39a36615271ef1c88`,32파일/설정 해시 일치. 양 전체 빌드 경고/오류0, 대상별 규칙24289개·영한443키/FactionDef12필드 통과. 최종 ZIP hash는 TESTING 기록 참조. 다음 착수는 전체 합병/정당한 독립·집단 권한·미지원 실제 이전/상위 소실의 큰 잔여이며, 재협상으로 root ID가 바뀐 과거 이전 이력의 return adapter도 임의 우회하지 않는다. honor-1 ACTIVE30분 유지, 현재1단계 미완료다.

## 0.3.46 — 같은 당사자 도시 계약 재협상

플레이어가 종주인 NPC 도시의 활성 계약에 재협상 창을 연결했다. 현 일반 NPC 대표가 기존 계약의 합의 해약과 새 경제 조건에 동의해야 하며, 마지막 확인에서 현재 Game·대표·도시·장부·이전/공납 기록을 다시 검사한다. 원 계약의 실제 상호 해약과 새 계약의 양측 서명을 복사본에서 원자적으로 확정한다. 과거 조건/서명/채무 근거, 모든 하위 계약과 소유권 이력은 보존한다. 새 계약의 실물 공납은 기존 별도 활성화 절차를 따른다.

별도 128건/32M 문자 협상 archive와 새 계약의 Amendment 연결·최종 동의 audit를 저장하고 함께 검증한다. 변조·누락·중복·오래된 확인·부분 해약·예약된 실물 이전/지급은 거절한다. 기존 JSON의 null 필드는 생략한다. 플레이어가 봉신인 계약, NPC 내부 계약, 집단/귀족/제공자 대표의 동의, 소속 변경·주민/물품 이전·자동 NPC 공납은 이번 경로에 포함하지 않는다. 다른 root 계약으로 바뀐 뒤 과거 소유권 복귀 증거를 확장하는 작업도 남는다.

현재 집중은 1단계이며 미완료다. 잔여 노력 추정은 1단계35%(7~9묶음), 2단계80%(5~7), 3단계95%(6~8), 4단계90%(5~7), 5단계100%(6~8), 6단계70%(4~6)를 유지한다. 전체 합병·정당한 일방 독립·집단 의결/복잡 전시 이적·미지원 주민/노예/지도자/로딩 맵·나머지 상위/중첩 소실과 복구가 주요 기초 잔여다.


## 최신 인수인계 —0.3.45 /2026-10-26

최신0.3.45 로컬 설치 완료:0.3.44 백업은.local/contracts-preview-1abd8878c7ff4759ace0378860e61496,32파일/설정 해시 일치, 활성화·언어/Steam 불변.양 전체 빌드 완료와ZIP SHA256은 TESTING 최신 기록을 따른다.

actual OwnerAtLoss!=baseline인 경우에도 SAME root ActiveCharter의 소실 전 최신 Applied transfer를 저장/검증해 관찰→별도 후손 승계→새 root charter로 연결했다. Transfers optional init field는 full historical ledger, latest root/target/baseline·서명 후 준비·LastTick<=loss·NoPrepared/Destroyed를 확인한다. Native preflight copy/post/final review/commit에서 exact current ledger를 다시 읽는다. actual owner를 별도 전달하며 normalized obs는 baseline용이다; legacy API는 이 mode를 authorize하지 않는다. baselineEqual old mode는 null JSON/hash/ctor unchanged, hardcoded legacy proof fixture도 통과한다. 역사 identity 증거이므로 옛 pawn/stock 복구나 native replay는 없다.128 records/32Mchars+128*1024 invalidation reserve로 퇴출 없이 다음 ownership attempt를 보존한다.

Contracts4173+Honor/runtime20093=24266/대상, EN/KO434키·FactionDef12. 최신 install/package 결과는 TESTING을 따른다. 잔여35/80/95/90/100/70%, 큰 묶음7~9/5~7/6~8/5~7/6~8/4~6,1단계 미완료다. 다음은 whole merger/정당한 독립의 실제 권한·실행/복구, 집단 의결·미지원 주민/노예/로딩 맵·상위/실물 소실 등의 큰 잔여다. 다른 root 계약의 과거 transfer·provider/mixed/cascade/reentry는 임의 면제하지 않는다. 반복honor-1 ACTIVE30분은 유지한다.

## 최신 인수인계 —0.3.44 /2026-10-26

최신0.3.44 로컬 설치 완료:0.3.43 백업은.local/contracts-preview-6b36734ab5eb469b9b47a8c30805b329,32파일/설정 해시 일치, 활성화·언어/Steam 불변. ZIP SHA256과 양 전체 빌드 완료 근거는 TESTING 최신 기록을 따른다. 설치 대기는 해소됐다.

한 isolated hostile upper loss의 원래 standard NPC ownership return을 실제 normal-return 관찰→별도 save proof→fresh current NPC 대표/원 higher/전체 후손 consent→atomic 후손 재연결→기존 새 root charter offer/sign으로 연결했다. 원 root는 Lost 그대로이며 old terms/signatures/debt/loss tick/하위 역사는 불변이다. source.actualOwner==baseline만 지원, 원/native captor→source 전후 상호 적대와 exact registered refs/Game/book/archive를 확인한다. process ownership epoch가 중간 callback을 막고, 다음 소유권 ATTEMPT는 native 전에 미사용 receipt를 invalidate한다; 실패/불확실을 성공으로 꾸미지 않는다. 추가 child loss에서도 역사 pairing은 유지하되 authority를 넓히지 않는다. 새 원 slot/새 receipt proof는 paired restore; old null JSON/ctor는 유지한다. Hegemony/WD2·정규화 baseline/상위 중첩·재진입 등은 계속 보류한다.

이번 시작에0.3.43을 백업(.local/contracts-preview-badf75d9d24f4bb1a62dde03773fbb91) 후 설치해32파일/설정 해시가 일치했다. 최신0.3.44 설치/패키지 근거는 TESTING을 따른다. Contracts4158+Honor/runtime20093=24251/대상, EN/KO434키·FactionDef12. 잔여35/80/95/90/100/70%, 큰 묶음7~9/5~7/6~8/5~7/6~8/4~6,1단계 미완료다. 다음은 whole-faction merger/정당한 독립의 실제 권한/실행/복구·집단 의결·미지원 보존/상위/실물 소실 등의 큰 잔여를 우선한다. 반복honor-1 ACTIVE30분은 유지하고 단계 완료로 과장하지 않는다.

## 최신 인수인계 — 0.3.43 / 2026-10-26

0.3.43도 게임 실행 중이어서 로컬 설치는 보류했다. 다음 실행에서 게임이 없으면 최신 묶음을 백업 후 설치하고32파일/설정 해시를 확인한다. 게임을 종료시키지 않는다. ZIP SHA256과 빌드 완료 근거는 TESTING 최신 기록을 따른다.

부분 leaf-loss 정리 사이의 추가 관찰을 동일 physical ledger/고정 정치·war scope에 한해 연결했다. CurrentSource는 모든 pending의 최소 original position에서 시작해, 이후에 ack된 더 오래된 observation의 의존성이 있으면 실제 원본까지 낮춘다. 그 실제 origin에서 정확한 ack 순서로 book/head별 timeline을 구성하고, 모든 관련 observed original book을 저장된 원 head와 비교한다. A/B관찰→B정리→D관찰 후 옛 A/새 D를 어느 쪽부터 정리해도 partial reload와 원 intent 완료로 이어진다. 모두 정리한 뒤 새 기간은 옛 책을 재생하지 않는다. 추가 저장 필드/Native 호출 없음; 주민/보존/상위/다른physical·war/재진입/재탈환은 여전히 보류한다.

Contracts4128+Honor/runtime20093=24221/대상, EN/KO431키·FactionDef12. 설치 시 game process가 다시 감지돼0.3.42 설치도 쓰기 전에 보류했으며, 이번 최신 설치/패키지 결과는 TESTING을 따른다. 활성화/언어를 유지하고 게임을 닫지 않는다. 잔여35/80/95/90/100/70%, 큰 묶음7~9/5~7/6~8/5~7/6~8/4~6. 현재1단계 미완료, 다음은 합병/정당한 독립·집단 권한/복잡 이적·나머지 상위/실물 집행 소실·재탈환 등 실제 미완료를 우선한다. 반복honor-1 ACTIVE30분은 유지한다.

## 최신 인수인계 — 0.3.42 / 2026-10-26

RimWorld가 실행 중이어서0.3.41/0.3.42 설치는 대기 상태다. 다음 실행에서 게임이 없으면 최신0.3.42를 백업 설치하고 파일/설정 해시를 확인한다. 게임을 종료시키지 않는다.

한 applied 정치 intent의 동일 원본 book/기존 physical ledger/head에 속한 여러 빈 unloaded leaf의 normal-return 파괴를 native observer에서 함께 기록한다. 원 witnesses는 그대로 두고 각 명시적 정리에서 같은 epoch의 실제 resolved sibling closures만 복사본에 반영한다. AcknowledgedBefore는 실제 prior command head를 별도 보존하며 원 witness digest에서 제외하고 null은 생략한다. 동일 tick 정리 순서·부분 reload·최종 생존 도시 journal 이전과 original full intent 완료까지 연결했다. upper/진행 보존/다른 epoch·재진입·재탈환 지원으로 부르지 않는다. 복수 Prepared 물리 이전으로 착각해 기존 global one-Prepared guard를 제거하지 않는다.

양 규칙 Contracts4117+Honor/runtime20093=24210, EN/KO431키·FactionDef12. 패키지/설치 결과는 TESTING 최신 완료 기록을 따른다. 잔여 노력은35/80/95/90/100/70%, 큰 묶음7~9/5~7/6~8/5~7/6~8/4~6이다. 다음 우선순위는 전체 합병/정당한 독립의 실제 권한·실행·복구 경로, 집단 의결/복잡 이적 및 나머지 upper/mixed/reentrant loss·recapture다. 지원되지 않는 native 경계는 원본/가드를 지키며 별도 구현하고 모델만으로 완료 처리하지 않는다. honor-1 ACTIVE30분을 유지한다.

## 최신 인수인계 — 0.3.41 / 2026-10-26

독립 Prepared ActiveCharter 빈 상위 도시의 정상 파괴를 PendingTownLoss.UpperScope로 실제 관찰하고 명시적 정리/paired upper archive를 연결했다. 원본 소실/관찰과 나중 정리(ClosureTick)를 분리하며 pending ledgerDestroyed/Book rootLost/archive가 모두 맞아야 복원한다. Root incoming만 닫고 children은 보존; 원 recorded NPC realm과 원 higher·전체 descendant 실제 owner/provenance를 다시 확인한다. 그 다음 기존 TownSuccessionWindow(root)를 열고 현 대표 별도 승계로 연결한다. currentGame guard/legacy leaf semantics/null fields/positional API 유지. 아직 정치 전환 중 upper·혼합/재진입/중단/후속 재탈환이나 전체 합병/정당한 독립을 구현했다고 부르지 않는다.

양 타깃 규칙 Contracts4095+Honor/runtime20093=24188개, EN/KO430키·FactionDef12. ZIP 해시는 TESTING의 이번 완료 기록을 따른다. 실행 중인 RimWorld 때문에0.3.41 로컬 설치는 쓰기 전 보류했다. 다음 실행에서 게임이 없을 때 백업 설치하고32파일/설정 해시를 확인한다; 게임을 종료시키지 않는다. 남은 노력은35/80/95/90/100/70%, 큰 묶음7~9/5~7/6~8/5~7/6~8/4~6이다. 다음은 아래 큰 잔여 우선이며, 이미 완성한 isolated pending upper 경로를 다시 구현하거나 전체 upper 정치 경로로 과장하지 않는다. 반복honor-1은 ACTIVE30분 그대로다.

honor-1은30분 반복/ACTIVE다. 중복 예약/동시 집행 없이 main·AGENTS·확정 결정·STATUS를 먼저 읽는다.1단계는 미완료다.

이번은 영지 소실 승계의 실제 NPC 세력과 플레이어 정치적 authority를 분리했다. 기존 native review는 PlayerPolitics.TerritoryAuthority를 NPC 대표로 선택해, 계약은 체결됐지만 도시는 원 NPC realm에 아직 있는 정상 scope도 막혔다. 이제 archive.OwnerAtLoss로 exact registered actual realm을 찾고 기존 ordinary 현재 대표/권한 검사를 적용한다. 원 higher가 player면 현 PlayerIdentity/native player/PlayerGroup binding/OriginalFaction/book owner를 검증해 player PARTY 역할과 NPC 현 chief를 별도로 기록한다. NPC higher면 같은 실제 기록 realm이어야 한다. 원 parent는 archive 고정 범위에서만 가져오며 모든 surviving/deeper owner·완료 transfer provenance/원 book/archive를 fresh re-review한 뒤 기존 atomic commit으로 연결한다. Native 도시/주민/자원·정치/외교는 바꾸지 않는다. 새 역할은 새 receipt에만 있고 옛 서명/조건/채무·하위·loss receipts/save schema는 불변이다. 별도 realm임을 영한 UI에서 알린다.

0.3.39의 NPC reparent/promotion final authorization·현 Game 경계는 유지한다. ordinary 등록/unique current office·alive/awake/정신/구금/extra/quest/우호도50/상호 비적대·provider 절차를 확인하며 union/royal을 단일 chief 승인으로 대체하지 않는다. 범용 realm lookup과 subtree exit 정책은 그대로다. 검토/창/component-owned Game를 확인하며 옛 actor IDs-only plan은 자동 승인하지 않는다.

0.3.38의 pack animals stock format2는 그대로 유지한다. 같은 root/animal/소속/native inventory parent/holder·원 물품과 raw unloadEverything/itemsNotForSale/unpacked refs를 읽고 실제 city confirm/cancel/recovery에서 원 hash/format2를 비교한다. 원 물품 이동/Drop/재생성/refaction/이중 저장은 없고0/1 해시는 유지한다. 손상/중첩/직접 carried/장비·의복 재고는 보류한다.

0.3.37의 resident profile7 기본 Gene 보존은 유지한다. 원 Gene/pawn/등록 def/ID/순서·내생/이식·raw xenotype/name/icon/hybrid·passion/같은 tracker acyclic override를 검증한다. cachedGenes null은 native가 만들 수 있으나 nonnull 불일치는 수리하지 않는다. 옛 profile1~6/5인수 Compose API와 저장된 프로필 복구를 보존한다. 지도자 둘은 계속 profile5라 genetic leader 지원으로 부르지 않는다.

0.3.36의 미배정 월드 지도자 개인 합류는 UnassignedSource=true/SeparateSuccession=true로 그대로 유지한다. 원 계약 도시를 former residence로 꾸미지 않고 현재 world/보존 등록 명부를 모두 확인한다. 실제 후임/같은 actor 소속·의료/목적지 추가·원 명부 불변을 검증하며 unknown roster/새 배정·원본 변화는 보류한다. 기존 JSON bool false 생략/assigned-source semantics도 유지한다.

0.3.35의 모든 native source/affiliation/legacy combined와 return receipt를 묶는 LeaderNativeCallScope, Ready 재진입 거절, Affiliation의 후임 생성 권한 없음, finally 임시 권한만 해제·exception/pending 원본 보존은 유지한다. 기존 지원 profile5/빈 수술/원 세력2도시 이상·own administration/상호 동맹/비로딩·GC/redress·책임/채무 가드도 그대로다.

## 다음 착수점: 전체 합병 실제 coordinator

민간 소실 복구의 다음 독립 실행 묶음은 **standalone Prepared upper-town transfer의 정상 실제 파괴**다. 현 PendingTownLossArchive/observer는 leaf만 지원한다. positive native witness에 원 upper scope를 함께 묶고, book·Destroyed transfer·pending loss receipt·TownSuccessionArchive를 사본에서 함께 검증/교체한 뒤 이번 actual-NPC-realm 승계 경로로 이어가는 구현이 필요하다. 이미 있는 leaf API의 가드를 제거하거나 orphan upper를 archive 없이 닫지 않는다. 먼저 정치 조정/재진입 없는·동일 actual surviving realm·빈 비로딩 scope로 제한하더라도 실제 마지막 재연결까지 연결해야 하며, pending 정치/혼합 realm은 별도 adapter 없이는 그대로 보류한다. 이 상위 pending 경로는 아직 미구현이다.

0.3.32의 whole-faction 정치 제안/확인만 있고 합병 실행은 없다. 설치1.7의 PawnUtility.GetFactionLeaderFaction은 defeated faction도 leader pointer를 검사한다. FactionManager.Remove는 temporary만 지원하고 남은 pawn에 SetFaction(null)을 호출한다. SettlementDefeatUtility는 파괴·전투 보상/편지이므로 평화 합병에 사용하지 않는다. TryGenerateNewLeader는 native 내부에서 leader=null 뒤 새 후임을 생성한다. source 마지막 지도자를 평범하게 SetFaction하면 계속 새 source 후임이 생길 수 있다. 분석 원문은 ignored .local/merger-*.cs에만 있으며 Git/배포 금지다. 거대한 merger-Faction.il은 출력하지 않는다.

추가로 실제 FactionManagerTick은 defeated를 제외하지 않고 모든 registered faction의 FactionTick을 호출한다. Faction.ShouldHaveLeader는 IsPlayer/Hidden/temporary/humanlike만 검사하고 defeated를 제외하지 않아, native office를 null로 만든 뒤 defeated 표기만 하는 방법은 지속적 null leader 오류까지 남길 수 있다. 따라서 native clear 뒤 생성만 끊는 단일 hook도 완전한 종료 adapter가 아니다. registered historical source 정체성과 현재 office/GC·일반 native UI/AI·제삼자 권리를 함께 검증한 정치 종료 경계를 먼저 마련해야 한다. 이번에는 vacancy/office patch나 defeated/leader 쓰기를 전혀 추가하지 않았다.

전체 정치 동의/별도 개인 동의·모든 실제 source 도시/world object/관련 pawn·장부/채무/제삼자 권리를 exact scope로 연결하고, source office 종료와 원본 주민/재고 이전·등록된 역사적 source 정체성·중단 복구를 함께 처리해야 한다. 임의 leader pointer/defeated/temporary 쓰기나 source를 own administration으로 속여 마지막 도시 가드를 면제하지 않는다. 검증된 별도 종료 경계가 없으면 해당 분기는 보류하고 다른 독립 작업을 진행한다. 아직 native vacancy hook/transpiler나 새 merger row/마지막 base 예외는 전혀 추가하지 않았다.

개인 합류의 미배정 지도자 분기는 준비됐지만 합병에서 일반 개인 합류 API의 source2도시 가드를 면제해서 재사용하지 않는다. 별도 coordinator가 정치 전체 동의·개인 동의·모든 실제 자산을 묶고, source가 끝나는 경우의 native office 종료 경계를 별도로 검증해야 한다. 초기 구현 범위를 좁힐 경우에도 실제 source pawn/world object 전체를 확인하며 남겨 둔 주민/포로/제삼자 권리를 삭제·무시하지 않는다. 완료를 못 하는 native 중간 상태만 남기는 사용자 집행 버튼을 추가하지 않는다.

다른 큰 잔여: 실제 owed protection의 고의 거부 증거와 정당한 일방 독립, 미지원 주민/노예·로딩 맵/복잡 stock·지도자 수술/역할/작위, 귀족/집단 의결·복잡 이적, 상위/집행 중·재진입/중단/점령·재탈환 소실과 전체 후손/복구다. 개인 split succession이 끝난 source actor 경계는 지원하지만 affiliation Applying의 불확실 source actor를 다시 호출하지 않는다. Hegemony/WD2는 선택형 권위 adapter 없이 우회하지 않는다.

## 검사·게시·설치

1.6은 pinned1.6.4871/net472,1.7은 설치1.7.4985/net10.0이다. 코드·규칙·영한·패키지만 검사하고 게임/엔진은 시작하지 않는다. 양 전체 빌드 대상별 Contracts4095 + Honor/runtime20093 =24188개, 공용 EN/KO430키와 FactionDef12필드다. 패키지/설치·해시는 TESTING 최신 기록을 따른다. 검증 소스는 공개 main, 로컬 설치는 게임 없는 상태에서 백업 후 진행하고 활성화/언어/Steam은 유지한다. 질문은 가능한 독립 작업 뒤 모으고 없는 답변으로 동의를 꾸미지 않는다.

잔여 노력 추정:1단계35%(7~9묶음),2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6). 이번은 실제 소실 재연결의 지원 범위 확장이며 상위/진행 중·재진입/재탈환과 전체 합병 등 큰 묶음 완료로 세지 않는다. 확정 명세의 실제 실행·저장/복구·영한 안내·양 타깃 기준을 충족할 때만 완료 보고/반복 종료한다.
