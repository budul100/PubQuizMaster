@echo off
setlocal
echo === Rebuild Database ===
echo.

set APPNAME=pubquizmaster

set ROOT=%~dp0..
set MIGRATIONS=%ROOT%\%APPNAME%.Data\Migrations
set DATA_PROJECT=%ROOT%\%APPNAME%.Data
set WEB_PROJECT=%ROOT%\%APPNAME%.Web

set CONTAINER=%APPNAME%-db-1
set DB_USER=pubquiz
set MAX_WAIT_SECONDS=60

echo Do you want to delete all existing migrations first?
echo Recommended for a test database: creates one fresh InitialCreate from the current model.
echo.
choice /c YN /m "Delete migrations folder contents [Y/N]"
rem errorlevel N means "N or higher", so check the higher value first
if errorlevel 2 goto skipdelete

echo.
echo Deleting migrations including the model snapshot...
del /q "%MIGRATIONS%\*.cs" >nul 2>&1
echo Done.

:skipdelete
echo.
echo [1/5] Stopping containers and removing volumes...
docker compose -f "%ROOT%\docker-compose.dev.yml" down -v
if errorlevel 1 ( echo ERROR: docker compose down failed & pause & exit /b 1 )

echo.
echo [2/5] Starting containers...
docker compose -f "%ROOT%\docker-compose.dev.yml" up -d
if errorlevel 1 ( echo ERROR: docker compose up failed & pause & exit /b 1 )

echo.
echo [3/5] Waiting for PostgreSQL to be ready...
rem -h localhost checks TCP: during first-time initialization Postgres only listens on the socket
set /a WAITED=0

:waitloop
docker exec %CONTAINER% pg_isready -h localhost -U %DB_USER% >nul 2>&1
if not errorlevel 1 goto dbready
if %WAITED% geq %MAX_WAIT_SECONDS% ( echo ERROR: PostgreSQL not ready after %MAX_WAIT_SECONDS% seconds & pause & exit /b 1 )
timeout /t 2 /nobreak >nul
set /a WAITED+=2
goto waitloop

:dbready
echo PostgreSQL is ready.

echo.
echo [4/5] Checking migrations...
rem "if errorlevel" is evaluated at runtime, %ERRORLEVEL% inside a block would be expanded too early
if exist "%MIGRATIONS%\*.cs" (
    echo Migrations found -- skipping migrations add.
) else (
    echo No migrations found -- creating InitialCreate...
    dotnet ef migrations add InitialCreate --project "%DATA_PROJECT%" --startup-project "%WEB_PROJECT%"
    if errorlevel 1 ( echo ERROR: migrations add failed & pause & exit /b 1 )
)

echo.
echo [5/5] Applying migrations...
dotnet ef database update --project "%DATA_PROJECT%" --startup-project "%WEB_PROJECT%"
if errorlevel 1 ( echo ERROR: Migration failed & pause & exit /b 1 )

echo.
echo === Done. Database rebuilt successfully. ===
pause