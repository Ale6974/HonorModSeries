# M2.4 실제 조립 감사 — Preview 0.3.123 / 2026-11-18

## 실제 실행 경로

새 null-omitted `QuantityV21=1` before marker가 있는 `StorageContinuationV20`의 split/partial-merge만 허용한다. 옛 take/merge/put에는 이 marker를 허용하지 않고 옛 output을 새 권한으로 해석하지 않는다. 직전 동일 선반 V19/V20 양성 결과·원 calls·전체 original physical/수량을 유지한다.

추가 split은 같은 원 선반 물품을 positive partial request로 집는 실제 int TryStartCarry hook에 연결한다. 독립 body/int 반환 후 원 물품은 같은 refs로 남아 정확한 taken만큼 감소하고 별도 native-created carried Thing이 생겨야 한다. seal/과거 Added ID 재사용, 원 ID Removed, 새 ID Added 누락, 변경된 HP/품질/회전 등은 거부한다. V19 root 및 이전 calls는 유지한다.

지정 partial-merge는 actual TryDropCarriedThing의 count overload/Direct 경로로, 0 < request < carried.Count이고 유일한 original target의 여유가 request 이상인 경우다. 같은 basic native type/def/stuff/HP/quality, 빈 comps, unpatched dispatch/helper를 요구한다. true native body 반환 이후 SAME carried input이 같은 native owner에 Count-request로 남고 SAME target이 정확히 request만큼 증가해야 한다. global Added/Removed는 없다. count overload가 내부 임시 조각을 만들어 전량 흡수하는 native 경로를 관측하며, survey에 없는 임시 ID를 만들어 기록하지 않는다. whole request의 capacity-limited 부분 실패/overflow, comp callback merge, 다른 holder/ground는 이번 범위가 아니다.

## 저장·중단 복구 조립

추가 split의 실제 cargo refs는 `QuantityCargoV21`에 순서대로 reference-only Scribe한다. 원 `SplitCargoV19`와 original map refs는 그대로다. native carry/map가 유일한 deep-save owner다. 새 목록은 새 split 개수와 exact pairing되며 missing ref는 같은 original ID의 나중 양성 Removed가 있을 때만 허용한다. native ref를 생성·교체·재바인딩하지 않는다. partial-merge는 같은 carried ref를 유지하므로 새 ref를 추가하지 않는다. terminal은 extra refs만 해제한다.

실제 before→독립 body/count/bool→전체 현재 출력→append-only actual resolution 및 refs의 동시 commit→기존 최신 full-party 동의/actual SetFaction·정상 반환·검증/도시·최종 receipt→Scribe/load/explicit recovery에 연결했다. 기존 V20 predecessor/complete coverage/StorageContinuationCallV20 승인 prefix가 새 quantity 이벤트도 포함한다. 원 clinical/census/의료·수술/계약·채무·제삼자 권리/native player control은 그대로다. unknown start/false/partial 출력은 raw 보존하며 replay/owner만으로 완료하지 않는다.

## 코드·규칙·설치 API 검사

통합 검사는 기존 V19/V20 이후 새 분할→부분 합치기2회→나머지 전량 합치기→추가 분할→원 기록 보존/JSON 재직렬화·복원→최종 Applied 장부 export/restore까지 연결한다. 명시 marker 누락, 잘못된 request, 새 ID 누락/과거 ID 재사용, partial merge에서 input 소멸 주장, native 반환 전 append를 거부한다. EN/KO 검토 안내도 연결했다.

현재 설치된 게임은 **1.7.5011 rev24**이며 Assembly-CSharp SHA256 `7FBCE7A5B2A26CAC500150D3CFF408CDBBD8F1797456BC741252E44C2D27BDC4`로 이전5005와 다르다. 새 current ThingOwner metadata read SHA `98CDB8369EE0CD7BF6FF07E049D9EB2000EF7F48C059B405FFAA255F0D866AAA`는 기존5005 전체 클래스 감사와 일치한다. 해당 count overload body는 원 carried의 SplitOff(count), true GenDrop이면 반환, false면 원으로 reabsorb한다. 이것만으로 전체5011 호출 그래프가 승인된 것은 아니다. NativeTransferBodies whitelist는 변경하지 않았다. 새 work 실행은 기존 audited5005 MVID 범위이고5011/미확인1.6 bodies는 계속 보류한다. 버전 compile 성공을5011 native 실행 증거로 부르지 않는다.

설치 Harmony의 parameterless PatchAll 경고에 따라 여섯 모듈이 자신의 typeof(Mod).Assembly를 명시하도록 변경했다. 자동 호출자 assembly 추론을 사용하지 않으며 새 native 권한은 추가하지 않는다.

## 남은 완료 조건

M2.4 전체 미완료. capacity-limited 자동 부분 합치기, 일반 ground/복합 holder, V13/V16 등 다른 stock protocol 혼합, comp merge/UFT/기타 반복 모드·연결 시설, 역할/개인 권리·부모/follower/정치/후손 조립이 남았다. 현재5011 전체 body 재감사도 별도 미완료다. M1 10/10,M2 0/18,전체10/100 유지. 과거 노력 %는 최신 측정값이 아니다. Preview Steam 게시/자동 예약 없음.
최종 검사·설치: 양 타깃 전체 build 및 최종 native compile 경고/오류0. Honor20093/Contracts6206/ENKO628, XML/두 runtime allowlist/패키지 검사 통과. 패키지 `artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.123.zip` SHA256 `820FCA585972EB6B12B4811938C59E07426D85B9F1F7E344CACB0A31C67FE435`. 기존 다섯 모듈은 `.local/install-20261118-023307-774a4f82`, Preview는 `.local/contracts-preview-cc3d92894d9140a2b47146d96ccf1cfc`에 백업 후 설치했다. 다섯 모듈 전체 파일/Preview32파일 일치, ModsConfig/Prefs activation·language hash 불변. 설치된5011에 대한 work/royal native whitelist를 열지 않았다는 제한은 유지한다. 다음 착수점은5011 전체 body 감사와 capacity-limited 자동 부분 merge/다른 stock protocol 조립이며, 원 이력·unknown calls는 보존한다.