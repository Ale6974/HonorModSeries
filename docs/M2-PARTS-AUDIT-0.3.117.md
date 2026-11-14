# M2.4 부품·조립 감사 — Preview 0.3.117 / 2026-11-14

## 실제 부분 소비 경로

원 비연결 선반의 기본 중립 물품이 같은 ID/같은 실제 Thing으로 남으면서 native 제작에 일부 소비되는 경우를 연결했다. 원 Stock/StorageV7/물리 기록·참조는 덮어쓰지 않는다. 대상은 원 소속 호출 전 .116 StorageOriginsV12로 처음 확인한 선반이며 이후 같은 원 물품의 연속 부분 소비도 보존한다. 새 수량은 복원 명령이 아니라 관측 증거다.

1. `StorageStackObservation.CaptureStorageStacks`가 native 제작 직전 원 선반/원 물품/원 settings·cells·membership과 실제 job.placedThings의 요구량을 읽는다. 양수이고 원 수량보다 작은 요청만 지원한다. 중복 입력/완제품 작업물/완전 소모/이동/보관 조합을 허용하는 추정은 없다. getter 미패치와 감사한5005 경계를 요구한다.
2. `WorkProgress.BindStorageStacks`는 native body 전에 새 `StorageStacksV13`의 원 물리 목록/정확한 Uses를 기존 raw Scribe 필드에 저장한다. 이미 BodyReturned/Returned인 이벤트에 추가하거나 before를 나중 출력으로 교체할 수 없다. 최초 before는 원 scope, 후속 before는 직전 양성 after와 같아야 한다.
3. 실제 정상 body return 뒤 같은 원 Thing refs의 물리 상태를 재확인한다. 원 ID/정의/재료/셀/회전/체력/품질/membership은 같고 Count만 정확한 요구량만큼 줄어야 한다. 증가·과소/과다 소비·원 물품 완전 소실·다른 필드 변경은 raw Started/BodyReturned를 보존하고 중단한다. 모르는 호출은 replay하지 않는다.
4. `WorkAssetResolutions.AppendStacks`는 새 version3 실제 재산 root 또는 기존 원 재산 항목에 순서가 있는 `StorageStackEventsV13`를 추가한다. frame 완료는 필수 조건이 아니다. 원 선반/원 물품 참조를 그대로 사용하며 별도 deep-save를 만들지 않는다. 각 실제 관측의 현재 선반 소속·물리 상태도 검증한다.
5. 새 현재 full-party 동의→실제 SetFaction/정상 반환/검증→도시→최종 receipt→Scribe/load/명시 복구에 공통 경로로 연결한다. `StorageStackCallV13`은 실제 소속 호출 당시의 증거 prefix만 고정한다. 이후 변화는 최신 동의가 필요하며 과거 호출 승인/출력을 덮어쓰거나 호출을 반복하지 않는다.
6. 로드 후에도 같은 원 refs/기록/현재 수량을 읽어 검증한다. 수량 setter, SplitOff, ConsumeIngredient, 운반/재생성/health reset은 관측·복구 코드에서 호출하지 않는다. 원 계약·채무·실적·제삼자 권리·주민 의료·native player control은 그대로다. old null JSON/기존 API/version1·2 provenance 규칙은 유지한다.

## 코드·검사

새 규칙 부품 `WorkStorageStacks.cs`, 실제 관측 부품 `StorageStackObservation.cs`, 공통 작업 journal/실제 재산 호출/현재 승인 UI에 조립했다. 최초 소비·정확한 요구량·다른 필드 변조 차단·미확인 호출 차단·동일 ID 연속 소비·과거 실제 호출 prefix·후속 frame 조립·최종 Applied 장부의 저장/복원·기존 null 필드 생략을 규칙 검사했다.

설치1.7.5005 DLL SHA256 `82387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125` 일치. 기존 native Toils_Recipe.CalculateIngredients의 부분 SplitOff 경로와 실제 설치 DLL ThingCountClass.Count의 read-only countInt getter를 metadata로 확인했다. 관측 코드는 getter에 대한 다른 패치가 있으면 거부한다. 새 실행은 기존 감사5005 범위만 사용하며 unknown1.6 native body에는 권한을 부여하지 않는다.

## 남은 범위

수량 증가/운반 합치기, count 변화와 membership 교체의 복합 조합, 원 호출이 이미 반환된 미기록 선반, 집기 관측, 제작 주문 자체의 진행/반복 상태, 연결 설비·역할/개인 권리, 부모 follower/정치/후손 조립은 미완료다. M2.4 전체 완료 아님. M1 10/10,M2 0/18,전체10/100 유지. 기존 잔여25–40%/3묶음 추정은 이번에 재측정하지 않았다. Preview Steam 게시/자동 예약 없음.
검사·설치: 양 타깃 전체 빌드/최종 native 컴파일 경고·오류0, Honor20093/Contracts6089/ENKO620, 두 runtime DLL allowlist/패키지 검사 통과. `artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.117.zip` SHA256 `5E875D4508AB2AC23AB10200D54A1574B14E0CB6FC9970B7A1522B310548719C`. 이전 로컬 Preview는 `.local/contracts-preview-8dc112d608fe4653b1c08839adea3ff3`에 백업한 뒤32파일 일치 설치했다. 활성화·언어 설정 hash 불변. 다음 착수는 부분 소비 이력과 이후 membership 변화의 안전한 조합 또는 실제 집기 관측이며, 기존 before/after/호출 증거를 새 범위로 소급 확대하지 않는다.