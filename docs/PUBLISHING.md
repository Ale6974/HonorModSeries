# 게시 상태와 절차 — 0.2.1

2026-10-24 사용자가 Core/네 부속 모드의 공개 게시와 공개 모음집을 승인했습니다. 각 모드는 공개 업로드했고 비로그인 페이지 접근도 확인했습니다. **모음집은 API의 Public 설정과 6개 항목 등록까지 완료했지만 Steam 웹의 마지막 ‘공개’ 단계가 남아 있습니다.** 사용자는 외출 중이라 이 버튼을 나중에 직접 누르겠다고 답했습니다. 모음집 완료 전에는 일반 사용자에게 공개된 모음집이라고 안내하지 마세요.

| 항목 | 링크 | 상태 |
|---|---|---|
| Core | https://steamcommunity.com/sharedfiles/filedetails/?id=3825494597 | 공개, Harmony 필수 |
| Society | https://steamcommunity.com/sharedfiles/filedetails/?id=3825925256 | 공개, Core/Harmony 필수 |
| Nobility | https://steamcommunity.com/sharedfiles/filedetails/?id=3825925320 | 공개, Core/Harmony/Royalty 필수 |
| Diplomacy | https://steamcommunity.com/sharedfiles/filedetails/?id=3825925370 | 공개, Core/Harmony 필수 |
| Oaths | https://steamcommunity.com/sharedfiles/filedetails/?id=3825925424 | 공개, Core/Harmony 필수 |
| 추천 모음집 | https://steamcommunity.com/sharedfiles/filedetails/?id=3825922644 | 6개 등록, 마지막 공개 버튼 대기 |

## GitHub

검증 소스·번역·개발 문서·테스트·게시 ID는 https://github.com/Ale6974/HonorModSeries 에 게시합니다. 게임/Harmony/Steamworks DLL, 실제 개인 모드 설정, 세이브, 인증정보, 전체 엔진 로그, 디버그 심볼은 게시하지 않습니다. 로컬 Git Credential Manager 인증을 사용하며 비밀값을 출력하지 않습니다.

## Steam 도구

`tools/SteamWorkshopUploader`는 사용자 소유 게임의 Steamworks.NET / steam_api64.dll과 로그인된 데스크톱 Steam 세션을 사용합니다. DLL은 도구의 무시된 빌드 폴더에만 참조하며 모드/저장소에 포함하지 않습니다. 공개 실행은 사용자의 명시적 공개 승인에 근거합니다.

```powershell
# 먼저 게임 경로를 GamePaths.props에 설정하고 Build.ps1 실행
# 아래 'GAME', 'MOD', 'STATE', 'DESCRIPTION'은 해당 경로로 대체
 dotnet run --project tools/SteamWorkshopUploader -c Release -- status GAME
 dotnet run --project tools/SteamWorkshopUploader -c Release -- publish-public GAME MOD STATE DESCRIPTION
 dotnet run --project tools/SteamWorkshopUploader -c Release -- require GAME PARENT_ID CHILD_ID
 dotnet run --project tools/SteamWorkshopUploader -c Release -- require-app GAME ITEM_ID DLC_APP_ID
 dotnet run --project tools/SteamWorkshopUploader -c Release -- verify-series GAME release/workshop-items.json
```

CreateItem 직후 ID를 무시된 `.local/steam-*.json`에 저장합니다. 모드는 About/PublishedFileId.txt에서도 복구하므로 재시도에 중복 항목을 만들지 않습니다. collection 명령은 preview 폴더, 상태 파일과 설명 파일을 받습니다. 모음집은 별도로 Steam 웹의 **공개** 버튼을 눌러야 합니다. API의 Public 값만으로 웹 게시 완료를 판단하지 마세요. 마지막 단계 뒤 비로그인 페이지에서 모음집 제목과 6개 항목이 보이는지 확인하세요.

필수 항목은 AddDependency, Nobility의 Royalty는 AddAppDependency(1149640)로 등록했습니다. verify-series는 release/workshop-items.json과 실제 공개 범위·소유권·금지 여부·children·DLC 목록을 비교합니다. API 읽기 검증은 모두 통과했지만 모음집 웹 워크플로 완료를 대신하지 않습니다.

새 법적 약관 동의가 요구되면 도구가 멈추며 사용자가 직접 처리합니다. 이번 업로드에서는 새 동의가 요구되지 않았습니다. 미리보기는 tools/New-WorkshopPreview.ps1로 만든 자체 타이포그래피이며 외부 게임 이미지나 제삼자 DLL을 재배포하지 않습니다. 버전/검증/미구현 범위는 README·STATUS·TESTING 및 각 영어/한국어 게시 설명에 명시합니다.
