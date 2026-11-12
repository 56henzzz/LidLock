@echo off
rem Compila LidLock con el csc.exe que ya trae Windows (.NET Framework 4): no hace falta instalar nada.
rem Variables opcionales: LIDLOCK_VERSION (ej. 1.2.0) y LIDLOCK_REPO (ej. usuario/LidLock; en GitHub Actions se toma solo).
setlocal

set "ROOT=%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo No se encontro csc.exe de .NET Framework 4.
    exit /b 1
)

set "VERSION=%LIDLOCK_VERSION%"
if "%VERSION%"=="" set "VERSION=0.0.0"
set "REPO=%LIDLOCK_REPO%"
if "%REPO%"=="" set "REPO=%GITHUB_REPOSITORY%"
if "%REPO%"=="" set "REPO=OWNER/LidLock"

if not exist "%ROOT%build" mkdir "%ROOT%build"

set "GEN=%ROOT%build\BuildInfo.g.cs"
> "%GEN%" echo using System.Reflection;
>> "%GEN%" echo [assembly: AssemblyTitle("LidLock")]
>> "%GEN%" echo [assembly: AssemblyProduct("LidLock")]
>> "%GEN%" echo [assembly: AssemblyVersion("%VERSION%.0")]
>> "%GEN%" echo [assembly: AssemblyFileVersion("%VERSION%.0")]
>> "%GEN%" echo namespace LidLock { internal static class BuildInfo { public const string Version = "%VERSION%"; public const string Repo = "%REPO%"; } }

set "RSP=%ROOT%build\csc.rsp"
> "%RSP%" echo /nologo /target:winexe /optimize+ /debug- /codepage:65001 /platform:anycpu
>> "%RSP%" echo /out:"%ROOT%build\LidLock.exe"
>> "%RSP%" echo /win32icon:"%ROOT%assets\lidlock.ico"
>> "%RSP%" echo /win32manifest:"%ROOT%src\app.manifest"
for %%F in ("%ROOT%assets\icons\*.png") do >> "%RSP%" echo /resource:"%%F",LidLock.%%~nxF
>> "%RSP%" echo "%ROOT%src\*.cs"
>> "%RSP%" echo "%GEN%"

"%CSC%" @"%RSP%"
if errorlevel 1 exit /b 1

echo.
echo Listo: %ROOT%build\LidLock.exe  (version %VERSION%, repo %REPO%)
exit /b 0
