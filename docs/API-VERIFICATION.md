# 1.7 API·런타임 검증 기록

## 포로 관리 세력 변경과 네이티브 초기화 (0.3.16)

Version.txt의 1.7.4985 rev1269와 runtimeconfig net10.0 / 10.0.13-waylithclr.dev를 재확인했다. 설치 DLL의 Pawn_GuestTracker.SetGuestStatus(Faction, GuestStatus)를 조사했다. Prisoner 분기는 자신의 faction을 바꾸지 않고 host를 변경하지만, resistance/will을 새로 뽑고 의료 등급·수술 목록을 초기화한다. 기존 모드가 Enslave이면 상호작용 모드도 변경한다. Slave 분기는 Pawn.SetFaction을 호출하므로 같은 경로로 안전하다고 가정하지 않는다. 새 지원은 일반 포로만이며 Enslave 모드를 차단하고 native SetGuestStatus를 직접 호출한다. private hostFactionInt나 pawn faction의 원시 쓰기는 없다.

Pawn.DropAndForbidEverything은 destroyGearOnDrop을 먼저 처리하고, 그 밖의 unheld/unspawned world pawn은 장비/재고 드롭 전에 반환함을 확인했다. 장비 파괴/보유/부모 맵 상태는 사전 차단한다. Pawn_WorkSettings.EnableAndInitializeIfNotAlreadyInitialized는 priorities가 있으면 재초기화하지 않는다. 초기화되지 않은 작업 설정은 보류한다. GetPriority는 수동 우선순위 설정에 따라 표시 값을 3으로 정규화하므로 검증된 priorities DefMap과 읽기 전용 인덱서를 이용해 원래 값을 대조한다. GuestTracker의 25개 인스턴스 필드 목록과 Pawn/host 링크를 확인하며, host와 저항도/의지 외의 지원 필드를 별도로 요약한다. 추가/미지원 구조나 비어 있지 않은 복잡 모집 기록·이념 참조는 거절한다. 손님 상태 변경에 의한 침대 해제를 피하기 위해 할당 재산을 차단한다.

SetGuestStatus의 실제 반환 뒤 관찰한 저항도/의지 값 쌍만 저장하고, 다른 보존 증거 및 같은 목적 관리 세력이 일치할 때 원래 값을 복원한 뒤 권한을 지운다. 예외 발생으로 반환을 관측하지 못한 경우 초기화 값을 만들어내지 않는다. 준비/진행/검증/참조 해제는 기존 주민 집행과 같은 도시 저널에서 수행하며 GC 취소·임시 참조 보호를 유지한다.

1.6.4871 참조에서 GuestTracker 멤버와 네이티브 함수 시그니처를 확인하고 net472로 별도 컴파일한다. 이는 설치된 1.6 구현/Mono 또는 1.7의 실제 호출·알림·저장 복구 증거가 아니다. 디컴파일 자료는 로컬 전용이며 배포하지 않는다.

## 일반 이념 신도 관측과 보존 (0.3.15)

설치 Version.txt 1.7.4985 rev1269와 runtimeconfig net10.0 / 10.0.13-waylithclr.dev를 재확인했다. Ideo.Notify_MemberChangedFaction는 각 교리 알림을 호출하며, 기본 Precept 알림은 비어 있다. Precept_RoleSingle은 선택된 인물이면서 이전 세력이 플레이어일 때 역할 해제를 수행한다. 새 경로는 이전/목적 모두 NPC이며 역할 보유자는 별도 차단한다. 다른 알림 구현과 Harmony 패치가 있는 알림은 보존을 가정하지 않는다. FactionIdeosTracker.Notify_MemberGainedOrLost의 주 이념 재계산은 플레이어 세력 조건임을 확인했다.

Pawn_IdeoTracker.Certainty getter는 아기에게 certaintyInt=0을 쓸 수 있다. 관측은 타입을 검증한 private certaintyInt 필드의 읽기만 사용한다. 설치 본체와 1.6.4871 참조에서 필드와 PreviousIdeos 시그니처를 확인했다. 노화역행 요구는 Pawn.SetFaction의 ResetAgeReversalDemand 영향이 남으므로 지원에서 제외한다. 개인 이념/역할/확신도나 세력 주 이념을 쓰는 우회를 추가하지 않는다. 1.6은 참조/Framework 빌드이며 해당 게임 구현이나 실제 실행 증거가 아니다. 1.7도 엔진 실행·변환 이벤트·Scribe·Harmony 동작은 미검증이다.

## 원본 의료 지시의 집행 보관 (0.3.14)

설치 Version.txt 1.7.4985 rev1269와 runtimeconfig net10.0 / 10.0.13-waylithclr.dev를 재확인했다. 설치 DLL의 Bill_Medical/Bill/BillStack/ThingFilter/Scribe_BodyParts를 조사했다. BillStack.Clear는 목록을 비우며, AddBill은 unsaved billStack 링크와 목록을 연결한다. Bill_Medical의 원본 part, consumedMedicine, uniqueRequiredIngredients, iterationCompleted와 Bill의 loadID/recipe/필터/의사 제한 등을 확인했다. 단순 Bill.Clone을 보존으로 가정하지 않는다.

분리 시 원본 목록의 사본을 먼저 할당하고 원래 List를 비우며, 복원은 용량을 먼저 확보한 뒤 동일 원본과 검증된 billStack 링크를 원래 List에 되돌린다. 네이티브 Pawn.SetFaction은 그대로 호출한다. 소유권 이전은 동기 경로이며 중간에 엔진 저장이 실행된다는 가정을 하지 않는다. Detached 원본만 Native Deep 저장하고 attached 지시를 중복 저장하지 않는다. 설치 Scribe_BodyParts는 body Def/part index를 저장한다. 별도 저장 시점 참조 존재 증거와 원래 지시 정보를 대조해 디코딩 누락만 복원한다.

