@echo off
echo ========================================================
echo Starting Microservices System (Up and Wait for Health)
echo Baseline Commit: 7f556ba (branch main)
echo ========================================================

docker compose -f docker-compose.microservices.yml up -d --wait
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Failed to start Microservices containers or healthcheck timed out.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Microservices System is healthy and ready!
echo Frontend: http://localhost:3000
echo Backend / Gateway: http://localhost:5000
echo SQL Server: localhost:1433
echo RabbitMQ: localhost:15672
pause
