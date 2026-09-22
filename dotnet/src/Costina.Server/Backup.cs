using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Costina.Core.Domain;
using Costina.Core.Persistence;
using Npgsql;

namespace Costina.Server;

// D5.4 (#27, ADR-011): copia de seguridad automatizada con RESTAURACION VERIFICABLE.
//  - Copia: pg_dump (formato custom) del esquema native_d1 con el rol de ejecucion, dentro de una
//    instantanea exportada; en esa MISMA instantanea se calcula la huella de cada tabla y se guarda
//    en un manifiesto junto al SHA-256 del fichero. El archivo se comprueba legible (pg_restore --list).
//  - Restauracion: solo sobre una instalacion recien aprovisionada (nunca pisa datos), con el rol
//    propietario; despues recalcula las huellas y exige que coincidan con el manifiesto.
// Una copia en el mismo disco no protege de perder el disco: COSTINA_BACKUP_COPY replica cada copia
// en un segundo destino. Las copias contienen datos del negocio y hashes de credenciales: sensibles.
public sealed record BackupManifest(int Format, DateTimeOffset CreatedAt, string ServerVersion, string InstallationId,
    string TenantId, string CompanyId, string LocationId, string File, long SizeBytes, string Sha256,
    SortedDictionary<string, TableDigest> Tables);

public sealed class BackupHealth
{
    private readonly object gate = new();
    private DateTimeOffset? lastSuccessAt; private string? lastFile, lastError; private bool? copyReplicated;
    public (DateTimeOffset? LastSuccessAt, string? LastFile, string? LastError, bool? CopyReplicated) Read() { lock (gate) return (lastSuccessAt, lastFile, lastError, copyReplicated); }
    public void Succeeded(DateTimeOffset at, string file, bool? replicated) { lock (gate) { lastSuccessAt = at; lastFile = file; lastError = null; copyReplicated = replicated; } }
    public void Failed(string error) { lock (gate) lastError = error; }
}

