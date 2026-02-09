# UnrealEditorBridge 빌드 가이드

이 문서는 UnrealEditorBridge 프로젝트의 전체 빌드 절차를 설명한다.
C# 솔루션(Protocol, Adapter, WPF)과 UE5 플러그인 두 부분을 순서대로 빌드해야 한다.

---

## 1. 사전 요구사항

| 항목 | 버전 | 비고 |
|------|------|------|
| **운영체제** | Windows 10 22H2 이상 / Windows 11 | Memory-Mapped File(MMF) API 사용으로 Windows 전용 |
| **.NET SDK** | 8.0 이상 | `dotnet --version` 으로 확인 |
| **Unreal Engine** | 5.7 | Sample 프로젝트의 `EngineAssociation`이 `"5.7"` |
| **Visual Studio** | 2022 (17.8 이상 권장) | C++ 게임 개발 워크로드, .NET 데스크톱 개발 워크로드 필요 |

### .NET SDK 설치 확인

```powershell
dotnet --version
# 출력 예: 8.0.400
```

8.0 미만이면 [https://dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0) 에서 SDK를 설치한다.

### Visual Studio 2022 워크로드 확인

Visual Studio Installer에서 다음 워크로드가 설치되어 있는지 확인한다:

- **C++를 사용한 게임 개발** (Unreal Engine 빌드에 필요)
- **.NET 데스크톱 개발** (WPF 프로젝트 빌드에 필요)

---

## 2. C# 프로젝트 빌드

### 2.1 솔루션 구조

```
UnrealEditorBridge.sln
  src/
    UnrealEditorBridge.Protocol/    ← .NET 8.0 (플랫폼 독립)
    UnrealEditorBridge.Adapter/     ← .NET 8.0-windows (Protocol 참조)
    UnrealEditorBridge.Wpf/         ← .NET 8.0-windows WPF (Adapter 참조)
```

프로젝트 의존성 순서: **Protocol --> Adapter --> WPF**

`dotnet build`는 의존성 그래프를 자동으로 해석하므로, 솔루션 루트에서 한 번에 빌드할 수 있다.

### 2.2 전체 빌드 (권장)

```powershell
cd D:\workspace\UnrealEditorBridge
dotnet restore
dotnet build
```

또는 솔루션 파일을 명시적으로 지정:

```powershell
dotnet build UnrealEditorBridge.sln -c Debug
```

### 2.3 개별 프로젝트 빌드 (순서 준수)

의존성 순서에 맞추어 하나씩 빌드할 수도 있다:

```powershell
# 1단계: Protocol (의존성 없음)
dotnet build src/UnrealEditorBridge.Protocol/UnrealEditorBridge.Protocol.csproj

# 2단계: Adapter (Protocol 참조)
dotnet build src/UnrealEditorBridge.Adapter/UnrealEditorBridge.Adapter.csproj

# 3단계: WPF (Adapter 참조)
dotnet build src/UnrealEditorBridge.Wpf/UnrealEditorBridge.Wpf.csproj
```

### 2.4 Release 빌드

배포용 빌드는 `-c Release` 옵션을 추가한다:

```powershell
dotnet build -c Release
```

---

## 3. NuGet 패키지 의존성

WPF 프로젝트(`UnrealEditorBridge.Wpf`)는 다음 NuGet 패키지를 사용한다:

| 패키지 | 버전 | 용도 |
|--------|------|------|
| **Unity.Container** | 5.11.11 | DI(의존성 주입) 컨테이너 |
| **Prism.Core** | 9.0.537 | MVVM 커맨드 (`DelegateCommand`) |
| **ReactiveUI** | 20.1.1 | 반응형 MVVM 바인딩 |
| **ReactiveUI.WPF** | 20.1.1 | ReactiveUI의 WPF 플랫폼 지원 |

`dotnet restore`를 실행하면 위 패키지가 자동으로 복원된다. NuGet.org에 접근할 수 없는 환경에서는 사내 NuGet 피드를 `nuget.config`에 등록해야 한다.

> **참고:** ReactiveUI.WPF 패키지에 `NU1701` 경고 억제가 설정되어 있다. 이는 의도적인 것이며 무시해도 된다.

---

## 4. UE5 플러그인 빌드

### 4.1 플러그인 복사

소스 디렉토리의 플러그인을 UE5 프로젝트의 `Plugins` 폴더에 복사한다:

```powershell
# 소스 위치
# D:\workspace\UnrealEditorBridge\ue5-plugin\UnrealEditorBridge\

# 대상 위치 (Sample 프로젝트 기준)
# D:\UnrealEngine\Sample\Plugins\UnrealEditorBridge\

xcopy /E /I /Y "D:\workspace\UnrealEditorBridge\ue5-plugin\UnrealEditorBridge" "D:\UnrealEngine\Sample\Plugins\UnrealEditorBridge"
```

복사 후 디렉토리 구조:

```
D:\UnrealEngine\Sample\
  Plugins\
    UnrealEditorBridge\
      UnrealEditorBridge.uplugin
      Source\
        UnrealEditorBridge\
          UnrealEditorBridge.Build.cs
          ...
```

### 4.2 플러그인 모듈 구성

`UnrealEditorBridge.Build.cs`의 모듈 의존성:

- **Public:** Core, CoreUObject, Engine, AssetRegistry, UnrealEd, EditorSubsystem
- **Private:** Json, JsonUtilities, Projects

`UnrealEditorBridge.uplugin`의 모듈 설정:

- **Type:** Editor (에디터 전용)
- **LoadingPhase:** PostEngineInit
- **PlatformAllowList:** Win64

### 4.3 커맨드라인 빌드

UE5의 `Build.bat`을 사용하여 에디터 타겟을 빌드한다:

```powershell
# UE5 엔진 루트에서 실행 (경로는 환경에 맞게 수정)
"C:\Program Files\Epic Games\UE_5.7\Engine\Build\BatchFiles\Build.bat" ^
    SampleEditor Win64 Development ^
    -project="D:\UnrealEngine\Sample\Sample.uproject"
```

### 4.4 에디터에서 빌드

1. `Sample.uproject`를 더블클릭하여 UE5 에디터를 실행한다.
2. 에디터가 플러그인을 자동으로 감지하고 빌드를 제안하면 **예**를 클릭한다.
3. 빌드 완료 후 **편집 > 플러그인**에서 "Unreal Editor Bridge"가 활성화되어 있는지 확인한다.

---

## 5. WPF 애플리케이션 실행

빌드가 완료되면 다음 명령으로 WPF 앱을 실행한다:

```powershell
dotnet run --project src/UnrealEditorBridge.Wpf
```

또는 빌드된 바이너리를 직접 실행:

```powershell
# Debug 빌드 기준
.\src\UnrealEditorBridge.Wpf\bin\Debug\net8.0-windows\UnrealEditorBridge.Wpf.exe
```

Release 빌드로 실행:

```powershell
dotnet run --project src/UnrealEditorBridge.Wpf -c Release
```

---

## 6. 빌드 오류 및 해결 방법

### 6.1 Live Coding 충돌

**증상:** UE5 에디터에서 Live Coding이 활성화된 상태에서 플러그인을 수정하면 빌드가 실패하거나 에디터가 충돌한다.

**원인:** Live Coding은 실행 중인 에디터 모듈의 바이너리를 핫 패치하는데, 이 과정에서 IPC 관련 코드(MMF 핸들, Mutex 등)가 꼬일 수 있다.

**해결:**

1. UE5 에디터를 종료한다.
2. **편집 > 에디터 개인설정 > 일반 > Live Coding**에서 "Enable Live Coding" 체크를 해제한다.
3. 에디터를 재시작한 뒤 전체 빌드를 수행한다.

```
에디터 개인설정 > 일반 > Live Coding > Enable Live Coding: OFF
```

### 6.2 DLL 잠금 (파일 사용 중 오류)

**증상:** `dotnet build` 실행 시 `The process cannot access the file because it is being used by another process` 오류가 발생한다.

**원인:** WPF 앱이 실행 중이면 빌드 출력 DLL이 잠겨 덮어쓸 수 없다.

**해결:**

1. 실행 중인 `UnrealEditorBridge.Wpf.exe` 프로세스를 종료한다.
2. 그래도 해결되지 않으면 프로세스를 강제 종료한다:

```powershell
taskkill /IM UnrealEditorBridge.Wpf.exe /F
```

3. `bin/` 및 `obj/` 디렉토리를 삭제하고 다시 빌드한다:

```powershell
dotnet clean
dotnet build
```

### 6.3 TUniquePtr 불완전 타입 오류

**증상:** UE5 플러그인 C++ 컴파일 시 `TUniquePtr` 관련 "incomplete type" 오류가 발생한다.

```
error C2027: use of undefined type 'SomeClass'
note: see declaration of 'TUniquePtr<SomeClass>'
```

**원인:** `TUniquePtr`의 소멸자가 호출될 때 포인티 타입의 완전한 정의가 필요한데, 헤더에서 전방 선언만 사용하고 `.cpp`에서 include를 누락한 경우 발생한다.

**해결:**

1. `TUniquePtr<T>`를 멤버로 가진 클래스의 `.cpp` 파일에서 `T`의 헤더를 반드시 include 한다.
2. 소멸자를 헤더가 아닌 `.cpp` 파일에서 정의한다 (기본 소멸자라도 명시적으로):

```cpp
// MyClass.h
class FMyClass
{
public:
    ~FMyClass();  // 소멸자 선언만
private:
    TUniquePtr<FSomeType> Impl;
};

// MyClass.cpp
#include "SomeType.h"  // 완전한 타입 정의 필요
FMyClass::~FMyClass() = default;  // 여기서 정의
```

### 6.4 NuGet 패키지 복원 실패

**증상:** `dotnet restore` 시 패키지를 다운로드하지 못한다.

**해결:**

1. NuGet.org 접근이 가능한지 확인:

```powershell
dotnet nuget list source
```

2. 필요시 NuGet.org를 소스로 추가:

```powershell
dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org
```

3. NuGet 캐시를 초기화한 뒤 재시도:

```powershell
dotnet nuget locals all --clear
dotnet restore
```

### 6.5 TargetFramework 호환성 오류

**증상:** `net8.0-windows` 관련 빌드 오류가 발생한다.

**원인:** Adapter와 WPF 프로젝트는 `net8.0-windows` 타겟을 사용하며, Windows 전용 API(Memory-Mapped File, WPF 등)에 의존한다.

**해결:**

- 반드시 Windows에서 빌드해야 한다.
- .NET SDK가 Windows 데스크톱 런타임을 포함하는지 확인:

```powershell
dotnet --list-runtimes
# Microsoft.WindowsDesktop.App 8.0.x 항목이 있어야 한다
```

---

## 7. 빌드 검증 체크리스트

빌드가 성공했는지 다음 항목을 확인한다:

- [ ] `dotnet build`가 경고 없이(`NU1701` 제외) 성공하였는가
- [ ] `src/UnrealEditorBridge.Wpf/bin/Debug/net8.0-windows/` 디렉토리에 실행 파일이 생성되었는가
- [ ] UE5 에디터를 열었을 때 "Unreal Editor Bridge" 플러그인이 활성화되어 있는가
- [ ] `dotnet run --project src/UnrealEditorBridge.Wpf` 실행 시 WPF 윈도우가 정상적으로 표시되는가

---

## 8. 클린 빌드

이전 빌드 산출물을 모두 제거하고 처음부터 다시 빌드하려면:

```powershell
# C# 클린 빌드
dotnet clean
dotnet build

# UE5 클린 빌드 (Intermediate 폴더 삭제)
Remove-Item -Recurse -Force "D:\UnrealEngine\Sample\Intermediate"
Remove-Item -Recurse -Force "D:\UnrealEngine\Sample\Binaries"
# 이후 에디터 또는 Build.bat으로 재빌드
```
