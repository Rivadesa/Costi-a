# D5.5 — Construccion del paquete de Windows. Se ejecuta SOLO en CI (runner Windows aislado), nunca en
# un PC del restaurante. Publica motor y cliente autocontenidos, empaqueta PostgreSQL con version exacta
# fijada, registra un manifiesto con hashes y compila el instalador NSIS.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot\..\..").Path
$artifacts = Join-Path $root 'artifacts'
$package = Join-Path $artifacts 'package'
if (Test-Path $package) { Remove-Item $package -Recurse -Force }
New-Item -ItemType Directory -Force $package | Out-Null

# Version unica: la de los csproj (la misma que sirve /health).
$version = ([xml](Get-Content "$root/dotnet/src/Costina.Server/Costina.Server.csproj")).SelectSingleNode('//Version').InnerText
$desktopVersion = ([xml](Get-Content "$root/dotnet/src/Costina.Desktop/Costina.Desktop.csproj")).SelectSingleNode('//Version').InnerText
if ($version -ne $desktopVersion) { throw "Server ($version) and desktop ($desktopVersion) versions differ." }

Push-Location "$root/dotnet"
try {
  foreach ($item in @(@('Costina.Server', 'server'), @('Costina.Desktop', 'desktop'))) {
    dotnet publish "src/$($item[0])" -c Release -r win-x64 --self-contained true -o "$package/$($item[1])"
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $($item[0])" }
  }
} finally { Pop-Location }

# PostgreSQL: binarios oficiales de EDB, version EXACTA. El SHA-256 fijado impide que un cambio del
# fichero remoto entre en el paquete sin revision. (Huella calculada sobre la descarga HTTPS oficial:
# no es una firma independiente del proveedor.)
$postgresVersion = '17.11-3'
$postgresUrl = "https://get.enterprisedb.com/postgresql/postgresql-$postgresVersion-windows-x64-binaries.zip"
$postgresSha256 = '4B8DB0930C38F6EF845DB919551DEDDA3B6B845AEB0927B3D79A6E8E9E4537CF'   # observado en el run 35331381837 (descarga HTTPS oficial)
$archive = Join-Path $artifacts "postgresql-$postgresVersion-windows-x64-binaries.zip"
if (-not (Test-Path $archive)) { Invoke-WebRequest -Uri $postgresUrl -OutFile $archive -MaximumRetryCount 3 }
$actualSha256 = (Get-FileHash $archive -Algorithm SHA256).Hash
if ($postgresSha256 -and $actualSha256 -ne $postgresSha256) { throw "PostgreSQL archive hash mismatch: expected $postgresSha256, got $actualSha256" }
if (-not $postgresSha256) { Write-Warning "PostgreSQL archive SHA-256 is not pinned yet. Observed: $actualSha256" }
$vendor = Join-Path $artifacts 'postgres-vendor'
if (Test-Path $vendor) { Remove-Item $vendor -Recurse -Force }
& 7z x $archive "-o$vendor" -y | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL archive extraction failed' }
New-Item -ItemType Directory -Force "$package/pgsql" | Out-Null
# Solo el servidor y sus herramientas: nada de pgAdmin, StackBuilder, documentacion, cabeceras ni simbolos.
foreach ($name in @('bin', 'lib', 'share')) { Copy-Item "$vendor/pgsql/$name" "$package/pgsql/$name" -Recurse }
Get-ChildItem "$package/pgsql" -Recurse -File -Include '*.pdb' | Remove-Item -Force
Get-ChildItem "$vendor/pgsql" -File | Where-Object { $_.Name -match '(?i)(license|licence|copyright|notice)' } | Copy-Item -Destination "$package/pgsql"
# Runtime de Visual C++ junto a los binarios (un Windows limpio puede no tenerlo). Solo desde el
# directorio de redistribucion de Microsoft del propio runner, nunca de sitios de descarga de DLL.
if (-not (Test-Path "$package/pgsql/bin/vcruntime140.dll")) {
  $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
  $vs = & $vswhere -latest -products '*' -property installationPath
  $crt = Get-ChildItem "$vs/VC/Redist/MSVC" -Directory | Sort-Object Name -Descending | ForEach-Object {
    Get-ChildItem "$($_.FullName)/x64" -Directory -Filter 'Microsoft.VC*.CRT' -ErrorAction SilentlyContinue
  } | Select-Object -First 1
  if (-not $crt) { throw 'Microsoft app-local runtime source unavailable' }
  Copy-Item "$($crt.FullName)/*.dll" "$package/pgsql/bin/" -Force
}
& "$package/pgsql/bin/pg_ctl.exe" --version
if ($LASTEXITCODE -ne 0) { throw 'Bundled PostgreSQL does not run' }

Copy-Item "$root/docs/native/D5.5-LEEME.txt" "$package/LEEME.txt"
$files = Get-ChildItem $package -Recurse -File | ForEach-Object {
  @{ path = $_.FullName.Substring($package.Length + 1); sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash }
}
@{ product = 'Costina'; version = $version; commit = $env:GITHUB_SHA; run = $env:GITHUB_RUN_ID
   postgres = @{ version = $postgresVersion; url = $postgresUrl; archiveSha256 = $actualSha256; pinned = [bool]$postgresSha256 }
   signed = $false; files = @($files) } | ConvertTo-Json -Depth 6 | Set-Content "$package/build-manifest.json" -Encoding utf8

$nsis = "${env:ProgramFiles(x86)}\NSIS\makensis.exe"
if (-not (Test-Path $nsis)) { choco install nsis -y --no-progress; if ($LASTEXITCODE -ne 0) { throw 'NSIS installation failed' } }
Push-Location "$root/deploy/windows"
try { & $nsis "/DVERSION=$version" installer.nsi; if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' } } finally { Pop-Location }
$setup = Join-Path $artifacts "Costina-Setup-$version.exe"
@{ file = (Split-Path $setup -Leaf); version = $version; commit = $env:GITHUB_SHA; run = $env:GITHUB_RUN_ID; signed = $false
   sha256 = (Get-FileHash $setup -Algorithm SHA256).Hash } | ConvertTo-Json | Set-Content "$artifacts/Costina-Setup-$version.json" -Encoding utf8
Write-Host "Built $setup"
