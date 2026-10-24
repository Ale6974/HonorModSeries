# 검증과 플레이 점검

기준: RimWorld 1.7.4985 rev1269 unstable, Harmony 3.0.0-rc.3, .NET SDK 10.0.402. 이 환경에는 설치된 DLC를 모두 활성화한 격리 테스트를 사용했습니다. `-mod` 옵션은 활성 모드 설정 파일을 읽거나 수정하지 않고, `-savedatafolder`는 작업용 폴더를 사용합니다.

## 자동 검증

- 설치 게임 DLL 참조 Release 빌드: 오류 0, 경고 0.
- 엔진 독립 규칙: 20,080개 검증 통과. 0~1 전 구간의 단계 하한·자연 감소, 사건 하락, 범위 제한, 64가지 참가 자격 조합, 이념 우선, 선혈·항복·사망 종료 조건을 검사합니다.
- XML 구문: 모든 모드·테스트 Def 및 번역 파일 파싱 통과.
- 영어·한국어 각각 실제 엔진에서 게임 언어 설정, Need, Job, 세 특성, 소셜 기억, 기즈모, 알림, Ideology 교리 번역 검사를 통과했습니다. 설치된 로컬 모드가 실제 로드됐는지도 확인했습니다. Keyed 문구 22개와 포맷 인수의 일치를 빌드 스크립트에서 확인했습니다.
- 모든 DLC를 비활성화한 한국어 구성에서도 실제 로드·번역 검사를 통과했습니다. DLC 전용 번역이 제외되고 Honor Def 번역 11개가 모두 적용됐으며 Honor 관련 번역 오류는 0개였습니다. 이 구성에서 게임 기본 한국어 데이터의 별도 진단 36개가 발생하는 것은 구분하여 기록했습니다.
- 실제 엔진: 성공한 바닐라 모욕으로 신청 방향·기존 소셜 싸움 억제·준비·실제 근접 공격·선혈 종료·처치·명예·작업 종료·중복 처리 방지를 확인했습니다.
- 실제 엔진 저장: 전투 중 `HonorActive.rws` 저장 → 프로세스 종료 → 실제 로드 → Pawn·Need 참조, Fighting 상태와 양측 Job 복원 → 전투 완료·처치·방어구 재착용을 확인했습니다.

- 실제 엔진 리볼버 선혈전: 준비·실제 사격·선혈 종료·처치·방어구 복원·명예 중복 방지를 통과했습니다.
- 실제 엔진 항복전: 실제 근접 공격으로 항복 조건에 도달해 종료·처치·방어구 복원·명예 중복 방지를 통과했습니다.
- 실제 엔진 사망전: 실제 근접 공격으로 한 명이 사망할 때까지 진행하고, 생존자 처치·방어구 복원·명예 중복 방지를 통과했습니다.

초기 테스트에서 바닐라 초기 정착민·야생동물이 경기장에 진입하자 안전 중단이 발생했습니다. 테스트 도구는 시험 지역을 통제하도록 수정했습니다. 여러 게임 엔진을 동시에 시작한 시도 중 하나는 엔진 작업 스레드 초기화에서 종료되었습니다. 반복 검사는 한 프로세스씩 실행합니다. 테스트용 의존성 메타데이터 경고는 제거했고, Headless의 바닐라 DrawTexture stub 경고는 게임 호환층에서 발생하는 것으로 별도 구분합니다.

## 재현

```powershell
./tools/Build.ps1 -DotnetPath 'C:/path/to/dotnet.exe'
./tools/Test-InGame.ps1 -RimWorldDir 'F:/SteamLibrary/steamapps/common/RimWorld' -Scenario FirstBlood
./tools/Test-InGame.ps1 -RimWorldDir 'F:/SteamLibrary/steamapps/common/RimWorld' -Scenario Ranged
./tools/Test-InGame.ps1 -RimWorldDir 'F:/SteamLibrary/steamapps/common/RimWorld' -Scenario Surrender
./tools/Test-InGame.ps1 -RimWorldDir 'F:/SteamLibrary/steamapps/common/RimWorld' -Scenario Death
./tools/Test-InGame.ps1 -RimWorldDir 'F:/SteamLibrary/steamapps/common/RimWorld' -Scenario English -UseInstalledCore
./tools/Test-InGame.ps1 -RimWorldDir 'F:/SteamLibrary/steamapps/common/RimWorld' -Scenario Korean -UseInstalledCore
# DLC 없는 구성도 실제 개인 설정과 별개로 검사할 수 있음
./tools/Test-InGame.ps1 -RimWorldDir 'F:/SteamLibrary/steamapps/common/RimWorld' -Scenario Korean -UseInstalledCore -BaseGameOnly
# Checkpoint 결과에 출력된 SaveData 경로를 Resume에 사용
./tools/Test-InGame.ps1 -RimWorldDir 'F:/SteamLibrary/steamapps/common/RimWorld' -Scenario Checkpoint
./tools/Test-InGame.ps1 -RimWorldDir 'F:/SteamLibrary/steamapps/common/RimWorld' -Scenario Resume -SaveDataDir 'C:/path/to/Checkpoint/SaveData'
```

이미 Honor.Core를 설치한 경우 `-UseInstalledCore`로 그 설치본을 검사하거나, 같은 packageId가 중복되지 않도록 수동 설치본을 따로 보관한 상태에서 검사하세요. 스크립트는 자신의 고유 이름 임시 모드 폴더만 `artifacts`로 회수합니다. 로그·테스트 세이브도 저장소의 무시된 `artifacts`에 남습니다. 실제 플레이에는 TestMods를 설치하지 않습니다.

