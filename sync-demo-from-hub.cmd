@echo off
REM Export hub demo projects (esti_projectoffice) → Connect catalog + AStudio import file.
REM Then relaunch AStudio; it will import into firm.db and Flush projectStatus to the hub.
REM
REM Prereq: docker compose up -d · seed:demo already applied (14 projects typical)

setlocal EnableExtensions
set "OUT=%LOCALAPPDATA%\AStudio\hub-demo-projects.json"
set "CATALOG=%LOCALAPPDATA%\AORMS-Connect\catalog.json"
if not exist "%LOCALAPPDATA%\AStudio" mkdir "%LOCALAPPDATA%\AStudio"
if not exist "%LOCALAPPDATA%\AORMS-Connect" mkdir "%LOCALAPPDATA%\AORMS-Connect"

echo Exporting esti_projectoffice from Docker hub...
docker exec esti-db psql -U esti -d esti -t -A -F "|" -c "SELECT id, ref, title, status, updated_at FROM esti_projectoffice WHERE archived_at IS NULL ORDER BY updated_at DESC;" > "%TEMP%\hub-demo-raw.txt"
if errorlevel 1 (
  echo FAIL: docker/psql export. Is esti-db running?
  exit /b 1
)

powershell -NoProfile -Command ^
  "$rows = Get-Content '%TEMP%\hub-demo-raw.txt' | Where-Object { $_.Trim() -ne '' } | ForEach-Object {" ^
  "  $p = $_ -split '\|',5; [pscustomobject]@{ Id=$p[0]; Ref=$p[1]; Title=$p[2]; Status=$p[3]; UpdatedAt=$p[4] }" ^
  "};" ^
  "if ($rows.Count -lt 1) { Write-Error 'No projects exported — run: docker compose exec backend pnpm --filter @esti/backend seed:demo'; exit 2 };" ^
  "$rows | ConvertTo-Json -Depth 3 | Set-Content -Encoding utf8 '%OUT%';" ^
  "$cat = $rows | ForEach-Object { [pscustomobject]@{ id=$_.Id; ref=$_.Ref; title=$_.Title; status=$_.Status; updatedAt=$_.UpdatedAt } };" ^
  "$cat | ConvertTo-Json -Depth 3 | Set-Content -Encoding utf8 '%CATALOG%';" ^
  "Write-Host ('Wrote {0} projects → {1}' -f $rows.Count, '%OUT%');" ^
  "Write-Host ('Connect catalog → {0}' -f '%CATALOG%')"

if errorlevel 1 exit /b 1

echo.
echo Next: run-local-hub.cmd  (auto-imports when Projects empty, then Flush)
echo Or in AStudio: Projects → Import from Connect / Home will pull hub demo.
echo.
endlocal
