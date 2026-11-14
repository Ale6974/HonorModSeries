# M2.4 실제 조립 감사 — Preview 0.3.119 / 2026-11-14

## 혼합 소비의 실제 실행·저장·복구

새 WorkStorageMixtures/StorageMixtureObservation은 원 선반의 마지막 양성 수량 출력과 같은 원 물품·actual job.placedThings의 정확한 요구량을 body 전에 StorageMixturesV16으로 기록한다. 원 StorageV7/count journal/refs는 교체하지 않는다. 일부 입력은 같은 원 refs의 Destroyed/비spawned/holdingOwner없음과 global survey Removed를 확인하고, 살아남는 모든 원 물품은 같은 refs/정의/재료/셀/회전/체력/품질과 정확한 잔여 수량을 별도 After에 저장한다. 원래부터 입력이 아니던 물품도 그대로 포함한다.

StorageMixtureV16은 마지막 count output과 첫 membership 변화만 연결한다. 새 명시 before 증거 없는 old V13/V14를 재해석하지 않는다. 실제 출력·새 현재 full-party 동의·SetFaction/독립 정상 반환/검증·도시/최종 receipt·Scribe/load/명시 복구에 조립했다. 이후 보관 membership 변화에도 남은 원 물품의 숫자/물리를 검증한다. 추가 부분 소비를 원 증거 rebase로 처리하지 않는다. 원 실제 소속 호출 승인 당시 membership prefix가 없었다면 새 mixture marker를 그 과거 승인에 끼워 넣지 않는다.

## 전량 직접 합치기의 실제 실행·저장·복구

새 WorkStorageMerges/StorageMergeObservation은 기존 두 native TryDropCarriedThing observer를 사용한다. 새 scope는 Direct/운반 물품 전량/원 비연결 선반/유일한 같은 정의·재료 대상/같은 체력·품질/충분한 stackLimit/같은 실제 타입의 기본 Thing 또는 comp 없는 ThingWithComps에 한정한다. 부분 합치기·체력 가중 평균·comp callback 권한을 추가하지 않는다.

body 전에 StorageMergeV17에 같은 원 선반 전체 physical scope, 원 또는 직전 양성 stack counts, 원 actual calls, 다른 원 운반 입력 ID·정확한 수량·physical proof를 저장한다. 입력 ID는 원 seal 또는 이전 양성 Added여야 한다. transient same-call cargo witness는 중복 deep-save하지 않는다. native bool 정상 반환 후 true라면 같은 실제 cargo refs의 파괴/비spawned/holdingOwner없음과 정확한 Removed, Added없음, 대상 하나의 정확한 수량 증가/다른 원 physical 불변을 함께 검증한다. false는 같은 운반 입력·원 선반 physical 불변을 검증할 뿐 success root를 만들지 않는다. 불명 body/output은 raw를 보존하고 replay하지 않는다.

StorageMergeEventsV17/version5 actual root와 reference-only 원 선반/물품 refs를 실제 remaining SetFaction/새 full-party 동의/도시/최종 receipt/Scribe/load/명시 복구로 연결했다. StorageMergeCallV17은 당시 실제 호출 승인 prefix만 고정하고 뒤의 수량 변화는 최신 동의를 요구한다. recovery는 합치기/이동/수량 setter/HP writer/재생성/health reset을 호출하지 않는다. 원 stock proof는 덮어쓰지 않는다.

## 보존·검사·제한

old null JSON/API/root1~4 provenance/unknown call guards를 유지한다. 원 계약·채무·실적·제삼자 권리·주민/의료·native player control은 변경하지 않는다. normal return과 actual 원 refs 없는 결과를 목표 소속/개수만으로 완료하지 않는다. 새 fields는 before body에 명시적으로 기록한다. 새 cargo refs를 원 물품 refs로 바꾸지 않는다.

규칙 검사: 원 2종 물품의 부분 소비→하나는 완전 소모/다른 하나 잔존→새 보관 물품→최종 Applied 저장/복원; whole-cargo native merge 실패/성공/연속 증가→과거 실제 호출 승인 prefix 보존→최종 Applied 저장/복원. 잘못된 before/수량/체력/입력 ID·뒤늦은 증거·실패한 내려놓기·unknown call/marker 변조를 거부한다. 원/positive lineage 없는 운반 물품을 채택하지 않는다.

현재1.7.5005 DLL SHA256 `82387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125` 일치. 기존 carry/Thing native 몸체와 새 metadata-read ThingWithComps/ThingUtility를 확인했다. TryAbsorbStackNumToTake는 stackLimit/현재 수량만 읽는다. 실제 source는 base/dispatch CanStackWith/TryAbsorbStack와 CarriedThing/AllComps/physical getters/보조함수 미패치를 요구하며 comp 없는 대상만 허용한다. 같은 HP로 native 가중 HP가 달라지지 않는 결과만 받아들인다. unknown1.6 native body 권한은 여전히 보류한다.

M2.4 전체 미완료. 실제 pickup, partial merge/split/comp items, mixture 이후 추가 수량 소비·새 보관 물품의 count history, UFT/다른 반복 모드·연결 설비, 역할/개인 권리·부모 follower/정치/후손 조립이 남았다. 기존25–40%/3묶음·여섯 단계의 과거 수치는 현재 측정값으로 재산정하지 않았다. M1 10/10,M2 0/18,전체10/100 유지. Preview Steam/자동 예약 없음.
검사·설치: 양 타깃 전체 빌드와 최종 native compiler 경고/오류0, 최종 Honor20093/Contracts6141/ENKO624, 양 runtime allowlist/패키지 검사 통과. `artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.119.zip` SHA256 `ACB3FFBE30C7C0D8E01895938F87447CC688CE2C5AD1B78764F63E92C8D8DA26`. 이전 로컬 Preview는 `.local/contracts-preview-5886a5a60ba048cda15151a44fc6e021`에 백업 후 최종32파일 일치 설치, 활성화/언어 hash 불변. 다음 착수는 native whole-item pickup의 동일 객체/실제 carry owner/반환값/보관 membership·예약 이력 연결이다. unknown started native call 재실행·구 snapshot 교체 없이 이어간다.