## 수동 플레이 점검표 — 아직 별도 검증 필요

- 한국어 설정 화면·주민 기즈모·결투장 선택·거절 불가 흐름과 준비 실패 안내를 화면에서 확인.
- 기본 제외 세 종류 각각, 설정 허용, 수면·다운 연기, 치유 후 재개, 징집·구금 전환과 이동 중 중단.
- 단계 경계에서 장기 경과, 세이브 재개, 특성별 동일 사건 민감도.
- 일반 옷 유지, 잠긴 방어구·장비 파괴·강제 교체·부상으로 착용 불가능해진 경우.
- 아군·동물 진입, 새 화재·습격, 외부 사격, 투사체가 날아오는 도중 종료.
- Ideology만 / Royalty만의 개별 구성과 교리 선택, 다른 소셜·전투 모드와의 조합. 기본 게임만의 장기 결투 플레이.
- 사망·쌍방 다운·장시간 결투·전투 중 이념 변경·맵 이탈과 저장 재개.
- 장기간 정착지 플레이 및 성능. CE 등 대형 전투 변경 모드는 호환성 미검증.

자동 엔진 검사 통과가 화면·장기 플레이·모든 모드 조합 검증을 대신하지 않습니다.

## 0.2.0 부속 모드 검증 — 2026-10-24

이 아래 결과는 0.2.0의 실제 검사입니다. 위 Core 0.1.x 기록은 당시 검사 이력입니다. 모든 게임 검사는 사용자 세이브와 별도 save-data 폴더에서 실행했습니다.

| 검사 | 결과 |
|---|---|
| Core + 네 부속 모드, 두 테스트 어셈블리 빌드 | 경고/오류 0 |
| 순수 규칙 검사 | 20,080 assertions 통과 |
| XML / 영어·한국어 keyed 키와 자리표시자 | Core 22, Diplomacy 10, Nobility 3, Oaths 10, Society 5 일치 |
| 한국어 전체 시리즈 로드 | 버튼·기억·DefInjected 모두 적용, Honor 번역 오류 0 |
| DLC 없는 영어 구성 | Core/Society/Diplomacy/Oaths 로드·번역 통과, Nobility 제외 |
| DLC 없는 실제 시리즈 플레이 | 사과 제안/수락과 거절 조건, 숙적, 혈연 명예와 목격, 지연 소문, 의식 Job, 맹세 성공/위반, 실제 월드 퀘스트·맵·대표 결투·처치·퀘스트 종료·은/우호도·중복 방지 통과 |
| Royalty 포함 전체 시리즈 | 실제 Knight 작위 배율·대리인 지정·최초 규칙 유지·본인 맹세 반영 포함 통과 |
| 시리즈 전투 중 저장/재개 | 사회 기록, 진행 맹세, 외교 퀘스트/장소/대표, 세력 대기 기간, 진행 전투 참조 보존; 재개 뒤 실제 전투·응급 처치·승리 은·우호도·한 번만 보상 통과 |
| Steam | 다섯 개 모드 Public, 금지 아님, Required Items 일치; Nobility의 Royalty App Dependency 확인. 다섯 모드 비로그인 페이지 접근 확인 |
| 모음집 | Public API 값·6개 구성 확인. 웹의 최종 ‘공개’ 버튼은 사용자가 나중에 처리하기로 함 |
| 로컬 설치 | 다섯 모드의 모든 배포 파일 해시가 소스 배포 폴더와 일치, 이전 활성 모드 98개 순서 유지하고 부속 4개 추가, 한국어 설정 유지 |

실패 후 수정한 중요 사항: 대표 결투 타격에도 바닐라 외교 공격 페널티가 중첩되는 경로를 발견했습니다. 지정된 대표와 합의한 상대의 해당 결투 타격만 예외로 처리하고, 수정 후 승리 우호도 +10·은 100 및 중복 방지 검사를 통과했습니다. 테스트 초기 틱 0을 ‘소문 없음’으로 쓰지 않도록 시험 기한도 보정했습니다.

[선별 엔진 메시지](evidence/series-0.2.0-engine.txt)에는 개인 경로·Steam 식별자·전체 엔진 로그를 포함하지 않습니다.

재현: tools/Build.ps1 후 tools/Test-Series.ps1을 실행합니다. `-Scenario Korean`, `-Scenario English -BaseGameOnly`, `-Scenario Gameplay -BaseGameOnly`, `-Scenario Checkpoint`, 결과의 SaveDataDir로 `-Scenario Resume -SaveDataDir ...`를 사용합니다. 이 스크립트는 설치된 Honor 모드를 검사 동안 자기 artifacts로 옮겨 중복 패키지를 피하고 finally에서 복원합니다. 사용자의 ModsConfig/Prefs/저장 게임은 수정하지 않습니다. 게임은 닫고 한 프로세스씩 검사하세요. 실제 플레이용은 Install-Local.ps1입니다.

자동 검사는 직접 월드 맵 생성 후 대표 전투를 진행합니다. 기본 CaravanArrivalAction_Enter 사용은 DLL에서 확인했으나 화면에서 긴 상단 이동/왕복, 모든 UI 흐름, 장기 정착지와 전체 모드 조합은 수동 검증이 남았습니다. 공개 범위는 이 한계를 포함한 실험판입니다.

설치된 0.2.0 Core만 활성화한 기본 게임 실제 모욕→선혈전→종료·처치 회귀 검사도 통과했습니다 (`FirstBlood -UseInstalledCore -BaseGameOnly`). 부속 모드를 끈 독립 Core 구성을 확인한 결과입니다.
