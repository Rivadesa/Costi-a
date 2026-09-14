[CmdletBinding()]
param(
    [string]$AppUrl = 'http://127.0.0.1:8000',
    [string]$DbHost = '127.0.0.1',
    [int]$DbPort = 5432,
    [string]$DbName = 'hospitality',
    [string]$DbUser = 'hospitality',
    [switch]$ForceEnv
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found in PATH."
    }
}

function ConvertFrom-Secure([Security.SecureString]$Value) {
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
    }
}

function Quote-DotEnv([string]$Value) {
    if ($Value.Contains("`r") -or $Value.Contains("`n")) {
        throw 'Environment values may not contain line breaks.'
    }

    $backslash = [string][char]92
    $quote = [string][char]34
    $escaped = $Value.Replace($backslash, $backslash + $backslash).Replace($quote, $backslash + $quote)
    return $quote + $escaped + $quote
}

function Set-DotEnvValue([string]$Path, [string]$Key, [string]$Value) {
    $lines = [System.Collections.Generic.List[string]](Get-Content -LiteralPath $Path)
    $replacement = "$Key=$Value"
    $found = $false

    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match ('^' + [Regex]::Escape($Key) + '=')) {
            $lines[$i] = $replacement
            $found = $true
            break
        }
    }

    if (-not $found) {
        $lines.Add($replacement)
    }

    Set-Content -LiteralPath $Path -Value $lines -Encoding UTF8
}

function Resolve-BuildSha([string]$Root) {
    $buildFile = Join-Path $Root 'BUILD.txt'
    if (Test-Path -LiteralPath $buildFile) {
        foreach ($line in Get-Content -LiteralPath $buildFile) {
            if ($line -match '^Commit:\s+([0-9a-fA-F]{7,40})$') {
                return $Matches[1].ToLowerInvariant()
            }
        }
    }

    if (Get-Command 'git' -ErrorAction SilentlyContinue) {
        try {
            $sha = (& git -C $Root rev-parse HEAD 2>$null).Trim()
            if ($LASTEXITCODE -eq 0 -and $sha -match '^[0-9a-fA-F]{7,40}$') {
                return $sha.ToLowerInvariant()
            }
        } catch {
            # Build metadata is diagnostic only; installation must not fail because git is unavailable.
        }
    }

    return 'dev'
}

Assert-Command 'php'

$repoRoot = Split-Path -Parent $PSScriptRoot
$backend = Join-Path $repoRoot 'backend'
$envExample = Join-Path $backend '.env.example'
$envFile = Join-Path $backend '.env'
$vendorAutoload = Join-Path $backend 'vendor\autoload.php'
$buildSha = Resolve-BuildSha $repoRoot

if (-not (Test-Path -LiteralPath $backend)) {
    throw "Backend directory not found: $backend"
}
if (-not (Test-Path -LiteralPath $envExample)) {
    throw "Missing .env.example: $envExample"
}

Write-Host 'Hospitality OS V1A pilot server bootstrap' -ForegroundColor Cyan
Write-Host 'This script configures a PILOT installation; it is not a production installer.' -ForegroundColor Yellow
Write-Host "Server build: $buildSha"
Write-Host ''

$dbPasswordSecure = Read-Host 'PostgreSQL password for the hospitality user' -AsSecureString
$pilotPasswordSecure = Read-Host 'Password for the five pilot application users (12+ chars)' -AsSecureString
$dbPassword = ConvertFrom-Secure $dbPasswordSecure
$pilotPassword = ConvertFrom-Secure $pilotPasswordSecure

if ($pilotPassword.Length -lt 12) {
    throw 'Pilot application password must contain at least 12 characters.'
}

if (-not (Test-Path -LiteralPath $envFile)) {
    Copy-Item -LiteralPath $envExample -Destination $envFile
    Write-Host 'Created backend/.env from .env.example.'
}
elseif ($ForceEnv) {
    Copy-Item -LiteralPath $envExample -Destination $envFile -Force
    Write-Host 'Recreated backend/.env because -ForceEnv was supplied.' -ForegroundColor Yellow
}
else {
    Write-Host 'Existing backend/.env will be updated in place.'
}

