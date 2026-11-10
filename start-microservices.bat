@echo off
echo Starting Microservices...
docker compose -f docker-compose.microservices.yml start
echo Done.
pause
