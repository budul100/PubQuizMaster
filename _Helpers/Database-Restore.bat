@echo off
setlocal

set APPNAME=pubquizmaster

set BACKUP_DIR=%HiDrive%\Veranstaltungen\PubQuiz\Archiv\Backups
set PROJECT_DIR=C:\path\to\%APPNAME%

set CONTAINER=%APPNAME%-db-1
set DB=pubquiz
set USER=pubquiz

echo === Restore ===
echo.
echo Available DB backups:
echo.
dir /b "%BACKUP_DIR%\*.dump" 2>nul
if %ERRORLEVEL% neq 0 ( echo No backups found in %BACKUP_DIR% & pause & exit /b 1 )

echo.
set /p DB_FILENAME=Enter DB filename (without path): 

if not exist "%BACKUP_DIR%\%DB_FILENAME%" (
    echo ERROR: File not found: %BACKUP_DIR%\%DB_FILENAME%
    pause & exit /b 1
)

echo.
echo Available media backups:
dir /b "%BACKUP_DIR%\%APPNAME%-media_*.zip" 2>nul

echo.
set /p MEDIA_FILENAME=Enter media filename (leave empty to skip): 

echo.
echo WARNING: This will drop and recreate the database "%DB%".
if not "%MEDIA_FILENAME%"=="" echo          Media directory will be OVERWRITTEN.
choice /c YN /m "Continue?"
if %ERRORLEVEL% equ 2 ( echo Aborted. & pause & exit /b 0 )

echo.
echo [1/3] Dropping database...
docker exec %CONTAINER% psql -U %USER% -d postgres -c "DROP DATABASE IF EXISTS %DB%;"
if %ERRORLEVEL% neq 0 ( echo ERROR: Drop failed & pause & exit /b 1 )

echo [2/3] Creating database...
docker exec %CONTAINER% psql -U %USER% -d postgres -c "CREATE DATABASE %DB% OWNER %USER%;"
if %ERRORLEVEL% neq 0 ( echo ERROR: Create failed & pause & exit /b 1 )

echo [2b/3] Ensuring pgvector extension...
docker exec %CONTAINER% psql -U %USER% -d %DB% -c "CREATE EXTENSION IF NOT EXISTS vector;"
if %ERRORLEVEL% neq 0 ( echo ERROR: Extension setup failed & pause & exit /b 1 )

echo [3/3] Restoring database...
docker exec -i %CONTAINER% pg_restore -U %USER% -d %DB% --no-owner --exit-on-error < "%BACKUP_DIR%\%DB_FILENAME%"
if %ERRORLEVEL% neq 0 ( echo ERROR: DB restore failed & pause & exit /b 1 )

if not "%MEDIA_FILENAME%"=="" (
    if not exist "%BACKUP_DIR%\%MEDIA_FILENAME%" (
        echo ERROR: Media file not found: %BACKUP_DIR%\%MEDIA_FILENAME%
        pause & exit /b 1
    )
    echo [4/4] Restoring media...
    powershell -NoProfile -Command "Expand-Archive -Path '%BACKUP_DIR%\%MEDIA_FILENAME%' -DestinationPath '%PROJECT_DIR%\media' -Force"
    if %ERRORLEVEL% neq 0 ( echo ERROR: Media restore failed & pause & exit /b 1 )
)

echo.
echo Done. Database restored from %DB_FILENAME%.
if not "%MEDIA_FILENAME%"=="" echo Media restored from %MEDIA_FILENAME%.
echo.
echo NOTE: Run "dotnet ef database update" to verify migration state.
pause
