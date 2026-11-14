# M2.4 부품·조립 감사 — Preview 0.3.116 / 2026-11-14

## 실제 추가 경로

- `WorkStorageOrigins.cs`: 원 선반·설정/물리·중립 원 물품 전체 hash와 원 재산 호출 prefix를 독립적인 작업 전 증거로 검증한다. 순서/중복/소유/연결 그룹/이미 시작한 호출/호출 역행은 거부한다.
- `StorageOriginObservation.cs`: 실제 BeginWorkObservation에서 원 Map/선반/물품 참조와 설정·셀·물리 상태를 확인한다. 정확한 기본 중립 물품을 보관하고 원 호출이 아직 시작되지 않은 비연결 원 선반만 기록한다. 기존 추적 선반은 이전 연속 이력을 사용한다.
- `WorkProgressEvent.StorageOriginsV12`: native 작업 전에 저장되는 별도 null-omitted 증거. 원 장부를 바꾸거나 뒤늦은 현재 상태를 원본으로 꾸미지 않는다. 기존 API/구버전 null 직렬화는 유지한다.
- `StorageProgressObservation.cs`: native 정상 body return과 전후 실제 소실/출력 후, 양성 제작/성공한 내려놓기의 membership 변화만 새 독립 version2 장부를 시작한다. 원 StorageV7/원 물품 참조와 새 현재 내용물 참조를 별도로 보존한다. 새 문서/참조는 함께 확정한다.
- `WorkAssetResolutions.CreateStorage`: 프레임 완료 없이도 새 원 선반 provenance로 실제 재산 인계를 시작한다. version1의 프레임 provenance 요건은 그대로다. 후속 frame successor/원 나머지 재산도 공통 장부에 추가할 수 있다.
- 기존 현재 full-party 승인/실제 SetFaction의 Started·Returned·Verified/도시 호출/최종 receipt/Scribe·로드·명시 복구 경로에 조립했다. 과거 호출 승인 prefix/원 임상·계약·채무·제삼자 권리/native player control을 유지한다. 모르는 Started 호출을 다시 실행하거나 target 소속만으로 완료하지 않는다.
- EN/KO 실제 승인 화면에 작업 전 원 선반 증거 건수를 표시한다.

## 검사 범위

원 선반 독립 root 생성(프레임 이벤트 없음), 시작만 있고 출력 없는 거부, 변조된 원 물품 hash/원인 없는 구 기록 거부, version1 위장 거부, 실제 호출 순서/재실행 차단, 이어진 frame 조립과 최종 Applied 저장·복원 규칙을 검사했다. 네이티브 실행은 기존에 감사한 1.7.5005 경계만 재사용하며 DLL SHA256 `82387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125`가 일치한다. 새로운 native API 권한을 만들지 않았다. unknown1.6 native body는 계속 보류한다.

## 미완료

원 호출이 이미 반환된 미기록 선반, 동일 ID의 수량만 바뀐 최초 변동, 집기/이동 관측, 제작 주문 자체의 진행·반복 상태, 복합 연결 설비·역할/개인 권리·부모 follower/정치/후손 조립. 원 설정/셀·객체가 사라지거나 바뀌면 raw 증거를 보존하고 중단하며 재생성/운반/초기화하지 않는다. M2.4 전체 완료 아님. M1 10/10,M2 0/18,전체10/100 유지. 이전 잔여25–40%/3묶음은 새로운 측정값이 아니다.
검사·배포: 양 타깃 전체 빌드 경고/오류0, Honor20093/Contracts6078, EN/KO619, 양 runtime DLL allowlist/패키지 검사 통과. `artifacts/HonorModSeries-0.2.2-with-Contracts-0.3.116.zip` SHA256 `BB13AB48FC28665C10447DAECEB98457CADA2BEFE52ED9E04B3F43D2CB289F57`. 이전 로컬 Preview는 `.local/contracts-preview-83cb859bd3ae4193a6b5b95160d29ea1`에 백업했고 최종32파일 일치 설치, 활성화/언어 설정 hash 불변. Preview Steam 게시/예약 없음.