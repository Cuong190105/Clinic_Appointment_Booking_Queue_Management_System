@echo off
echo ========================================================
echo Starting Monolith System (Build, Up, and Wait for Health)
echo ========================================================

docker compose -f docker-compose.monolith.yml up -d --build --wait
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Failed to start Monolith containers or healthcheck timed out.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Monolith System is healthy and ready!
echo Frontend: http://localhost:3100
echo Backend:  http://localhost:5100
echo SQL Server: localhost:1434
pause