public sealed class BackupRunner(ServerSettings settings, BusinessScope scope, string serverVersion)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static string Folder => Path.Combine(ServerSettings.DataRoot, "backups");
    public string? CopyFolder => settings.Get("COSTINA_BACKUP_COPY");
    private int Keep => int.TryParse(settings.Get("COSTINA_BACKUP_KEEP"), out var keep) && keep is >= 1 and <= 3650 ? keep : 14;

    private string Tool(string name)
    {
        var executable = OperatingSystem.IsWindows() ? name + ".exe" : name;
        return settings.Get("COSTINA_PG_BIN") is { Length: > 0 } folder ? Path.Combine(folder, executable) : executable;
    }

    // Credenciales por ENTORNO del proceso hijo, nunca por argumentos (visibles en la lista de procesos).
    private async Task<string> RunTool(string name, NpgsqlConnectionStringBuilder connection, IEnumerable<string> arguments, CancellationToken ct)
    {
        var info = new ProcessStartInfo(Tool(name)) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        info.Environment["PGHOST"] = connection.Host; info.Environment["PGPORT"] = connection.Port.ToString();
        info.Environment["PGDATABASE"] = connection.Database; info.Environment["PGUSER"] = connection.Username;
        info.Environment["PGPASSWORD"] = connection.Password;
        Process process;
        try { process = Process.Start(info) ?? throw new InvalidOperationException("Cannot start " + name); }
        catch (System.ComponentModel.Win32Exception)
        { throw new InvalidOperationException($"{name} was not found. Set COSTINA_PG_BIN to the PostgreSQL bin directory of the same major version as the server."); }
        using (process)
        {
            var output = process.StandardOutput.ReadToEndAsync(ct); var error = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            if (process.ExitCode != 0) throw new InvalidOperationException($"{name} failed with code {process.ExitCode}: {(await error).Trim()}");
            return await output;
        }
    }

    public async Task<BackupManifest> BackupAsync(string runtimeConnection, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Folder);
        var builder = new NpgsqlConnectionStringBuilder(runtimeConnection);
        var createdAt = DateTimeOffset.UtcNow;
        var name = $"costina-{createdAt:yyyyMMdd-HHmmss}Z.backup";
        var temporary = Path.Combine(Folder, name + ".tmp");
        try
        {
            SortedDictionary<string, TableDigest> tables; string installation;
            await using (var source = NpgsqlDataSource.Create(runtimeConnection))
            await using (var connection = await source.OpenConnectionAsync(ct))
            await using (var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct))
            {
                string snapshot;
                await using (var export = new NpgsqlCommand("SELECT pg_export_snapshot()", connection, transaction))
                    snapshot = (string)(await export.ExecuteScalarAsync(ct))!;
                await using (var identity = new NpgsqlCommand("SELECT id::text FROM native_d1.installation", connection, transaction))
                    installation = (string)(await identity.ExecuteScalarAsync(ct))!;
                tables = await BackupDigest.ComputeAsync(connection, transaction, ct);
                // La transaccion sigue abierta mientras pg_dump copia: la copia y las huellas ven exactamente lo mismo.
                await RunTool("pg_dump", builder, ["--format=custom", "--schema=native_d1", "--snapshot=" + snapshot, "--no-password", "--file=" + temporary], ct);
                await transaction.CommitAsync(ct);
            }
            await RunTool("pg_restore", builder, ["--list", temporary], ct); // el archivo es legible de principio a fin
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            string sha;
            await using (var stream = File.OpenRead(temporary)) sha = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
            var manifest = new BackupManifest(1, createdAt, serverVersion, installation, scope.TenantId, scope.CompanyId, scope.LocationId,
                name, new FileInfo(temporary).Length, sha, tables);
            var final = Path.Combine(Folder, name);
            File.Move(temporary, final);
            File.WriteAllText(final + ".json", JsonSerializer.Serialize(manifest, Json));
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(final + ".json", UnixFileMode.UserRead | UnixFileMode.UserWrite);
            Prune(Folder);
            return manifest;
        }
        finally { try { File.Delete(temporary); } catch (IOException) { } }
    }

    // Segundo destino (otro disco, NAS): se copia y se comprueba el SHA-256 de lo copiado.
    public async Task<bool?> ReplicateAsync(BackupManifest manifest, CancellationToken ct = default)
    {
        if (CopyFolder is not { Length: > 0 } destination) return null;
        Directory.CreateDirectory(destination);
        var target = Path.Combine(destination, manifest.File);
        File.Copy(Path.Combine(Folder, manifest.File), target, overwrite: true);
        File.Copy(Path.Combine(Folder, manifest.File + ".json"), target + ".json", overwrite: true);
        await using (var stream = File.OpenRead(target))
            if (Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant() != manifest.Sha256)
                throw new IOException("The replicated backup does not match its checksum.");
        Prune(destination);
        return true;
    }

    private void Prune(string folder)
    {
        foreach (var old in Directory.GetFiles(folder, "costina-*.backup").OrderDescending(StringComparer.Ordinal).Skip(Keep))
        { File.Delete(old); File.Delete(old + ".json"); }
    }

    public static BackupManifest? Latest()
    {
        if (!Directory.Exists(Folder)) return null;
        foreach (var file in Directory.GetFiles(Folder, "costina-*.backup.json").OrderDescending(StringComparer.Ordinal))
            try { if (JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(file), Json) is { } manifest) return manifest; }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { }
        return null;
    }

    // Restauracion explicita con el rol PROPIETARIO sobre una instalacion recien aprovisionada.
    public async Task RestoreAsync(string backupFile, string ownerConnection, CancellationToken ct = default)
    {
        backupFile = Path.GetFullPath(backupFile);
        if (!File.Exists(backupFile) || !File.Exists(backupFile + ".json"))
            throw new InvalidOperationException("Backup file or its .json manifest not found; both are required.");
        var manifest = JsonSerializer.Deserialize<BackupManifest>(await File.ReadAllTextAsync(backupFile + ".json", ct), Json)
            ?? throw new InvalidDataException("Unreadable backup manifest.");
        await using (var stream = File.OpenRead(backupFile))
            if (Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant() != manifest.Sha256)
                throw new InvalidDataException("Backup checksum mismatch: the file is damaged or was altered. Nothing was restored.");
        if ((manifest.TenantId, manifest.CompanyId, manifest.LocationId) != (scope.TenantId, scope.CompanyId, scope.LocationId))
            throw new InvalidOperationException($"Backup scope {manifest.TenantId}/{manifest.CompanyId}/{manifest.LocationId} differs from this installation's scope; provision the new installation with the same scope. Nothing was restored.");
        await using var source = NpgsqlDataSource.Create(ownerConnection);
        var store = new PostgresStore(source);
        if (await store.SchemaExistsAsync(ct))
            throw new InvalidOperationException("This installation already has data. Restore only runs on a freshly provisioned installation and never overwrites. Nothing was restored.");
        await RunTool("pg_restore", new NpgsqlConnectionStringBuilder(ownerConnection),
            ["--dbname=" + new NpgsqlConnectionStringBuilder(ownerConnection).Database, "--no-owner", "--no-acl", "--single-transaction", "--exit-on-error", "--no-password", backupFile], ct);
        SortedDictionary<string, TableDigest> restored;
        await using (var connection = await source.OpenConnectionAsync(ct))
        await using (var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct))
            restored = await BackupDigest.ComputeAsync(connection, transaction, ct);
        var differences = manifest.Tables.Keys.Union(restored.Keys).Where(table =>
            !manifest.Tables.TryGetValue(table, out var expected) || !restored.TryGetValue(table, out var actual) || expected != actual).ToArray();
        if (differences.Length > 0)
            throw new InvalidDataException("Restored data does NOT match the backup manifest in: " + string.Join(", ", differences) + ". Do not operate this installation.");
        // Privilegios del rol de ejecucion y, si los binarios son mas nuevos que la copia, esquema al dia.
        await store.InitializeAsync(ct);
        Console.WriteLine($"Restored and verified {manifest.Tables.Count} tables ({manifest.Tables.Values.Sum(t => t.Rows)} rows) from {manifest.File} taken at {manifest.CreatedAt:O}. Installation identity {manifest.InstallationId} preserved.");
    }
}

