# M1 완료 근거 — Preview0.3.76 / 2026-10-31

STAGE1-MILESTONES.md의 고정 M1 항목 5개를 코드·규칙·저장 복원·양 타깃 빌드·패키지 기준으로 대조한다. 아래 실행은 실제 게임 컴포넌트의 적용 경로를 뜻하며 진단 화면이나 계획 모델만을 완료 근거로 사용하지 않는다.

| 고정 완료 조건 | 실제 적용 경로 | 저장·복구와 검사 근거 |
|---|---|---|
| 당사자·정착지·실제 세력 분리, 플레이어 통제 유지 | LiveCharterLedger의 party/binding, CurrentNativeFaction; PersonalFealtyLedger의 원 Realm/CurrentRealm 분리. 정치 계약 변경으로 native 플레이어 세력·주민을 재작성하지 않는다. | 별도 Scribe 참조와 Book/개인 이력 복원, 실제 소속 provenance 대조. PersonalOathTests, MixedRealmReparentTests, MixedTownSuccessionTests. |
| 4단계·순환 금지·직속/간접 권한 | CharterBook의 생성/서명/전체 계층 검사, 실제 재봉신·해약·재협상과 PersonalOathBook의 전체 활성 그래프 검사. | 새 결과를 복사본에서 구성하고 전체 그래프 복원 후 적용한다. 원 party/서약 ID와 역할을 보존한다. 기존 topology/동의 검사와 개인·영지 승계 검사. |
| 중간 이적/종료 시 전체 하위·제삼자 권리 유지 | LiveSubcharters.ApplySubcharter, NpcTerminations, scoped bilateral release/amendment, TownSuccession.ApplyTownSuccession. 다중 실제 소유자 각각의 현재 수락과 원 이전 증거를 요구한다. | 원 전체 범위와 원 조건/서명/채무를 묶는다. 새 승계 계약은 개별 대표 map을 사용하며 교체하지 않는 내부 계약은 그대로 둔다. MixedRealmReparentTests, ScopedContractAuthorityTests, MixedTownSuccessionTests. |
| NPC/플레이어 후계와 기존 실적·채무 보존 | PersonalFealtyLedger의 양방향 서약·후계 지정·실제 사망 관측·250tick/명시 승계·본인 해약; NPC 영지는 현재 실제 대표의 별도 결정을 사용한다. | 원 Pawn/Faction 참조와 append-only 이력·양의 사망 증거·원 서약 ID를 보존한다. territorial 공납 장부/영수증은 초기화하지 않으며 개인 채무를 만들어 복사하지 않는다. PersonalOathTests, PlayerCharterRenewalTests, ScopedContractAuthorityTests. 개인 의무 실행은 원래 M1의 후계 보존 조건과 구분한다. |
| 원 조건·서명·종료 사유와 실제 소속 불일치 및 복구 | 저장된 city-call 시작/정상 반환의 기존 복구, 계약 권리만의 미확인 통제자 정리, 실제 소실·개별 반환·합쳐진 범위·옛 반환 사실의 별도 현재 정리, 다중 소유자 승계. | 원 종료 계약을 부활시키거나 옛 반환 permission을 확대하지 않는다. 정부/정리 archive와 root marker 및 개별 서명 map을 재계산·짝 검사한다. TownSuccessionMandateTests, TownOwnershipReturnTests, TownLossMergeTests, MixedTownSuccessionTests와 기존 pending-loss 검사. |

## 마지막 미연결 경로의 구현

0.3.75까지는 실제 소유 세력이 다른 생존 후손의 승계가 same-realm 검사에서 막혔다.0.3.76은 그 검사만 제거한 것이 아니라 다음을 실제 경로에 연결했다.

- 원 전체 생존 후손과 NPC 상위 당사자 각각의 현재 대표·실제 소유자·검증된 원 이전 이력·상호 평화 및 정부 결정을 묶는다. 누락·추가 당사자·상충 대표·권한 미확인은 거부한다.
- 단일·cascade 후계 계획과 결과에 null-omitted PartyAuthorities를 추가한다. 실제 교체 대상별 상위/봉신 대표를 사용해 새 계약에 서명하며 원 단일 대표 API/기록은 그대로 둔다.
- 현재 일반·연합·귀족 대표가 섞인 경우에도 전체 원 범위와 같은 scope hash를 요구한다. 모든 원 내부 계약의 유지 동의도 검사한다.
- 직접 반환과 확대 전 반환의 별도 현재 정리에 같은 서명 map을 재현한다. 기존 양의 반환 사실·소실·정부/정리 marker와 모든 제삼자 원 권리를 보존한다.
- 원 Book과 현재 Game을 마지막에 대조하고, native 재검토 뒤 Book/소실/정부/정리 모델을 callback 없이 교체한다. 미완료 인계와 실패 복원은 그대로 차단한다.

## 완료 판정과 남은 단계

M1은 코드·저장 규칙·양 타깃 빌드·패키지 기준으로 완료한다. 기존 고정 가중치10점을 부여해 잔여 작업군 기준10/100점이다. 기존 환산식65 +35×10/100의 참고 진척은68.5%, 잔여 노력 추정은31.5%다. 정밀 공수 측정이나 일정 보장은 아니다.

M2~M9는 완료 처리하지 않는다. 주민/수술/노예의 물리 이전, 지도자 실제 이동, 전체 정당한 독립/전시 가입/전체 합병과 광범위한 물리 중단 조합은 각 고정 작업군에 남는다. 권한 주체나 원 증거가 없거나 provider/quest/custody 상태가 불명확한 경우에는 원본을 보존하며 보류한다. 이는 동의·사망·native 호출 성공을 가정해 처리하는 경로가 아니다.

검사 결과: 양 타깃 전체 빌드의 Honor20,093개/Contracts5,022개, 영한533개 문구 및 최종 native 코드 재컴파일 통과. 패키지 SHA256은444ECF4831A6A95550BBC939FC6E538DB6A86664D21CD43AC6CD042734FA0F12다. 로컬 설치는 실행 중인 게임의 파일 보호 검사에서 교체 전에 중단됐으므로 개발 완료와 로컬 배포 대기를 구분한다.
