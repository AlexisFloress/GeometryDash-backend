param([switch]$Migrate)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.local/dotnet'
    $env:NUGET_PACKAGES = Join-Path $projectRoot '.local/nuget'
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    dotnet restore GeometryDash.Api
    if ($LASTEXITCODE -ne 0) { throw 'No se pudieron restaurar los paquetes NuGet.' }
    if ($Migrate) { dotnet run --no-launch-profile --project GeometryDash.Api -- --migrate }
    else { dotnet run --no-launch-profile --project GeometryDash.Api --urls http://localhost:5080 }
    if ($LASTEXITCODE -ne 0) { throw 'El proceso del backend terminó con error.' }
} finally { Pop-Location }
