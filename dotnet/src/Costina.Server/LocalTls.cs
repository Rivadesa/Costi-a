using System.Collections.Concurrent;
using System.Formats.Asn1;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace Costina.Server;

// D5.3 (#27, ADR-011): HTTPS en la LAN con una CA LOCAL de la instalacion (decision del promotor: el
// servidor es local-primary y debe funcionar sin Internet). La raiz se instala una vez en cada
// dispositivo; a partir de ahi el certificado valida sin excepciones y NUNCA se pide ignorar avisos.
//
// Una raiz privada instalada en tablets es un riesgo si se filtra su clave: podria suplantar
// cualquier web ante esos dispositivos. Por eso la CA nace con RESTRICCIONES DE NOMBRE criticas:
// solo puede avalar los nombres del servidor indicados al crearla y direcciones IPv4 privadas.
// Su clave queda en tls\ca.key, fuera del alcance de la cuenta del servicio; el motor solo lee
// tls\server.pfx. Renovar el certificado del servidor no obliga a tocar los dispositivos.
public static partial class LocalTls
{
    public static string Folder => Path.Combine(ServerSettings.DataRoot,"tls");
    public static string CaCertificate => Path.Combine(Folder,"ca.crt");
    public static string CaKey => Path.Combine(Folder,"ca.key");
    public static string ServerPfx => Path.Combine(Folder,"server.pfx");
    public static string ServerCertificate => Path.Combine(Folder,"server.crt");

    [GeneratedRegex(@"^(?=.{1,253}$)[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?(\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)*$")]
    private static partial Regex DnsName();

    // Redes privadas y de enlace local (direccion + mascara): unico espacio IP que la CA puede avalar.
    private static readonly (byte[] Address,byte[] Mask)[] PrivateNetworks =
    [
        (new byte[]{10,0,0,0},new byte[]{255,0,0,0}),(new byte[]{172,16,0,0},new byte[]{255,240,0,0}),
        (new byte[]{192,168,0,0},new byte[]{255,255,0,0}),(new byte[]{169,254,0,0},new byte[]{255,255,0,0}),
        (new byte[]{127,0,0,0},new byte[]{255,0,0,0})
    ];

