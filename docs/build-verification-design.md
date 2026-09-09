# 전체 빌드 및 로컬 경계 검증 설계

orchestrator: Codex

기존 성공 경로와 오류 계약을 유지하며 테스트만 보강한다. 복원은 현재 프로젝트 선언 버전을 사용한다. 외부 서비스 대체 구현을 운영 코드에 삽입하지 않는다.

AutoAgent는 임시 SQLite를 사용해 저장소/이슈 저장과 읽기 전용 실패를 검증한다. UnrealEditorBridge는 실제 Protocol 파서의 바이너리 경계를 검증한다. key-man은 임시 메타데이터 파일만 사용하며 WindowsCredentialStore를 호출하는 기존 smoke/MCP 실행은 제외한다. JDashboard는 기존 전체 테스트를 실행하고 ClaimsActor 권한 우선순위와 식별자 누락을 보강한다.

대안: 실제 사용자 환경을 호출하는 통합 검증은 승인 범위를 넘으므로 제외한다. 테스트 격리용 fixture는 실제 생산 소스를 직접 호출한다.
검증: 전체 솔루션 빌드, 기존/신규 로컬 검사, git diff --check. 실제 UE5 에디터, GUI, MongoDB 및 자격 증명 통합은 별도 환경 요건으로 보고한다.

이 저장소의 최종 테스트: 0 시퀀스와 None 이벤트 거부, 헤더 heartbeat 및 최대 시퀀스 보존, 1바이트 부족한 헤더 거부를 추가했다.
