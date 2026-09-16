@echo off
setlocal
echo === Rebuild Local Database ===
echo.

set APPNAME=pubquizmaster

set ROOT=%~dp0..
set MIGRATIONS=%ROOT%\%APPNAME%.Data\Migrations
set DATA_PROJECT=%ROOT%\%APPNAME%.Data
set WEB_PROJECT=%ROOT%\%APPNAME%.Web
set EF_ARGS=--project "%DATA_PROJECT%" --startup-project "%WEB_PROJECT%"

rem Only for this script: the design-time factory reads appsettings.Development.json and the user secrets
set ASPNETCORE_ENVIRONMENT=Development

echo [1/4] Target database:
rem Shows provider, data source and database name from the connection string
dotnet ef dbcontext info %EF_ARGS%
if errorlevel 1 ( echo ERROR: Could not read the DbContext configuration & pause & exit /b 1 )
echo.
choice /c YN /m "Drop this database and rebuild it [Y/N]"
if errorlevel 2 ( echo Aborted. & pause & exit /b 0 )

echo.
echo Delete all existing migrations first?
echo Recommended for a test database: creates one fresh InitialCreate from the current model.
choice /c YN /m "Delete migrations folder contents [Y/N]"
rem errorlevel N means "N or higher", so check the higher value first
if errorlevel 2 goto skipdelete

echo.
echo Deleting migrations including the model snapshot...
del /q "%MIGRATIONS%\*.cs" >nul 2>&1
echo Done.

:skipdelete
echo.
echo [2/4] Dropping database...
rem Fails if other clients are connected, e.g. the running app or pgAdmin
dotnet ef database drop --force %EF_ARGS%
if errorlevel 1 ( echo ERROR: database drop failed. Close the app and other database clients. & pause & exit /b 1 )

echo.
echo [3/4] Checking migrations...
rem "if errorlevel" is evaluated at runtime, %ERRORLEVEL% inside a block would be expanded too early
if exist "%MIGRATIONS%\*.cs" (
    echo Migrations found -- skipping migrations add.
) else (
    echo No migrations found -- creating InitialCreate...
    dotnet ef migrations add InitialCreate %EF_ARGS%
    if errorlevel 1 ( echo ERROR: migrations add failed & pause & exit /b 1 )
)

echo.
echo [4/4] Creating database and applying migrations...
dotnet ef database update %EF_ARGS%
if errorlevel 1 ( echo ERROR: Migration failed & pause & exit /b 1 )

echo.
echo === Done. Database rebuilt successfully. ===
pause
