# 성능·안정성 설계

기존 공개 계약과 성공 경로를 보존한다. 반복 조회/중복 열거는 요청 범위 안에서 줄이고, 외부 입력의 잘못된 형식은 기존 실패 경계에서 처리한다. 전역 예외 삼키기, 권한 오류의 성공 위장, 기능 추가는 하지 않는다.
검증은 기존 테스트 프로젝트를 우선하며, 의존성 없는 핵심 코드는 독립 회귀 실행 프로젝트에서 실제 소스를 검증한다. 기존 테스트 없는 레거시 프로젝트는 빌드 환경 제한을 기록한다.
대안: 무조건 캐시나 전역 catch는 데이터 신선도/오류 계약이 달라질 수 있어 제외한다.

확정 구현: EventRecordParser에서 24바이트 미만 슬롯은 invalid/빈 payload로 반환해 IPC 잘린 데이터가 파서 범위 예외로 확산하지 않게 했다. 유효 슬롯과 기존 payload 자르기 계약은 보존한다.
회귀 위치: tests/RegressionTests/Program.cs
검증 결과: dotnet run --project tests/RegressionTests/RegressionTests.csproj: 53개 검사 통과.
