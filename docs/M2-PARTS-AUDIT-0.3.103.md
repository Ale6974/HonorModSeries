# M2 기반 구현 감사 — Preview0.3.103

2026-11-13. 직전 공개 main440e3df/0.3.102에서 이어서 구현했다. 이 버전은 M3 진입이나 M2 전체 완료를 뜻하지 않는다. M1 10/10, M2 미완료0/18, 전체10/100의 통합 점수는 유지한다.

## 실제 조립한 부품

| 항목 | 새 실제 경로 | 저장·중단 처리 |
|---|---|---|
| B05/M2.3 지도 위 작위 주민 | 새 RoyalMappedV1/map22 + clinical13 → 현재 source/target/수여/제삼자 작위 승인 → 원 resident/asset/city 호출 | 원 Map/Pawn/왕실 권리/area/care/surgery/census/독립 정상 반환 짝을 유지한다. 원 1~12 임상·map3~21/null bytes를 승격하지 않는다. |
| B05 권리 상태 | 정확한 native Psycast의 비시전/효과 없음 상태 및 비수여 작위(favorCost≤0)의 원 후계 키 부재 | 기본 Ability의 기존 원문 형식은 유지하고 Psycast만 별도 class wrapper를 추가했다. 능력 중복 소유를 거부한다. 수여 가능 작위의 누락 후계는 여전히 보류한다. |
| B06/M2.4 진행 중 지면 휴식 | 새 GroundRestV1/map23 + clinical14 → 기존 실제 Pawn.SetFaction | native ClearMind_NewTemp(true)→StopAll(true)가 원 LayDown을 유지하는 조건만 사용한다. 작업을 종료/복제/재시작하거나 driver/toil/예약을 쓰지 않는다. |
| B06 같은 작업의 지면 예약 | 실제 원 ReservationManager/Reservation/Pawn/Job/현재 위치 한 칸을 검증 → 같은 native 호출 | 각 주민 한 행, maxPawns1/stack−1/layer null, 원 Job 참조와 현재 칸만 허용한다. 목적지/다른 지도/물품/다른 사람 예약은 기존 차단을 유지한다. |

## 원본과 native 근거

설치1.7.5005 DLL SHA256 `82387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125`를 다시 확인했다. 새 권한은 MVID `3523c04c-c298-43ae-9571-20caaef22acc`만 사용한다. 다른1.6/1.7 본문의 서명 일치로 실행 범위를 넓히지 않는다.

정확한 Psycast/생성자, RoyalTitleDef.Awardable 및 AssignHeirIfNone, Pawn.SetFaction/ClearMind_NewTemp, Pawn_JobTracker.StopAll/ClearQueuedJobs/ExposeData, Job/JobDriver/JobDriver_LayDown/JobQueue의 원 저장·초기화 본문, RestUtility/PawnUtility/PawnPostureUtility와 Toils_LayDown, ReservationManager/Reservation을 메타데이터로 대조했다. 실패한 타입 조회는 근거에 포함하지 않았다. `autohack`은 작업 자체가 아닌 해킹 허용 설정이므로 이를 자율 진행 작업으로 구현/집계하지 않았다.

현재 원 Job·driver 소유, 전체 native 저장 필드, 현재 단계·잔여 tick·수면·자세·재생성되는 native toil 구성 및 callback/closure 구성을 읽기 전용으로 묶었다. native 저장에 속하지 않는 iterator thread ID는 게임 진행도 증거에서 제외한다. 외부 callback·알 수 없는 closure/자료·현재 칸 이외 예약은 보류한다. 원 Job은 Reference로만 Scribe하고 기존 native Pawn.jobs가 Job/driver/queue를 유일하게 deep-save한다. 원 ReservationManager가 예약을 유일하게 저장한다.

의도한 소속 변경에 따라 Reservation.Faction은 claimant의 새 소속을 따라 계산되지만 원 claimant/job/target/layer/수량·행 순서·예약 객체를 재작성하지 않는다. 원 주민의 독립 호출/정상 반환 기록이 이 권한 변화를 담당한다.

## 호출·저장·복구

