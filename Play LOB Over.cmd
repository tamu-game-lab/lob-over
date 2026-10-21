@echo off
setlocal
cd /d "%~dp0"
where node >nul 2>nul
if errorlevel 1 (
  echo Node.js is missing. Install Node.js 20 or newer from https://nodejs.org then run this file again.
  pause
  exit /b 1
)
if not exist "Monitor\node_modules" (
  pushd Monitor
  call npm.cmd ci
  if errorlevel 1 (
    popd
    echo Dependencies could not be installed.
    pause
    exit /b 1
  )
  popd
)
powershell -NoProfile -ExecutionPolicy Bypass -File "Monitor\src\host\launch.ps1"
node Monitor/src/host/server.js Data/Scenarios/level1_texas.json --discovery
if errorlevel 1 pause
