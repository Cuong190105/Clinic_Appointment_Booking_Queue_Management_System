@echo off
echo Stopping Microservices...
docker compose -f docker-compose.microservices.yml stop
echo Done.
pause
