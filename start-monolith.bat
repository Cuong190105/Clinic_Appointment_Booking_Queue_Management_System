@echo off
echo Starting Monolith...
docker compose -f docker-compose.monolith.yml start
echo Done.
pause
