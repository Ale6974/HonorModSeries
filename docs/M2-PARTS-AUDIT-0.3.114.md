# M2.4 — 지형 공사가 함께 제거한 원 오물의 실행·저장·복구

2026-11-14 / Preview0.3.114. 원 공사 입력 이외의 소실 때문에 막히던 일반적인 native terrain 공사 경로를 연결했다. 전체 M2.4 완료는 아니다.

## 원 범위와 호출 직전 상태

새 NativeWorkFrame.FilthV10은 원 지형 프레임 칸에 있던 중립 native Filth의 ID/정의/칸/전체 상태 proof를 원 map snapshot에 결합한다. 원 LoadedMapRecord.Things의 실제 참조를 그대로 사용한다. source 시점 proof는 덮어쓰지 않는다. 호출 직전의 같은 원 filth 참조/정의/칸을 확인하고 현재 두께·sources·growTick·disappearAfterTicks·그림 상태·기본 물리·native home-list membership proof를 WorkProgressEvent.FilthV10에 따로 기록한다. 자연히 달라진 원 물체의 현재 상태는 별도 증거이며 원 상태의 재작성/복원 권한이 아니다.

source 목록과 현재 목록이 완전히 같아야 한다. 새로 생긴 오물·다른 칸/정의·대체 객체·임의 파생 클래스·quest/사유 물체·다른 holder는 허용하지 않는다. native Map 소유, 정확한 Filth/정의 타입·등록, bounded sources/draw data와 유한 좌표·기본 getter/ID/파괴·deSpawn/RemoveAllFilth/native registry 경계의 미패치를 확인한다. native home filth registry도 같은 Map의 정확한 list이어야 하며 중복 membership은 보류한다. derived registry를 재구축하거나 고치지 않는다.

## 실제 native 실행과 양성 출력

기존 실제 BeginWorkObservation이 원 주 입력(frame/resources)과 별도 ancillary filth를 함께 시작 기록으로 저장한다. 이 코드는 공사/청소를 실행하지 않는다. 기존 게임 Frame.CompleteConstruction → FilthMaker.RemoveAllFilth → inherited Thing.Destroy/Filth.DeSpawn의 자연 실행을 관측한다.

정상 Frame 본문 반환을 먼저 기록한 후 같은 원 filth가 실제 Destroyed/비spawn 상태이고 native home registry에서 제거됐는지 확인한다. 전체 지도 전후 ID 집합과 원 주 입력+ancillary 입력 **전부**의 소실을 검증한 뒤에만 검증된 반환을 기록한다. 일부 제거·추가 미확인 소실·예외·registry 불일치는 원 시작/본문 반환을 그대로 두고 보류한다. missing object나 대상 소유권만으로 완료하지 않고, 불확실한 호출을 재실행하지 않는다.

순수 WorkProgress 규칙은 새 명시 원 frame tag가 있을 때에만 ancillary 입력을 허용한다. 옛 null 원 범위·다른 job kind·중복/다른 ID·정의/칸·잘못된 proof·주 입력과 중복을 거부한다. 새 tag가 있는 입력은 시작 때 생략할 수 없다. 기존 v1/v2/v3의 원 호출/주민/재산 집행 조건은 그대로이며, 새 원 tag가 별도로 있는 ancillary 관측만 추가한다. v1/v2에 새 작업 grant/haul authority를 부여하지 않는다. 실제 terrain/property 후계 집행은 기존 각 scope 제한을 유지한다.

## 현재 승인·저장·명시 복구·도시 완료

원 FilthV10과 호출 직전 FilthV10, 양성 본문/검증 반환·소실 목록이 원 WorkProgress archive에 함께 보존된다. 기존 native Scribe/로딩 검사/현재 full-party 동의/ResolutionsV5/지형 수락/마지막 도시 SetFaction/WorkCompletion receipt로 이어간다. 원 지형/호출/계약·채무·제삼자 권리·주민/의료·플레이어 통제를 바꾸지 않는다. 승인 화면에도 제거가 포함된 원 오물 개수와 재청소/재생성하지 않는다는 안내를 EN/KO로 표시한다.

파괴된 원 filth를 다시 deep-save/재생성하지 않으며 hash proof를 게임 물체로 복원하지 않는다. native Map이 실제 물체의 유일한 저장 소유자이고, 별도 증거는 기존 원 map/event JSON에 보존된다. 예전 null JSON/hash/API 호출은 그대로다. 앞서 원 tag 없이 불확실해진 옛 사건에 새 필드를 채워 반환으로 바꾸지 않는다.

## API 근거와 검사

현재5005 DLL SHA256 `82387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125`가 기존 검토와 같다. Filth/FilthMaker/Thing/ListerFilthInHomeArea 전체 metadata를 읽어 저장 필드·실제 RemoveAllFilth 선택과 native Destroy/DeSpawn·registry 제거 흐름을 확인했다. 게임 원문은 ignored .local에만 보관한다. 새 native 관측은 기존5005 gate만 사용하며 미확인1.6 본문을 허용하지 않는다.

fixture는 원/현재 proof 분리, 누락/대체/중복 입력·다른 job/옛 scope 거부, 본문 반환 없이 소실을 완료할 수 없음, 일부 제거/미확인 물체 소실 거부, 전체 정상 제거와 지형 수락/최종 도시 receipt 저장 round-trip, 원 call 미시작 유지와 옛 null bytes를 검사한다. 양 타깃 전체 빌드 및 최신 규칙/최종 native 컴파일 경고·오류0, Honor20093/Contracts6050/ENKO617, 양 타깃 package allowlist와 백업 후 로컬32파일 일치·활성화/언어 hash 불변을 확인했다. 패키지 hash·백업 위치는 STAGE1-HANDOFF에 기록한다.

## 남은 범위

새로 생성된 ancillary 물체·다른 추가 출력, 연속 후계 소실/지형 변화·보관 물품 소비/교체, 연결 shelf group/전력·연료·시설, 더 넓은 작업/특수 역할·개인 권리 및 부모 follower/정치·후손의 실제 조립은 남아 있다. 일부 일반적인 추가 소실을 연결한 것으로 M2.4 전체를 완료로 판정하지 않는다. 기존 잔여 노력 추정25–40%/3묶음은 이번 부분 변경만으로 재측정하지 않았다. M1 10/10,M2 0/18,전체10/100 유지. Preview Steam·자동 예약 없음.