// Copia diaria desatendida (solo modo instalacion): a la hora local configurada, o en cuanto la
// ultima copia tenga mas de 30 horas (incluido el primer arranque). Un fallo nunca tumba el motor:
// se registra y queda visible en /diagnostics.
public sealed class BackupService(BackupRunner runner, BackupHealth health, ServerSettings settings, string runtimeConnection, ILogger<BackupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var hour = int.TryParse(settings.Get("COSTINA_BACKUP_HOUR"), out var configured) && configured is >= 0 and <= 23 ? configured : 5;
        if (BackupRunner.Latest() is { } latest) health.Succeeded(latest.CreatedAt, latest.File, null);
        try { await Task.Delay(TimeSpan.FromSeconds(5), ct); } catch (OperationCanceledException) { return; }
        while (!ct.IsCancellationRequested)
        {
            var age = DateTimeOffset.UtcNow - (health.Read().LastSuccessAt ?? DateTimeOffset.MinValue);
            if (age > TimeSpan.FromHours(30) || (age > TimeSpan.FromHours(20) && DateTime.Now.Hour == hour))
            {
                try
                {
                    var manifest = await runner.BackupAsync(runtimeConnection, ct);
                    bool? replicated = null;
                    try { replicated = await runner.ReplicateAsync(manifest, ct); }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    { replicated = false; logger.LogError("Backup replication failed: {ErrorType}", e.GetType().Name); }
                    health.Succeeded(manifest.CreatedAt, manifest.File, replicated);
                    logger.LogInformation("Backup {File} written ({Bytes} bytes)", manifest.File, manifest.SizeBytes);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                catch (Exception e)
                {
                    health.Failed(e is InvalidOperationException ? e.Message : e.GetType().Name);
                    logger.LogError("Backup failed: {ErrorType}", e.GetType().Name);
                    // Tras un fallo no se reintenta cada minuto: siguiente intento en 15 minutos.
                    try { await Task.Delay(TimeSpan.FromMinutes(15), ct); } catch (OperationCanceledException) { return; }
                }
            }
            try { await Task.Delay(TimeSpan.FromMinutes(1), ct); } catch (OperationCanceledException) { return; }
        }
    }
}
