using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Costina.Client;

namespace Costina.Desktop;

// Cabecera EN CLARO del fichero durable: la clave de idempotencia (no es secreta), la identidad de
// instalacion/ambito/rol a la que pertenece la orden y, desde D6.7 (version 3), el ACTOR que la hizo
// ("user:<nombre>" o "device:<nombre>": el de la auditoria y la barra de estado, no una credencial).
// El cuerpo va cifrado a continuacion. Una cabecera de version 2 (0.40.0 y anteriores) no lleva actor.
public sealed record PendingHeader(int V, string Key, string Installation, string Scope, string Role, string? Actor = null);

// Almacen durable de la orden incierta, cifrado con DPAPI del usuario actual.
// Aislamiento: un fichero por instalacion + ambito + rol + hueco; el hueco se reserva con un bloqueo
// entre procesos mientras la ventana vive, asi dos ventanas del mismo rol nunca comparten ni se borran
// la orden, y al rearrancar cada ventana recupera la suya (o la de una ventana caida).
// Evidencia: un fichero ilegible NO se borra; se informa con su clave y solo se aparta a cuarentena
// por decision explicita. Un fichero de otra instalacion nunca se reenvia. La clave de acceso NUNCA pasa por aqui.
// D6.7: el hueco es del ROL, y con el mismo rol entran varias identidades (cada responsable con su usuario; un dispositivo
// antiguo con rol principal). El servidor guarda la idempotencia por actor: la orden de otro actor, o una sin actor anotado,
// se devuelve BLOQUEADA (OtherActor, UnknownActor) y nunca restaurada. El contenido se descifra solo para describirla.
public sealed class DpapiPendingStore : IPendingStore, IDisposable
{
    private const int MaxSlots = 16, MaxActor = 512;
    private static readonly byte[] Entropy = "costina-pending-v2"u8.ToArray();
    private static readonly JsonSerializerOptions HeaderJson = new(JsonSerializerDefaults.Web);
    private readonly string path;
    private readonly FileStream slotLock;
    private readonly PendingHeader identity;
    public int Slot { get; }

