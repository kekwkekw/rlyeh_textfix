# RlyehTextFix 빌드

## 요구 사항

- Visual Studio 2022
- MSBuild
- .NET 6 SDK/runtime 구성
- 본인 소유/설치한 게임의 BepInEx 6 IL2CPP 환경
- BepInEx가 생성한 `BepInEx\interop` 참조 어셈블리

게임 또는 Unity 어셈블리는 이 저장소에 포함하지 않습니다.

## 빌드

PowerShell:

```powershell
.\build\Build-RlyehTextFix.ps1 -GameDir "C:\Games\rlyehshoujotaix_cl"
```

또는 Visual Studio/MSBuild에서 `GameDir` 속성을 명시합니다.

```powershell
MSBuild.exe .\src\RlyehTextFix\RlyehTextFix.csproj `
  /restore /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU `
  /p:GameDir="C:\Games\rlyehshoujotaix_cl"
```

참조 DLL은 `Private=false`이므로 빌드 출력에 BepInEx/Unity/Game DLL이 복사되지
않아야 합니다.
