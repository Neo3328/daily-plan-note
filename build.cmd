@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo The .NET Framework 4.x C# compiler was not found.
  exit /b 1
)
if not exist bin mkdir bin
"%CSC%" /nologo /target:winexe /optimize+ /out:bin\DailyPlanNote.exe /win32icon:assets\note.ico src\*.cs
exit /b %errorlevel%
