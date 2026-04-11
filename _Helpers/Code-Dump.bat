@echo off
setlocal enabledelayedexpansion

set "SCRIPT=.\Code-Dump.ps1"
set "EXCLUDE=''"

if not exist "%SCRIPT%" (
    echo ERROR: Script not found: %SCRIPT%
    pause
    exit /b 1
)

echo.
echo  Excluded file patterns: %EXCLUDE%

:: --- Collect projects (folders containing a .csproj) ---
set "ROOT=%~dp0.."
set "idx=0"

echo.
echo  Available projects:
echo  --------------------------------------------------
echo   0  ^| [Full solution]

for /f "delims=" %%F in ('dir /b /s /a-d "%ROOT%\*.csproj" 2^>nul') do (
    set /a idx+=1
    for %%D in ("%%~dpF.") do (
        set "PROJECT_!idx!=%%~nxD"
        echo   !idx!  ^| %%~nxD
    )
)

set "MAX=%idx%"
echo  --------------------------------------------------
echo.

:: --- User selection ---
:ask
set "CHOICE="
set /p CHOICE=" Select project (0-%MAX%): "

if "%CHOICE%"=="" goto ask
if "%CHOICE%"=="0" goto full_solution

:: Validate numeric input
set /a CHECK=%CHOICE% 2>nul
if "%CHECK%"=="%CHOICE%" (
    if %CHOICE% GEQ 1 if %CHOICE% LEQ %MAX% goto single_project
)
echo  Invalid selection, try again.
goto ask

:: --- Full solution dump ---
:full_solution
echo.
echo  Dumping full solution...
powershell -ExecutionPolicy Bypass -NoProfile -File "%SCRIPT%" -excludeFiles %EXCLUDE%
goto done

:: --- Single project dump ---
:single_project
set "SELECTED=!PROJECT_%CHOICE%!"
echo.
echo  Dumping project: %SELECTED%
powershell -ExecutionPolicy Bypass -NoProfile -File "%SCRIPT%" -projectFilter "%SELECTED%" -excludeFiles %EXCLUDE%
goto done

:done
if %ERRORLEVEL% neq 0 (
    echo.
    echo Script exited with error code %ERRORLEVEL%.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo  Done.
pause