1.6.4871 참조의 동일 멤버를 사용하는 net472 빌드도 확인하지만 해당 게임의 메서드 몸체/실제 직렬화는 확인하지 않았다. 필터 범위는 구조체 전체를 빈 JSON 객체로 요약하지 않고 min/max를 명시하며 허용 Def와 특별 필터 상태도 대조한다. 건강/장비/관계 상태를 의료 지시 기록으로 임의 복원하지 않는다. 소스/규칙/빌드는 실제 엔진의 보존·저장/복구 증거가 아니며 디컴파일 자료는 로컬에만 둔다.

말단 영지 상실은 설치 WorldObject.Destroy()의 세계 목록 제거·destroyed 필드 설정·세력/컴포넌트/퀘스트 알림 경로와 Destroyed getter를 확인했다. Prefix는 실제 바인딩/소유권/장부를 검토하고 Postfix는 정상 반환 뒤 실제 파괴·제거·장부 불변을 재확인한다. 중단된 원본 호출·미완료 집행·소유권 불일치는 처리하지 않는다. 1.6은 동일 시그니처 컴파일만 확인했으며 이벤트 실행은 양 버전 모두 미검증이다.

## 제한된 주민 집행과 GC 참조 보호 (0.3.13)

설치 Version.txt는 1.7.4985 rev1269, runtimeconfig는 net10.0 / 10.0.13-waylithclr.dev임을 다시 확인했다. 설치 DLL의 Pawn.SetFaction, PawnComponentsUtility.AddAndRemoveDynamicComponents, Pawn_NeedsTracker, Pawn_PlayerSettings.ResetMedicalCare/Notify_FactionChanged, BillStack.Clear, Pawn_GuestTracker, Pawn_AgeTracker.ResetAgeReversalDemand, Faction.Notify_PawnJoined와 WorldPawnGC를 조사했다. 수술 지시/의료·허용 영역/정신·작업/욕구/지도자/이념 등의 변동을 무시하지 않는다. 첫 집행 경로는 수술 목록이 비고 역할/특수/이념/유전자/진행 작업 등이 없는 일반 NPC 주민만 허용하며 의료 등급만 명시적으로 복원한다.

설치 WorldPawnGC의 private string GetCriticalPawnReason(Pawn)와 public CancelGCPass()를 확인했다. 1.6.4871 참조 DLL에도 동일 시그니처가 있음을 확인했으나 extern 참조이므로 실제 구현을 확인한 것은 아니다. 중요 사유 postfix는 준비/진행 중 실제 집행 참조만 보존하고 기존 사유를 덮어쓰지 않는다. 진단 기준 기록과 공유 ForcefullyKeptPawns 집합에는 쓰지 않는다. 신규 주민 집행은 해당 메서드와 모드 패치 소유자 확인이 가능할 때만 허용한다. allowedAreas private 필드는 이름/타입과 빈 목록을 확인하는 읽기 전용 가드다.

관측 요약은 모든 임상/모드 상태의 전수 증거가 아니다. 기존 Pawn·건강/장비/사회 객체를 유지하는 네이티브 경로와 대조를 작성한 것이며, 코드 검사만으로 실제 주민 보존·GC/Harmony·Scribe 복구 성공을 주장하지 않는다. 디컴파일 자료와 엔진 DLL은 로컬에만 보관한다. 양 버전 실제 게임 실행은 계속 보류한다.

## 하위 전체 합의 해약과 실제 도시 이전 (0.3.12)

설치 Version.txt 1.7.4985 rev1269와 RimWorld.runtimeconfig.json의 net10.0 / 10.0.13-waylithclr.dev를 다시 읽었다. 설치 DLL의 Pawn.SetFaction, Pawn_RelationsTracker.Notify_ChangedFaction, Pawn_PlayerSettings.Notify_FactionChanged/ResetMedicalCare를 재조사했다. 의료/수술/허용 영역/정신·작업·동적 컴포넌트/욕구·지도자/이념 등의 처리가 있으므로 주민 전체 보존을 일부 필드 복사로 대신하지 않는다. 신규 주민 이전·강제 후임/전체 세력 합병 우회를 추가하지 않았다. 디컴파일 산출물은 로컬에만 둔다.

이번 실제 세계 연결은 기존 검증한 Settlement.SetFaction·Scribe·Faction/Settlement 참조·재고 보존 경로를 재사용한다. 상위 계약 해약과 내부 계약 유지, 전체 하위 도시의 개별 실제 소유권 확인을 조정한다. 영지 집행의 ReleasedRoot는 저장한 이탈 의도의 대상·정확한 계약/영지·원 정체성·허용 시각과 일치해야 한다. 전시 연합 수락과 전체 합병을 추정하지 않는다. 1.6은 참조 API/net472 컴파일이며 설치된 1.6 게임 구현 확인이 아니다. 양 버전 엔진 실행·UI·Scribe 복원·자원 보존은 미검증이다.

## NPC 실계약 계층·거래 동물·제한된 적대 전환 (0.3.11)

예약 재개 시 GitHub main과 로컬 6248b5d 일치를 확인했다. 설치 Version.txt 1.7.4985 rev1269, runtime net10.0 / 10.0.13-waylithclr.dev를 다시 읽었다. 기존 검증한 Faction/Settlement/Pawn/ThingOwner/Scribe 참조를 사용한다. 설치 Pawn 코드의 RaceProps.Animal과 carryTracker.CarriedThing 사용을 확인했다. NPC 초기 내부 계약은 실제 세력 대표와 현재 정착지·원 소속을 읽고 동일 LiveCharterLedger/CharterBook에 저장하며 NativeFaction 변경·주민/재고 쓰기 호출은 하지 않는다. NPC 하위 계약 교체는 복사본에서 양측 동의/한도 검사를 끝낸 뒤 장부·참조를 교체한다.

