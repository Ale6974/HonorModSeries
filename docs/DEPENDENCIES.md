# 의존성·권장 구성 / Dependencies

모든 항목은 **RimWorld 1.7.4985 rev1269 unstable / Waylith / .NET 10**, **Harmony 3의 1.7 폴더**를 기준으로 검증했습니다. 1.6과 호환되지 않습니다. 이후 1.7 패치는 재검증이 필요합니다.

| 항목 | 필수 모드 | 필수 DLC | 다른 부속 모드 필요? |
|---|---|---|---|
| Honor.Core 0.2.1 | Harmony 3 (1.7) | 없음 | 없음 |
| Honor.Society 0.2.1 | Harmony + Core 0.2.1 이상 | 없음 | 없음 |
| Honor.Nobility 0.2.1 | Harmony + Core 0.2.1 이상 | **Royalty** | 없음 |
| Honor.Diplomacy 0.2.1 | Harmony + Core 0.2.1 이상 | 없음 | 없음 |
| Honor.Oaths 0.2.1 | Harmony + Core 0.2.1 이상 | 없음 | 없음 |
| Contracts Preview 0.3.2 (선택형 개발 모드, Workshop 미게시) | Harmony + Core 0.2.1 이상 | 없음 | 없음 |

Preview는 설치 DLL에 대한 빌드와 순수 규칙 검사만 통과했다. 게임 실행 검증은 보류 중이며, 실제 계약 모드의 플레이 가능성을 검증했다는 뜻이 아니다. `TestMods`의 별도 모듈로 설치하며 기본 공개 모음집/기존 다섯 모드의 필수 의존성에 포함하지 않는다. [개발 범위와 설치](CONTRACTS-PREVIEW.md).

Preview 0.3.1의 실제 공납은 별도로 켜는 수동 전달이다. 확인하면 실제 상단 소지품을 이전하므로 가상 모델과 구분한다. 일반 은 통화 정착지에 한정하며, DLC가 없어도 되지만 작위 제한/호의 통화 거래는 아직 지원하지 않는다. 보호 임무나 세력 편입이 완성된 버전으로 보지 않는다.

0.3.2는 별도 확정 후 적격 NPC 도시를 실제 자유도시연합으로 초기 편입할 수 있다. Union Def는 Core의 OutlanderFactionBase를 상속하고 DLC는 필수가 아니다. 플레이어 가입·계약에 따른 소속 집행·표결은 아직 미연결이다. 모드 제거가 진행 중인 실제 공납/연합 참조를 보존한다고 보장하지 않는다.

권장 모음집은 Harmony와 다섯 Honor 모드를 포함합니다. **Royalty가 없다면 Nobility만 구독·활성화하지 마세요.** 나머지 네 Honor 모드는 DLC 없이 함께 쓸 수 있습니다. Ideology가 있으면 신청자의 명예 결투 교리를 사용하고, 없으면 기술 수준 규칙을 사용합니다. Biotech·Anomaly·Odyssey는 필수가 아닙니다.

로딩 순서: Harmony → RimWorld 및 보유 DLC → Honor.Core → 선택한 부속 모드. 부속 모드끼리는 순서에 의존하지 않습니다. About.xml의 필수 항목과 loadAfter, Steam의 Required Items와 Nobility의 Royalty App Dependency를 함께 등록합니다. Steam 구독은 게임 내 활성화를 대신하지 않습니다.

로컬 설치본을 쓰면 같은 모드의 Steam 구독본을 동시에 활성화하지 마세요. 모음집에 Core를 필수 항목으로 포함하더라도 다른 사용자의 저장 게임에서 모드를 제거해도 안전하다는 의미는 아닙니다. 저장된 Need·관계 기억·퀘스트를 쓰므로 제거 전 세이브를 백업하세요.

English: Harmony 3 for RimWorld 1.7 is required by every module. Core 0.2.1+ is required by each optional expansion; expansions do not depend on one another. Only Nobility requires Royalty. Without Royalty, skip Nobility in the collection. Load Harmony, the game/DLC, Core, then optional modules. Enable subscribed mods in the game. Use one copy of each package (local OR Workshop).
