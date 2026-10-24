# Honor.Contracts 연결 흐름 개발 미리보기

## 용도와 상태

사용자가 선택한 전체 흐름 우선 개발의 첫 연결 버전이다. `PoliticalWorld`는 실제 Faction과 별개의 정치 주체 ID를 사용한다. `Honor.Contracts.Preview`는 **별도 선택형 개발 모드**이며 공개 Core/Society/Nobility/Diplomacy/Oaths에 자동으로 포함되지 않는다. 실제 플레이 증명 없이 완성 모드로 Workshop에 게시하지 않는다.

설치 게임 1.7.4985 rev1269의 Assembly-CSharp/UnityStubs/Waylith와 Harmony 3 RC DLL로 컴파일했다. 새 게임 실행·UI 조작·Scribe 게임 저장 재로딩은 수행하지 않았다. 빌드는 API 참조 검사이지 엔진 동작 증거가 아니다.

## 이번에 연결한 범위

- 안정적인 정치 주체 ID와 native faction ID 분리. 플레이어 ID는 하나로 유지하며 주민·기지의 실제 소속을 수정하지 않는다.
- 직속 계약, 순환/중복 종속 금지, 최상위 포함 4단계. 하위 집단 전체를 포함해 이동 후 깊이를 검사한다. 최상위 대외 외교 그룹을 조회한다.
- 고정 가치 공납, 5일 주기/1일 유예 예제. 봉신 배송은 운송 몫 공제, 종주 방문 징수는 추가 비용. 원금·인수 가치 분리. 기한 이후 신뢰 하락은 한 번만 적용하며 자동 명예 유죄/전쟁/해약 없음.
- 전체 모델 영수증 중복 금지, 충분한 실제 거래가치가 전달됐다는 **어댑터 입력 규약**. 미리보기 버튼은 가상 영수증일 뿐 실제 거래가격 계산/물품 인수 검증이 아니다. 초과 납부가 다른 의무를 갚지 않는다.
- NPC 대상 공격 요청, 기한, 병력 예약, 동맹 구원, 가용 병력과 원정 병력의 중복 방지, 결과/손실/명예 한 번 반영. 모델 보호 조항은 기한 내 **병력 응답**만 인정한다. 다른 지원 방식과 면책은 추후 추가한다.
- 명시한 보호 조항 미이행 → 시정 통보 → 실제 시간 기한 → 미해결이면 정당한 독립. 낮은 명예만으로 독립 불가. 양측이 수락한 시정 합의로 독립 사유를 해소할 수 있다. 독립/합의 해약 시 하위 계약·과거 공납 기록 유지, 연합 그룹으로 이동.
- NPC 패배 결과는 기록하지만 소속 이전/점령 계약을 자동 생성하지 않는다. 기존 소유권/계약을 덮어쓰지 않는다. 플레이어 기지는 추상 공격의 패배 대상에서 제외한다.
- 설정된 간격으로 번영을 간략 갱신. 날짜 기한은 턴 간격과 별개. 큰 시간 도약도 중간 기한·턴을 순서대로 처리한다.
- 명령 재생 기반 버전 1 저장: 직렬화/복원 시 같은 검증 경로. 연속 시간 갱신 압축, 미래 스키마/손상 입력 거절. 게임 Scribe에는 JSON 문자열과 시계 연결 설정을 저장한다. 복원 실패 시 원본 문자열을 보존하고 모델을 중지한다.
- 영·한 keyed 문구와 placeholder 일치 검사. 현재 게임 언어를 따르고 언어 설정을 변경하지 않는다.

초기 모델 제한: 주체 최대 200, 명령 최대 10,000, 시간 전진 호출당 최대 120일, 전체 모델 6,000일, 턴 0.25~10일(화면 1~10일). 용병단/평의회/세계 생성/개인 서약/장기 경제를 구현한 것이 아니다. 화면의 초기 정착지·병력·전투 공식·번영 수치는 시험 데이터이며 확정 밸런스가 아니다. 인구/병종·장비별 충원·별도 공납 수송 상단은 아직 없다.

## 로컬 빌드와 선택형 설치

기존 `GamePaths.props`와 .NET 10 SDK 설정 후:

```powershell
./tools/Build.ps1 -DotnetPath '<local .NET 10 SDK>/dotnet.exe'
./tools/Install-ContractsPreview.ps1 -RimWorldDir '<RimWorld directory>'
```

설치기는 게임을 실행하거나 모드 활성 설정을 변경하지 않는다. 기존 동일 package ID 설치가 있으면 `.local`에 백업한다. 직접 확인할 때 게임 모드 목록에서 **Honor.Contracts — Development Preview**를 선택한다. 로드 순서는 Harmony 3 → Core 0.2.1+ → Preview. 다른 부속 모드/DLC는 필수 아님. 이후 사용자 요청 전까지 에이전트가 게임을 실행하지 않는다.

향후 사용 경로(미실행): 월드맵에서 플레이어 정착지 선택 → 계약 개발 미리보기 → 모델 생성. 계약 합의 → 5일 경과 → 가상 배송 기록, 또는 가상 공격 → 구원 병력 파견 → 기한 경과 → 결과. 구원을 보내지 않았다면 시정 요구 → 합의 시정 또는 3일 대응 기간 뒤 독립. 화면의 버튼은 모두 명시적인 모의 사건 입력이며 NPC 동의나 실제 전투 증거를 만들어 내지 않는다.

## 다음 연결 작업

실제 정착지/정치 주체 매핑과 NPC 권한, 세력 소속 반영/플레이어 특례, 실제 거래가격·물품 이전·수송 상단, 실제 퀘스트/전투 결과 증거, 항복·점령의 정착지 보존, 게임 세계 사건 생성과 알림, 이행 면책·반복 미납 제재, 평의회 가중 표결, 세계 생성/기존 저장 선택, 용병단과 개인 서약을 순차적으로 연결한다. 기존 순수 규칙의 RescuePledge·FactionHonorLedger와 새 모델의 통합도 남아 있다. 현재 미리보기의 독립 명예 수치는 Core Need/바닐라 우호도에 전파하지 않는다.

English: An opt-in, code-validated development sandbox for the broad contract → tribute → relief → cure/exit flow. It preserves its own journal and has English/Korean UI. It does not change real faction ownership, transfer goods, create playable rescue quests, or simulate player defeat. No gameplay validation or Workshop release is claimed.
