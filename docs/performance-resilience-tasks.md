# 성능·안정성 실행 작업

orchestrator: Codex

| 작업 | owner | model | effort | depends_on | parallel_group | files | verification | status |
|---|---|---|---|---|---|---|---|---|
| 저장소 조사 | Codex | gpt-6-astra | high | 없음 | dotnet-repos | 규칙·README·소스 | 소스 검토 | completed |
| 확인된 개선 및 회귀 | Codex | gpt-6-astra | high | 저장소 조사 | dotnet-repos | tests/RegressionTests/Program.cs 및 관련 소스 | 아래 결과 | completed |
| 전체 기능 통합 검증 | Codex | gpt-6-astra | high | 확인된 개선 및 회귀 | dotnet-repos | 저장소 전체 | 아래 한계 | not_completed |

EventRecordParser에서 24바이트 미만 슬롯은 invalid/빈 payload로 반환해 IPC 잘린 데이터가 파서 범위 예외로 확산하지 않게 했다. 유효 슬롯과 기존 payload 자르기 계약은 보존한다.

검증: dotnet run --project tests/RegressionTests/RegressionTests.csproj: 53개 검사 통과.

한계: 전체 .NET 솔루션 빌드는 sandbox 복원 실패 후 권한 재시도 도구가 중단되어 결과 미확인. UE5 C++ 플러그인/실제 MMF 통합은 미검증.

위 완료 표시는 확인된 변경과 회귀 범위에 한정한다. 모든 기능·모든 실패 상황의 테스트 작성을 완료했다는 의미가 아니다. 그룹 간에는 상위 Codex 세션과 병렬 진행했고 그룹 내부는 조사→변경→검증 의존성으로 순차 진행했다. commit/push/배포 없음.
