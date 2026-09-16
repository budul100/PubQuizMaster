@echo off
setlocal enabledelayedexpansion

set APPNAME=pubquizmaster
set WEB_PROJECT=%APPNAME%.Web

chcp 65001 >nul

cd /d "%~dp0\..\%WEB_PROJECT%" 2>nul || (
    echo ERROR: %WEB_PROJECT% folder not found.
    echo Run this script from the _Helpers folder of the solution.
    pause
    exit /b 1
)

echo Checking user secrets...
dotnet user-secrets list >nul 2>&1 || (
    echo ERROR: No UserSecretsId found in project. Run 'dotnet user-secrets init' manually first.
    pause
    exit /b 1
)
echo OK.
echo.

echo ======================
echo   User Secrets Setup
echo ======================
echo.
echo This script sets local dev secrets via dotnet user-secrets.
echo Press ENTER to keep the existing value.
echo.

:: --- ConnectionStrings:Default ---
call :GetSecret "ConnectionStrings:Default" CURRENT_VAL
echo [1/2] ConnectionStrings:Default
echo     PostgreSQL connection string for local dev.
echo     Example: Host=localhost;Port=5432;Database=pubquizmaster;Username=postgres;Password=postgres
if defined CURRENT_VAL (echo     Current: !CURRENT_VAL!) else (echo     Current: ^(not set^))
:: Reset first: set /p keeps the previous value when ENTER is pressed
set "NEW_VAL="
set /p NEW_VAL="    Value: "
if not "!NEW_VAL!"=="" (
    dotnet user-secrets set "ConnectionStrings:Default" "!NEW_VAL!" >nul
    echo     Set.
) else (
    echo     Kept.
)
echo.

:: --- Auth:AdminPassword ---
call :GetSecret "Auth:AdminPassword" CURRENT_VAL
echo [2/2] Auth:AdminPassword
echo     Admin password for the login page. Required, the app does not start without it.
if defined CURRENT_VAL (echo     Current: ******** ^(set^)) else (echo     Current: ^(not set^))
:: Hidden input via PowerShell; the value is passed to dotnet directly, so special characters survive
powershell -NoProfile -Command ^
    "$s = Read-Host '    Value (hidden)' -AsSecureString;" ^
    "$p = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($s));" ^
    "if ([string]::IsNullOrEmpty($p)) { '    Kept.' } else { dotnet user-secrets set 'Auth:AdminPassword' $p | Out-Null; '    Set.' }"
echo.

echo ============================================
echo  Done. Current secrets:
echo ============================================
:: Password is masked in the listing
dotnet user-secrets list | findstr /v /b /c:"Auth:AdminPassword ="
call :GetSecret "Auth:AdminPassword" CURRENT_VAL
if defined CURRENT_VAL echo Auth:AdminPassword = ********
echo.
pause
exit /b 0


:: ============================================
:: Subroutine: read current value for a key
:: Usage: call :GetSecret "Key:Name" VARNAME
:: ============================================
:GetSecret
set "_KEY=%~1"
set "%~2="
set "_RAW="
:: /c: treats the search string literally (without it, spaces split it into OR terms)
for /f "tokens=1,* delims==" %%A in ('dotnet user-secrets list 2^>nul ^| findstr /b /c:"%_KEY% ="') do (
    set "_RAW=%%B"
)
if defined _RAW set "%~2=!_RAW:~1!"
goto :eof