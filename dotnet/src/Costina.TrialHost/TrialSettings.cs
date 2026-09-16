using System.Net;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Costina.TrialHost;

// Credentials belong to this Windows user and this disposable trial, not a production deployment.
public sealed record TrialSettings(int Format, string Tenant, int DatabasePort, int ApiPort,
    string BootstrapPassword, string OwnerPassword, string RuntimePassword,
    string MainKey, string ServiceKey, string KitchenKey, bool Ready)
{
    public const string Database = "costina_trial_d1_lab";
    public string Company => "restaurant";
    public string Location => "local";
    public Uri Endpoint => new($"http://127.0.0.1:{ApiPort}");
    public string Key(string role) => role switch {
        "main" => MainKey, "service" => ServiceKey, "kitchen" => KitchenKey,
        _ => throw new ArgumentException("Rol de ensayo no reconocido.")
    };
    public string Connection(string role, string? database = null) => new NpgsqlConnectionStringBuilder {
        Host="127.0.0.1", Port=DatabasePort, Database=database ?? Database,
        Username="costina_"+role,
        Password=role switch { "bootstrap"=>BootstrapPassword, "owner"=>OwnerPassword, "runtime"=>RuntimePassword, _=>throw new ArgumentException("Rol de base no reconocido.") },
        Pooling=false, Timeout=5, CommandTimeout=30, IncludeErrorDetail=false
    }.ConnectionString;
    public void Validate()
    {
        if(Format!=1 || !Tenant.StartsWith("trial-",StringComparison.Ordinal)
           || !Guid.TryParseExact(Tenant[6..],"N",out _) || DatabasePort==ApiPort
           || DatabasePort is < 1024 or > 65535 || ApiPort is < 1024 or > 65535)
            throw new InvalidOperationException("Configuración incompatible. No se restablecerán los datos.");
        var secrets=new[]{BootstrapPassword,OwnerPassword,RuntimePassword,MainKey,ServiceKey,KitchenKey};
        if(secrets.Any(x=>x.Length!=64 || !x.All(Uri.IsHexDigit)) || secrets.Distinct(StringComparer.Ordinal).Count()!=6)
            throw new InvalidOperationException("Configuración de credenciales no válida.");
    }
    public static TrialSettings Create()
    {
        string Secret()=>Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        int db=FreePort(); int api; do {api=FreePort();} while(api==db);
        return new(1,"trial-"+Guid.NewGuid().ToString("N"),db,api,Secret(),Secret(),Secret(),Secret(),Secret(),Secret(),false);
    }
    private static int FreePort()
    {
        var listener=new TcpListener(IPAddress.Loopback,0); listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; } finally {listener.Stop();}
    }
}

public static class SettingsVault
{
    private static readonly byte[] Entropy=Encoding.UTF8.GetBytes("Costina/native-trial/d13/v1");
    public static void RestrictDirectory(string path)
    {
        Directory.CreateDirectory(path);
        var user=WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("Usuario Windows desconocido.");
        var acl=new DirectorySecurity(); acl.SetAccessRuleProtection(true,false);
        foreach(var sid in new[]{user,new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null)})
            acl.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
        new DirectoryInfo(path).SetAccessControl(acl);
    }
    public static TrialSettings Load(string root)
    {
        var clear=ProtectedData.Unprotect(File.ReadAllBytes(Path.Combine(root,"installation.dat")),Entropy,DataProtectionScope.CurrentUser);
        try {
            var state=JsonSerializer.Deserialize<TrialSettings>(clear) ?? throw new InvalidOperationException("Configuración vacía.");
            state.Validate(); return state;
        } finally {CryptographicOperations.ZeroMemory(clear);}
    }
    public static void Save(string root,TrialSettings state)
    {
        state.Validate(); var clear=JsonSerializer.SerializeToUtf8Bytes(state);
        try {
            var encrypted=ProtectedData.Protect(clear,Entropy,DataProtectionScope.CurrentUser);
            var temporary=Path.Combine(root,"installation.pending");
            using(var file=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None)) {
                file.Write(encrypted); file.Flush(true);
            }
            File.Move(temporary,Path.Combine(root,"installation.dat"),true);
        } finally {CryptographicOperations.ZeroMemory(clear);}
    }
}
