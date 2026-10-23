# GitHub 및 Steam 게시

사용자가 2026-10-24에 승인한 게시 위치는 공개 GitHub `Ale6974/HonorModSeries`, Steam은 **비공개 시험 게시**입니다. 이 승인으로 Steam 공개 배포까지 허용한 것은 아닙니다.

## GitHub

검증한 소스·번역·개발 문서·테스트를 https://github.com/Ale6974/HonorModSeries 에 게시합니다. 게임·Harmony·Steamworks DLL, 실제 개인 모드 설정, 세이브, 인증정보, 전체 엔진 로그는 게시하지 않습니다. 검사 로그에는 필요한 Honor 메시지만 남깁니다. 게임 DLL이 필요 없는 규칙 테스트는 독립 실행할 수 있고, 전체 빌드는 사용자 소유 게임 설치를 참조해야 합니다.

로컬 Git Credential Manager의 인증으로 원래 커밋 기록을 보존해 게시합니다. 이 환경의 연결 앱은 파일 쓰기를 403으로 거부하므로 업로드에는 로컬 Git을 사용합니다. 인증값을 채팅이나 저장소에 복사하지 않습니다.

## Steam

`tools/SteamWorkshopUploader`는 설치된 게임의 Steamworks.NET DLL과 `steam_api64.dll`을 참조하는 로컬 전용 도구입니다. 소스만 GitHub에 게시하며 참조한 DLL은 배포하지 않습니다. 사용자의 로그인된 Steam 데스크톱 세션에서 RimWorld 소유권을 확인합니다. 비밀번호·API 키 입력은 받지 않습니다.

```powershell
dotnet run --project tools/SteamWorkshopUploader -c Release -- status 'F:/SteamLibrary/steamapps/common/RimWorld'
dotnet run --project tools/SteamWorkshopUploader -c Release -- publish-private 'F:/SteamLibrary/steamapps/common/RimWorld' 'F:/SteamLibrary/steamapps/common/RimWorld/mods/Honor.Core' '.local/steam-workshop.json' 'release/steam-description.txt'
dotnet run --project tools/SteamWorkshopUploader -c Release -- verify 'F:/SteamLibrary/steamapps/common/RimWorld' '.local/steam-workshop.json'
```

최초 생성 직후 ID를 `.local/steam-workshop.json`에 저장해 재시도 시 같은 항목을 갱신합니다. 업로드 전 `Private`를 명시하고, 업로드 후 Steam에서 읽은 앱·소유자·공개 범위를 확인합니다. 기존 항목이 공개 상태이면 이 시험 도구는 변경을 거절합니다. 도구에는 공개 게시 명령이 없습니다.

비공개 시험 항목: https://steamcommunity.com/sharedfiles/filedetails/?id=3825494597 . 첫 업로드에서 Steam API로 앱 294100, 현재 로그인 사용자의 소유, 공개 범위 `Private`, 파일 크기 81,814바이트를 확인했습니다. 첫 게시에서 새 약관 동의는 요구되지 않았습니다. 비공개 항목은 게시 계정으로 로그인해야 볼 수 있습니다. ID는 `About/PublishedFileId.txt`에도 기록하며, `.local` 상태 파일이 없어져도 기존 ID를 우선 사용합니다.

새 Steam 약관 동의가 필요하면 도구는 이를 알리고 멈춥니다. 사용자가 직접 동의한 뒤 저장된 ID로 재실행합니다. 업로드 설명은 `release/steam-description.txt`의 영어·한국어 문구를 사용합니다. 공개 출시 전에는 현재 MVP 제한, 호환성, 미구현 기능, 미리보기·스크린샷을 다시 검토합니다.
