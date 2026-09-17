using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Costina.Domain;
using Costina.Persistence;
using Costina.Server;
using Npgsql;

// Deliberately local-only engineering cut. Installer/user/device authentication are separate D1/D2 work.
if(Environment.GetEnvironmentVariable("COSTINA_LAB_MODE") != "true")
    throw new InvalidOperationException("D1.1 is restricted to an explicit laboratory installation.");
string RequiredEnvironment(string name) => Environment.GetEnvironmentVariable(name)
    ?? throw new InvalidOperationException($"Missing {name}; no default credentials are provided.");
var connectionString = RequiredEnvironment("COSTINA_DB");
var db = new NpgsqlConnectionStringBuilder(connectionString).Database ?? "";
if(!db.EndsWith("_d1_lab",StringComparison.Ordinal) && !db.EndsWith("_d1_test",StringComparison.Ordinal))
    throw new InvalidOperationException("Use an isolated database ending in _d1_lab or _d1_test, never the legacy database.");
var scope = new BusinessScope(RequiredEnvironment("COSTINA_TENANT"),RequiredEnvironment("COSTINA_COMPANY"),RequiredEnvironment("COSTINA_LOCATION"));
await using var source = NpgsqlDataSource.Create(connectionString);
var store = new PostgresStore(source);
if(args.Length == 1 && args[0] == "init-lab")
{
    await store.InitializeAsync(); await LabConfiguration.Seed(store,scope);
    Console.WriteLine("D1 laboratory schema/fixtures initialized. Existing data was not reset."); return;
}
// D4.1: alta explicita de usuarios. La contrasena entra por stdin (nunca argumento ni variable
// de entorno, nunca eco ni log). No hay credenciales por defecto: sin usuarios solo funcionan
// las claves de laboratorio, que se retiran en D4.3.
if(args.Length == 3 && args[0] == "create-user")
{
    await store.CheckAsync();
    var password = Console.ReadLine() ?? "";
    var created = await new IdentityStore(source,scope).CreateUserAsync(args[1],password,args[2]);
    Console.WriteLine($"User created with role {args[2]} (id {created}). Tokens are issued only at login.");
    return;
}
if(args.Length != 0) throw new ArgumentException("Only init-lab, create-user <username> <role> or normal startup is supported.");
await store.CheckAsync();
var installation = await store.InstallationAsync();
// Version unica del paquete (csproj) y commit del build (SourceLink): una sola fuente para /health y /session.
var informational = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
var plus = informational.IndexOf('+');
var serverVersion = plus < 0 ? informational : informational[..plus];
var serverBuild = plus < 0 ? "" : informational[(plus + 1)..];
var roles = new[] { "main", "service", "kitchen" };
var keys = roles.ToDictionary(r=>r,r=>RequiredEnvironment("COSTINA_KEY_"+r.ToUpperInvariant()));
if(keys.Values.Any(k=>k.Length<32) || keys.Values.Distinct().Count()!=3)
    throw new InvalidOperationException("Provide three distinct random access keys of at least 32 characters.");
