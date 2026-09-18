using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Costina.Desktop;

// Credencial durable del puesto emparejado, cifrada con DPAPI del usuario actual (mismo criterio
// que la orden incierta): un fichero por puerto, ilegible en claro, corrupto se descarta y borra.
// La contrasena del USUARIO nunca pasa por aqui; solo el token del dispositivo emitido en D4.2.
public sealed class DpapiDeviceStore(Uri endpoint)
{
    private static readonly byte[] Entropy = "costina-device-v1"u8.ToArray();
    private readonly string path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Costina",
        // D5.3: un puesto puede emparejarse con servidores de la LAN; el fichero distingue tambien el equipo.
        endpoint.Host == "127.0.0.1" ? $"device-{endpoint.Port}.bin"
            : $"device-{new string(endpoint.Host.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' ? c : '_').ToArray())}-{endpoint.Port}.bin");

    public void Save(string deviceToken)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, ProtectedData.Protect(Encoding.UTF8.GetBytes(deviceToken), Entropy, DataProtectionScope.CurrentUser));
    }

    public string? Load()
    {
        try
        {
            if (!File.Exists(path)) return null;
            var token = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser));
            return token.StartsWith("dev.", StringComparison.Ordinal) ? token : null;
        }
        catch (Exception e) when (e is CryptographicException or IOException or FormatException)
        {
            Clear();
            return null;
        }
    }

    public void Clear() { try { File.Delete(path); } catch (IOException) { } }
}