    public static async Task ProvisionAsync(ServerSettings settings,bool renew)
    {
        if(!File.Exists(ServerSettings.ServerFile))
            throw new InvalidOperationException("This data root is not provisioned; run provision first.");
        var host=Environment.MachineName.ToLowerInvariant();
        var names=(settings.Get("COSTINA_TLS_NAMES") ?? host+","+host+".local")
            .Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(n=>n.ToLowerInvariant()).Distinct().ToArray();
        if(names.Length==0) throw new ArgumentException("COSTINA_TLS_NAMES is empty.");
        var dns=new List<string>(); var addresses=new List<IPAddress>();
        foreach(var name in names)
        {
            if(IPAddress.TryParse(name,out var address))
            {
                if(address.AddressFamily!=AddressFamily.InterNetwork || !IsPrivate(address))
                    throw new ArgumentException($"{name}: only private IPv4 addresses can be certified by the local CA.");
                addresses.Add(address);
            }
            else if(DnsName().IsMatch(name)) dns.Add(name);
            else throw new ArgumentException($"{name} is neither a DNS name nor an IPv4 address.");
        }
        if(dns.Count==0) throw new ArgumentException("At least one stable DNS name is required: addresses change, names should not.");

        Directory.CreateDirectory(Folder);
        var now=DateTimeOffset.UtcNow;
        X509Certificate2 authority;
        if(renew)
        {
            if(!File.Exists(CaCertificate) || !File.Exists(CaKey))
                throw new InvalidOperationException("There is no local CA to renew from; run provision-tls first.");
            authority=X509Certificate2.CreateFromPemFile(CaCertificate,CaKey);
        }
        else
        {
            if(File.Exists(CaCertificate) || File.Exists(CaKey))
                throw new InvalidOperationException("This installation already has a local CA that devices may trust; use renew-tls. Replacing the CA means re-installing it on every device.");
            using var authorityKey=ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var request=new CertificateRequest($"CN=Costina Local CA {Convert.ToHexString(RandomNumberGenerator.GetBytes(4))}, O=Costina",authorityKey,HashAlgorithmName.SHA256);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,true,0,true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign|X509KeyUsageFlags.CrlSign,true));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey,false));
            request.CertificateExtensions.Add(NameConstraints(dns));
            authority=request.CreateSelfSigned(now.AddDays(-1),now.AddYears(10));
            WritePrivate(CaKey,authorityKey.ExportPkcs8PrivateKeyPem());
            File.WriteAllText(CaCertificate,authority.ExportCertificatePem());
        }
        using(authority)
        {
            using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var request=new CertificateRequest($"CN={dns[0]}",key,HashAlgorithmName.SHA256);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false,false,0,true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature,true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")],false));
            var alternative=new SubjectAlternativeNameBuilder();
            foreach(var name in dns) alternative.AddDnsName(name);
            foreach(var address in addresses) alternative.AddIpAddress(address);
            request.CertificateExtensions.Add(alternative.Build());
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey,false));
            request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(authority,true,false));
            var serial=RandomNumberGenerator.GetBytes(16); serial[0]&=0x7F;
            // 397 dias: por debajo del maximo que aceptan los navegadores moviles; renew-tls lo reemite sin tocar dispositivos.
            var notAfter=now.AddDays(397)<authority.NotAfter ? now.AddDays(397) : new DateTimeOffset(authority.NotAfter).AddDays(-1);
            using var certificate=request.Create(authority,now.AddDays(-1),notAfter,serial);
            using var complete=certificate.CopyWithPrivateKey(key);
            var temporary=ServerPfx+".tmp";
            File.WriteAllBytes(temporary,complete.Export(X509ContentType.Pfx));
            if(!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary,UnixFileMode.UserRead|UnixFileMode.UserWrite);
            File.Move(temporary,ServerPfx,overwrite:true);
            File.WriteAllText(ServerCertificate,certificate.ExportCertificatePem());
            Console.WriteLine($"Local CA SHA-256 fingerprint: {Fingerprint(authority)}");
            Console.WriteLine($"Server certificate for {string.Join(", ",names)} valid until {certificate.NotAfter.ToUniversalTime():yyyy-MM-dd}. Install {CaCertificate} on each device and compare the fingerprint. Restart the engine to serve HTTPS.");
        }
        if(OperatingSystem.IsWindows()) await ServiceInstaller.ProtectTlsAsync();
    }

    public static string Fingerprint(X509Certificate2 certificate) =>
        string.Join(':',Convert.ToHexString(certificate.GetCertHash(HashAlgorithmName.SHA256)).Chunk(2).Select(pair=>new string(pair)));

    private static bool IsPrivate(IPAddress address)
    {
        var bytes=address.GetAddressBytes();
        return PrivateNetworks.Any(network=>bytes.Select((b,i)=>(byte)(b&network.Mask[i])).SequenceEqual(network.Address));
    }

    // RFC 5280 4.2.1.10: NameConstraints ::= SEQUENCE { permittedSubtrees [0] GeneralSubtrees }.
    // dNSName [2] IA5String; iPAddress [7] OCTET STRING (direccion + mascara). Critica.
    private static X509Extension NameConstraints(IEnumerable<string> dns)
    {
        var writer=new AsnWriter(AsnEncodingRules.DER);
        using(writer.PushSequence())
        using(writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific,0)))
        {
            foreach(var name in dns)
                using(writer.PushSequence()) writer.WriteCharacterString(UniversalTagNumber.IA5String,name,new Asn1Tag(TagClass.ContextSpecific,2));
            foreach(var (address,mask) in PrivateNetworks)
                using(writer.PushSequence()) writer.WriteOctetString(address.Concat(mask).ToArray(),new Asn1Tag(TagClass.ContextSpecific,7));
        }
        return new X509Extension("2.5.29.30",writer.Encode(),critical:true);
    }

    private static void WritePrivate(string path,string content)
    {
        File.WriteAllText(path,"");
        if(!OperatingSystem.IsWindows()) File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite);
        File.WriteAllText(path,content);
    }
}

// D5.3: al abrir la LAN, el freno de 400 ms por intento deja de bastar. Tope de fallos por origen y
// objetivo (IP + usuario, o IP + emparejamiento): 10 fallos en 5 minutos bloquean 60 s con 429. En
// memoria del proceso; acotado. No sustituye a contrasenas fuertes ni a la revocacion.
public sealed class AttemptThrottle
{
    private sealed class Entry { public readonly Queue<DateTimeOffset> Failures=new(); public DateTimeOffset BlockedUntil; }
    private readonly ConcurrentDictionary<string,Entry> entries=new(StringComparer.Ordinal);
    private const int Limit=10;
    private static readonly TimeSpan Window=TimeSpan.FromMinutes(5), Block=TimeSpan.FromSeconds(60);

    public static string Key(HttpContext context,string target) =>
        (context.Connection.RemoteIpAddress?.ToString() ?? "?")+"|"+target.Trim().ToLowerInvariant();

    private static string Bounded(string key) => key.Length>200 ? key[..200] : key;

    public bool IsBlocked(string key)
    {
        if(!entries.TryGetValue(Bounded(key),out var entry)) return false;
        lock(entry) return entry.BlockedUntil>DateTimeOffset.UtcNow;
    }

    public void Failed(string key)
    {
        if(entries.Count>10_000) entries.Clear(); // cota dura de memoria ante un barrido de objetivos
        var entry=entries.GetOrAdd(Bounded(key),_=>new Entry());
        lock(entry)
        {
            var now=DateTimeOffset.UtcNow;
            while(entry.Failures.Count>0 && now-entry.Failures.Peek()>Window) entry.Failures.Dequeue();
            entry.Failures.Enqueue(now);
            if(entry.Failures.Count>=Limit) { entry.BlockedUntil=now+Block; entry.Failures.Clear(); }
        }
    }

    public void Succeeded(string key) => entries.TryRemove(Bounded(key),out _);
}
