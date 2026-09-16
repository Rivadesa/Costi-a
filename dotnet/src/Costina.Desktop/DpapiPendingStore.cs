using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Costina.Client;

namespace Costina.Desktop;

// Persistencia de la orden incierta cifrada con DPAPI del usuario actual, en LOCALAPPDATA.
// Un fichero por (puerto, rol): dos puestos con roles distintos en el mismo Windows no chocan.
// Un fichero ilegible o corrupto se descarta: restaurar algo dudoso seria peor que perderlo,
// y la Idempotency-Key del servidor sigue protegiendo contra duplicados en todo caso.
// La clave de acceso NUNCA pasa por aqui.
public sealed class DpapiPendingStore(Uri endpoint, string role) : IPendingStore
{
    private static readonly byte[] Entropy = "costina-pending-v1"u8.ToArray();
    private readonly string path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Costina",
        $"pending-{endpoint.Port}-{role}.bin");

    public void Save(PendingCommand command)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var plain = JsonSerializer.SerializeToUtf8Bytes(command, ApiClient.Json);
        File.WriteAllBytes(path, ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser));
    }

    public PendingCommand? Load()
    {
        try
        {
            if (!File.Exists(path)) return null;
            var plain = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<PendingCommand>(plain, ApiClient.Json);
        }
        catch (Exception e) when (e is CryptographicException or JsonException or IOException)
        {
            Clear();
            return null;
        }
    }

    public void Clear() { try { File.Delete(path); } catch (IOException) { } }
}
