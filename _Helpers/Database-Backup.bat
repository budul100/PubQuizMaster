@echo off
setlocal

set APPNAME=pubquizmaster
set BACKUP_DIR=%HiDrive%\Veranstaltungen\PubQuiz\Archiv\Backups
set PROJECT_DIR=C:\path\to\%APPNAME%

set CONTAINER=%APPNAME%-db-1
set DB=pubquiz
set USER=pubquiz

if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"

for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmm"') do set TIMESTAMP=%%i
set DB_FILENAME=%APPNAME%_%TIMESTAMP%.dump
set MEDIA_FILENAME=%APPNAME%-media_%TIMESTAMP%.zip

echo === Backup ===
echo Target: %BACKUP_DIR%\%DB_FILENAME%
echo.

echo [1/2] Backing up database...
docker exec %CONTAINER% pg_dump -U %USER% -Fc %DB% > "%BACKUP_DIR%\%DB_FILENAME%"
if %ERRORLEVEL% neq 0 ( echo ERROR: DB backup failed & pause & exit /b 1 )

echo [2/2] Backing up media directory...
powershell -NoProfile -Command "Compress-Archive -Path '%PROJECT_DIR%\media\*' -DestinationPath '%BACKUP_DIR%\%MEDIA_FILENAME%' -Force"
if %ERRORLEVEL% neq 0 ( echo ERROR: Media backup failed & pause & exit /b 1 )

echo.
echo Done. Saved:
echo   %BACKUP_DIR%\%DB_FILENAME%
echo   %BACKUP_DIR%\%MEDIA_FILENAME%
pause