var port = int.Parse(Environment.GetEnvironmentVariable("COSTINA_PORT") ?? "5088");
if(port is < 1024 or > 65535) throw new ArgumentException("Invalid laboratory port.");
var builder=WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService(options=>options.ServiceName="Costina D1 Laboratory");
builder.WebHost.ConfigureKestrel(options=> { options.Listen(IPAddress.Loopback,port); options.Limits.MaxRequestBodySize=32768; });
builder.Services.AddSingleton(store);
builder.Services.AddSingleton(source);
builder.Services.AddSingleton(scope);
builder.Services.AddSingleton<IdentityStore>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IEventSink,HubEventSink>();
builder.Services.AddSingleton<OutboxPublisher>();
builder.Services.AddHostedService<OutboxPublisherService>();
var app=builder.Build();
var identityStore=app.Services.GetRequiredService<IdentityStore>();
app.UseRouting();
app.Use(async (context,next)=>
{
    context.Response.Headers.CacheControl="no-store";
    // Eco de la clave de idempotencia: cualquier respuesta, incluido un rechazo, identifica el comando
    // al que contesta. El cliente no cierra una incertidumbre con una respuesta que no la lleve.
    var idempotency=context.Request.Headers["Idempotency-Key"].ToString();
    if(idempotency.Length is >0 and <=128 && idempotency.All(ch=>ch is >= '!' and <= '~'))
        context.Response.Headers["Idempotency-Key"]=idempotency;
    try
    {
        if(context.Request.Path=="/health" || context.Request.Path=="/api/native/v1/auth/login") { await next(); return; }
        var bearer=context.Request.Headers.Authorization.ToString();
        var provided=bearer.StartsWith("Bearer ",StringComparison.Ordinal) ? bearer[7..] : "";
        // D4.1: primero sesion de usuario (token de corta vida con renovacion deslizante y
        // revocacion); las claves de rol de laboratorio siguen como respaldo hasta D4.3.
        string? role=null; AuthenticatedSession? userSession=null;
        if(provided.Length>0)
        {
            userSession=await identityStore.AuthenticateAsync(provided,context.RequestAborted);
            if(userSession is not null) role=userSession.Role;
            else
            {
                var digest=SHA256.HashData(Encoding.UTF8.GetBytes(provided));
                role=keys.FirstOrDefault(k=>CryptographicOperations.FixedTimeEquals(digest,SHA256.HashData(Encoding.UTF8.GetBytes(k.Value)))).Key;
            }
        }
        if(role is null) { context.Response.StatusCode=401; await context.Response.WriteAsJsonAsync(new {error="unauthorized"}); return; }
        if(context.Request.Headers.Keys.Any(k=>new[]{"X-Tenant-Id","X-Company-Id","X-Location-Id"}.Contains(k,StringComparer.OrdinalIgnoreCase)))
            throw new ArgumentException("Scope is assigned by this server, not request headers.");
        context.Items["role"]=role;
        // Actor de auditoria: la persona real cuando hay sesion; el rol de laboratorio si no.
        context.Items["actor"]=userSession is null ? "lab-"+role : "user:"+userSession.Username;
        context.Items["sessionId"]=userSession?.SessionId;
        var access=context.GetEndpoint()?.Metadata.GetMetadata<RouteAccess>();
        var action=context.Request.RouteValues["action"]?.ToString();
        bool allowed=access is not null && access.Roles.Contains(role,StringComparer.Ordinal);
        // La restriccion de cocina aplica a los comandos de comedor ({action} presente).
        // Un POST sin action con metadatos que admiten kitchen (negociacion del hub) no es un comando.
        if (role=="kitchen" && HttpMethods.IsPost(context.Request.Method) && action is not null)
            allowed &= action is "preparation-start" or "preparation-ready" or "ready" or "review-preparation";
        if (role=="service" && action is "complete" or "cancel-unstarted" or "review-preparation") allowed=false;
        if(!allowed) { context.Response.StatusCode=403; await context.Response.WriteAsJsonAsync(new {error="forbidden"}); return; }
        await next();
    }
    catch(Exception e) when(e is RuleViolation or StoreConflict or StoreNotFound or ArgumentException or JsonException or OverflowException or BadHttpRequestException)
    {
        context.Response.StatusCode=e is StoreNotFound ? 404 : e is RuleViolation or StoreConflict ? 409 : 422;
        await context.Response.WriteAsJsonAsync(new {error=e is RuleViolation r ? r.Code : e is StoreConflict c ? c.Code : e is StoreNotFound ? "not_found" : "invalid_request"});
    }
    catch(PostgresException e) when(e.SqlState is "23505" or "40001" or "40P01" or "55P03")
    {
        context.Response.StatusCode=409;
        await context.Response.WriteAsJsonAsync(new {error=e.ConstraintName=="one_active_occupancy_per_table" ? "table_occupied" : "storage_conflict"});
    }
    catch(Exception e)
    {
        app.Logger.LogError("D1 request failed: {ErrorType}",e.GetType().Name);
        context.Response.StatusCode=503;
        await context.Response.WriteAsJsonAsync(new {error="operation_unconfirmed",message="Reload state and retry with the same key."});
    }
});
ExecutionIdentity Identity(HttpContext context)=>new(scope,(string)context.Items["actor"]!);
IResult Json(object value)=>Results.Text(Wire.Encode(value),"application/json");
async Task<IResult> Write<T>(HttpContext context,Func<Unit,ExecutionIdentity,T,Task<object>> work)
{
    using var reader=new StreamReader(context.Request.Body);
    var body=await reader.ReadToEndAsync(context.RequestAborted);
    if(body.Length>32768) throw new ArgumentException("Request too large.");
    var request=JsonSerializer.Deserialize<T>(body,Wire.Json);
    if(request is null) throw new ArgumentException("Empty command.");
    var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(context.Request.Method+"\n"+context.Request.Path+"\n"+body)));
    var identity=Identity(context);
    var response=await store.ExecuteAsync(identity,context.Request.Headers["Idempotency-Key"].ToString(),fingerprint,
        unit=>work(unit,identity,request),context.RequestAborted);
    return Results.Text(response,"application/json");
}
const string prefix="/api/native/v1";
app.MapGet("/health",async ()=>{ await store.CheckAsync(); return Results.Json(new {status="ready",mode="local-laboratory",version=serverVersion,build=serverBuild}); });
string Role(HttpContext c)=>(string)c.Items["role"]!;
app.MapGet(prefix+"/board",(Func<HttpContext,Task<IResult>>)(async c=>{
    var rows=await store.ReadAsync(Identity(c),u=>u.Board(),c.RequestAborted);
    return Json(rows.Select(r=>Affordances.Filter(r,Role(c))).ToArray());})).WithMetadata(new RouteAccess("main","service","kitchen"));
