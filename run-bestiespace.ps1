Write-Host "========================================================" -ForegroundColor Magenta
Write-Host "  Starting BestieSpace (Web API + Blazor Client) 🌸" -ForegroundColor Yellow
Write-Host "========================================================" -ForegroundColor Magenta

Start-Process pwsh -ArgumentList "-NoExit", "-Command", "dotnet run --project BestieSpace.Api\BestieSpace.Api.csproj"
Start-Sleep -Seconds 3
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "dotnet run --project BestieSpace.Client\BestieSpace.Client.csproj"

Write-Host "`nBoth API (port 5000) and Client (port 5002) are starting!" -ForegroundColor Green
Write-Host "Open http://localhost:5002 in your browser to experience BestieSpace! 💌`n" -ForegroundColor Cyan
