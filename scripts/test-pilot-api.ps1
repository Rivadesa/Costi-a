[CmdletBinding()]
param(
    [string]$ApiBase = 'http://127.0.0.1:8000/api/v1',
    [string]$Email = 'admin@hospitality.local'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function ConvertFrom-Secure([Security.SecureString]$Value) {
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
    }
}

$ApiBase = $ApiBase.TrimEnd('/')
$passwordSecure = Read-Host "Password for $Email" -AsSecureString
$password = ConvertFrom-Secure $passwordSecure

try {
    Write-Host "Checking $ApiBase/meta ..." -ForegroundColor Cyan
    $meta = Invoke-RestMethod -Method Get -Uri "$ApiBase/meta" -TimeoutSec 5
    if ($meta.api_version -ne 'v1') {
        throw "Unexpected API version: $($meta.api_version)"
    }

    Write-Host 'Authenticating local pilot user...'
    $loginBody = @{
        email = $Email
        password = $password
        device_name = "pilot-smoke-$env:COMPUTERNAME"
    } | ConvertTo-Json

    $login = Invoke-RestMethod -Method Post -Uri "$ApiBase/auth/login" -ContentType 'application/json' -Body $loginBody -TimeoutSec 5
    if (-not $login.access_token) {
        throw 'Login did not return an access token.'
    }

    $headers = @{ Authorization = "Bearer $($login.access_token)" }
    Write-Host 'Fetching operational configuration...'
    $configuration = Invoke-RestMethod -Method Get -Uri "$ApiBase/configuration" -Headers $headers -TimeoutSec 5

    $tableCount = @($configuration.data.tables).Count
    $menuCount = @($configuration.data.menus).Count
    $stationCount = @($configuration.data.stations).Count

    if ($tableCount -lt 1 -or $menuCount -lt 1 -or $stationCount -lt 1) {
        throw "Incomplete operational configuration: tables=$tableCount menus=$menuCount stations=$stationCount"
    }

    Write-Host ''
    Write-Host 'Pilot API smoke test PASSED.' -ForegroundColor Green
    Write-Host "Application: $($meta.application)"
    Write-Host "Tables:      $tableCount"
    Write-Host "Menus:       $menuCount"
    Write-Host "Stations:    $stationCount"
}
finally {
    $password = $null
}