기존 실제 도시 검토에서 새 원 map/clinical/작업 증거를 준비하고 원 census/care/area/자산 호출과 함께 사전 저장한다. 각 native 호출 전후 원 증거를 다시 대조한다. 부분 처리 뒤에도 이전 정상 반환을 재실행하지 않으며, Started만 있는 호출은 재실행/소유자 추정 완료를 하지 않는다. Job/목표/진행도/예약/원 객체가 변경·소실되면 원 자료를 유지하고 보류한다.

왕실 지도 경로와 지면 휴식 경로는 현재 별도 형식이며 둘을 묵시적으로 혼합하지 않는다. 원 지도/임상/권리 문서나 다른 목적지의 옛 승인을 새 실행에 복사하지 않는다. 후속 왕실 이전은 새 별도 원 자료와 현재 합의 및 기존 Applied 원 기록에 대한 연결을 요구한다.

## 검사 근거

`PrivateRoyalRightsTests`는 world/mapped 두 경로의 수여 세력 승인·권리 변경·중복 능력·원 지도 형식·독립 주민/자산 호출·부분 저장과 계약/채무/실적 보존을 검사한다. `GroundRestTests`는 원 Job/progress proof, 다른 Job·지도·칸·주민·공유/수량 예약 거부, 새 형식/원 census/care/call 및 부분 저장 후 완료 경로를 검사한다. native engine 객체 대신 규칙 경계의 검사를 수행하며 native 연결은 코드/참조 빌드로 확인한다.

## 남은 M2 및 다음 착수점

- M2.3: 수여 가능한 작위의 후계 키 누락, 상태 보유/커스텀 능력, 배정 왕좌·사적 관리 및 M3/M4 혼합 상태. native 로드의 후계 생성/정규화를 보존이라고 부르지 않는다.
- M2.4: jobGiver/ThinkTree 원 참조·저장 키, 일반 건설/제작/운반·물품/다중 예약·대기열·이동, 원 작업이 자연 진행/종료한 뒤의 안전한 새 실행/미시작 의도 종료. 현재 원 증거를 새 진행도로 덮거나 원 작업을 재시작하지 않는다.
- M2.5: 집단/Lord·군사·개별 관리 권한의 실제 현재 승인과 집행.
- M2.6: 남은 vanilla/사용자 정의 holder와 복합 사유 재산. 소유 컨테이너의 현존 구현은 그대로 재사용한다.
- M2.7~9: 복합 소실/귀환·확정 소실·다른 목적지/여러 도시의 남은 조합.
- M2.10: M3/4/5/6/7 소유 기능과 전체 통합 감사. M3는 아직 시작하지 않았다.

다음 구현은 B06의 jobGiver/ThinkTree 저장 경계와 native 작업 진행 후의 명시적 재검토/미시작 의도 종료부터 이어간다. 예약 범위를 다른 작업에 일반화하기 전에 해당 native cleanup/저장 본문과 현재 물품/개인 권한을 확인해야 한다.

## 여섯 단계의 잔여 노력

| 단계 | 마지막 역사적 추정 | 이번 상태 |
|---|---:|---|
| 1 계약·정치 | 31.5% | 위 M2 부품 진전, 전체 완료 점수는10/100 유지; 새 노력 비율 미재산정 |
| 2 공납·정산 | 80% | 변경 없음 |
| 3 군사·구원 | 95% | 변경 없음 |
| 4 이탈·분쟁 | 90% | 변경 없음 |
| 5 용병 | 100% | 변경 없음 |
| 6 통합·배포 | 70% | 이번 양 타깃·영한·패키지 검사만 반영; 전체 단계 완료 아님 |

역사적 비율은 최신 측정값이 아니다. 부품 검증·실제 조립·마일스톤 전체 완료를 구분하고 코드량/검사 수만으로 새 완료율을 계산하지 않는다.

최종 검사: 양 타깃 전체 빌드 Honor20093/Contracts5775, 경고·오류0, EN/KO593; 패키지 DLL allowlist 통과. 백업 contracts-preview-cf5ca26bfd21415d9e5980dddde778ce 후32개 파일 일치 및 ModsConfig/Prefs hash 불변. ZIP SHA256 5F9E24D4D198200924EA74F6A6D6DF4FC48A1CE9E9F00D19AA1842CAD539916D.
