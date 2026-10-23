# 실행 로그 발췌

실제 게임 stdout에서 `[Honor.Core]`와 `[Honor.Tests]` 메시지를 발췌했습니다. 행 끝 공백을 제거했으며 게임의 일반 스택 추적과 계정 메타데이터는 제외했습니다.

`save-load.log`와 `checkpoint.log`는 최종 코드의 실제 저장·재개 검사입니다. `ranged.log`는 리볼버 선혈전, `surrender.log`와 `death.log`는 실제 규칙별 상태·종료와 검증 결과입니다. 초기 테스트 코드의 공통 완료 문구가 모든 규칙을 first blood로 표시하던 문제는 수정했으며 해당 두 로그에서는 그 공통 완료 문구를 제외하고 `mode=Surrender/Death`와 개별 통과 검증을 보존했습니다.

전체 로그·세이브·시험용 모드 복사본은 Git에서 제외된 `artifacts` 또는 이 작업의 `work`에 보관됩니다.
