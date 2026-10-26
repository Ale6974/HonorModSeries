# 1단계 예약 개발 인수인계 — 0.3.34 / 2026-10-26

honor-1은30분 반복/ACTIVE다. 새 예약/대화나 재설정 없이 main·AGENTS·확정 결정·STATUS를 읽고 실제 남은 통합을 이어간다. 추가 신호는 중복 집행/빌드/설치를 시작하지 않는다.

이번에는 NEW 개인 합류를 SeparateSuccession=true로 만들고 native 사임/후임 선정과 개인 소속 변경의 저장 경계를 분리했다. Prepared → SuccessionApplying(native Notify_LeaderLost 전 기록) → source 정상 반환/정확한 현재 후임 관측 → SuccessionRecorded(actor는 아직 source/guest callback 미시작) → Applying(Pawn.SetFaction 시작) → 기존 target 관측/의료1회/명부 집행/완료다. 명시 복구는 source office를 재실행하지 않고 아직 시작하지 않은 affiliation부터 이어간다. PawnUtility.IsFactionLeader가 false인지 재확인하므로 SetFaction은 source 승계를 반복하지 않는다. source/affiliation 각각 정상 반환과 불확실한 현재 관측의 attribution/시각을 보존한다. source 반환이 없더라도 실제 정당한 후임과 원 source actor/원 care·명부/stock/book이 긍정 확인되면 FromNormalReturn=false로만 관측한다. null/바뀐 returned successor는 나중 ruler로 채우지 않는다.

구버전0.3.33 JSON의 enum0~4/전체 prepared bytes와 omitted defaults/combined 의미를 유지한다. 새 fields/true flag는 새 요청에만 있으며 legacy 근거 없는 Applying에 source receipt를 소급 생성하지 않는다. affiliation Applying에서 actor가 아직 source인 불확실 call은 여전히 반복하지 않는다. 사임을 시작한 상태도 미변경 준비로 취소하지 않는다. UI는 saved stage를 보여주되 UI-only cache를 사용하며 실제 실행/복구는 매번 uncached 원 snapshot/현재 native facts를 읽는다. 취소 버튼은 Prepared일 때만 있다.

GC pin만으로는 native world-pawn redress에 의한 원본 건강/gear 변경을 막지 못해, 실제 playing game의 resident/retained-office/personal join이 reserve한 정확한 Pawn refs도 GetValidCandidatesToRedress 결과에서 제외한다. unrelated candidates/순서는 유지하고 entry/world generation은 component를 조회하지 않는다. scope 안의 전체 no-redress/no-new-relations 규칙은 그대로다. restore-failed resident journal에서도 알려진 원 refs는 GC/redress에서 보존한다. nested native mutation callback에서는 새 집행/복구를 시작하지 않는다. 완료/취소의 historical refs는 pin하지 않는다.

## 다음 착수점

**전체 합병 실제 executor**가 가장 큰 잔여다. source가 끝나는 합병은0.3.32 별도 정치 제안/확인까지만 있다. 개인 합류는 source2도시 이상 존속/own administration/지원 saved leader만 다루므로 합병/마지막 base 가드를 면제하지 않는다. native SetFaction은 개별 owner/person 변경이고 faction 전체 제거/모든 pawn·world object/제삼자 의무 보존이 아니다. FactionManager의 해당 자동 제거는 temporary 경로뿐이다. 정치 전체 동의/개인 동의·전체 native population/custody/stock/contract/debt/third-party rights를 실제 coordinator/보존/중단 복구에 묶어야 한다.

개인 합류에는 **새 affiliation Applying의 source actor / 구버전 combined Applying의 source actor** 중단이 남는다. 이미 시작된 guest/medical/surgery/faction callbacks가 어디까지 수행되었는지 근거 없이 다시 SetFaction하지 않는다. 이번 split source-office 관측은 이 affiliation call 재시도의 허가가 아니다. source/target ruler·원 지문/명부/stock/외교 변화도 자동 치유하지 않는다. runtime API를 확인하고 새 긍정 반환 경계/안전한 continuation이 증명될 때만 확장한다. null 후임을 재생성/대입하거나 raw pointer/faction 쓰기로 해결하지 않는다.

다른 큰 잔여는 실제 보호 의무/고의 거부에 따른 정당한 일방 독립, 미지원 주민/노예·로딩 맵/복잡 stock·지도자 수술/역할/작위, 집단 의결·복잡 이적, 상위/집행 중·재진입/중단/점령·재탈환 소실과 전체 후손/복구다. Hegemony/WD2는 선택형 권위 adapter 없이 우회하지 않는다. 질문은 가능한 독립 작업 뒤 모아서 요청하고 없는 답변을 동의로 쓰지 않는다.

## 검사·설치·게시

1.6은 pinned1.6.4871/net472,1.7은 설치1.7.4985/net10.0이다. 게임을 시작하지 않고 코드·규칙·저장/복구·영한 안내·패키지를 검사한다. 로컬은 게임이 없을 때 백업 후 설치하고 활성화/언어는 유지한다. 검증 소스는 공개 main에 게시하며 게임/외부 DLL·로그/설정·도구/세이브는 제외한다. Steam은 기존 제한을 유지한다.

최종 규칙은 대상별 Contracts3861 + Honor/runtime20093 = 23954, EN/KO421개 키다. 양 전체 빌드와 마지막 candidate/null-safe/restore-failed refs/UI-cache 가드 뒤 양 Preview를 확인했다. 패키지/설치 결과는 TESTING을 따른다.

잔여 노력 추정은1단계35%(7~9묶음),2단계80%(5~7),3단계95%(6~8),4단계90%(5~7),5단계100%(6~8),6단계70%(4~6)다. 이번은 개인 중단의 추가 실제 실행 경계이며 전체 합병/독립/미지원 보존 등 큰 묶음이 남아 추정은 유지한다. 확정 명세의 실행·저장/복구·영한 안내·양 타깃 검사를 모두 충족할 때만1단계 완료로 보고하고 반복을 종료한다.
