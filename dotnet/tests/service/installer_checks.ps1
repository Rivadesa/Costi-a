# D5.5 — Comprobaciones del INSTALADOR real en un Windows de CI (elevado): instalacion completa en
# silencio, servicios, datos separados del programa, desinstalar/reinstalar conservando datos,
# actualizacion con copia previa, modo puesto adicional con la CA, y equipo nuevo desde una copia.
param([Parameter(Mandatory = $true)][string]$Setup)
$ErrorActionPreference = 'Stop'
$program = Join-Path $env:ProgramFiles 'Costina'
$dataRoot = Join-Path $env:ProgramData 'Costina'
$engine = Join-Path $program 'server\Costina.Server.exe'
$base = 'http://127.0.0.1:5088'
$api = "$base/api/native/v1"
$password = 'instalador-password-123'
$script:results = @()
$script:failed = $false

function Check([string]$name, [scriptblock]$body) {
    try { & $body; $script:results += @{name = $name; passed = $true }; Write-Host "PASS $name" }
    catch { $script:results += @{name = $name; passed = $false; error = "$_" }; $script:failed = $true; Write-Host "FAIL $name :: $_" }
}
function Assert($condition, [string]$message) { if (-not $condition) { throw $message } }
function Run-Setup([string[]]$arguments) {
    $process = Start-Process -FilePath $Setup -ArgumentList $arguments -Wait -PassThru
    return $process.ExitCode
}
function Run-Uninstall {
    # _?= hace que el desinstalador no se copie a TEMP y devuelva el control solo al terminar.
    $process = Start-Process -FilePath (Join-Path $program 'Uninstall.exe') -ArgumentList '/S', "_?=$program" -Wait -PassThru
    return $process.ExitCode
}
function Wait-Health([int]$seconds = 90) {
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

try {
    Assert (-not (Test-Path $dataRoot)) "The runner already has $dataRoot; refusing to reuse it."

    Check 'silent full installation: program, services and separate data root' {
        $code = Run-Setup @('/S', '/MODE=full', '/TENANT=inst-tenant', '/COMPANY=inst-company', '/LOCATION=inst-location', '/DEMO=1')
        Assert ($code -eq 0) "Installer exit code $code"
        foreach ($path in "$program\server\Costina.Server.exe", "$program\desktop\Costina.Desktop.exe", "$program\pgsql\bin\postgres.exe",
            "$program\build-manifest.json", "$program\Uninstall.exe", "$dataRoot\config\server.json", "$dataRoot\pg\PG_VERSION", "$dataRoot\tls\ca.crt") {
            Assert (Test-Path $path) "Missing $path"
        }
        Assert (-not (Get-ChildItem $program -Recurse -Include 'server.json', 'PG_VERSION', '*.backup')) 'Mutable data leaked into the program directory.'
        $database = Get-CimInstance Win32_Service -Filter "Name='CostinaPostgres'"
        $service = Get-CimInstance Win32_Service -Filter "Name='Costina'"
        Assert ($database.StartMode -eq 'Auto' -and $database.StartName -match 'NetworkService') "Unexpected database service: $($database.StartMode) $($database.StartName)"
        Assert ($service.StartMode -eq 'Auto' -and $service.StartName -eq 'NT SERVICE\Costina' -and $service.State -eq 'Running') 'Unexpected engine service.'
        Assert ((Get-Service Costina).ServicesDependedOn.Name -contains 'CostinaPostgres') 'The engine must depend on its database service.'
        Assert ((Wait-Health).mode -eq 'installation') 'Engine is not serving the installation.'
        $listening = Get-NetTCPConnection -State Listen -LocalPort 5544
        Assert (@($listening | Where-Object { $_.LocalAddress -notin '127.0.0.1', '::1' }).Count -eq 0) 'PostgreSQL must listen on loopback only.'
        $manifest = Get-Content "$program\build-manifest.json" -Raw | ConvertFrom-Json
        Assert ($manifest.version -eq (Invoke-RestMethod "$base/health").version) 'Manifest version differs from the running engine.'
        Assert ((Invoke-RestMethod "$base/health").demo) 'A /DEMO=1 installation must be flagged as demo.'
        # D6.1: el producto INSTALADO aloja la PWA en el mismo origen, con su CSP.
        $pwa = Invoke-WebRequest "$base/app/" -UseBasicParsing
        Assert ($pwa.StatusCode -eq 200 -and "$($pwa.Headers['Content-Security-Policy'])" -match "script-src 'self'") 'The installed engine does not host the PWA with its CSP.'
        # D5.6: 'status' resume la instalacion (servicios, salud, certificado, huella, copia) sin secretos.
        $report = & $engine status | Out-String
        Assert ($LASTEXITCODE -eq 0) "status reported an unhealthy installation: $report"
        foreach ($expected in 'Service CostinaPostgres\s+RUNNING', 'Service Costina\s+RUNNING', 'DEMO', 'Local CA fingerprint', 'installation') {
            Assert ($report -match $expected) "status output lacks '$expected': $report"
        }
        Assert ($report -notmatch 'Password=') 'status must never print secrets.'
        $fingerprint = (Get-Content "$dataRoot\tls\ca-fingerprint.txt")[1]
        Assert ($report -match [regex]::Escape($fingerprint)) 'status and ca-fingerprint.txt disagree.'
    }
    Check 'first administrator and HTTPS with the local root' {
        $password | & $engine create-user jefa main | Out-Null
        Assert ($LASTEXITCODE -eq 0) 'create-user failed'
        $token = (Login).token
        Assert $token 'Login failed after installation.'
        # Con los datos demo el producto INSTALADO recorre un servicio real: abrir mesa con el rol de ejecucion.
        $opened = Invoke-RestMethod "$api/dining/services" -Method Post -ContentType 'application/json' -Body (@{tableId = 'M1'; pax = 2; menuId = 'LAB-TASTING' } | ConvertTo-Json) `
            -Headers @{Authorization = "Bearer $token"; 'Idempotency-Key' = [guid]::NewGuid().ToString('N') }
        Assert $opened.serviceId 'Could not open a table on the installed product.'
        $script:serviceId = $opened.serviceId
        Import-Certificate -FilePath "$dataRoot\tls\ca.crt" -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
        Assert ((Invoke-RestMethod "https://$($env:COMPUTERNAME.ToLower()):5443/health" -TimeoutSec 10).mode -eq 'installation') 'HTTPS did not validate.'
        Get-ChildItem Cert:\LocalMachine\Root | Where-Object { $_.Subject -like '*Costina Local CA*' } | Remove-Item
    }
    Check 'uninstalling removes program and services but keeps every piece of data' {
        $code = Run-Uninstall
        Assert ($code -eq 0) "Uninstaller exit code $code"
        Assert (-not (Get-Service Costina, CostinaPostgres -ErrorAction SilentlyContinue)) 'Services are still registered.'
        Assert (-not (Test-Path "$program\server")) 'Program files were left behind.'
        foreach ($path in "$dataRoot\config\server.json", "$dataRoot\config\owner.json", "$dataRoot\pg\PG_VERSION", "$dataRoot\tls\ca.key") {
            Assert (Test-Path $path) "Uninstall removed data: $path"
        }
    }
    Check 'reinstalling over existing data: backup first, explicit upgrade, same users' {
        $before = @(Get-ChildItem "$dataRoot\backups" -Filter '*.backup' -ErrorAction SilentlyContinue).Count
        $code = Run-Setup @('/S', '/MODE=server')
        Assert ($code -eq 0) "Installer exit code $code"
        Wait-Health | Out-Null
        Assert ((Login).token) 'The user did not survive uninstall + reinstall.'
        $after = @(Get-ChildItem "$dataRoot\backups" -Filter '*.backup').Count
        Assert ($after -gt $before) 'The update did not take a backup before upgrading.'
        Assert (-not (Test-Path "$program\desktop")) 'Server-only mode must not install the desktop application.'
    }
    Check 'in-place update (services running) keeps working' {
        $code = Run-Setup @('/S', '/MODE=server')
        Assert ($code -eq 0) "Installer exit code $code"
        Wait-Health | Out-Null
        Assert ((Login).token) 'Login failed after in-place update.'
    }

    # Material para los dos ultimos escenarios, guardado FUERA de la raiz de datos.
    $kit = Join-Path $env:RUNNER_TEMP 'costina-kit'
    New-Item -ItemType Directory -Force $kit | Out-Null
    & $engine backup | Out-Null
    Assert ($LASTEXITCODE -eq 0) 'Manual backup failed.'
    $backup = Get-ChildItem "$dataRoot\backups" -Filter '*.backup' | Sort-Object Name | Select-Object -Last 1
    Copy-Item $backup.FullName, "$($backup.FullName).json" $kit
    Copy-Item "$dataRoot\tls\ca.crt" $kit
    $sourceInstallation = (Invoke-RestMethod "$api/session" -Headers @{Authorization = "Bearer $((Login).token)" }).installationId

    Check 'additional workstation: application only, trusts the constrained local CA, no services' {
        Assert ((Run-Uninstall) -eq 0) 'Uninstall failed.'
        $parked = "$dataRoot.parked"
        Rename-Item $dataRoot $parked           # simula OTRO equipo: sin datos de servidor
        try {
            $code = Run-Setup @('/S', '/MODE=client', "/CA=$kit\ca.crt")
            Assert ($code -eq 0) "Installer exit code $code"
            Assert (Test-Path "$program\desktop\Costina.Desktop.exe") 'Desktop application missing.'
            Assert (-not (Test-Path "$program\server")) 'Client mode must not install the server.'
            Assert (-not (Test-Path $dataRoot)) 'Client mode must not create a server data root.'
            Assert (-not (Get-Service Costina, CostinaPostgres -ErrorAction SilentlyContinue)) 'Client mode must not register services.'
            Assert (@(Get-ChildItem Cert:\LocalMachine\Root | Where-Object { $_.Subject -like '*Costina Local CA*' }).Count -eq 1) 'The local CA was not trusted.'
            # Una raiz sin restricciones de nombre se rechaza: no se instala cualquier CA.
            $rogue = New-SelfSignedCertificate -Subject 'CN=Costina Local CA FAKE' -KeyUsage CertSign -TextExtension @('2.5.29.19={critical}{text}ca=1') -CertStoreLocation Cert:\CurrentUser\My
            Export-Certificate -Cert $rogue -FilePath "$kit\rogue.cer" | Out-Null
            & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot '..\..\..\deploy\windows\import-ca.ps1') -Path "$kit\rogue.cer" | Out-Null
            Assert ($LASTEXITCODE -ne 0) 'An unconstrained CA must be rejected.'
            Assert ((Run-Uninstall) -eq 0) 'Uninstall failed.'
        }
        finally {
            Get-ChildItem Cert:\LocalMachine\Root | Where-Object { $_.Subject -like '*Costina Local CA*' } | Remove-Item -ErrorAction SilentlyContinue
            if (Test-Path $dataRoot) { Remove-Item $dataRoot -Recurse -Force }
            Rename-Item $parked $dataRoot
        }
    }
    Check 'new machine from a backup: verified restore, same users and installation identity' {
        $original = "$dataRoot.original"
        Rename-Item $dataRoot $original         # simula un equipo LIMPIO; el original queda intacto
        try {
            $code = Run-Setup @('/S', '/MODE=server', '/TENANT=inst-tenant', '/COMPANY=inst-company', '/LOCATION=inst-location', "/RESTORE=$kit\$($backup.Name)")
            Assert ($code -eq 0) "Installer exit code $code"
            Wait-Health | Out-Null
            $token = (Login).token
            Assert $token 'The restored installation does not accept the original user.'
            $restored = (Invoke-RestMethod "$api/session" -Headers @{Authorization = "Bearer $token" }).installationId
            Assert ($restored -eq $sourceInstallation) "Installation identity changed: $sourceInstallation -> $restored"
            # Los MISMOS registros de negocio: la mesa abierta antes de la copia sigue abierta tras restaurar, y la marca demo viaja con los datos.
            $service = Invoke-RestMethod "$api/dining/services/$($script:serviceId)" -Headers @{Authorization = "Bearer $token" }
            Assert ($service.data.tableId -eq 'M1') 'The service opened before the backup is missing after the restore.'
            Assert ((Invoke-RestMethod "$base/health").demo) 'The demo flag must travel with the restored data.'
            # Un ambito distinto debe rechazarse y no dejar una instalacion a medias.
            Assert ((Run-Uninstall) -eq 0) 'Uninstall failed.'
            Remove-Item $dataRoot -Recurse -Force
            $code = Run-Setup @('/S', '/MODE=server', '/TENANT=otro', '/COMPANY=inst-company', '/LOCATION=inst-location', "/RESTORE=$kit\$($backup.Name)")
            Assert ($code -ne 0) 'Restoring into a different scope must fail.'
            Assert (-not (Test-Path "$dataRoot\pg\PG_VERSION") -and -not (Test-Path "$dataRoot\config\server.json")) 'A failed fresh setup must not leave a half installation.'
            Assert (-not (Get-Service Costina, CostinaPostgres -ErrorAction SilentlyContinue)) 'A failed fresh setup must not leave services.'
        }
        finally {
            if (Test-Path "$program\Uninstall.exe") { Run-Uninstall | Out-Null }
        }
    }
}
catch { $script:failed = $true; $script:results += @{name = 'setup'; passed = $false; error = "$_" }; Write-Host "FAIL setup :: $_" }
finally {
    # El instalador silencioso no muestra la salida del motor: si algo fallo y no hay instalacion, se repite
    # setup-server a mano SOLO para dejar el motivo en el log de CI.
    if ($script:failed -and (Test-Path $engine) -and -not (Test-Path "$dataRoot\config\server.json")) {
        $env:COSTINA_TENANT = 'inst-tenant'; $env:COSTINA_COMPANY = 'inst-company'; $env:COSTINA_LOCATION = 'inst-location'
        Write-Host '--- diagnostic re-run of setup-server ---'
        & $engine setup-server 2>&1 | Out-Host
        Write-Host "--- exit $LASTEXITCODE ---"
    }
    New-Item -ItemType Directory -Force -Path 'artifacts/installer' | Out-Null
    @{passed = @($script:results | Where-Object { $_.passed }).Count; failed = @($script:results | Where-Object { -not $_.passed }).Count; results = $script:results } |
        ConvertTo-Json -Depth 5 | Set-Content 'artifacts/installer/installer-checks.json' -Encoding utf8
    foreach ($root in $dataRoot, "$dataRoot.original") {
        if (Test-Path "$root\logs") { Copy-Item "$root\logs" "artifacts/installer/logs-$(Split-Path $root -Leaf)" -Recurse -Force -ErrorAction SilentlyContinue }
    }
    & sc.exe query Costina | Out-File 'artifacts/installer/sc-query.txt'
    & sc.exe query CostinaPostgres | Out-File 'artifacts/installer/sc-query.txt' -Append
}
Write-Host "Installer checks: $(@($script:results | Where-Object { $_.passed }).Count) passed; $(@($script:results | Where-Object { -not $_.passed }).Count) failed"
if ($script:failed) { exit 1 }
exit 0
