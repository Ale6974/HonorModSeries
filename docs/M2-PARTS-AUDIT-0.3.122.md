# M2.4 실제 조립 감사 — Preview 0.3.122 / 2026-11-15

## 새 실제 경로

`WorkStorageContinuation`/`StorageContinuationObservation`은 최초 V19 분할 기록을 유지한 채 별도의 V20 before/after를 추가한다. 직전 동일 선반의 양성 V19/V20 sequence, 전체 원 물품 physical/수량, 원 faction-call prefix를 작업 전에 고정한다. 중간 출력 누락·다른 predecessor·뒤늦은 before·미확인 반환은 거부한다. 기존 V18/V19 또는 이미 실행된 호출을 새 권한으로 승격하지 않는다.

- take: 원 선반에 남은 SAME original Thing 전량 요청. 독립 native body 정상 반환/int taken 후 같은 실제 객체가 같은 원 Pawn native carry owner에 있어야 한다. 선반에서만 빠지고 global Added/Removed는 없다.
- merge: 앞서 확인한 native split 또는 original carried input 전량을 같은 원 선반의 유일한 호환 원 더미에 재합친다. 실제 true placement 반환, 같은 cargo의 Destroyed/unspawned/no-holder, exact global Removed 하나, 원 target count의 정확한 증가가 필요하다. 기본 같은 타입/def/stuff/HP/품질·빈 comps·stackLimit만 지원한다.
- put: 전량 집기한 SAME original Thing이 같은 선반의 허용 셀로 실제 복귀해야 한다. 새 split clone을 original ref로 바꾸지 않는다. global Added/Removed 없이 같은 native ref/map/current shelf membership과 physical을 확인한다.

모두 복구에서 native pickup/place/split/absorb/생성/수량·HP setter를 호출하지 않는다. native 정상 body→독립 반환 기록→현재 출력 검증→별도 actual resolution append를 기존 controller에 연결했다. 원 V7/원 physical/census/계약·채무·제삼자 권리·주민 의료/native player control은 변경하지 않는다.

## 저장·중단 복구

V20는 원 WorkProgressV1/WorkAssetResolutionV5 raw JSON에 null-omitted로 저장되어 기존 Scribe/load/explicit recovery와 연결된다. continuation sequence는 원 V19 마지막 양성 출력부터 순서대로 연결되며 complete 검사는 모든 양성 V20의 actual resolution 반영을 요구한다. StorageContinuationCallV20은 actual SetFaction 당시 prefix만 고정한다. 호출 뒤 새 관측은 과거 승인을 바꾸거나 반환된 호출을 재실행하지 않는다. 최신 작업/인계·도시·최종 receipt는 새 full-party 동의를 요구한다.

새 alias/deep-save owner를 만들지 않는다. 원 저장 refs/LoadedMap.Things와 V19 SplitCargoV19 refs를 그대로 사용한다. carried original은 native carry가, 복귀 물품은 native map이 실제 객체를 저장한다. 사라진 split refs는 나중 양성 Removed가 있을 때만 허용한다. unknown started/native false·부분 출력은 raw 보존하며 성공으로 처리하지 않는다.

## 검사와 한계

규칙 통합은 반복 partial→같은 선반 whole restack→원 잔량 whole pickup→same-ref put-back→현재 승인→최종 Applied 장부 export/restore를 검사한다. 과거 split call approval prefix가 뒤 관측으로 바뀌지 않는지, before/quantity/Removed/sequence/누락 split/oversized prefix 변조와 미확인 출력이 거부되는지도 검사한다. EN/KO 검토 화면에 후속 작업 건수를 추가했다.

설치 DLL SHA256은 기존 audited5005 `82387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125`와 일치한다. 기존 native int TryStartCarry/Thing/ThingOwner/ThingWithComps/ThingUtility 관측·metadata audit 범위만 재사용하며 새 API/body 권한을 추측하지 않는다. unknown1.6 native bodies는 계속 보류한다.

M2.4 전체는 미완료다. 일반 ground/복합 holder, continuation 뒤 추가 partial pickup·partial merge·다른 stock protocol 혼합, comp merge, UFT/기타 반복 모드·연결 시설, 역할/개인 권리와 부모/follower/정치/후손 조립은 이 변경으로 완료되지 않는다. M1 10/10,M2 0/18,전체10/100 유지. 과거 노력 추정 %는 현재 완료율이 아니다. Preview Steam/자동 예약 없음.
검사·설치: 양 타깃 전체 빌드/native compiler 경고·오류0, 최종 Honor20093/Contracts6187/ENKO627, XML/두 runtime allowlist/패키지 검사 통과. `artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.122.zip` SHA256 `EBB11F9F203E70D9AB41BFF1EE38DBD2FDB3B0B294E081ED2C7B0E10797C91E7`. 이전 로컬 Preview는 `.local/contracts-preview-8d59d12e18484e81bfde69ea4894b166`에 백업하고 최종32파일 일치 설치했다. 활성화·언어 hash 불변. 다음 착수점은 continuation 뒤 추가 분할·부분 합치기와 일반 ground/복합 stock protocol이며, 원 기록을 rebase하거나 unknown 호출을 replay하지 않는다.