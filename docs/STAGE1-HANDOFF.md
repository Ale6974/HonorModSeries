# 1단계 예약 개발 인수인계 — 0.3.33 / 2026-10-26

honor-1은30분 반복/ACTIVE다. 새 예약/대화나 재설정 없이 main·AGENTS·확정 결정·STATUS를 읽고 실제 남은 통합을 이어간다. 추가 예약 신호는 중복 집행/빌드/설치를 시작하지 않는다.

이번에는 별도 개인 요청/동의 뒤 지원 저장 지도자의 실제 native Pawn.SetFaction → 원 세력 native 후임 → 임상/의료 관측 → 목적지 saved roster에 같은 Pawn 추가 → 출발지 제거 → 검증/완료와 명시 복구를 연결했다. 개인 응답 정책75/상호 동맹·source2도시 이상 존속·own administration·unloaded/profile5/빈 수술/지원 stock 범위다. 도시/합병 서명은 개인 동의가 아니며 기존 일반 resident leader 제외를 제거하지 않았다. 원 book/transfer/도시 owner/채무는 그대로다. 실제 상단 여행/향후 맵 출현을 보장하지 않는다.

native 승계가 기존 world pawn을 redress하며 건강/gear/kind/관계를 바꿀 수 있음을 DLL에서 확인했다. 정확한 Source/kind/current Game의 실행 scope 동안만 GetValidCandidatesToRedress 결과를 비우고 GeneratePawn 요청의 CanGeneratePawnRelations를 false로 하여 기존 Pawn 재생성/재장비·새 가족 관계를 막는다. native kind/gender/map-member 선택 및 Source.leader는 native가 처리한다. scope 밖에서는 component 조회조차 하지 않고 finally에서 scope를 해제한다. private 후보 getter와 요청 속성의1.6 참조 metadata도 확인했다. unknown Harmony/scenario/kind/comp/provider callbacks는 시작 전 보류한다.

Applying을 native call 전에 저장하고 정상 반환 후임/관측 care를 별도 보존한다. 현재 target/원 지문/정당한 source successor를 재검사하며 불확실 call에서 actor가 아직 source이면 재실행하지 않는다. 정상 반환 없이 target/실제 후임/원 care가 긍정 확인되면 별도 현재 관측으로만 복구하며 call attribution은 false다. null/달라진 후임을 나중 ruler로 채우지 않는다. care reset 복원 권한은 첫 쓰기/확인 전에 한 번 소진하므로 later old reset 값도 덮지 않는다. double-reference 경계는 forward, 미변경 Prepared만 cancel한다. pending actor/관측 후임은 GC hook으로 보호하고 native write 전에 GC pass를 취소한다. 완료/취소 후 실행 native refs는 모두 비우고 원 JSON/labels/IDs 이력은 남긴다. pending은 다른 정치/새 납부/지도자 작업·자동 공유를 동결하며 정치 화면에도 직접 복구 UI가 있다.

## 다음 착수점

**전체 합병 실제 executor**와 지도자 joining의 남은 interrupted-native 경계가 필요하다. source를 없애는 합병은 아직0.3.32 별도 정치 제안/확인만 있다. 이번 개인 합류는 source가 존속하는 경우만 다루므로 합병/마지막 base 가드를 면제하지 않는다. native SetFaction은 개별 객체 owner/개인의 faction만 바꾸며 전체 faction 제거/모든 pawn·world object/제삼자 의무 보존이 아니다. FactionManager 자동 제거는 해당 temporary 경로뿐이라 마지막 도시 이전을 해산으로 부르지 않는다.

개인 합류의 **native Notify_LeaderLost가 끝나고 actor faction 변경 전 예외가 난 source-side Applying 상태**는 재실행하지 않고 보류한다. 이를 풀려면 정상 native succession의 정확한 반환 경계를 별도로 기록하고 현재 successor/원 actor/이미 실행한 guest·medical·surgery callbacks와 중복을 판별하는 실제 continuation이 필요하다. 단순 가드 해제, old leader pointer raw 복구, 불확실 SetFaction 반복으로 해결하지 않는다. Target steward/원 지문/명부/stock/외교가 바뀐 pending도 자동 치유하지 않는다. null successor를 나중 관계없는 새 ruler로 채우지 않는다.

다른 큰 잔여는 source-ending merger의 전체 동의/개인·도시 보존/모든 source pawn/world object/권리 처리, 실제 보호 의무/고의 거부에 따른 정당한 일방 독립, 미지원 주민/노예·로딩 맵/복잡 stock·지도자 수술/역할/작위, 집단 의결·복잡 이적, 상위/집행 중·재진입/중단/점령·재탈환 소실과 전체 후손/복구다. Hegemony/WD2는 선택형 권위 adapter 없이 우회하지 않는다. 질문은 가능한 독립 작업 뒤 모아서 요청하고 없는 답변을 동의로 쓰지 않는다.

## 검사·설치·게시

1.6은 pinned1.6.4871/net472,1.7은 설치1.7.4985/net10.0이다. 게임을 시작하지 않고 코드·규칙·저장/복구·영한 안내·패키지를 검사한다. 로컬은 게임이 없을 때 백업 후 설치하고 활성화/언어는 유지한다. 검증 소스는 공개 main에 게시하며 게임/외부 DLL·로그/설정·도구/세이브는 제외한다. Steam은 기존 제한을 유지한다.

최종 규칙은 대상별 Contracts3834 + Honor/runtime20093 = 23927, EN/KO415개 키다. 양 전체 빌드와 마지막 native 가드/현재 Game/참조정리 뒤 양 Preview, 마지막 권위/제삼자/재고 회귀 뒤 양 Contracts 호스트를 확인했다. 패키지/설치 결과는 TESTING을 따른다.

잔여 노력 추정은1단계35%(7~9묶음),2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6)다. 이번은 제한된 실제 개인 합류/승계·복구 경로의 통합이나 다른 큰 지원/합병/중단 범위가 남아 추정은 유지한다. 확정 명세의 실행·저장/복구·영한 안내·양 타깃 검사를 모두 충족할 때만1단계 완료로 보고하고 반복을 종료한다.
