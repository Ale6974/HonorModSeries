# M2.4 진행 작업 — Preview0.3.104

2026-11-13. M2.4 전체 완료 판정이 아닌 실제 추가 구현 기록이다. M1 10/10, M2 미완료0/18, 전체10/100 유지.

## 이번 실제 연결

1. 자율적으로 제공된 원 LayDown의 ThinkTreeDef/원 노드 저장 키/노드 종류/등록 트리 구조 증거를 map24/clinical15에 묶었다. 실제 기존 resident/asset/city native 호출, care/area/census, Scribe 및 명시적 Recover에 연결한다. 원 Job은 native jobs가 유일하게 deep-save하고 원 ThinkTreeDef는 Def 참조로만 저장한다. node를 새로 만들거나 Job을 재시작하지 않는다.
2. native Job.ExposeData가 같은 원 등록 노드에서 파생하는 jobGiverKey 캐시는 새 형식에서만 그 저장 의미로 정규화해 읽는다. native 필드를 쓰지 않는다. 원 cache는 −1 또는 정확한 원 키만 허용하고 다른 양성/음성 키는 거부한다. 기존 map23/clinical14/무제공자 원문과 권한은 확장하지 않는다.
3. UI에서 직접 준비한 새 단독 given-work 이전만 StandaloneWorkV1을 사전 기록한다. 자율 대기 스케줄러·후손 배치·전시/정치·귀족 후속·독립 이전에 이 권한을 소급하지 않는다. 기존 Prepare/mark는 별도 physical 복사본에서 모두 검토한 뒤 원 참조와 함께 staging한다.
4. 원 임상/census·주민 호출/care/area·자산·도시 기록이 실제 변경을 하나도 시작하지 않았다는 증거를 보관한 경우, UI의 ‘실행 전인 이 이전 준비 중단’이 자체 준비만 취소한다. 원 전체 이전·원 작업 문서와 no-start 증거는 Cancelled 이력에 보관한다. 계약·채무·소속·건강·현재 작업/진행도/예약을 쓰거나 되돌리지 않는다. 원 Job이 자연 종료/풀 반환으로 참조가 사라지거나 재사용돼도 그 새 참조를 옛 Job으로 간주하지 않고, 오직 원 기록과 no-start 증거로 자체 준비만 종료한다. 실제 원 Pawn/도시/지도 및 source 소속은 여전히 확인한다.
5. 위 조건의 아직 미실행인 단독 준비는 로딩 시 현재 작업 진행도 변화만으로 전체 장부를 고장 상태로 만들지 않는다. 실제 인계 복구는 여전히 원 Job/원 진행도/원 참조·증거 일치를 요구한다. Started 또는 불명확한 native 호출이 있으면 이 중단 경로를 사용할 수 없고 원 자료를 유지한다. 중단 후 재시도는 새 검토/원 증거/호출 기록을 만든다.

## API와 보존 범위

현재5005 DLL SHA256 `82387F78262461E53BB06A67C3945912B876D1FDB07235122BBB6E59D073F125`와 기존 MVID를 재확인했다. ThinkTreeDef.TryGetThinkNodeWithSaveKey 및 ThinkNode.UniqueSaveKey/ChildrenRecursive, Job.ExposeData의 실제 본문을 대조했다. 새 어댑터는 호출을 발급하는 AI를 실행하지 않고, raw native tree를 순회해 같은 실제 노드 참조·중복 없는 저장 키를 확인한다. 순환/큰 트리/알 수 없는 노드 클래스/바뀐 원 매핑/패치된 읽기·복원 경계는 보류한다. unknown1.6 native 본문 허용은 추가하지 않는다.

기존 지면 칸 예약의 같은 Pawn/Job/현재 칸/max1/stack−1/layer null 제한을 유지한다. 대기열·일반 건설/제작/운반·물품/공유/다중 예약·이동·군사·사유 관리·royal+work 혼합에는 권한을 넓히지 않는다. 공개 모드 API를 호출할 때 standaloneWork의 기본값은 false이며, 실제 UI의 단독 확인 경로만 true를 사용한다.

## 검사

GroundRestTests는 기존 무제공자와 새 given-work의 실제 규칙 조립을 각각 검사한다. 새 형식/등록 노드 증거·다른 트리/키/타입·원 주민·care/call·자산 호출·부분 저장·정상 종료·원 no-start 취소 이력을 대조한다. 시작된 주민/care/자산 호출, 옛 형식/다른 원문/재취소는 거부한다. 이 검사는 규칙 경계의 검사이고 native 연결은 양 타깃 참조 빌드로 확인한다.

## 남은 M2.4

- 일반 건설/제작/운반 등 타입별 원 자원·driver·toil 진행 보존 및 Native StopAll 정리 경계.
- 원 물품/공유·다중 예약과 작업 대기열·이동 경계의 실제 집행.
- 이미 native 호출이 시작된 뒤 원 작업/건강이 자연 진행한 경우의 전체 중단 복구와 부모 스케줄러/후손/정치 조합.
- 귀족·개인 관리·군사 조합 및 M2.10 통합 감사.

지원 범위를 줄여 M2.4 완료로 판정하지 않는다. 다음 착수는 일반 작업을 타입별로 나누고 종료/자원/예약/저장 본문부터 확인해 독립 어댑터를 연결하는 작업이다. 여섯 단계의 역사적 잔여 노력31.5/80/95/90/100/70%는 최신 측정값이 아니며 이번 국소 구현만으로 새 완료율을 산출하지 않았다. 큰 작업 묶음은 위4개가 M2.4에 남아 있고 나머지 단계는 이번에 구현하지 않았다.

최종 결과: 양 타깃 전체 빌드와 최종 native 컴파일 경고/오류0, Honor20093/Contracts5819, EN/KO596. 패키지 allowlist 통과; 백업 contracts-preview-7b15c6bd5c334b60a0f595143da48578 후32개 파일 일치 및 ModsConfig/Prefs 불변. ZIP SHA256 343E40B126CC25E3A5933B3919CC4EC7E274CA66F466B111803CA6E3A921A426.
