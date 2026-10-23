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
