# M2.4 — 기초·임시·다리·채색 지형의 실제 완공 수락

2026-11-14 / Preview0.3.113. 기존 일반 바닥 경로의 원 증거·현재 동의·native 관측·저장·명시 복구·최종 도시 인계를 복합 지형의 **해당 공사 칸**까지 확장했다. 전체 M2.4 완료 판정은 아니다.

## 원 프레임과 실제 층을 분리해 보존

새 NativeWorkFrame.TerrainLayersV9는 공사 대상 층(top=0/foundation=1/temporary=2), 원 현재 visible/top/foundation/temporary/color를 기록한다. 원 정의 이름 TerrainV8·프레임/자재·원 지도/임상/호출 scope와 함께 전체 원 지도 hash에 묶인다. 실제 공사 전 원 레이어를 보존하는 원문이며 이후 결과로 덮어쓰지 않는다.

기초·임시·다리 대상이나 이미 기초/임시 층·색상이 있는 칸의 새 준비만 별도 layer 범위를 갖는다. 예전 plain frame은 null 생략과 옛 byte/hash/권한을 유지한다. 옛 범위에는 새로운 겹층/색상 허가를 소급하지 않는다.

검토한 native Frame.CompleteConstruction이 정상 반환하고 원 프레임/자재가 소모됐으며 새 Thing이 없는 경우, 실제 해당 칸의 모든 top/foundation/temporary/color와 visible terrain, under-terrain, roof를 읽는다. 완공한 정의가 원 공사 대상 층에 **실제로** 있어야 한다. 예를 들어 기초 Setter가 오류를 출력하고 원 프레임만 사라졌다면, 원하는 기초가 실제 배열에 없으므로 승인 가능한 완료가 되지 않는다. native 공사가 바꾼 층/색상은 실제 결과로 따로 기록하고 원 상태는 유지한다.

## 실제 승인·집행·저장·중단 복구

실제 결과 WorkTerrainAcceptance.LayersV9를 ResolutionsV5의 현재 전체 당사자 승인에 포함한다. 같은 원 Map/terrain/roof owner, 등록된 정의 참조, 정확한 배열 타입/크기·칸·모든 층·색상·지붕·아래 지형을 현재 동의 검토와 명시 복구 때 다시 확인한다. 다른 셀/층/색상이나 대체 지도에 승인할 수 없다.

기존 실제 ReviewWorkProgress → 현재 full-party 동의 → ResumeAssetResolutions → 별도 Accepted/승인 ID → 마지막 원 도시 SetFaction/최종 WorkCompletion receipt로 실행한다. Accepted는 원 공사가 현재 도시 지형에 남아 있음을 별도로 수락한 기록이다. 건물 호출이 아닌 항목에는 Actual Thing/Asset/Started/Returned/Verified를 만들 수 없다. 지형 setter·채색 setter·재생성·소유권만으로 완료·옛 호출 replay를 추가하지 않는다.

실제 승인 팝업에는 공사 전/관측된 완공의 좌표·목표 공사 층·정의·실제 표면/기초/임시/색상/visible 상태를 영한으로 표시한다. 최대4건을 펼치고 추가 포함 건수를 명시한다. 게임의 현재 언어에 따른 Def label을 사용하며 별도 언어 설정을 만들지 않는다. 화면 표시를 native 권한이나 실행 완료 증거로 사용하지 않는다.

원 프레임 장부와 새 layered 수락은 기존 raw Scribe/로딩 검증/현재 승인 archive/명시 복구/최종 receipt 경로를 그대로 이용한다. 새 native 객체를 deep-save하거나 층 배열을 다시 설치하지 않는다. 원 계약·채무·제삼자 권리·주민/의료/작업·플레이어 통제는 유지한다. 다른 원 재산과 섞인 완료도 모든 원 asset의 양성 반환/별도 실제 인계/지형 수락을 요구한다.

## 구현 부품과 API 근거

- WorkTerrainLayers/WorkTerrainLayerRules: 범위·대상 층·현재 실제 정의·옛 null 모드/새 모드 결합 규칙. API에 map writer가 없다.
- TerrainWorkEvidence: audited5005의 원 source frame과 실제 native terrain 배열/owner/정의/getter를 읽는 재사용 부품. 원 source row와 결과 row의 역할을 구분한다.
- ConstructionEvidence와 WorkAssetResolutions: 실제 원 준비/정상 완공 관측/현재 동의/복구/도시 완료 조립. 같은 helper로 현재 모든 층을 다시 읽어 비교한다.

설치5005 DLL SHA256 `82387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125`는 기존 검토와 같다. 전체 TerrainGrid metadata의 TerrainAt/UnderTerrainAt, top/foundation/temp/color 배열과 owner, Frame의 foundation/temp/top 분기, SetFoundation의 under-grid 거부/임시 층 처리 및 SetTempTerrain의 기존 층·색상 변경 흐름을 읽었다. 실제 읽기는 정확한 native 타입/크기와 미패치 getter를 확인한다. recovery에서 native setter를 호출하지 않는다. 새 권한은 기존5005 gate에 한정하며 미확인1.6 본문을 허용하지 않는다. 게임 원문은 ignored .local 밖에 게시하지 않는다.

## 검사·남은 범위

원/결과 층 불일치·옛 null 권한·빠진 layer 증거·다른 실제 정의/색상·가짜 native 호출을 거부하는 fixture, foundation 수락의 실제 규칙 흐름/현재 승인/archive/전체 마지막 도시 receipt round-trip, top/bridge와 temporary의 목표 층 결합, 옛 null JSON을 검사한다. 양 타깃 전체 빌드·최종 native 컴파일 경고/오류0, Honor20093/Contracts6026, 최종 ENKO616, 양 타깃 패키지 allowlist와 백업 후 로컬32파일 일치·활성화/언어 hash 불변을 확인했다. 패키지 hash·백업 경로는 STAGE1-HANDOFF에 기록한다.

현재는 기존 zero-new-Thing/원 입력 소실 조건을 유지한다. 필스/추가 물체 등 다른 자연 출력, 완공 후 다시 일어난 지형/색상 변화·연속 소실, 연결 선반 그룹·전력/시설 등 특수 재산, 역할·개인 권리, 부모 follower/정치·후손의 실제 조립은 남아 있다. 부품 확장이 이 전체의 완료를 대신하지 않는다. M2.4 전체 미완료이며 기존 잔여 노력 추정25–40%/3묶음은 이번 부분 변경만으로 재측정하지 않는다. M1 10/10,M2 0/18,전체10/100 유지. Preview Steam·자동 예약 없음.