실제 소속 집행은 지원 거래 동물의 기존 Pawn·소속·관측 건강/장비/상태와 재고 형식을 전후 대조한다. Pawn.SetFaction·건강/훈련/장비 쓰기를 호출하지 않는다. 시민의 소속 변경이나 전체 동물 컴포넌트 상태의 보존 증거와 구분한다. 합의 해약한 이전 종주의 적대 이전은 실제 기존 계약의 양측 해약과 이전 정치 명령에서만 출발 세력을 유도하고, 현재 플레이어 후손과 대상 세력·진행 의도에 한정해 관측 자격을 허용한다. 일반 정치/선전포고·관계 잠금 우회나 임의 소유권 정당화 API가 아니다.

1.6은 동일 코드의 참조/net472 빌드이며 설치 1.6 게임 구현 검증이 아니다. 1.7도 이번 변경을 게임에서 실행하지 않았다. 실제 NPC 대표·지도/거래·적대 전환·Harmony·Scribe 저장 복구/초기 배치·원 세력 멸망은 런타임 검증 대상이다. 의료/정신/작업을 초기화하는 Pawn.SetFaction을 주민 보존 경로로 조용히 사용하거나 raw faction 필드를 쓰지 않는다. 1단계 완료로 판정하지 않는다.

## 1.6/1.7 호환성 빌드 (0.2.2 / Contracts 0.3.10, 2026-10-25)

