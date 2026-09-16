# Build-only packaging. Executed in isolated Windows CI, never on a restaurant PC.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot\..\..").Path
$artifact = Join-Path $root 'artifacts'
$bundle = Join-Path $artifact 'trial-package'
New-Item -ItemType Directory -Force $bundle | Out-Null
Push-Location "$root\dotnet"
try {
  foreach ($item in @(@('Costina.Desktop','desktop'), @('Costina.Server','server'), @('Costina.Launcher','launcher'))) {
    dotnet publish "src/$($item[0])" -c Release -r win-x64 --self-contained true -o "$bundle/$($item[1])"
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $($item[0])" }
  }
} finally { Pop-Location }
$url = 'https://get.enterprisedb.com/postgresql/postgresql-17.11-3-windows-x64-binaries.zip'
$archive = Join-Path $artifact 'postgresql-17.11-3-windows-x64-binaries.zip'
Invoke-WebRequest -Uri $url -OutFile $archive -MaximumRetryCount 2
$vendorHash = (Get-FileHash $archive -Algorithm SHA256).Hash
$vendor = Join-Path $artifact 'postgres-vendor'
& 7z x $archive "-o$vendor" -y | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL archive extraction failed' }
New-Item -ItemType Directory -Force "$bundle/pgsql" | Out-Null
foreach ($name in @('bin','lib','share')) { Copy-Item "$vendor/pgsql/$name" "$bundle/pgsql/$name" -Recurse }
# Copy vendor-provided notices when present; never bundle pgAdmin, StackBuilder or font files.
Get-ChildItem "$vendor/pgsql" -File | Where-Object { $_.Name -match '(?i)(license|licence|copyright|notice)' } | Copy-Item -Destination "$bundle/pgsql"
$fonts=Get-ChildItem $bundle -Recurse -File | Where-Object { $_.Extension -in '.ttf','.otf','.woff','.woff2' }
if ($fonts) { throw 'Unexpected fonts in runtime package; review instead of distributing' }
# App-local runtime files come only from the Microsoft redistribution directory, never DLL download sites.
$vswhere="${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs=& $vswhere -latest -products '*' -property installationPath
$crt=Get-ChildItem "$vs/VC/Redist/MSVC" -Directory | Sort-Object Name -Descending | ForEach-Object {
  Get-ChildItem "$($_.FullName)/x64" -Directory -Filter 'Microsoft.VC*.CRT' -ErrorAction SilentlyContinue
} | Select-Object -First 1
if (!$crt) { throw 'Microsoft app-local runtime source unavailable' }
Copy-Item "$($crt.FullName)/*.dll" "$bundle/pgsql/bin/" -Force
& "$bundle/pgsql/bin/pg_ctl.exe" --version
if ($LASTEXITCODE -ne 0) { throw 'Bundled PostgreSQL cannot start' }
@{format=1; version='0.2.1-d1.3'; postgres='17.11'; postgresUrl=$url; postgresArchiveSha256=$vendorHash;
  vendorChecksumNote='Digest computed on official HTTPS download, not an independently signed EDB checksum';
  vcRuntimeSource=$crt.Name; sourceCommit=$env:GITHUB_SHA; run=$env:GITHUB_RUN_ID; kind='single-user-trial-not-SCM-service'} |
  ConvertTo-Json | Set-Content "$bundle/trial-package.json" -Encoding utf8
Copy-Item "$root/docs/native/D1.3-LEEME.txt" "$bundle/LEEME.txt"
$manifest=Get-ChildItem $bundle -Recurse -File | ForEach-Object {
  @{path=$_.FullName.Substring($bundle.Length+1);sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash}
}
@{commit=$env:GITHUB_SHA;run=$env:GITHUB_RUN_ID;version='0.2.1-d1.3';files=@($manifest)} |
  ConvertTo-Json -Depth 5 | Set-Content "$bundle/build-manifest.json" -Encoding utf8
$nsis="${env:ProgramFiles(x86)}\NSIS\makensis.exe"
if (!(Test-Path $nsis)) { choco install nsis --version=3.11 -y --no-progress; if ($LASTEXITCODE -ne 0) { throw 'NSIS installation failed' } }
Push-Location "$root/deploy/native-trial"
try { & $nsis installer.nsi; if($LASTEXITCODE -ne 0){throw 'Installer compilation failed'} } finally {Pop-Location}
