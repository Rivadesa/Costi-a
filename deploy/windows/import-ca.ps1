# D5.5 — Puesto adicional: confiar en la CA LOCAL del servidor Costina (D5.3).
# Solo acepta una raiz de Costina CON restricciones de nombre criticas: asi, instalarla no permite a
# nadie suplantar otras webs ante este equipo. -ShowOnly imprime la huella para compararla con la que
# muestra el servidor (provision-tls o /diagnostics) ANTES de confiar.
param([Parameter(Mandatory = $true)][string]$Path, [switch]$ShowOnly)
$ErrorActionPreference = 'Stop'
try {
    $certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new((Resolve-Path $Path).Path)
    if ($certificate.Subject -notlike 'CN=Costina Local CA *') { throw "No es una CA local de Costina: $($certificate.Subject)" }
    $constraints = $certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.30' }
    if (-not $constraints -or -not $constraints.Critical) { throw 'La CA no lleva restricciones de nombre criticas: se rechaza.' }
    $basic = $certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.19' }
    if (-not $basic -or -not $basic.CertificateAuthority) { throw 'El certificado no es una autoridad de certificacion.' }
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    $fingerprint = ([System.BitConverter]::ToString($sha256.ComputeHash($certificate.RawData))).Replace('-', ':')
    if ($ShowOnly) { Write-Output $fingerprint; exit 0 }
    $store = [System.Security.Cryptography.X509Certificates.X509Store]::new('Root', 'LocalMachine')
    $store.Open('ReadWrite'); $store.Add($certificate); $store.Close()
    Write-Output "CA local de Costina instalada. Huella SHA-256: $fingerprint"
    exit 0
}
catch { Write-Output "ERROR: $_"; exit 1 }
