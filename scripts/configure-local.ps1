param([switch]$Force)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$configPath = Join-Path $projectRoot 'GeometryDash.Api/appsettings.Local.json'

if ((Test-Path -LiteralPath $configPath) -and !$Force) {
    throw 'Ya existe appsettings.Local.json. Usa -Force solo si deseas reemplazarlo.'
}

$connection = Read-Host 'Cadena de conexión PostgreSQL (Host=localhost;Database=geometrydash;Username=...;Password=...)' -AsSecureString
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($connection)

try {
    $plainConnection = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    $keyBytes = New-Object byte[] 48
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()

    try {
        $rng.GetBytes($keyBytes)
    }
    finally {
        $rng.Dispose()
    }

    @{ ConnectionStrings = @{ Database = $plainConnection }; Jwt = @{ Key = [Convert]::ToBase64String($keyBytes) } } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $configPath -Encoding UTF8
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer);
    $plainConnection = $null
}

Write-Host 'Configuración local creada. Está excluida de Git.'
