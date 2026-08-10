@echo off
setlocal
REM Build AQC bbs_engine.dll into vendor\AQC\BBSDesktop\build (MSVC x64).
REM Requires Visual Studio 2022 C++ tools + CMake.

set "ROOT=%~dp0"
set "AQC=%ROOT%vendor\AQC\BBSDesktop"
set "BUILD=%AQC%\build"

where cmake >nul 2>&1
if errorlevel 1 (
  echo cmake not on PATH.
  exit /b 1
)

if not exist "%AQC%\CMakeLists.txt" (
  echo Missing vendor\AQC — run: git submodule update --init --recursive
  exit /b 1
)

echo Configuring bbs_engine (VS 2022 x64)...
cmake -S "%AQC%" -B "%BUILD%" -G "Visual Studio 17 2022" -A x64
if errorlevel 1 exit /b 1

echo Building bbs_engine Release...
cmake --build "%BUILD%" --config Release --target bbs_engine
if errorlevel 1 exit /b 1

REM Multi-config generators put DLL under build\Release\
if exist "%BUILD%\Release\bbs_engine.dll" (
  copy /Y "%BUILD%\Release\bbs_engine.dll" "%BUILD%\bbs_engine.dll" >nul
)

if not exist "%BUILD%\bbs_engine.dll" (
  echo Build finished but bbs_engine.dll not found under %BUILD%
  exit /b 1
)

echo OK: %BUILD%\bbs_engine.dll
dir "%BUILD%\bbs_engine.dll"
exit /b 0
