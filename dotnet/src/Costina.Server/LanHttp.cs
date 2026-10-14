using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Costina.Core.Domain;
using Costina.Core.Hosting;
using Costina.Core.Persistence;
using static Costina.Core.Hosting.Requests;

namespace Costina.Server;

// B.2 (ADR-014): CONEXION SENCILLA por la red local. Los moviles y tablets llegan al motor por HTTP sin cifrar, con la IP del
// equipo (sin nombre .local ni certificado que instalar). Es un modo PROVISIONAL hasta el certificado publico con dominio
// propio: apagado por defecto, lo enciende solo el responsable (orden lan-http-set, auditada) y se aplica al momento.
// - Escucha propia (COSTINA_LAN_HTTP_PORT, por defecto 5089; 0 = sin escucha). En una instalacion, en todas las interfaces;
//   en el laboratorio solo si se pide de forma explicita y nunca fuera del propio equipo (loopback).
// - Apagada: cualquier peticion por ese puerto recibe una pagina que lo explica, ANTES de la identidad y de la PWA.
// - Encendida: lo mismo que por HTTPS (identidad, permisos, freno de intentos). Solo desde direcciones privadas: nunca se
//   atiende a un origen publico aunque un router reenvie el puerto. El firewall la abre solo en redes privadas o de dominio.
public sealed class LanHttpState(int port, bool listening)
{
    private volatile bool enabled;
    // Peticiones en curso por el puerto sencillo (sobre todo el WebSocket del tiempo real, que no vuelve a pasar por la puerta):
    // al apagarla se cortan todas en el acto, como al revocar un dispositivo.
    private readonly ConcurrentDictionary<HttpContext, byte> live = new();
    public int Port { get; } = port;
    public bool Listening { get; } = listening;
    public bool Enabled
    {
        get => enabled;
        set
        {
            enabled = value;
            if (!value) foreach (var context in live.Keys) context.Abort();
        }
    }
    internal void Enter(HttpContext context) { live[context] = 0; if (!enabled) context.Abort(); }
    internal void Leave(HttpContext context) => live.TryRemove(context, out _);
}

public sealed record LanAddress(string Address, string Adapter, bool Gateway, bool Virtual, bool Suggested);
public sealed record LanNetwork(string Name, string Category);
public sealed record LanHttpCommand(bool? Enabled = null);

public static class LanHttp
{
    public const int DefaultPort = 5089;
    public static readonly string[] Actions = ["lan-http-set"];
    public static bool Allows(string role, string action) => role == "main" && Actions.Contains(action, StringComparer.Ordinal);

    // Puerto de la escucha: en una instalacion 5089 salvo otro valor (0 = ninguna); en el laboratorio, solo si se pide.
    public static int Port(ServerSettings settings, bool laboratory, int httpPort, int tlsPort)
    {
        var text = settings.Get("COSTINA_LAN_HTTP_PORT");
        var port = text is null ? (laboratory ? 0 : DefaultPort) : int.Parse(text);
        if (port == 0) return 0;
        if (port is < 1024 or > 65535 || port == httpPort || port == tlsPort) throw new ArgumentException("Invalid LAN HTTP port.");
        return port;
    }

