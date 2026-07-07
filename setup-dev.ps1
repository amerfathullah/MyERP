#!/usr/bin/env pwsh
# MyERP Development Setup Script
# Run this after starting PostgreSQL (via Docker or locally)

$ErrorActionPreference = "Stop"

Write-Host "=== MyERP Development Setup ===" -ForegroundColor Cyan

# 1. Apply EF Core migrations
Write-Host "`n[1/4] Applying database migrations..." -ForegroundColor Yellow
Push-Location "src/MyERP.DbMigrator"
dotnet run
Pop-Location

# 2. Generate Angular proxy services
Write-Host "`n[2/4] Generating Angular API proxies..." -ForegroundColor Yellow
Push-Location "angular"
npx abp generate-proxy -t ng
Pop-Location

# 3. Start API
Write-Host "`n[3/4] Starting API server..." -ForegroundColor Yellow
Write-Host "  Run: cd src/MyERP.HttpApi.Host && dotnet run" -ForegroundColor Gray

# 4. Start Angular
Write-Host "`n[4/4] Starting Angular frontend..." -ForegroundColor Yellow
Write-Host "  Run: cd angular && npm start" -ForegroundColor Gray

Write-Host "`n=== Setup complete ===" -ForegroundColor Green
Write-Host "API:      http://localhost:5000" -ForegroundColor White
Write-Host "Swagger:  http://localhost:5000/swagger" -ForegroundColor White
Write-Host "Angular:  http://localhost:4200" -ForegroundColor White
Write-Host "Login:    admin / 1q2w3E*" -ForegroundColor White
