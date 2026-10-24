# 1.7 API·런타임 검증 기록

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