설치 게임은 계속 1.7.4985 rev1269, net10.0 / 10.0.13-waylithclr.dev다. 1.6 게임을 설치하거나 Steam 분기를 변경하지 않았다. [Ludeon 허가를 받아 배포하는 RimRef](https://github.com/krafs/RimRef)의 고정 Krafs.Rimworld.Ref 1.6.4871 서명 참조를 사용해 6개 모듈을 net472로 컴파일했다. 1.6 게임 구현/Mono 실행을 확인한 것은 아니다. 설치 Harmony 1.6 DLL은 2.4.3.0+b79f2f1ac971e5bc224cda3efedbbb4084efbfb6, 1.7 DLL은 3.0.0-rc.3이다. 1.6 API 바인딩은 서명·타입 검사이고 Harmony 설치/역패치·퀘스트/맵/저장 동작의 성공 증거가 아니다.

순수 규칙과 게임 어셈블리를 버전별로 분리하고, 공용 null/enum/clamp/finite/SHA helper 및 빌드용 PolySharp로 새 BCL/컴파일러 의존성을 제거했다. net472 규칙은 Windows .NET Framework에서, net10 규칙은 .NET 10에서 실행했다. Contracts 1.6은 System.Text.Json 10.0.13과 필요한 Microsoft 런타임 DLL 9개를 자체 1.6 폴더에 포함하며 1.7에서는 이를 로드하지 않는다. Native Mono와 타 모드 DLL 충돌은 미검증이다. GameComponent/Scribe/Harmony/정착지 등 1.7 구현 조사 결과를 1.6 구현 검증으로 간주하지 않는다. [정확한 범위와 재현 명령](VERSION-SUPPORT.md).

## 주민 기준 기록 관측·저장 (0.3.9)

Version.txt 1.7.4985 rev1269, net10.0 / 10.0.13-waylithclr.dev와 Harmony 3.0.0.0-rc.3+3c0e32cd3fd20e4ae27c47cb8c888844766f9f1a를 재확인했다. 설치 DLL의 Verse.Hediff, Verse.Pawn_EquipmentTracker, RimWorld.Pawn_ApparelTracker, Verse.Pawn_InventoryTracker를 조사하고 GetUniqueLoadID/Severity/Part, AllEquipmentListForReading, WornApparel, inventory.innerContainer 참조를 확인했다. 기존 Pawn 생존/위치/포로/지도자/mental/job getter와 health.hediffSet.hediffs를 읽어 버전이 고정된 관측 요약을 만든다. 물품 생성·이동·Pawn.SetFaction·기존 장부 수정은 호출하지 않는다.

별도 ResidentBaselineStore는 Scribe_Values에 자체 JSON 1개를 저장한다. 원본 Scribe/게임 저장 재로딩·Pawn/장비/건강 변동의 런타임 관측·JSON/SHA256의 Waylith 실행·UI 렌더링은 검증하지 않았다. 기존 주민에 Scribe 보존 참조를 추가하거나 private faction 필드를 쓰지 않으며, 모든 상태를 복원하는 API가 있다고 가정하지 않는다. 본 기준 기록은 관측·진단 전용이며 주민 이전 승인/완료/복구 증거로 쓰지 않는다.

## 저장 주민의 세력 변경 부작용과 읽기 전용 점검 (0.3.8)

설치 Version.txt 1.7.4985 rev1269, runtimeconfig 10.0.13-waylithclr.dev/net10.0, Harmony 3.0.0.0-rc.3+3c0e32cd3fd20e4ae27c47cb8c888844766f9f1a를 재확인했다. Assembly-CSharp의 Verse.Pawn을 다시 조사했다. SetFaction(Faction,Pawn)는 guest.SetGuestStatus(null), 지도 등록/예약/Lord 변경, 지도자 상실 처리, health.surgeryBills.Clear, playerSettings.ResetMedicalCare, ClearMind_NewTemp, needs/relations/Ideo 알림 등을 호출한다. NPC 비플레이어 간 변경도 주민의 모든 상태를 그대로 보존하는 경로라고 가정할 수 없다. 이번에는 이 메서드나 private faction 필드를 쓰는 우회를 추가하지 않는다.

Settlement.ExposeData는 previouslyGeneratedInhabitants를 참조로 저장하며 Notify_MyMapRemoved는 destroyed/non-world-pawn 참조를 제거한다. 이 목록 자체가 현재 주민의 소속 이전 권한을 증명하지는 않는다. 점검은 기존 Settlement/Pawn 참조, Faction, Spawned, holdingOwner, IsWorldPawn, IsPrisoner/IsSlave, PawnUtility.IsFactionLeader와 현재 재고 보관자를 읽는다. 실제 주민 재등장·다른 곳의 참조·정신/예약 상태·UI 렌더링·저장/복구는 실행 검증하지 않았다.

## 방문 도시의 일반 물품 보존 (0.3.7, 코드/빌드만)

2026-10-25에 설치 Version.txt 1.7.4985 rev1269, net10.0 / 10.0.13-waylithclr.dev, Harmony 3.0.0.0-rc.3+3c0e32cd3fd20e4ae27c47cb8c888844766f9f1a를 재확인하고 Assembly-CSharp의 Settlement와 Settlement_TraderTracker를 다시 조사했다. GetDirectlyHeldThings()는 stock 필드를 그대로 반환하며 재생성하지 않는다. StockListForReading은 null/빈 재고에서 RegenerateStock을 호출하므로 이번 검사에서 사용하지 않는다. EverVisited는 everGeneratedStock, NextRestockTick은 보관자/생성 이력/마지막 생성 시각에 따른 조회다. TraderKind는 현재 settlement.Faction.def.baseTraderKinds에서 선택하므로 별도 캐시 초기화를 추측해 추가하지 않는다.

SetFaction의 주민/재고 이관 API를 가정하지 않는다. 미로딩/저장 주민 부재/일반 물품만 허용하고 현재 재고의 ID·종류·재료·수량·내구도·품질과 재입고 정보를 요약해 실제 소유권과 함께 검사한다. 거래 재고의 Pawn·Corpse·MinifiedThing·IThingHolder 및 보관자 컴포넌트는 제외하며 Pawn.SetFaction은 호출하지 않는다. Settlement.Material은 private cachedMat을 재사용한다. 확인한 이름/타입이 일치할 때 Harmony AccessTools로 FieldInfo를 얻어 이전 후 null로 비우고 풀 material은 파괴하지 않는다. 타입/멤버가 달라지면 이전을 거절한다. Reflection 필드 쓰기·물품 보존·Harmony 패치와의 상호작용·실제 Scribe 저장/로드·게임 런타임 SHA256 실행은 검증하지 않았다.

## 소속·봉신 통합 전환 (0.3.6, 코드/빌드만)

동일 설치 Version.txt의 1.7.4985 rev1269와 runtimeconfig net10.0 / 10.0.13-waylithclr.dev를 재확인했다. 새로운 게임 멤버 시그니처를 추측해 추가하지 않고 기존 검증한 GameComponent/Scribe_Values, Faction 대표/관계/알림, Settlement.SetFaction 참조를 재사용한다. 순수 조정 장부는 System.Text.Json과 .NET SHA256으로 현재 명령 이력/영지 장부 일치를 검증한다. 실제 Waylith 게임 프로세스에서 이 JSON/해시 경로를 실행한 것은 아니다.

플레이어 native faction 변경 없이 먼저 실제 소속 명령을 확인하고, 공유 외교 및 도시별 이전을 확인한다. 준비/소속 적용/완료를 각각 기록하고 동일한 명령 앞부분·원 해약·새 계약·대상 집합을 재검사한다. 새 대외 관계가 봉신 원 세력을 적대로 만드는 경우는 전시 이전 API가 있다고 가정하지 않고 거절한다. 실제 Harmony 재진입·캐시·퀘스트·엔진 저장 복구·권위자 소실 처리는 실행 검증하지 않았다.

## 독립 플레이어 관리청과 일괄 이전 (0.3.5, 코드/빌드만)

1.7.4985 rev1269, net10.0 / 10.0.13-waylithclr.dev 및 Harmony 3.0.0.0-rc.3을 재확인했다. 설치 FactionManager를 다시 조사했으며 FactionManagerTick은 GoodwillManagerTick·정착지 근접 우호도 검사·FactionTick을 호출한다. 일반 NPC 세력 조회는 비플레이어/숨김/멸망 등의 조건으로 이루어져 새 관리청이 모든 퀘스트에서 자동 제외된다고 가정하지 않는다. canGenerateQuestSites=false가 모든 퀘스트 차단 API라는 주장도 하지 않는다.

기존 검증한 FactionGeneratorParms hidden override, FactionManager.Add, Scribe_References/Values, Faction.CanChangeGoodwillFor/관계 객체/알림을 사용한다. 영지 관리청의 외교 원본은 플레이어이며 외국 종주의 외교 복사 대상에서 제외한다. 관리청 사건을 플레이어 쌍으로 재전달할 때 원래 NPC 쌍의 변경 허용과 재진입 guard를 검사한다. 내부 플레이어/관리청 사건은 외국 종주로 우회하지 않는다. 실제 Harmony 설치 순서·게임 내 재진입·근접/자연 우호도·관리청 무영지 상태·주민/퀘스트 생성·저장 복원은 실행 검증하지 않았다.

## 계약에 따른 NPC 영지 소속 집행 (0.3.4, 코드/빌드만)

Version.txt 1.7.4985 rev1269, runtimeconfig net10.0 / 10.0.13-waylithclr.dev 및 Harmony 3.0.0.0-rc.3을 재확인했다. 설치 DLL을 다시 조회한 WorldObject.SetFaction(Faction)은 def.canHaveFaction을 검사하고 factionInt를 설정하는 virtual 메서드다. Settlement에는 이를 도시 주민·재고 이전까지 수행하는 형태로 재정의한 메서드가 없으며, 플레이어 기지/맵 처리는 native faction에 의존한다. 따라서 대상과 목적지 모두 비플레이어로 한정하고 HasMap/EverVisited/previouslyGeneratedInhabitants 및 실제 세력 자격을 검사한다.

실행 전 준비 기록을 남기고 SetFaction 이후 실제 도시 참조와 Faction.GetUniqueLoadID, 미로딩/미방문 상태를 확인한다. 계약의 최초 정체성을 임의로 새 세력으로 수정하지 않고, 완료된 소유권 실행의 현재 일치만 별도 관측 어댑터에서 인정한다. NPC 계층/점령 권한이나 맵·재고 이관 API가 있다고 가정하지 않는다. 실제 맵·거래 재고·생성 주민·world object 캐시·세력 멸망/퀘스트·Scribe 복구 경로는 실행 검증하지 않았다.

## 플레이어 공유 외교 (0.3.3, 코드/빌드만)

설치 Version.txt의 1.7.4985 rev1269와 RimWorld.runtimeconfig.json의 net10.0 / 10.0.13-waylithclr.dev를 재확인했다. 설치 DLL의 Faction.RelationWith(Faction,bool)는 세력별 mutable FactionRelation을 반환한다. 관계 객체를 대표 세력 것으로 바꿔 돌려주면 양방향 변경과 바닐라 알림이 잘못 처리될 수 있으므로 getter를 패치하지 않는다. 실제 플레이어·상대의 별도 relation 값에 baseGoodwill/kind를 함께 반영하고 Notify_RelationKindChanged(Faction,FactionRelationKind,bool,string,GlobalTargetInfo,out bool)를 호출한다.

TryAffectGoodwillWith(Faction,int,bool,bool,HistoryEventDef,GlobalTargetInfo?)와 CanChangeGoodwillFor, CalculateAdjustedGoodwillChange를 조사하고 해당 오버로드에 컴파일했다. 플레이어 관련 원래 변경 금지 조건과 자연 우호도 보정을 유지한 뒤 대표 세력 쌍으로 전달한다. NPC끼리 변경은 바닐라의 플레이어 HistoryEvent를 생성하지 않으므로 원래 플레이어 사건의 실제 변화분을 별도로 기록한다. RelationKind는 단순 goodwill 부호가 아니라 바닐라 hysteresis 결과이므로 원본 kind를 유지한다.

GoodwillWith의 플레이어 상한 적용, GoodwillSituationManager.GetMaxGoodwill, QuestUtility.IsGoodwillLockedByQuest를 확인했다. 원본과 충돌하면 공유를 중단하며 상한/잠금을 우회하지 않는다. SetRelationDirect와 Notify_GoodwillSituationsChanged 후에도 재검사하고 250틱 주기로 확인한다. 실제 패치 설치·사건 재진입·퀘스트 진행·조약 호환성·게임 저장 재로딩은 실행하지 않았다. 설치 DLL 조사와 빌드 결과를 엔진 성공 증거로 취급하지 않는다.

## 1단계 NPC 자유도시 초기 배치 (2026-10-24, 코드/빌드만)

Version.txt 1.7.4985 rev1269 및 기존 .NET10/Waylith 구성을 재확인했다. 설치 DLL의 FactionGenerator.NewGeneratedFaction(FactionGeneratorParms)는 hidden override가 false인 경우 무작위 정착지를 생성해 월드에 추가한다. 따라서 이번 연합 생성은 `new FactionGeneratorParms(def, hidden: true)`로 정착지 생성을 억제하고 `faction.hidden = false` 후 FactionManager.Add로 등록한다. 초기 관계 생성은 FactionDef의 적대 규칙을 사용하고, hidden 상태 자체를 수치 판단에 사용하지 않는 것을 조사했다. 실제 생성 동작을 실행 검증하지는 않았다.

WorldObject.SetFaction은 이 설치본에서 factionInt를 설정하는 메서드이며 정착지의 trader/맵/기존 주민을 일괄 이주하는 API가 아니다. Settlement의 player 판정·map generator·방문 가능 여부는 Faction.OfPlayer에 의존한다. 따라서 플레이어나 로딩/방문/주민 생성된 도시는 대상으로 삼지 않고, 기존 계약 참조도 우회하지 않는다. `Settlement.EverVisited`는 trader.EverVisited이며 실제 재고 생성 이력을 반영한다. 원 세력 정착지 수는 각 변경 직전에 현재 WorldObjects에서 다시 계산한다.

FactionDef 필드와 기본 OutlanderFactionBase 상속 구조를 설치 XML에서 확인했다. 자동 생성 수·가중치를 0으로 두고 raidsForbidden/canRequestMilitaryAid/canGenerateQuestSites 제한을 명시했다. 이 Def의 실제 XML inheritance 해석, 새/기존 저장 초기화, 세력 생성/이름·관계·캐시·저장 재로딩과 언어 전환은 실행 검증하지 않았다. 소스 및 빌드 결과는 해당 엔진 증거를 대신하지 않는다.

## 실물 공납 직접 전달 (2026-10-24, 코드/빌드만)

동일 설치 1.7.4985 rev1269와 net10.0 / 10.0.13-waylithclr.dev를 재확인했다. 설치 DLL에서 Tradeable.InitPriceDataIfNeeded의 입력과 TradeUtility.GetPricePlayerSell(Thing,float,float,float,float,float,float,TradeCurrency), GetPricePlayerBuy(Thing,float,float,float,float), PlayerSellableNow를 조사했다. 통화는 시장가를 쓰고 일반 물품은 가격형·협상가·정착지·약물/축산물·난이도 보정 및 판매/구매 가격 상한을 반영한다. 전역 TradeSession은 변경하지 않는다. 표기/반올림 차이까지 실제 거래 화면과 대조한 결과는 없다.

Settlement_TraderTracker는 IThingHolder이고 GetDirectlyHeldThings가 실제 거래 재고 ThingOwner를 반환한다. 기본 GiveSoldThingToTrader는 거절된 물품을 Destroy할 수 있어 공납 경로에서는 이를 호출하지 않는다. ThingOwner.TryTransferToContainer(Thing,ThingOwner,int,out Thing,bool)의 반환 수량과 실제 목적지 보관자·스택 수량을 확인하며 병합을 비활성화한다. 임시 보관은 IThingHolder + Scribe_Deep, 실제 참조는 Scribe_References로 작성했다. NPC 기존 거래 재고의 생성/갱신은 바닐라 StockListForReading 경로다. NPC 공납 자원을 새로 만들어 지급하지 않는다.

컴파일은 실제 상단 도착·가격 대조·물품 보존·예외/모드 훅·엔진 Scribe 저장 복구의 증거가 아니다. 이 검증은 사용자 지시에 따라 아직 수행하지 않았다.

## 실제 영지 장부 연결 (2026-10-24, 코드 검증만)

Version.txt 1.7.4985 rev1269와 runtimeconfig의 net10.0 / 10.0.13-waylithclr.dev, 설치 Harmony 3 RC를 재확인했다. 설치 DLL 기준으로 Settlement/WorldObject의 GetUniqueLoadID, Destroyed, Faction, Faction의 GetUniqueLoadID/leader/HasGoodwill/defeated/HostileTo/GoodwillWith, FactionManager.AllFactionsListForReading, Scribe_References/Scribe_Collections와 GameComponent.ExposeData에 바인딩하고 Preview를 컴파일했다. WorldObject.GetUniqueLoadID는 WorldObject_ + ID를 반환하므로 번역 이름이나 현재 타일을 저장 키로 사용하지 않는다.

실제 세력 단위 종주와 단일 NPC 정착지 봉신 참조를 구분했다. Native player faction은 SetFaction 호출 없이 보존한다. 관측/소유권 재검사와 별도 동의 장부는 실제 자원 거래·Faction 외교 변경·점령·보호 임무 성공을 뜻하지 않는다. 이번에도 게임 실행과 엔진 Scribe 왕복 검증은 수행하지 않았다.

## Contracts 개발 미리보기 바인딩 (2026-10-24, 코드 검증만)

설치 Version.txt 1.7.4985 rev1269 및 RimWorld.runtimeconfig.json의 net10.0 / 10.0.13-waylithclr.dev를 재확인했다. Harmony 제품 버전은 3.0.0.0-rc.3. GameComponent의 GameComponentTick/ExposeData를 설치 DLL에서 재조회하고, 기존 검증한 Settlement.GetGizmos 패턴과 Window/Listing_Standard/Widgets/Scribe_Values API를 사용해 별도 Preview를 컴파일했다. 실제 게임 실행·Scribe 게임 저장 복구·창 표시·Harmony 패치 성공 증거는 아니다.

참조 DLL SHA-256(파일 자체는 저장소에 포함하지 않음):

| DLL | SHA-256 |
|---|---|
| Assembly-CSharp.dll | 0BE7A7EA4B3111D4430F45B36C234A5E43287880B0514B23915F2EF9A0629E16 |
| UnityStubs.dll | EA962C9274514E0633D4E4C5471A1A98AFF5FC52AF20CA2E506C16DCD8BE6F81 |
| Waylith.dll | C69C2A2828C5594B58C9B28F0D19D5B25FF5CB16CEB46DC64F34DD59B7AFFC26 |
| 0Harmony.dll | C8EF7856B2BE3CF24B345C9B5C2F197F68FCF92649635064FCE7B3CCC8F77CB0 |

새 정치 모델은 게임 Faction을 변경하지 않고, 모델의 공격 결과를 실제 점령 증거로 취급하지 않는다. 상세 범위는 CONTRACTS-PREVIEW.md를 참조한다. 최신 사용자 지시에 따라 게임 실행 검증은 보류했다.

확인일: 2026-10-24 (Asia/Seoul). 추측한 1.6 API를 1.7 사실로 취급하지 않습니다.

## 1차 출처

- [Ludeon: Announcing the Waylith Engine and Update 1.7, 2026-10-12](https://ludeon.com/blog/2026/10/announcing-the-waylith-engine-and-update-1-7/): Waylith 이전과 unstable 배포를 확인했습니다.
- [Ludeon 제공 Modder Primer for 1.7](https://docs.google.com/document/d/e/2PACX-1vSykw5_DYaaw42nEznI9zmBZVT_Vgg2bxYq_v2XWvAsHC3QSKFuDK2OyiQ5WC9jvVRUcrjpFt7xLLF5/pub): .NET 10, Assembly-CSharp·Waylith·UnityStubs 참조, Unity 호환층 유지, Headless와 mod 실행 옵션을 확인했습니다. 개발 중 갱신되는 문서입니다.
- [Harmony 제작자 릴리스](https://github.com/pardeike/Harmony/releases/tag/v3.0.0-rc.3): 설치 파일의 제품 버전과 일치하는 릴리스입니다. 오래된 rc2를 임의로 고정하지 않았습니다.

## 설치 파일

게임: `F:/SteamLibrary/steamapps/common/RimWorld`.

`Version.txt`: `1.7.4985 rev1269`. Steam 설치 메타데이터: unstable, buildid 26115268.

`RimWorld.runtimeconfig.json`: `tfm = net10.0`, 동봉 런타임 `10.0.13-waylithclr.dev`. Unity 어셈블리 리디렉션 등 WaylithCLR 설정이 실제 존재합니다. 빌드에는 표준 .NET SDK 10.0.402를 사용하고 게임 실행에는 게임 동봉 런타임을 사용했습니다.

설치 Harmony: workshop `294100/2009463077/1.7/Assemblies/0Harmony.dll`. 어셈블리 버전 3.0.0.0, 제품 버전 `3.0.0.0-rc.3+3c0e32cd3fd20e4ae27c47cb8c888844766f9f1a`. Workshop About의 모드 버전 3.0.1.0과 DLL 어셈블리 버전은 서로 다른 메타데이터입니다.

| 파일 | SHA-256 |
|---|---|
| Assembly-CSharp.dll | 0BE7A7EA4B3111D4430F45B36C234A5E43287880B0514B23915F2EF9A0629E16 |
| UnityStubs.dll | EA962C9274514E0633D4E4C5471A1A98AFF5FC52AF20CA2E506C16DCD8BE6F81 |
| Waylith.dll | C69C2A2828C5594B58C9B28F0D19D5B25FF5CB16CEB46DC64F34DD59B7AFFC26 |
| 0Harmony.dll | C8EF7856B2BE3CF24B345C9B5C2F197F68FCF92649635064FCE7B3CCC8F77CB0 |

## 실제 DLL에서 확인하고 사용한 연결점

- `RimWorld.Pawn_InteractionsTracker.TryInteractWith(Pawn recipient, InteractionDef intDef) : bool`. 성공한 모욕만 Postfix에서 등록합니다. `InteractionWorker_Insult`에는 별도 `Interacted` 오버라이드가 없어 그 메서드를 추정해서 패치하지 않았습니다.
- `CheckSocialFightStart(InteractionDef interaction, Pawn initiator) : bool`. 해당 모욕이 Core 참가 자격에 맞을 때만 기존 소셜 싸움을 억제합니다. Tracker의 실제 private `pawn` 필드와 bool 결과 타입을 사용합니다.
- `RimWorld.Need` 생성자·`NeedInterval()`·`ExposeData()`, `NeedDef` 및 기본 Need 업데이트 간격 150틱에 맞춥니다. 단계는 정규화된 Need 값에서 계산하므로 사건 하락과 저장값 사이 별도 단계 불일치가 없습니다.
- `Pawn.PreApplyDamage(ref DamageInfo, out bool)`와 `Pawn.PostApplyDamage(DamageInfo, float)`를 사용합니다. 반환형·결과·주입 인수는 정확한 실제 타입으로 컴파일하며 Harmony 적용 성공은 게임 로그에서 확인합니다.
- `Verse.AI.JobDriver`, `ToilMaker`, `Toils_Goto.GotoCell`, `Pawn.meleeVerbs.TryMeleeAttack`, `Verb.TryStartCastOn`을 사용합니다. 바닐라 `JobDriver_AttackMelee`의 실제 호출 경로와 비교했습니다.
- `ThingDef.Verbs`는 public 속성이며 소문자 `verbs`는 private입니다. 컴파일러 검증에 따라 public 속성으로 수정했습니다.
- `Pawn_ApparelTracker.TryDrop` / `Wear`, `TendUtility.DoTend(Pawn, Pawn, Medicine)`를 실제 서명으로 사용합니다.
- `GenCommandLine`, `GenFilePaths`에서 `-savedatafolder=...`, `QuickStarter`에서 `-quicktest`를 확인했습니다. 테스트는 별도 저장 폴더와 명시적 `-mod` 목록으로 실행합니다.
- 무기·의복 Def명과 Armor 태그는 설치 게임 XML에서 확인합니다. Ideology 전용 교리는 `MayRequire`로 분리하고 Core는 DLC가 없어도 교리 없는 기술 fallback을 사용하도록 작성했습니다. DLC 없는 실행 자체는 별도 테스트 대상입니다.

게임 DLL·역컴파일된 게임 소스는 이 저장소나 배포 파일에 포함하지 않습니다. 향후 unstable 업데이트 후 위 해시가 달라지면 빌드·패치·실플레이 검사를 다시 수행해야 합니다.

## 0.2.0 부속 모드 API 확인 (2026-10-24)

같은 설치 Assembly-CSharp.dll의 실제 서명과 XML을 기준으로 컴파일·엔진 검사했습니다.

- Pawn_RoyaltyTracker.MostSeniorTitle.def.seniority / SetTitle(Faction,RoyalTitleDef,bool,bool,bool): 귀족 배율 및 실제 작위 테스트.
- Pawn_RelationsTracker.FamilyByBlood / AddDirectRelation: 혈연 명예. Thought_MemorySocial: 목격·소문·숙적 의견.
- QuestNode.RunInt() / TestRunInt(Slate), QuestUtility.GenerateQuestAndMakeAvailable(QuestScriptDef,Slate), Quest.AddPart, Quest.End: 추정한 구버전 QuestNode 서명을 쓰지 않습니다.
- MapParent.PostMapGenerate, protected TickInterval(int), ShouldRemoveMapNow(out bool), CanBeSettled, 기본 CaravanArrivalAction_Enter: 대표 장소·만료·기본 상단 진입.
- TileFinder.TryFindNewSiteTile(out PlanetTile,PlanetTile,int,int,...), Verse.GetOrGenerateMapUtility.GetOrGenerateMap(PlanetTile,IntVec3,WorldObjectDef,...): PlanetTile와 실제 위치 생성.
- Faction.Notify_MemberTookDamage(Pawn,DamageInfo)와 TryAffectGoodwillWith(Faction,int,...): 합의된 대표 타격에만 기본 외교 피해 페널티를 억제하고 지정 승패 우호도를 적용합니다. 다른 대상 공격은 그대로입니다.
- JobDriver + Toils_General.Wait/Do, GameComponentTick, Scribe 컬렉션/참조: 의식·맹세·저장/재개.
- 설치 Core XML의 StaticWorldObjectBase, PeaceTalks 텍스처 경로, QuestScriptDef의 autoAccept/questNameRules/questDescriptionRules를 사용합니다.

Steam의 필수 항목/모음집 구성은 공식 [ISteamUGC 문서](https://partner.steamgames.com/doc/api/ISteamUGC)의 CreateItem/SetItemVisibility/AddDependency/AddAppDependency와 설치 Steamworks.NET 바인딩 서명을 확인해 사용합니다. 게시 후 QueryUGC children과 GetAppDependencies로 다시 읽습니다. Steamworks DLL은 소스/모드 배포에 포함하지 않습니다.

## 0.2.1 검토에서 확인한 사항

Verse.ModDependency.IsSatisfied는 실제 설치 DLL에서 packageId/alternativePackageIds의 활성 여부를 검사하고 버전 숫자를 비교하지 않습니다. 표시 문자열을 버전 강제 장치라고 안내하지 않습니다. 대여 무기 회수에는 실제 Thing.holdingOwner.Remove / Thing.Destroy를 사용하고 저장된 결투의 해당 무기 참조도 지웁니다. 맹세 이전은 GameComponent.LoadedGame 이후에 처리합니다.

Verse.GenGrid.Standable은 Walkable뿐 아니라 같은 셀 Thing의 def.passability가 모두 Traversability.Standable인지도 검사합니다. 통과 가능한 돌덩이도 기립 장소를 막을 수 있어 생성 결투장 준비에서 제거합니다.

## Contracts 설계 검토 (2026-10-24)

설치 Version.txt의 1.7.4985 rev1269와 RimWorld.runtimeconfig.json의 net10.0 / 10.0.13-waylithclr.dev를 다시 확인했습니다. 기존 설치 DLL에서 확인한 Settlement : MapParent의 Faction/TraderKind/Goods 및 거래 전달 함수, WorldObject.SetFaction/ExposeData/Destroyed, Faction.leader/defeated/RelationWith/TryAffectGoodwillWith를 설계에 참고했습니다. 정착지 거래 재고가 별도 영토 국고나 지속 생산 경제라는 전제는 두지 않습니다. 자치권·봉신 관계는 새 계약 데이터로 구현할 설계이며 기본 엔진에서 이미 보장된다고 주장하지 않습니다.

이번 구현은 엔진 독립 계약 규칙뿐이므로 새로운 게임 메서드 바인딩·Harmony 패치를 추가하지 않았고 새 기능의 엔진 검증도 수행하지 않았습니다. 실제 계약 모듈 구현 시 해당 바인딩을 다시 컴파일·실행 검증해야 합니다.

## Contracts 군사 경로 검토 (2026-10-24)

같은 1.7.4985 rev1269 설치 DLL의 `RimWorld.Planet.SettlementDefeatUtility`를 추가로 확인했습니다. `Settlement.TickInterval(int)`에서 `CheckDefeated(Settlement)`를 호출하며, 후자는 패배 판정 후 DestroyedSettlement 생성·원 소속 복사·map.info.parent 교체·원 Settlement.Destroy를 실행합니다. 다른 거점이 없으면 Faction.defeated를 true로 만들며 기본 우호도 보상과 패배 편지도 보냅니다. 따라서 정착지 보존은 이 부작용이 실행되기 전의 한정된 분기로 설계해야 합니다.

`IsDefeated(Map,Faction)`는 해당 세력의 생성된 humanlike Pawn 중 활성 위협이 남았는지를 검사합니다. 이 결과만으로 공격자의 주둔·행정 통제, 전역 세력 대표의 항복 동의, 자원 보유를 보장하지 않습니다.

`RimWorld.RaidStrategyWorker_Siege.MakeLordJob(IncidentParms,Map,List<Pawn>,int)`는 LordJob_Siege를 생성하고, CanUseWith는 기본 조건과 faction.def.canSiege를 확인합니다. 이는 맵의 습격 전략이며 플레이어 상단의 월드 포위·NPC 정착지에 대한 범용 사건 생성기가 이미 있다는 뜻은 아닙니다. Faction.RelationWith(Faction,bool)는 세력 쌍 관계로서 정착지별 정전 API로 간주하지 않습니다.

이번 변경도 엔진 독립 규칙만 포함합니다. 위 지점을 실제로 패치하거나 구원 사건을 게임에서 생성하지 않았으며 항복/점령과 봉신 방어의 엔진 성공을 주장하지 않습니다. 역컴파일 코드는 작업 폴더에서만 확인하고 Git/배포물에 포함하지 않습니다.

## 자유도시·기지 가치 설계 검토 (2026-10-24)

Version.txt의 1.7.4985 rev1269를 다시 확인하고 설치 DLL의 `Verse.Map` 및 `RimWorld.WealthWatcher`를 조사했습니다. Map.wealthWatcher는 WealthWatcher 필드이며, WealthTotal은 RecountIfNeeded 후 물품·건물·Pawn 가치를 합산합니다. 재계산에는 5000틱 캐시 조건이 있어 매 관측마다 강제로 전체 재집계하지 않도록 설계합니다. 실제 선택 시점은 게임의 집계 갱신 간격에 영향을 받을 수 있습니다.

`Map.PlayerWealthForStoryteller`는 고정 부 모드/건물 가중치 등에 따라 WealthTotal과 다를 수 있으므로 이번 기지 가치 기준으로 사용하지 않습니다. `Map.IsPlayerHome`은 중력선 착륙/엔진 조건으로도 true가 될 수 있어 도시 관측은 이 속성만으로 판단하지 않고 실제 Settlement 부모·현재 플레이어 소속도 확인해야 합니다. 임시 전투 맵이나 이동체가 홈으로 분류됐다고 도시 지위 선택을 생성하지 않습니다.

이번 구현은 기지 가치/소유권 관측값을 입력받는 엔진 독립 규칙입니다. 게임에서 WealthWatcher를 호출하는 새 모듈이나 편지·설정·저장을 추가하지 않았고, 실제 게임의 가치 변화/인식 선택 검사는 아직 하지 않았습니다.

## 세력 명예·신뢰·이탈 규칙 (2026-10-24)

사용자가 세력 명예를 추가 요청했으므로 Contracts의 별도 사건 장부로 계산합니다. 세력에 Pawn용 Need를 억지로 붙이거나, 바닐라에 같은 명예/신뢰/독립 API가 있다고 가정하지 않습니다. 이번 변경은 기존 ContractLedger와 엔진 독립 수치·시간·사건 순서 계산만 사용하며 새로운 게임/DLC 메서드나 Harmony 바인딩을 추가하지 않았습니다. 현재 게임에서 세력 행적을 평가하거나 실제 계약 영지가 이탈한 엔진 증거는 없습니다. 이 연결은 이후 설치 DLL 기준으로 검증해야 합니다.

## 구원 조건부 봉신 제안 규칙 (2026-10-24)

이번 RescuePledge 변경도 엔진 독립 코드만 사용합니다. 기존에 확인한 Quest/QuestPart API에 새 게임 바인딩을 추가하지 않았고, 바닐라가 지방 대표의 봉신 약속·일부 영지 독립·세력 분할을 자동 지원한다고 가정하지 않습니다. 실제 보호 퀘스트·NPC 방어 AI·지도·대표 권한·소속 변경은 후속 단계에서 설치 DLL로 검증해야 합니다. 조건부 약속의 상태 검사 결과는 실제 게임에서 미션을 수령/완료했다는 증거가 아닙니다.
