# M2.4 — 인계 전 프레임 완공과 실제 후계 재산의 별도 인계

2026-11-14 / Preview0.3.110. 원 standalone map25/clinical16/v3의 지원 기본 건물 경로를 실제 연결했다. M2.4 전체 완료 판정은 아니다.

## 원 프레임·새 건물·원 호출을 분리

원 소유 Frame의 기존 asset call이 **시작되지 않았고**, 같은 원 Map/Frame/정의·stuff·칸·회전에서 native CompleteConstruction이 정상 반환한 경우를 다룬다. 기존 frame 사건의 Started/body return 사실은 먼저 유지한다. 정확히 하나의 새 source 소유 기본 Building이 실제 생겼고, 원 entityDefToBuild/물리 위치와 일치하며 검토한 property/registry/권리 경계를 통과해야 한다. native 전후 물체 집합·원 frame/자재 소실·새 ID 출력을 검증한 뒤에만 별도 후계 준비를 저장한다.

후계 건물은 원 Thing 참조로 다시 연결하지 않는다. 원 임상/지도/재산 snapshot과 호출 기록은 그대로다. 새 `WorkAssetResolutionSave`는 원 transfer hash, 동결한 원 asset-call prefix, 원 asset ID→실제 object ID, 양성 완공 사건 index, 새 configuration/physical 원문, 별도 Started/Returned/Verified/승인 ID를 보관한다. 원 프레임 call은 계속 미실행 상태이며 반환으로 꾸미지 않는다.

## 실제 현재 승인 → native 인계 → 저장·중단 복구

1. 새로운 source 소유 건물은 해당 준비의 **같은 실제 Thing 참조**만 현재 survey에 포함한다. 임의 source 물체를 소유권만 보고 허용하지 않는다. 후계 준비 원문 `ResolutionsV5`를 현재 전체 당사자의 별도 행동 승인에 묶는다. 기존 계약·채무·제삼자 권리는 유지한다.
2. 주민·의료·영역 단계가 끝나면 `ResumeAssetResolutions`가 다음 원 asset에 대응하는 실제 물체를 선택한다. 동일 ref/ID/정의·stuff·수량·HP·칸·회전·quality·configuration·현재 권리·검토한 native 경계를 재확인한다. 기존 `SetFaction`을 한 번 호출하고 별도 Started를 먼저, 정상 반환을 즉시, 물리/설정 보존 확인을 그 뒤에 독립 기록한다.
3. 호출이 Started만 있으면 보류하며 replay/target-owner 완료를 하지 않는다. 양성 Returned 뒤 보존 확인만 남은 경우에는 새 현재 승인으로 **재호출 없이** 확인을 마칠 수 있다. 한 단계 뒤 변경된 장부/survey에는 새 현재 승인이 필요하다.
4. 앞 프레임의 소실 때문에 옛 순차 call prefix가 막혀도, 그 뒤의 **변경 없는 원 기본 재산**은 별도 동일-object call로 이어간다. 원 snapshot/ref/physical/configuration과 같아야 하며 새 준비·호출 원문을 별도 장부에 남긴다. 옛 asset journal은 더 이상 바꾸지 않는다. 여러 후계가 생기면 각기 다른 실제 객체/완공 사건과 연결한다.
5. 원 call의 양성 반환 또는 별도 실제 물체의 Returned+Verified가 **모든** 원 asset을 설명해야 마지막 원 도시 인계가 가능하다. 최종 WorkCompletion 영수증은 원 AssetCalls와 별도 ResolutionsV5를 함께 담고 Confirm/Restore/실제 native journal 연결을 검증한다.

## 지속성·불확실한 상태

별도 ref wrapper만 Scribe deep-save하고 실제 Thing은 reference-only로 저장한다. native Map이 유일한 Thing deep-save 소유자다. 로딩/명시 복구는 동결된 원 asset-call prefix, 원 완공 사건, 실제 ref 목록/순서, 현재 owner, configuration/physical, 각 호출의 실제 승인 archive를 확인한다. 최종 양성 완료 후 추가 actual ref만 해제하고 원문·ID·이력은 유지한다. 일반 legacy Recover로 별도 승인 조건을 우회하지 않는다.

관측 출력이나 새 객체가 불명확한 경우, 원/새 객체가 사라지거나 교체된 경우, 새 건물이 준비 후 손상·변경된 경우에는 원문을 유지하고 보류한다. 과거 미기록 완공을 소급하거나 tile/def가 비슷하다는 이유로 다른 건물을 골라 넣지 않는다. 새 ledger는1000개/3M 및 원 transfer/approval 기존 용량 경계 안에서만 추가한다.

## API 근거·검사

설치5005 DLL SHA256 `82387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125`, MVID `3523c04c-c298-43ae-9571-20caaef22acc`는 기존 검토와 동일하다. Frame.CompleteConstruction의 원 자재 소모/Destroy 후 실제 building 생성·원 faction/stuff/위치·회전·HP/스타일 적용 흐름을 다시 읽었다. 새 native writer는 검토한 기존 Thing/Building.SetFaction뿐이며 ThingMaker/GenSpawn/건설/health setter를 이 코드에서 실행하지 않는다. 새 native 실행은 기존 audited5005 gate만 사용하고 미확인1.6 본문 권한은 추가하지 않는다. 게임 원문은 ignored .local 밖에 게시하지 않는다.

양 타깃 전체 빌드·최종 native 컴파일, Honor20093/Contracts5962, ENKO607 기준. 완료 사건/새 ID 연결·호출 미시작 요구·범위 변조 거부·미확인/미검증 결과 거부·옛 동의 거부·동결 prefix 불변·새 영수증 round-trip과, 소실된 프레임 뒤 원 재산을 별도 인계해 전체 완료하는 순수 fixture를 추가했다. 실제 패키지/설치는 STAGE1-HANDOFF에 기록한다.

## 남은 범위

이번 후계 실행은 검토한 기본 Building 및 변경 없는 기본 원 재산이다. terrain 완공, 저장/작업대/전력·시설·사유/군사 등 특수 건물, 후계 자체가 다시 소실·변형되는 연속 사건, 인계가 시작됐으나 반환이 불확실한 프레임은 추가 실행 권한으로 추정하지 않는다. 이런 구조와 더 넓은 driver/역할·권리 부품, 부모 follower/정치·후손/M2.10 조립이 남아 있다.

M2.4 전체 미완료. 잔여 노력 계획 추정25–40%/큰3묶음(연속·특수 재산과 작업 확대 / 역할·개인 권리 / 부모·후손 통합). M1 10/10,M2 0/18,전체10/100 유지. 다른 여섯 단계의 과거 잔여 추정은 재측정하지 않았다. 공개 main만 게시하며 Preview Steam·예약 없음.