Set-DotEnvValue $envFile 'APP_ENV' 'local'
Set-DotEnvValue $envFile 'APP_DEBUG' 'false'
Set-DotEnvValue $envFile 'APP_URL' (Quote-DotEnv $AppUrl)
Set-DotEnvValue $envFile 'DB_CONNECTION' 'pgsql'
Set-DotEnvValue $envFile 'DB_HOST' (Quote-DotEnv $DbHost)
Set-DotEnvValue $envFile 'DB_PORT' ([string]$DbPort)
Set-DotEnvValue $envFile 'DB_DATABASE' (Quote-DotEnv $DbName)
Set-DotEnvValue $envFile 'DB_USERNAME' (Quote-DotEnv $DbUser)
Set-DotEnvValue $envFile 'DB_PASSWORD' (Quote-DotEnv $dbPassword)
Set-DotEnvValue $envFile 'HOSPITALITY_VERSION' '0.1.0'
Set-DotEnvValue $envFile 'HOSPITALITY_BUILD_SHA' (Quote-DotEnv $buildSha)

Push-Location $backend
try {
    if (-not (Test-Path -LiteralPath $vendorAutoload)) {
        Assert-Command 'composer'
        Write-Host 'PHP vendor dependencies are not packaged; installing with Composer...'
        & composer install --no-interaction --prefer-dist
        if ($LASTEXITCODE -ne 0) { throw 'composer install failed.' }
    } else {
        Write-Host 'Using packaged PHP vendor dependencies.'
    }

    Write-Host 'Generating Laravel application key...'
    & php artisan key:generate --force
    if ($LASTEXITCODE -ne 0) { throw 'php artisan key:generate failed.' }

    Write-Host 'Applying PostgreSQL migrations...'
    & php artisan migrate --force
    if ($LASTEXITCODE -ne 0) { throw 'php artisan migrate failed. Check PostgreSQL connectivity and credentials.' }

    Write-Host 'Provisioning Retiro pilot profile...'
    $env:HOSPITALITY_PROVISION_PASSWORD = $pilotPassword
    try {
        $provisionOutput = & php artisan hospitality:provision --profile=retiro-pilot --json
        if ($LASTEXITCODE -ne 0) {
            throw "Pilot provisioning failed: $($provisionOutput -join [Environment]::NewLine)"
        }
    }
    finally {
        Remove-Item Env:HOSPITALITY_PROVISION_PASSWORD -ErrorAction SilentlyContinue
    }

    $jsonLine = $provisionOutput | Select-Object -Last 1
    $provision = $jsonLine | ConvertFrom-Json
    if (-not $provision.ok) {
        throw "Pilot provisioning failed: $($provision.error)"
    }

    Set-DotEnvValue $envFile 'HOSPITALITY_TENANT_ID' (Quote-DotEnv ([string]$provision.tenant_id))
    Set-DotEnvValue $envFile 'HOSPITALITY_COMPANY_ID' (Quote-DotEnv ([string]$provision.company_id))
    Set-DotEnvValue $envFile 'HOSPITALITY_LOCATION_ID' (Quote-DotEnv ([string]$provision.location_id))

    & php artisan config:clear | Out-Null

    Write-Host ''
    Write-Host 'Pilot server bootstrap completed.' -ForegroundColor Green
    Write-Host "API base for clients: $($AppUrl.TrimEnd('/'))/api/v1"
    Write-Host "Server build: $buildSha"
    Write-Host 'Pilot users:'
    $provision.users.PSObject.Properties | ForEach-Object {
        Write-Host ("  {0,-30} {1}" -f $_.Name, $_.Value)
    }
    Write-Host ''
    Write-Host 'Start the internal pilot API with:' -ForegroundColor Cyan
    Write-Host '  php artisan serve --host=0.0.0.0 --port=8000'
    Write-Host ''
    Write-Host 'Before any real service, replace placeholder menu/table data and implement the production server process/backup plan.' -ForegroundColor Yellow
}
finally {
    Pop-Location
    $dbPassword = $null
    $pilotPassword = $null
}
