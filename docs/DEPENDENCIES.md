# 의존성·권장 구성 / Dependencies

현재 소스/로컬 배포는 **0.2.2 호환성 빌드 + Contracts Preview 0.3.12**이다. RimWorld 1.6.4871 참조/net472와 설치된 1.7.4985 rev1269/net10으로 각각 빌드·규칙 검사했다. 실제 1.6 게임/Mono 실행은 검증하지 않았다. 기존 Steam 공개본은 0.2.1로 유지하며 이 변경은 GitHub 소스와 로컬 ZIP으로 제공한다. [정확한 검증 범위](VERSION-SUPPORT.md).

| 항목 | 필수 모드 | 필수 DLC | 다른 부속 모드 필요? |
|---|---|---|---|
| Honor.Core 0.2.2 | 해당 게임 버전의 Harmony | 없음 | 없음 |
| Honor.Society 0.2.2 | Harmony + Core 0.2.2 이상 | 없음 | 없음 |
| Honor.Nobility 0.2.2 | Harmony + Core 0.2.2 이상 | **Royalty** | 없음 |
| Honor.Diplomacy 0.2.2 | Harmony + Core 0.2.2 이상 | 없음 | 없음 |
| Honor.Oaths 0.2.2 | Harmony + Core 0.2.2 이상 | 없음 | 없음 |
| Contracts Preview 0.3.12 (선택형 개발 모드) | Harmony + Core 0.2.2 이상 | 없음 | 없음 |

Harmony의 동일 Workshop 항목(2009463077)을 사용하되 1.6은 1.6/Assemblies의 2.x, 1.7은 1.7/Assemblies의 3.x를 사용한다. DLL을 직접 다른 버전 폴더에 복사하지 않는다. .NET SDK는 소스 빌드에만 필요하다.

Contracts 1.6의 JSON용 Microsoft MIT 라이브러리는 해당 모드의 1.6 Assemblies 폴더에 동봉한다. 별도 Workshop 선행 모드가 아니며 직접 설치할 필요가 없다. 라이선스/버전은 About의 ThirdPartyNotices에 포함한다. 1.7은 런타임 JSON을 쓰므로 이 라이브러리를 함께 로드하지 않는다. 타 모드가 제공하는 다른 JSON/Memory 라이브러리와의 런타임 호환성은 미검증이다.

권장 기본 묶음은 Harmony와 기존 5개 Honor 모드다. **Royalty가 없다면 Nobility를 제외**한다. Ideology는 결투 교리에 선택적으로 사용하며 필수가 아니다. Biotech·Anomaly·Odyssey도 필수가 아니다. Contracts는 별도 선택형이며 기존 5개 모드의 의존성이 아니다. 실제 계약/실물 공납/공유 외교가 있는 개발판이므로 가상 모델과 구분한다. [기능과 남은 제한](CONTRACTS-PREVIEW.md).

로딩 순서: Harmony → RimWorld 및 보유 DLC → Core → 선택한 부속 모드. 게임 언어를 따라 영어/한국어를 선택하고 다른 언어에는 영어를 사용한다. 동일 package ID의 로컬본과 Workshop본을 함께 활성화하지 않는다. 설치기는 기본적으로 기존 활성화/언어 설정을 바꾸지 않는다. 새 묶음과 오래된 Core를 섞지 말고 버전이 일치하는 폴더를 함께 설치한다. 모드 제거 또는 1.7 세이브의 1.6 역변환을 보장하지 않는다.

English: Use matching Harmony (2.x for 1.6; 3.x for 1.7), then Core 0.2.2+ and optional expansions from the same bundle. Only Nobility needs Royalty. Contracts Preview is optional, not a dependency of the five existing modules. Its 1.6 JSON libraries are bundled under MIT; no separate Workshop dependency is required. Both version builds and rule suites are checked, but direct 1.6 gameplay/Mono and mixed-mod library compatibility are untested. Steam items remain at 0.2.1 in this change. Do not enable duplicate local/Workshop packages or assume saves can be downgraded between game versions.
