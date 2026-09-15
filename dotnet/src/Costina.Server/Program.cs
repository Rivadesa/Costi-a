using System.Net;
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
if(args.Length != 0) throw new ArgumentException("Only init-lab or normal startup is supported.");
await store.CheckAsync();
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
var app=builder.Build();
app.Use(async (context,next)=>
{
    context.Response.Headers.CacheControl="no-store";
    try
    {
        if(context.Request.Path=="/health") { await next(); return; }
        var bearer=context.Request.Headers.Authorization.ToString();
        var provided=bearer.StartsWith("Bearer ",StringComparison.Ordinal) ? bearer[7..] : "";
        var digest=SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var role=keys.FirstOrDefault(k=>CryptographicOperations.FixedTimeEquals(digest,SHA256.HashData(Encoding.UTF8.GetBytes(k.Value)))).Key;
        if(role is null) { context.Response.StatusCode=401; await context.Response.WriteAsJsonAsync(new {error="unauthorized"}); return; }
        if(context.Request.Headers.Keys.Any(k=>new[]{"X-Tenant-Id","X-Company-Id","X-Location-Id"}.Contains(k,StringComparer.OrdinalIgnoreCase)))
            throw new ArgumentException("Scope is assigned by this server, not request headers.");
        context.Items["role"]=role;
        var path=context.Request.Path.Value ?? "";
        bool allowed=role=="main" || (!path.Contains("/checkout/",StringComparison.Ordinal) &&
            !path.Contains("/occupancy/",StringComparison.Ordinal) &&
            (HttpMethods.IsGet(context.Request.Method) || (role=="service" &&
                !path.EndsWith("/complete",StringComparison.Ordinal) && !path.EndsWith("/cancel-unstarted",StringComparison.Ordinal)) ||
                (role=="kitchen" && (path.EndsWith("/preparation-start",StringComparison.Ordinal) || path.EndsWith("/preparation-ready",StringComparison.Ordinal) || path.EndsWith("/ready",StringComparison.Ordinal)))));
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
ExecutionIdentity Identity(HttpContext context)=>new(scope,"lab-"+(string)context.Items["role"]!);
IResult Json(object value)=>Results.Text(Wire.Encode(value),"application/json");
async Task<IResult> Write<T>(HttpContext context,Func<Unit,ExecutionIdentity,T,Task<object>> work)
{
    using var reader=new StreamReader(context.Request.Body);
    var body=await reader.ReadToEndAsync(context.RequestAborted);
    if(body.Length>32768) throw new ArgumentException("Request too large.");
    var request=Wire.Decode<T>(body);
    var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(context.Request.Method+"\n"+context.Request.Path+"\n"+body)));
    var identity=Identity(context);
    var response=await store.ExecuteAsync(identity,context.Request.Headers["Idempotency-Key"].ToString(),fingerprint,
        unit=>work(unit,identity,request),context.RequestAborted);
    return Results.Text(response,"application/json");
}
const string prefix="/api/native/v1";
app.MapGet("/health",async ()=>{ await store.CheckAsync(); return Results.Json(new {status="ready",mode="local-laboratory",version="0.2.0-d1.1"}); });
app.MapGet(prefix+"/board",async (HttpContext c)=>Json(await store.ReadAsync(Identity(c),u=>u.Board(),c.RequestAborted)));
app.MapGet(prefix+"/services/{id}",async (HttpContext c,string id)=>Json(await store.ReadAsync(Identity(c),async u=>
    {var d=await u.Dining(id); return new Versioned<DiningView>(d.Version,d.Entity.View());},c.RequestAborted)));
app.MapGet(prefix+"/checkout/services/{id}",async (HttpContext c,string id)=>Json(await store.ReadAsync(Identity(c),async u=>
    {var a=await u.Account(id); return new Versioned<AccountView>(a.Version,a.Entity.View());},c.RequestAborted)));
app.MapPost(prefix+"/services",(HttpContext c)=>Write<OpenRequest>(c,LocalOperations.Open));
app.MapPost(prefix+"/services/{id}/commands/{action}",(HttpContext c,string id,string action)=>
    Write<DiningCommand>(c,(u,i,r)=>LocalOperations.Dining(u,i,id,action,r)));
app.MapPost(prefix+"/checkout/services/{id}/commands/{action}",(HttpContext c,string id,string action)=>
    Write<AccountCommand>(c,(u,i,r)=>LocalOperations.Account(u,i,id,action,r)));
app.MapPost(prefix+"/occupancy/{id}/release",(HttpContext c,string id)=>
    Write<ReleaseCommand>(c,(u,i,r)=>LocalOperations.Release(u,i,id,r)));
await app.RunAsync();
