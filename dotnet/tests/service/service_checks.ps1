# D5.2 — Comprobaciones del motor como servicio de Windows REAL (SCM, cuenta virtual, ACL, recuperacion).
# Se ejecuta elevado en el runner de CI contra el PostgreSQL nativo del propio runner. No usa datos reales.
param([Parameter(Mandatory = $true)][string]$Program)
$ErrorActionPreference = 'Stop'
$exe = Join-Path $Program 'Costina.Server.exe'
$base = 'http://127.0.0.1:5091'
$api = "$base/api/native/v1"
$password = 'servicio-password-123'
$dataRoot = Join-Path $env:ProgramData 'Costina'
$script:results = @()
$script:failed = $false

function Check([string]$name, [scriptblock]$body) {
    try { & $body; $script:results += @{name = $name; passed = $true }; Write-Host "PASS $name" }
    catch { $script:results += @{name = $name; passed = $false; error = "$_" }; $script:failed = $true; Write-Host "FAIL $name :: $_" }
}
function Assert($condition, [string]$message) { if (-not $condition) { throw $message } }
function Engine {
    $output = & $exe @args 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Costina.Server.exe $($args -join ' ') -> exit $LASTEXITCODE :: $output" }
    return $output
}
function Wait-Health([int]$seconds = 60) {
    for ($i = 0; $i -lt $seconds; $i++) {
        try { return Invoke-RestMethod "$base/health" -TimeoutSec 2 } catch { Start-Sleep -Seconds 1 }
    }
    throw "The engine did not answer /health within $seconds s"
}
function Login {
    $body = @{username = 'jefa'; password = $password } | ConvertTo-Json
    return Invoke-RestMethod "$api/auth/login" -Method Post -ContentType 'application/json' -Body $body `
        -Headers @{'Idempotency-Key' = [guid]::NewGuid().ToString('N') }
}
function EnginePid { (Get-CimInstance Win32_Service -Filter "Name='Costina'").ProcessId }

try {
    Assert (-not (Test-Path $dataRoot)) "The runner already has $dataRoot; refusing to reuse it."
    $pg = Get-Service 'postgresql*' | Select-Object -First 1
    Assert ($null -ne $pg) 'The runner image has no native PostgreSQL service.'
    Set-Service $pg.Name -StartupType Manual
    Start-Service $pg.Name
    $ready = $false
    for ($i = 0; $i -lt 60 -and -not $ready; $i++) {
        & (Join-Path $env:PGBIN 'pg_isready.exe') -h 127.0.0.1 -p 5432 | Out-Null
        $ready = $LASTEXITCODE -eq 0
        if (-not $ready) { Start-Sleep -Seconds 1 }
    }
    Assert $ready 'Native PostgreSQL did not accept connections.'

    # Provision + init + primer usuario con el entorno SOLO en esta consola; el servicio leera el fichero.
    $env:COSTINA_DB_BOOTSTRAP = 'Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=root'
    $env:COSTINA_DB_NAME = 'costina_service_d1_test'
    $env:COSTINA_TENANT = 'svc-tenant'; $env:COSTINA_COMPANY = 'svc-company'; $env:COSTINA_LOCATION = 'svc-location'
    $env:COSTINA_PORT = '5091'
    $env:COSTINA_PG_BIN = $env:PGBIN   # pg_dump/pg_restore del mismo PostgreSQL; viaja a server.json para el servicio
    Engine provision | Out-Null
    foreach ($name in 'COSTINA_DB_BOOTSTRAP', 'COSTINA_DB_NAME', 'COSTINA_TENANT', 'COSTINA_COMPANY', 'COSTINA_LOCATION', 'COSTINA_PORT', 'COSTINA_PG_BIN') {
        Remove-Item "Env:$name"
    }
    Engine init | Out-Null
    $password | & $exe create-user jefa main | Out-Null
    Assert ($LASTEXITCODE -eq 0) 'create-user failed'

    # D5.3: CA local y certificado ANTES de registrar el servicio (el orden inverso tambien esta soportado).
    $env:COSTINA_TLS_NAMES = "$($env:COMPUTERNAME.ToLower()),localhost"
    Engine provision-tls | Out-Null
    Remove-Item Env:COSTINA_TLS_NAMES

    Check 'install-service registers a delayed automatic service under the virtual account' {
        $env:COSTINA_PG_SERVICE = $pg.Name
        Engine install-service | Out-Null
        Remove-Item Env:COSTINA_PG_SERVICE
        $service = Get-CimInstance Win32_Service -Filter "Name='Costina'"
        Assert ($service.StartName -eq 'NT SERVICE\Costina') "Unexpected account: $($service.StartName)"
        Assert ($service.StartMode -eq 'Auto' -and $service.DelayedAutoStart) 'Service must be delayed automatic.'
        Assert ($service.PathName.StartsWith('"')) "Service path must be quoted: $($service.PathName)"
        Assert ((Get-Service Costina).ServicesDependedOn.Name -contains $pg.Name) 'Service must depend on PostgreSQL.'
        $failure = & sc.exe qfailure Costina | Out-String
        Assert ($failure -match 'RESTART') "Recovery actions missing: $failure"
    }
    Check 'configuration ACL: the service reads server.json only; owner credentials are out of its reach' {
        $owner = & icacls.exe (Join-Path $dataRoot 'config\owner.json') | Out-String
        $server = & icacls.exe (Join-Path $dataRoot 'config\server.json') | Out-String
        Assert ($owner -notmatch 'NT SERVICE' -and $owner -notmatch 'Users' -and $owner -notmatch 'Everyone') "owner.json is too open: $owner"
        Assert ($server -match 'NT SERVICE\\Costina') "server.json must be readable by the service: $server"
        Assert ($server -notmatch 'Users' -and $server -notmatch 'Everyone') "server.json is too open: $server"
    }
    Check 'TLS material ACL and firewall: the service reads server.pfx only; LAN opens on private/domain profiles' {
        $pfx = & icacls.exe (Join-Path $dataRoot 'tls\server.pfx') | Out-String
        $caKey = & icacls.exe (Join-Path $dataRoot 'tls\ca.key') | Out-String
        Assert ($pfx -match 'NT SERVICE\\Costina') "server.pfx must be readable by the service: $pfx"
        Assert ($caKey -notmatch 'NT SERVICE' -and $caKey -notmatch 'Users' -and $caKey -notmatch 'Everyone') "ca.key is too open: $caKey"
        $rule = Get-NetFirewallRule -DisplayName 'Costina-HTTPS-LAN'
        Assert ($rule.Enabled -eq 'True' -and $rule.Direction -eq 'Inbound' -and $rule.Action -eq 'Allow') 'Unexpected firewall rule.'
        Assert ("$($rule.Profile)" -notmatch 'Public' -and "$($rule.Profile)" -match 'Private') "Firewall profile must exclude public networks: $($rule.Profile)"
        Assert (($rule | Get-NetFirewallPortFilter).LocalPort -eq '5443') 'Firewall rule must open only the HTTPS port.'
    }
    Check 'the service starts without any user session and serves the installation' {
        Start-Service Costina
        $health = Wait-Health
        Assert ($health.mode -eq 'installation') "Unexpected mode $($health.mode)"
        $process = Get-CimInstance Win32_Process -Filter "ProcessId=$(EnginePid)"
        Assert ($process.SessionId -eq 0) 'The engine must run in session 0 (no interactive user).'
        $owner = Invoke-CimMethod -InputObject $process -MethodName GetOwner
        Assert ($owner.User -eq 'Costina') "Unexpected process owner $($owner.Domain)\$($owner.User)"
    }
    Check 'HTTPS validates with the local root and with no exceptions (SChannel, service account key)' {
        $rejected = $false
        try { Invoke-RestMethod 'https://localhost:5443/health' -TimeoutSec 10 | Out-Null } catch { $rejected = $true }
        Assert $rejected 'Without the local root installed the certificate must NOT validate.'
        Import-Certificate -FilePath (Join-Path $dataRoot 'tls\ca.crt') -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
        foreach ($name in 'localhost', $env:COMPUTERNAME.ToLower()) {
            $health = Invoke-RestMethod "https://${name}:5443/health" -TimeoutSec 10
            Assert ($health.mode -eq 'installation') "HTTPS health failed for $name"
        }
    }
    Check 'login and main-only diagnostics work through the service' {
        $login = Login
        $headers = @{Authorization = "Bearer $($login.token)" }
        $session = Invoke-RestMethod "$api/session" -Headers $headers
        Assert ($session.actor -eq 'user:jefa') "Unexpected actor $($session.actor)"
        $diagnostics = $null
        for ($i = 0; $i -lt 10; $i++) {
            $diagnostics = Invoke-RestMethod "$api/diagnostics" -Headers $headers
            if ($diagnostics.publisher.lastSuccessAt) { break }
            Start-Sleep -Seconds 1
        }
        Assert $diagnostics.runningAsService 'diagnostics must report a Windows service.'
        Assert ($diagnostics.publisher.lastSuccessAt) 'The outbox publisher never completed a cycle.'
        Assert ($diagnostics.outbox.pending -eq 0) 'Unexpected outbox backlog.'
        Assert ($diagnostics.dataRoot.freeBytes -gt 0) 'Free space was not reported.'
    }
    Check 'the service takes the first backup unattended and keeps it away from ordinary users' {
        $headers = @{Authorization = "Bearer $((Login).token)" }
        $backup = $null
        for ($i = 0; $i -lt 60; $i++) {
            $backup = (Invoke-RestMethod "$api/diagnostics" -Headers $headers).backup
            if ($backup.lastSuccessAt -or $backup.lastError) { break }
            Start-Sleep -Seconds 1
        }
        Assert (-not $backup.lastError) "Automatic backup failed: $($backup.lastError)"
        Assert ($backup.lastSuccessAt -and $backup.automatic) 'The service never took its first backup.'
        $file = Join-Path $dataRoot "backups\$($backup.lastFile)"
        Assert ((Test-Path $file) -and (Test-Path "$file.json")) 'Backup file or manifest missing.'
        $manifest = Get-Content "$file.json" -Raw | ConvertFrom-Json
        # E1b: manifiesto formato 2, claves esquema.tabla (core.users).
        Assert ($manifest.format -eq 2 -and $manifest.tables.'core.users'.rows -eq 1 -and $manifest.sha256 -eq (Get-FileHash $file -Algorithm SHA256).Hash.ToLower()) 'Manifest does not describe the file.'
        $acl = & icacls.exe (Join-Path $dataRoot 'backups') | Out-String
        Assert ($acl -notmatch 'Users' -and $acl -notmatch 'Everyone') "backups is too open: $acl"
    }
    Check 'the service writes a log file without secrets' {
        $logs = Get-ChildItem (Join-Path $dataRoot 'logs') -Filter 'server-*.log'
        Assert ($logs.Count -ge 1) 'No service log file was written.'
        $text = ($logs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
        Assert ($text -notmatch [regex]::Escape($password)) 'The log contains the password.'
        Assert ($text -notmatch 'Password=') 'The log contains a connection string.'
    }
    Check 'restarting the service keeps the data' {
        Restart-Service Costina
        Wait-Health | Out-Null
        Assert ((Login).token) 'The user did not survive the restart.'
    }
    Check 'a killed engine is restarted by the service manager' {
        $before = EnginePid
        Stop-Process -Id $before -Force
        Start-Sleep -Seconds 3
        Wait-Health 90 | Out-Null
        $after = EnginePid
        Assert ($after -ne 0 -and $after -ne $before) "The service manager did not restart the engine ($before -> $after)."
    }
    Check 'uninstall-service removes the service and never touches the data root' {
        Engine uninstall-service | Out-Null
        for ($i = 0; $i -lt 30 -and (Get-Service Costina -ErrorAction SilentlyContinue); $i++) { Start-Sleep -Seconds 1 }
        Assert (-not (Get-Service Costina -ErrorAction SilentlyContinue)) 'The service is still registered.'
        Assert (-not (Get-NetFirewallRule -DisplayName 'Costina-HTTPS-LAN' -ErrorAction SilentlyContinue)) 'The firewall rule must be removed with the service.'
        Assert (Test-Path (Join-Path $dataRoot 'tls\ca.key')) 'Uninstalling the service removed the local CA.'
        Assert (Test-Path (Join-Path $dataRoot 'config\server.json')) 'Uninstalling the service removed the configuration.'
        Assert (Test-Path (Join-Path $dataRoot 'config\owner.json')) 'Uninstalling the service removed the owner configuration.'
    }
}
catch { $script:failed = $true; $script:results += @{name = 'setup'; passed = $false; error = "$_" }; Write-Host "FAIL setup :: $_" }
finally {
    Get-ChildItem Cert:\LocalMachine\Root | Where-Object { $_.Subject -like '*Costina Local CA*' } | Remove-Item -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path 'artifacts/service' | Out-Null
    @{passed = @($script:results | Where-Object { $_.passed }).Count; failed = @($script:results | Where-Object { -not $_.passed }).Count; results = $script:results } |
        ConvertTo-Json -Depth 5 | Set-Content 'artifacts/service/service-checks.json' -Encoding utf8
    if (Test-Path (Join-Path $dataRoot 'logs')) { Copy-Item (Join-Path $dataRoot 'logs') 'artifacts/service/logs' -Recurse -Force -ErrorAction SilentlyContinue }
    & sc.exe query Costina | Out-File 'artifacts/service/sc-query.txt'
}
Write-Host "Service checks: $(@($script:results | Where-Object { $_.passed }).Count) passed; $(@($script:results | Where-Object { -not $_.passed }).Count) failed"
if ($script:failed) { exit 1 }
exit 0