    public DpapiPendingStore(SessionInfo session, string? directory = null)
    {
        var installation = session.InstallationId ?? throw new ArgumentException("El servidor no identifica su instalación.");
        if (installation.Length is 0 or > 64 || installation.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("Identidad de instalación no válida.");
        var role = session.Role;
        if (role.Length is 0 or > 32 || role.Any(c => !char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c) && c != '-'))
            throw new ArgumentException("Rol no válido.");
        var scope = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            session.TenantId + "\n" + session.CompanyId + "\n" + session.LocationId)))[..16].ToLowerInvariant();
        // D6.7: sin actor no se sabe de quien es cada orden: fallo cerrado (el motor lo envia en /session desde D4.1).
        var actor = session.Actor is { Length: > 0 and <= MaxActor } known ? known
            : throw new ArgumentException("El servidor no identifica quién ha entrado: actualiza el motor a la misma versión que este cliente.");
        identity = new(3, "", installation, scope, role, actor);
        var folder = directory ?? System.IO.Path.Combine(DesktopSettings.DataFolder, "pending");   // C1: misma raiz que desktop.json
        Directory.CreateDirectory(folder);
        var reserved = Reserve(folder, $"{installation}-{scope}-{role}");
        slotLock = reserved.Lock; Slot = reserved.Slot; path = reserved.Path;
    }

    // Primer hueco libre: el bloqueo exclusivo vive mientras esta instancia; el sistema lo libera si el
    // proceso muere, y la siguiente ventana que lo tome hereda la orden pendiente de ese hueco.
    private static (FileStream Lock, int Slot, string Path) Reserve(string folder, string stem)
    {
        for (var slot = 0; slot < MaxSlots; slot++)
        {
            try
            {
                var handle = new FileStream(System.IO.Path.Combine(folder, $"{stem}-{slot}.lock"),
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
                return (handle, slot, System.IO.Path.Combine(folder, $"{stem}-{slot}.bin"));
            }
            catch (IOException) { }
        }
        throw new InvalidOperationException("Demasiadas ventanas de este puesto abiertas a la vez; cierra alguna.");
    }

    public void Save(PendingCommand command)
    {
        var header = JsonSerializer.SerializeToUtf8Bytes(identity with { Key = command.Key }, HeaderJson);
        var body = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(command, ApiClient.Json), Entropy, DataProtectionScope.CurrentUser);
        var bytes = new byte[header.Length + 1 + body.Length];
        header.CopyTo(bytes, 0); bytes[header.Length] = (byte)'\n'; body.CopyTo(bytes, header.Length + 1);
        // Escritura duradera y sustitucion atomica: nunca queda un fichero final escrito a medias.
        var temporary = path + ".tmp";
        using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { file.Write(bytes); file.Flush(true); }
        File.Move(temporary, path, overwrite: true);
    }

    public PendingLoad Load()
    {
        byte[] bytes;
        try
        {
            if (!File.Exists(path)) return new(PendingOutcome.Absent, null, null, "");
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new(PendingOutcome.Unavailable, null, null, $"No se pudo leer el fichero de la orden pendiente ({e.GetType().Name}).");
        }
        var header = ReadHeader(bytes, out var offset);
        if (header is null) return new(PendingOutcome.Unreadable, null, null, "La cabecera del fichero está dañada.");
        if (header.Installation != identity.Installation || header.Scope != identity.Scope || header.Role != identity.Role)
            return new(PendingOutcome.Unreadable, null, header.Key, "La orden pertenece a otra instalación, ámbito o rol y no se reenvía.");
        var (command, damage) = ReadBody(bytes[offset..], header.Key);
        // D6.7: quien la hizo manda antes que el contenido. Sin actor anotado (0.40.0 o anterior) no se sabe si fue esta sesion;
        // de otro actor, reenviarla o consultarla con esta sesion no vale. En los dos casos se bloquea, con la orden solo para describirla.
        if (header.Actor is null)
            return new(PendingOutcome.UnknownActor, command, header.Key,
                "Se guardó con una versión anterior de Costiña, que no anotaba quién la hizo." + (damage is null ? "" : " " + damage));
        if (header.Actor != identity.Actor)
            return new(PendingOutcome.OtherActor, command, header.Key, damage ?? "", header.Actor);
        return command is not null ? new(PendingOutcome.Restored, command, command.Key, "") : new(PendingOutcome.Unreadable, null, header.Key, damage!);
    }

    // Cuerpo cifrado -> la orden, o por que no se pudo leer (sin borrar nada).
    private static (PendingCommand? Command, string? Damage) ReadBody(byte[] body, string key)
    {
        try
        {
            var plain = ProtectedData.Unprotect(body, Entropy, DataProtectionScope.CurrentUser);
            var command = JsonSerializer.Deserialize<PendingCommand>(plain, ApiClient.Json);
            return command is null || command.Key != key ? (null, "El contenido no coincide con su cabecera.") : (command, null);
        }
        catch (Exception e) when (e is CryptographicException or JsonException)
        {
            return (null, $"El contenido cifrado no se pudo descifrar o interpretar ({e.GetType().Name}).");
        }
    }

    private static PendingHeader? ReadHeader(byte[] bytes, out int bodyOffset)
    {
        bodyOffset = Array.IndexOf(bytes, (byte)'\n') + 1;
        if (bodyOffset <= 1) return null;
        try
        {
            var header = JsonSerializer.Deserialize<PendingHeader>(bytes.AsSpan(0, bodyOffset - 1), HeaderJson);
            // Version 2: sin actor (0.40.0 y anteriores). Version 3 (D6.7): con actor, obligatorio.
            return header is { V: 2, Key.Length: > 0 and <= 128, Actor: null } or { V: 3, Key.Length: > 0 and <= 128, Actor.Length: > 0 and <= MaxActor } ? header : null;
        }
        catch (JsonException) { return null; }
    }

    public void Clear(string key)
    {
        try
        {
            if (!File.Exists(path)) return;
            // Solo se borra el fichero que lleva ESTA clave confirmada, nunca uno generico del rol.
            if (ReadHeader(File.ReadAllBytes(path), out _)?.Key == key) File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public void Discard()
    {
        if (!File.Exists(path)) return;
        // Cuarentena con marca de tiempo: la evidencia sigue disponible para revisar, no se destruye.
        File.Move(path, $"{path}.cuarentena-{DateTime.UtcNow.Ticks}", overwrite: false);
    }

    public void Dispose() => slotLock.Dispose();
}