    // Direcciones privadas: loopback, 10/8, 172.16/12, 192.168/16, enlace local IPv4 e IPv6, IPv6 de ambito unico (fc00::/7).
    public static bool IsPrivate(IPAddress? address)
    {
        if (address is null) return false;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return true;
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254);
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return address.IsIPv6LinkLocal || (address.GetAddressBytes()[0] & 0xFE) == 0xFC;
        return false;
    }

    // Va ANTES de la PWA y de la identidad: por el puerto sencillo, o se atiende igual que por HTTPS o no se atiende nada.
    public static void Gate(WebApplication app, LanHttpState state)
    {
        if (!state.Listening) return;
        app.Use(async (context, next) =>
        {
            if (context.Request.IsHttps || context.Connection.LocalPort != state.Port) { await next(); return; }
            if (!IsPrivate(context.Connection.RemoteIpAddress))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            if (!state.Enabled)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";
                context.Response.Headers.XContentTypeOptions = "nosniff";
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(DisabledPage, context.RequestAborted);
                return;
            }
            state.Enter(context);
            try { await next(); }
            finally { state.Leave(context); }
        });
    }

    private const string DisabledPage = """
        <!doctype html><html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Costina</title></head>
        <body><h1>Conexión sencilla desactivada</h1>
        <p>Este dispositivo ha llegado al PC principal de Costina, pero la conexión sencilla por la red del local está apagada.</p>
        <p>En el PC principal, en Inicio › Dispositivos, el responsable puede activarla. Después vuelve a leer el QR.</p></body></html>
        """;

    // IPv4 privadas de este equipo con las que un movil de la misma red puede llegar al motor. Se propone la de un adaptador
    // con puerta de enlace que no sea virtual (VirtualBox, Hyper-V, VMware, WSL...): es la de la red del local.
    public static IReadOnlyList<LanAddress> Addresses()
    {
        var found = new List<(string Address, string Adapter, bool Gateway, bool Virtual)>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                var properties = nic.GetIPProperties();
                var gateway = properties.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
                var text = (nic.Name + " " + nic.Description).ToLowerInvariant();
                var isVirtual = new[] { "virtual", "vethernet", "hyper-v", "vmware", "vbox", "wsl", "docker", "loopback" }.Any(text.Contains);
                foreach (var unicast in properties.UnicastAddresses)
                {
                    var address = unicast.Address;
                    if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address) || !IsPrivate(address)) continue;
                    if (address.GetAddressBytes() is [169, 254, ..]) continue;   // sin DHCP: nadie llega por ahi
                    found.Add((address.ToString(), nic.Name, gateway, isVirtual));
                }
            }
        }
        catch (NetworkInformationException) { }
        var ordered = found.OrderBy(a => a.Virtual).ThenByDescending(a => a.Gateway).ThenBy(a => a.Address, StringComparer.Ordinal).ToList();
        var suggested = ordered.FirstOrDefault(a => a.Gateway && !a.Virtual).Address ?? ordered.FirstOrDefault(a => !a.Virtual).Address;
        return ordered.Select(a => new LanAddress(a.Address, a.Adapter, a.Gateway, a.Virtual, a.Address == suggested)).ToList();
    }

    // Redes a las que esta conectado el equipo y su tipo en Windows (public / private / domain). Con la red en "Publica" la regla
    // de firewall de Costina no se aplica y ningun movil llega: el puesto principal lo avisa. Fuera de Windows, lista vacia.
    public static IReadOnlyList<LanNetwork> Networks()
    {
        var result = new List<LanNetwork>();
        if (!OperatingSystem.IsWindows()) return result;
        try
        {
            var type = Type.GetTypeFromCLSID(new Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B"));
            if (type is null) return result;
            dynamic manager = Activator.CreateInstance(type)!;
            foreach (dynamic network in manager.GetNetworks(1))   // NLM_ENUM_NETWORK_CONNECTED
            {
                int category = network.GetCategory();
                result.Add(new LanNetwork((string)network.GetName(), category switch { 0 => "public", 1 => "private", 2 => "domain", _ => "unknown" }));
            }
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidCastException
            or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or TypeLoadException) { }
        return result;
    }

    // Rutas (nucleo, solo main): estado de la red para el puesto principal y la orden que enciende o apaga la conexion sencilla.
    public static void MapLanHttpRoutes(this WebApplication app, PostgresStore store, BusinessScope scope, LanHttpState state, int tlsPort, bool tlsEnabled)
    {
        const string prefix = "/api/native/v1";
        app.MapGet(prefix + "/installation/network", (Func<HttpContext, Task<IResult>>)(async c =>
        {
            state.Enabled = await store.LanHttpAsync(c.RequestAborted);
            return Results.Json(new
            {
                lanHttp = new { enabled = state.Enabled, port = state.Listening ? state.Port : (int?)null, listening = state.Listening },
                tls = new { enabled = tlsEnabled, port = tlsEnabled ? tlsPort : (int?)null },
                addresses = Addresses(),
                networks = Networks()
            }, Wire.Json);
        })).WithMetadata(new RouteAccess("main"));
        app.MapPost(prefix + "/installation/commands/{action}", async (HttpContext c, string action) =>
        {
            var result = await Write<LanHttpCommand>(c, store, scope, (u, i, r) => Apply(u, i, action, r, state));
            state.Enabled = await store.LanHttpAsync(c.RequestAborted);   // tambien tras una repeticion idempotente
            return result;
        }).WithMetadata(new RouteAccess("main") { ActionPolicy = Allows });
    }

    private static async Task<object> Apply(Unit unit, ExecutionIdentity identity, string action, LanHttpCommand request, LanHttpState state)
    {
        if (action != "lan-http-set") throw new StoreNotFound();
        if (request.Enabled is not { } enabled) throw new ArgumentException("enabled is required.");
        if (enabled && !state.Listening) throw new RuleViolation("lan_http_unavailable", "This engine has no LAN HTTP listener (COSTINA_LAN_HTTP_PORT).");
        await unit.Sql("UPDATE core.installation SET lan_http=@enabled", [("enabled", enabled)]);
        await unit.Events([new DomainEvent(Guid.NewGuid(), enabled ? "installation.lan_http_enabled" : "installation.lan_http_disabled", "installation",
            identity.Scope, identity.ActorId, DateTimeOffset.UtcNow, new Dictionary<string, string> { ["port"] = state.Port.ToString() })]);
        return new { lanHttp = new { enabled, port = state.Port } };
    }
}
