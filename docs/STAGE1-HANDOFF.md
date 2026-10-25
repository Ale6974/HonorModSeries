# 1단계 예약 개발 인수인계 — 0.3.29 / 2026-10-26

honor-1은30분 반복/ACTIVE다. 새 예약/대화나 재설정 없이 main·AGENTS·확정 결정·STATUS를 읽고 실제 남은 통합을 이어간다. 빌드/실행/설치를 중복하지 않는다.

이번에는 idle 개별 Prepared ActiveCharter 이전의 빈 비로딩 NPC 말단 도시에서 정상 실제 파괴를 별도 증거로 저장하고 현재 게임/객체/원본 재검토 후 영지 계약 종료와 별도 Destroyed 이전 결과를 확정하도록 연결했다. 원 book/transfer snapshot·도시/owner·빈 stock descriptor/proof·시각을 묶는다. 관측/리뷰는 장부를 바꾸지 않는다. source나 target 중 실제 소실 owner를 보존하며 Applied/Cancelled·합의 해약·불명예를 만들지 않는다. 동일 identity는 owner를 다시 정규화하지 않는다. 실제 양의 기록 없이 missing ref는 근거가 아니며, recorded proof가 있어도 revived ID/바뀐 원본/남은 객체가 다르면 보류한다. 완료 hash와 증거를 restore 때 쌍으로 검증한다. 일반 Recover로 우회할 수 없다.

## 다음 착수점

**정치 소속 변경의 coordinated intent가 Applied 상태인 동안, 주민/재고 없는 NPC 말단 follower의 실제 파괴와 명시적 손실 정리**를 검토한다. 지금 StandaloneLossReady는 political/subtree/leader/payment pending과 자신의 native writes를 제외한다. 단순 가드 해제가 아니다. 새 양의 정상-return proof를 정확한 정치 intent/명령 head·원 book/transfer·도시·실제 owner에 묶어야 한다. 원래 immutable Followers를 지우지 말고, 새 별도 손실 결과와 현재 생존 follower 전체를 함께 검증하는 phase/receipt/UI/저장 경로가 필요하다. 잃은 도시를 조용히 빼서 모두 이전 성공이라고 하지 않는다. 기존 부분 native 결과·계약/채무·원 동의/전쟁 수락은 보존하며, 같은 명령 head와 생존 도시 실제 결과를 재확인한 뒤만 계속한다. 자신의 승인 SetFaction나 재진입을 외부 소실로 오인하지 않는다.

주민/분리 수술/물품 custody, 하위 전체 해약, 상위 도시/적대 점령·중단/reentrant native return 등은 별도 보존·권한·복구 전까지 보류한다. pending 원본이나 GC 참조/수술을 버리거나 raw faction/stock 생성으로 해결하지 않는다. 다른 큰 잔여는 재탈환·전체 합병·실제 보호 의무/고의 거부에 근거한 정당한 독립·지도자 개인 합류/안전한 native 승계·미지원 주민/노예·로딩 맵/복잡 재고·집단 의결·전체 복구다.

## 검사·설치·게시

1.6은 pinned1.6.4871/net472,1.7은 설치1.7.4985/net10.0이다. 게임을 시작하지 않고 코드·규칙·저장/복구·영한 안내·패키지를 검사한다. native 진입점은 실제 DLL/참조로 확인하며 미확인 권한을 가정하지 않는다. 로컬은 게임이 없을 때 백업 후 설치하고 활성화/언어는 유지한다. 검증한 소스는 공개 main에 게시하고 게임/외부 DLL·로그/설정·도구/세이브는 제외한다. Steam은 기존 제한을 유지한다.

이번 최종 규칙은 대상별 Contracts3690 + Honor/runtime20093 = 23783, EN/KO380개 키다. 양 전체 빌드와 최신1.7 Preview 재빌드 통과. 패키지/설치는 TESTING을 따른다.

잔여 노력 추정은1단계35%(7~9묶음),2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6)다. 이번은 진행 중 소실 묶음의 좁은 개별 경로라 추정은 유지한다. 확정 명세의 실제 실행·저장/복구·영한 안내·양 타깃 검사를 모두 충족할 때만1단계 완료로 보고하고 반복을 종료한다.