app.MapGet(prefix+"/services/{id}",async (HttpContext c,string id)=>Json(await store.ReadAsync(Identity(c),async u=>
    {var d=await u.Dining(id); return new Versioned<DiningView>(d.Version,Affordances.Filter(d.Entity.View(true),Role(c)));},c.RequestAborted))).WithMetadata(new RouteAccess("main","service","kitchen"));
app.MapGet(prefix+"/checkout/services/{id}",async (HttpContext c,string id)=>Json(await store.ReadAsync(Identity(c),async u=>
    {var a=await u.Account(id); return new Versioned<AccountView>(a.Version,a.Entity.ViewWithActions());},c.RequestAborted))).WithMetadata(new RouteAccess("main"));
app.MapPost(prefix+"/auth/login",(Func<HttpContext,Task<IResult>>)(async c=>{
    using var reader=new StreamReader(c.Request.Body);
    var request=JsonSerializer.Deserialize<LoginRequest>(await reader.ReadToEndAsync(c.RequestAborted),Wire.Json);
    if(request is null||string.IsNullOrWhiteSpace(request.Username)||string.IsNullOrEmpty(request.Password))
        throw new ArgumentException("Username and password are required.");
    var login=await identityStore.LoginAsync(request.Username,request.Password,c.RequestAborted);
    if(login is null)
    {
        await Task.Delay(400,c.RequestAborted); // freno minimo y respuesta generica: no revela si el usuario existe
        c.Response.StatusCode=401; return Results.Json(new {error="invalid_credentials"});
    }
    var (token,expiresAt,role,username)=login.Value;
    return Results.Json(new {token,expiresAt,role,username});
}));
app.MapPost(prefix+"/auth/logout",(Func<HttpContext,Task<IResult>>)(async c=>{
    var sessionId=c.Items["sessionId"] as string;
    if(sessionId is not null) await identityStore.RevokeSessionAsync(sessionId,c.RequestAborted);
    return Results.Json(new {loggedOut=sessionId is not null});
})).WithMetadata(new RouteAccess("main","service","kitchen"));
app.MapPost(prefix+"/services",(Func<HttpContext,Task<IResult>>)(c=>Write<OpenRequest>(c,LocalOperations.Open))).WithMetadata(new RouteAccess("main","service"));
app.MapPost(prefix+"/services/{id}/commands/{action}",(HttpContext c,string id,string action)=>
    Write<DiningCommand>(c,(u,i,r)=>action=="add-consumption" ? LocalOperations.Consumption(u,i,id,r) : LocalOperations.Dining(u,i,id,action,r)))
    .WithMetadata(new RouteAccess("main","service","kitchen"));
app.MapPost(prefix+"/checkout/services/{id}/commands/{action}",(HttpContext c,string id,string action)=>
    Write<AccountCommand>(c,(u,i,r)=>LocalOperations.Account(u,i,id,action,r))).WithMetadata(new RouteAccess("main"));
app.MapPost(prefix+"/occupancy/{id}/release",(HttpContext c,string id)=>
    Write<ReleaseCommand>(c,(u,i,r)=>LocalOperations.Release(u,i,id,r))).WithMetadata(new RouteAccess("main"));
// Conciliacion de una orden cuyo resultado el cliente no pudo conservar: devuelve la respuesta
// guardada de ESA clave para el mismo actor, o found=false. Nunca ejecuta ni reintenta nada.
app.MapGet(prefix+"/commands/{key}",async (HttpContext c,string key)=>{
    var stored=await store.ReadAsync(Identity(c),u=>u.CommandResponse(key),c.RequestAborted);
    return Results.Text(stored is null ? Wire.Encode(new {key,found=false})
        : "{\"key\":"+Wire.Encode(key)+",\"found\":true,\"response\":"+stored+"}","application/json");
}).WithMetadata(new RouteAccess("main","service","kitchen"));
app.MapDesktopReadRoutes(source,scope,installation,serverVersion);
// Canal de notificaciones finas. El estado autoritativo se lee siempre en los GET anteriores.
app.MapHub<EventsHub>(prefix+"/events").WithMetadata(new RouteAccess("main","service","kitchen"));
await app.RunAsync();
