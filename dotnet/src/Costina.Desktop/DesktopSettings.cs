using System.IO;
using System.Text.Json;

namespace Costina.Desktop;

// Corte B (B2, F4): preferencias de ESTE equipo en %LOCALAPPDATA%\Costina\desktop.json: direccion del servidor, usuario del
// responsable y direccion con la que los dispositivos llegan al servidor. NUNCA secretos: ni contrasenas, ni tokens de sesion,
// ni el token del puesto (ese vive cifrado con DPAPI en DpapiDeviceStore). Lo ilegible se descarta; escribir nunca lanza.
public sealed class DesktopSettings
{
    public string Endpoint { get; set; } = "http://127.0.0.1:5088";
    public string Username { get; set; } = "";
    public string DeviceAddress { get; set; } = "";

    // Los checks apuntan la carpeta a un directorio temporal; la aplicacion usa el perfil del usuario.
    public static string? Folder { get; set; }
    // C1: carpeta de datos de ESTE equipo: preferencias, credencial cifrada del puesto (DpapiDeviceStore) y orden incierta
    // (DpapiPendingStore). Una sola raiz: con Folder apuntado a un temporal, los checks no leen ni escriben nada de la instalacion real.
    public static string DataFolder => Folder ?? System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Costina");
    public static string Path => System.IO.Path.Combine(DataFolder, "desktop.json");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static DesktopSettings Load()
    {
        try
        {
            if (!File.Exists(Path)) return new();
            var loaded = JsonSerializer.Deserialize<DesktopSettings>(File.ReadAllText(Path), Json) ?? new();
            // Fallo cerrado: un valor que parezca una credencial no se usa (y no se vuelve a escribir).
            if (LooksSecret(loaded.Endpoint) || LooksSecret(loaded.Username) || LooksSecret(loaded.DeviceAddress)) return new();
            if (loaded.Endpoint.Length == 0) loaded.Endpoint = new DesktopSettings().Endpoint;
            return loaded;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public void Save()
    {
        if (LooksSecret(Endpoint) || LooksSecret(Username) || LooksSecret(DeviceAddress)) return;
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(this, Json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public static bool LooksSecret(string value) => value.StartsWith("dev.", StringComparison.Ordinal) || value.Contains("password", StringComparison.OrdinalIgnoreCase)
        || value.Contains("token", StringComparison.OrdinalIgnoreCase) || value.Length > 300;
}
