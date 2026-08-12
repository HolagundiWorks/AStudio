@echo off
REM Launch AStudio against local Docker hub (esti compose on :4000).
REM Writes %%LocalAppData%%\AStudio\local-hub.json so Activate works even if env is lost.
REM
REM Prereq (from esti repo):
REM   docker compose up -d
REM   docker exec esti-backend sh -lc "cd /app/esti/backend && pnpm exec tsx src/scripts/seedDesktopLocalHub.ts"

setlocal EnableExtensions
set ESTI_HUB_URL=http://127.0.0.1:4000
set ESTI_LICENSE_API_URL=http://127.0.0.1:4000/platform
set ESTI_PRODUCT_API_KEY=hlp_sk_local_desktop_dev_do_not_use_in_prod
set ESTI_LICENSE_KEY=HLP-5JNZ-445W-M59T
set ESTI_OLLAMA_URL=http://127.0.0.1:11434
if not defined INSTALL_ID set INSTALL_ID=astudio-local-%COMPUTERNAME%

set "CFG=%LOCALAPPDATA%\AStudio\local-hub.json"
if not exist "%LOCALAPPDATA%\AStudio" mkdir "%LOCALAPPDATA%\AStudio"
> "%CFG%" echo {
>> "%CFG%" echo   "HubUrl": "http://127.0.0.1:4000",
>> "%CFG%" echo   "LicenseApiUrl": "http://127.0.0.1:4000/platform",
>> "%CFG%" echo   "ProductApiKey": "hlp_sk_local_desktop_dev_do_not_use_in_prod",
>> "%CFG%" echo   "LicenseKey": "HLP-5JNZ-445W-M59T",
>> "%CFG%" echo   "InstallId": "%INSTALL_ID%"
>> "%CFG%" echo }

set "EXE=%~dp0src\AStudio.App\bin\x64\Release\net8.0-windows10.0.19041.0\AStudio.exe"
if not exist "%EXE%" (
  echo Building AStudio first...
  call "%~dp0build-winui.cmd" || exit /b 1
)

echo.
echo Exporting hub demo projects (for firm.db + Flush)...
call "%~dp0sync-demo-from-hub.cmd"
echo.
echo AStudio → local hub %ESTI_HUB_URL%
echo Config: %CFG%
echo Licence: %ESTI_LICENSE_KEY%
echo Auto-Activate + demo import/Flush on launch when unbound.
echo.

REM Pass env into child explicitly (avoid setlocal/start loss).
powershell -NoProfile -Command ^
  "$env:ESTI_HUB_URL='http://127.0.0.1:4000';" ^
  "$env:ESTI_LICENSE_API_URL='http://127.0.0.1:4000/platform';" ^
  "$env:ESTI_PRODUCT_API_KEY='hlp_sk_local_desktop_dev_do_not_use_in_prod';" ^
  "$env:ESTI_LICENSE_KEY='HLP-5JNZ-445W-M59T';" ^
  "$env:ESTI_OLLAMA_URL='http://127.0.0.1:11434';" ^
  "$env:INSTALL_ID='%INSTALL_ID%';" ^
  "Start-Process -FilePath '%EXE%'"

endlocal
