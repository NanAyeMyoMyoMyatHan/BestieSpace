@echo off
echo ========================================================
echo   Starting BestieSpace (Web API + Blazor Client) 🌸
echo ========================================================
echo.

start "BestieSpace.Api (Port 5000)" dotnet run --project BestieSpace.Api\BestieSpace.Api.csproj
timeout /t 3 /nobreak >nul
start "BestieSpace.Client (Port 5002)" dotnet run --project BestieSpace.Client\BestieSpace.Client.csproj

echo.
echo Both API and Client are starting!
echo Visit http://localhost:5002 in your browser.
echo ========================================================
pause
