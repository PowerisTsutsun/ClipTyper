@echo off
rem Builds ClipTyper.exe with the C# compiler included in Windows (.NET Framework 4.x).
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo Could not find csc.exe. .NET Framework 4.x is required.
  exit /b 1
)
cd /d "%~dp0"
"%CSC%" -nologo -target:winexe -out:ClipTyper.exe -r:System.Windows.Forms.dll -r:System.Drawing.dll ClipTyper.cs
if errorlevel 1 exit /b 1
echo Built ClipTyper.exe
