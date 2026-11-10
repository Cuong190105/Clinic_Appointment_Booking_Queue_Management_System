@echo off
echo Stopping Monolith...
docker compose -f docker-compose.monolith.yml stop
echo Done.
pause
