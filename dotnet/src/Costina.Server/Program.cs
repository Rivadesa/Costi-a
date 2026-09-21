using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Costina.Domain;
using Costina.Persistence;
using Costina.Server;
using Npgsql;

// D5.1 (#27, ADR-011): ciclo de vida EXPLICITO. "provision" (superusuario, una vez) crea roles, base
// y configuracion; "init" crea el esquema en una base vacia; "upgrade" lo actualiza en una existente;
// "init-lab" anade ademas los fixtures ficticios. Los tres ultimos usan el rol PROPIETARIO. Un
// arranque normal usa el rol de EJECUCION y jamas migra, siembra ni lee las credenciales del propietario.
var administrative = (args.Length == 1 && args[0] is "provision" or "init" or "upgrade" or "init-lab" or "load-demo")
    || (args.Length == 2 && args[0] == "restore");
var settings = new ServerSettings(administrative);
if(args.Length == 1 && args[0] == "provision") { await Provisioner.RunAsync(settings); return; }
// D5.2: registro como servicio de Windows (consola elevada, una vez). Desinstalar nunca toca los datos.
if(args.Length == 1 && args[0] == "install-service") { await ServiceInstaller.InstallAsync(settings); return; }
if(args.Length == 1 && args[0] == "uninstall-service") { await ServiceInstaller.UninstallAsync(); return; }
// D5.6: resumen de solo lectura de la instalacion (guion fisico y soporte). Sin credenciales ni secretos.
if(args.Length == 1 && args[0] == "status") { await StatusReport.RunAsync(settings); return; }
// D5.5: backend del instalador de Windows (toda la logica aqui, probada en CI; el instalador solo copia y llama).
if(args.Length == 1 && args[0] == "setup-server") { await ServerSetup.InstallAsync(settings); return; }
if(args.Length == 1 && args[0] == "stop-services") { await ServerSetup.StopServicesAsync(); return; }
if(args.Length == 1 && args[0] == "remove-server") { await ServerSetup.RemoveAsync(settings); return; }
// D5.3: CA local y certificado del servidor para HTTPS en la LAN. renew-tls reemite sin tocar los dispositivos.
if(args.Length == 1 && args[0] is "provision-tls" or "renew-tls") { await LocalTls.ProvisionAsync(settings,args[0] == "renew-tls"); return; }
var mode = settings.Get("COSTINA_MODE") ?? (settings.Get("COSTINA_LAB_MODE") == "true" ? "laboratory" : null)
    ?? throw new InvalidOperationException("Set COSTINA_MODE=installation (provisioned roles) or COSTINA_LAB_MODE=true (engineering laboratory).");
if(mode is not ("laboratory" or "installation")) throw new InvalidOperationException("COSTINA_MODE must be installation or laboratory.");
var laboratory = mode == "laboratory";
var connectionString = settings.Required("COSTINA_DB");
var db = new NpgsqlConnectionStringBuilder(connectionString).Database ?? "";
if(laboratory && !db.EndsWith("_d1_lab",StringComparison.Ordinal) && !db.EndsWith("_d1_test",StringComparison.Ordinal))
    throw new InvalidOperationException("Use an isolated database ending in _d1_lab or _d1_test, never the legacy database.");
var scope = new BusinessScope(settings.Required("COSTINA_TENANT"),settings.Required("COSTINA_COMPANY"),settings.Required("COSTINA_LOCATION"));
if(administrative)
{
    // En laboratorio de un solo rol el propietario es la misma conexion; una instalacion exige la suya.
    var ownerConnection = settings.Get("COSTINA_DB_OWNER") ?? (laboratory ? connectionString
        : throw new InvalidOperationException("Missing COSTINA_DB_OWNER: schema commands never run with the runtime role."));
    await using var ownerSource = NpgsqlDataSource.Create(ownerConnection);
    // D5.4: restauracion verificada de una copia sobre una instalacion recien aprovisionada (nunca pisa datos).
    if(args[0] == "restore") { await new BackupRunner(settings,scope,BuildInfo.Version).RestoreAsync(args[1],ownerConnection); return; }
    var ownerStore = new PostgresStore(ownerSource);
    var exists = await ownerStore.SchemaExistsAsync();
    if(args[0] == "init" && exists)
    {
        Console.WriteLine("Schema already present: nothing was changed. Use upgrade after installing new binaries."); return;
    }
    if(args[0] == "upgrade" && !exists)
        throw new InvalidOperationException("There is no schema to upgrade; run init on a new installation.");
    // D5.6: datos de DEMOSTRACION en una instalacion, de forma explicita y solo sobre una configuracion vacia.
    // La instalacion queda marcada como demo (/health, /session, cliente) y no debe reutilizarse con datos reales.
    if(args[0] == "load-demo")
    {
        if(!exists) throw new InvalidOperationException("Run init before load-demo.");
        if(await ownerStore.HasConfigurationAsync(scope))
            throw new InvalidOperationException("This installation already has tables, menus or products: demo fixtures are only loaded into an empty configuration.");
        await LabConfiguration.Seed(ownerStore,scope);
        Console.WriteLine("Fictitious DEMO fixtures loaded. This installation is now flagged as a demo; never reuse it for real data."); return;
    }
    if(args[0] == "init-lab" && !laboratory)
        throw new InvalidOperationException("init-lab installs fictitious fixtures and only runs in laboratory mode.");
    await ownerStore.InitializeAsync();
    if(args[0] == "init-lab")
    {
        await LabConfiguration.Seed(ownerStore,scope);
        Console.WriteLine("D1 laboratory schema/fixtures initialized. Existing data was not reset."); return;
    }
    Console.WriteLine(args[0] == "init" ? "Schema initialized. Create the first user with create-user <username> main."
        : "Schema upgraded in place. Existing data was not reset."); return;
}
await using var source = NpgsqlDataSource.Create(connectionString);
var store = new PostgresStore(source);
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
// D5.5: alta INTERACTIVA del primer administrador al terminar la instalacion. La contrasena se teclea
// oculta y dos veces; nunca pasa por argumentos, entorno, ficheros ni el instalador.
if(args.Length == 1 && args[0] == "first-user")
{
    await store.CheckAsync();
    if(Console.IsInputRedirected) throw new InvalidOperationException("first-user is interactive; scripts use create-user with the password on stdin.");
    string Hidden(string prompt)
    {
        Console.Write(prompt); var typed = new System.Text.StringBuilder();
        for(var key = Console.ReadKey(true); key.Key != ConsoleKey.Enter; key = Console.ReadKey(true))
        {
            if(key.Key == ConsoleKey.Backspace) { if(typed.Length > 0) typed.Length--; }
            else if(!char.IsControl(key.KeyChar)) typed.Append(key.KeyChar);
        }
        Console.WriteLine(); return typed.ToString();
    }
    Console.WriteLine("COSTINA - Primer usuario administrador (rol principal). La contrasena necesita 12 caracteres o mas y no se muestra al teclear.");
    while(true)
    {
        Console.Write("Nombre de usuario: "); var name = Console.ReadLine() ?? "";
        var first = Hidden("Contrasena: "); var second = Hidden("Repite la contrasena: ");
        if(first != second) { Console.WriteLine("No coinciden. Vuelve a intentarlo."); continue; }
        try { await new IdentityStore(source,scope).CreateUserAsync(name,first,"main"); }
        catch(ArgumentException e) { Console.WriteLine("No valido: "+e.Message); continue; }
        catch(PostgresException e) when (e.SqlState == "23505") { Console.WriteLine("Ese usuario ya existe."); continue; }
        Console.WriteLine($"Usuario '{name.Trim().ToLowerInvariant()}' creado. Ya puedes entrar desde la aplicacion Costina. Pulsa Intro para cerrar.");
        Console.ReadLine(); return;
    }
}
// D5.4: copia manual inmediata (la desatendida la hace el propio servicio). Usa el rol de ejecucion.
if(args.Length == 1 && args[0] == "backup")
{
    await store.CheckAsync();
    var manual = new BackupRunner(settings,scope,BuildInfo.Version);
    var written = await manual.BackupAsync(connectionString);
    var replicated = await manual.ReplicateAsync(written);
    Console.WriteLine($"Backup {written.File} written under {BackupRunner.Folder} ({written.SizeBytes} bytes, sha256 {written.Sha256})."
        + (replicated == true ? " Replicated to the second destination." : " No second destination configured (COSTINA_BACKUP_COPY): a copy on the same disk does not survive losing the disk."));
    return;
}
if(args.Length != 0) throw new ArgumentException("Supported: status, provision, init, upgrade, init-lab, load-demo, provision-tls, renew-tls, install-service, uninstall-service, backup, restore <file>, setup-server, stop-services, remove-server, first-user, create-user <username> <role> or normal startup.");
// D5.2: al arrancar con el sistema, PostgreSQL puede tardar unos segundos en aceptar conexiones. Una
// instalacion espera (acotado, por debajo del plazo del SCM); un esquema ausente sigue fallando al instante.
var notices = new List<string>();
for(var attempt = 0; ; attempt++)
{
    try { await store.CheckAsync(); break; }
    catch(NpgsqlException e) when (!laboratory && attempt < 20 && (e is not PostgresException starting || starting.SqlState == "57P03"))
    { await Task.Delay(1000); }
}
// D5.1: una instalacion se niega a servir con una conexion capaz de DDL o de borrar auditoria.
if(await store.RuntimeOverprivilegedAsync())
{
    if(!laboratory) throw new InvalidOperationException("COSTINA_DB must use the provisioned runtime role: this connection can alter the schema or the audit trail.");
    notices.Add("AVISO: laboratorio de un solo rol; la conexion del motor puede alterar el esquema. Una instalacion usa provision.");
}
var installation = await store.InstallationAsync();
var demoState = new DemoState{IsDemo = await store.IsDemoAsync(scope)};
// Version unica del paquete (csproj) y commit del build (SourceLink): una sola fuente para /health y /session.
var informational = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
var plus = informational.IndexOf('+');
var serverVersion = plus < 0 ? informational : informational[..plus];
var serverBuild = plus < 0 ? "" : informational[(plus + 1)..];
// D4.3: las claves de rol de laboratorio COSTINA_KEY_* quedan RETIRADAS. Toda peticion
// autentica con sesion de usuario (login) o dispositivo emparejado. Si siguen definidas en el
// entorno se ignoran y se avisa: dejarlas activas seria mantener una puerta paralela.
foreach(var legacy in new[]{"COSTINA_KEY_MAIN","COSTINA_KEY_SERVICE","COSTINA_KEY_KITCHEN"})
    if(Environment.GetEnvironmentVariable(legacy) is not null)
        notices.Add($"AVISO: {legacy} ya no se usa desde D4.3; eliminala del entorno.");
var port = int.Parse(settings.Get("COSTINA_PORT") ?? "5088");
if(port is < 1024 or > 65535) throw new ArgumentException("Invalid laboratory port.");
var builder=WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService(options=>options.ServiceName=ServiceInstaller.Name);
// Las lineas de peticion de ASP.NET incluyen la query (el billete efimero del hub viaja ahi): fuera de
// cualquier log. Una instalacion registra ademas en fichero, porque un servicio no tiene consola.
builder.Logging.AddFilter("Microsoft.AspNetCore",LogLevel.Warning);
if(!laboratory) builder.Logging.AddProvider(new FileLoggerProvider(Path.Combine(ServerSettings.DataRoot,"logs")));
var startedAt=DateTimeOffset.UtcNow;
// D5.3: HTTP solo en loopback, siempre. La LAN se sirve UNICAMENTE por HTTPS y solo si la instalacion
// tiene certificado (provision-tls). El laboratorio nunca abre la LAN.
var tlsPort = int.Parse(settings.Get("COSTINA_TLS_PORT") ?? "5443");
if(tlsPort is < 1024 or > 65535 || tlsPort == port) throw new ArgumentException("Invalid TLS port.");
X509Certificate2? tlsCertificate = null;
if(File.Exists(LocalTls.ServerPfx))
{
    if(laboratory) notices.Add("AVISO: hay certificado TLS pero el laboratorio solo escucha en loopback; la LAN exige modo installation.");
    else tlsCertificate = X509CertificateLoader.LoadPkcs12FromFile(LocalTls.ServerPfx,null);
}
string? caFingerprint = null;
if(tlsCertificate is not null && File.Exists(LocalTls.CaCertificate))
{
    using var authority = X509CertificateLoader.LoadCertificateFromFile(LocalTls.CaCertificate);
    caFingerprint = LocalTls.Fingerprint(authority);
}
builder.WebHost.ConfigureKestrel(options=>
{
    options.Listen(IPAddress.Loopback,port);
    if(tlsCertificate is not null) options.ListenAnyIP(tlsPort,listen=>listen.UseHttps(tlsCertificate));
    options.Limits.MaxRequestBodySize=32768;
});
builder.Services.AddSingleton(store);
builder.Services.AddSingleton(source);
builder.Services.AddSingleton(scope);
builder.Services.AddSingleton<IdentityStore>();
builder.Services.AddSingleton<DeviceStore>();
builder.Services.AddSingleton<DeviceConnectionRegistry>();
builder.Services.AddSingleton<HubTicketStore>();
builder.Services.AddSingleton<PublisherHealth>();
builder.Services.AddSingleton<AttemptThrottle>();
builder.Services.AddSingleton<BackupHealth>();
var backupRunner = new BackupRunner(settings,scope,serverVersion);
if(!laboratory) builder.Services.AddHostedService(services=>new BackupService(backupRunner,services.GetRequiredService<BackupHealth>(),
    settings,connectionString,services.GetRequiredService<ILogger<BackupService>>()));
builder.Services.AddSignalR();
builder.Services.AddSingleton<IEventSink,HubEventSink>();
builder.Services.AddSingleton<OutboxPublisher>();
builder.Services.AddHostedService<OutboxPublisherService>();
var app=builder.Build();
var identityStore=app.Services.GetRequiredService<IdentityStore>();
var deviceStore=app.Services.GetRequiredService<DeviceStore>();
var deviceConnections=app.Services.GetRequiredService<DeviceConnectionRegistry>();
var hubTickets=app.Services.GetRequiredService<HubTicketStore>();
if(!await identityStore.AnyUserAsync())
    notices.Add("AVISO: no hay usuarios en este ambito. Crea el primero con: Costina.Server.exe create-user <usuario> main");
// Los avisos de arranque salen por el logger: consola en laboratorio, fichero cuando es un servicio.
foreach(var notice in notices) app.Logger.LogWarning("{Notice}",notice);
app.UseRouting();
app.Use(async (context,next)=>
{
    context.Response.Headers.CacheControl="no-store";
    if(context.Request.IsHttps) context.Response.Headers.StrictTransportSecurity="max-age=31536000";
    // Eco de la clave de idempotencia: cualquier respuesta, incluido un rechazo, identifica el comando
    // al que contesta. El cliente no cierra una incertidumbre con una respuesta que no la lleve.
    var idempotency=context.Request.Headers["Idempotency-Key"].ToString();
    if(idempotency.Length is >0 and <=128 && idempotency.All(ch=>ch is >= '!' and <= '~'))
        context.Response.Headers["Idempotency-Key"]=idempotency;
    try
    {
        // Anonimos: login, y el par claim/collect del emparejamiento (el dispositivo aun no tiene
        // credencial). Todos con respuesta generica y freno ante datos invalidos.
        bool anonymousAuth=context.Request.Path=="/api/native/v1/auth/login"
            || context.Request.Path=="/api/native/v1/auth/pairings/claim"
            || (context.Request.Path.StartsWithSegments("/api/native/v1/auth/pairings",out var pairingRest)
                && pairingRest.Value is not null && pairingRest.Value.EndsWith("/collect",StringComparison.Ordinal));
        if(context.Request.Path=="/health" || context.Request.Path=="/ca.crt" || anonymousAuth) { await next(); return; }
        var bearer=context.Request.Headers.Authorization.ToString();
        var provided=bearer.StartsWith("Bearer ",StringComparison.Ordinal) ? bearer[7..] : "";
        // D4.1: primero sesion de usuario (token de corta vida con renovacion deslizante y
        // revocacion); las claves de rol de laboratorio siguen como respaldo hasta D4.3.
        string? role=null; AuthenticatedSession? userSession=null; AuthenticatedDevice? device=null;
        bool isHubPath=context.Request.Path.StartsWithSegments("/api/native/v1/events");
        if(provided.Length==0 && isHubPath && context.Request.Query["access_token"].ToString() is {Length:>0} hubToken)
        {
            // Billete efimero de un solo uso, SOLO para el hub (preparacion PWA, F06). Nunca en logs.
            var ticket=hubTickets.Consume(hubToken);
            if(ticket is not null)
            {
                context.Items["role"]=ticket.Role; context.Items["actor"]=ticket.Actor;
                context.Items["deviceId"]=ticket.DeviceId; context.Items["station"]=ticket.Station;
                var hubAccess=context.GetEndpoint()?.Metadata.GetMetadata<RouteAccess>();
                if(hubAccess is null || !hubAccess.Roles.Contains(ticket.Role,StringComparer.Ordinal))
                { context.Response.StatusCode=403; await context.Response.WriteAsJsonAsync(new {error="forbidden"}); return; }
                await next(); return;
            }
        }
        if(provided.StartsWith("dev.",StringComparison.Ordinal))
        {
            // Credencial de dispositivo emparejado: dev.<id>.<secreto>. Solo su hash vive en la base.
            var parts=provided.Split('.',3);
            if(parts.Length==3) device=await deviceStore.AuthenticateAsync(parts[1],parts[2],context.RequestAborted);
            if(device is not null) role=device.Role;
        }
        else if(provided.Length>0)
        {
            userSession=await identityStore.AuthenticateAsync(provided,context.RequestAborted);
            if(userSession is not null) role=userSession.Role;
        }
        if(role is null) { context.Response.StatusCode=401; await context.Response.WriteAsJsonAsync(new {error="unauthorized"}); return; }
        if(context.Request.Headers.Keys.Any(k=>new[]{"X-Tenant-Id","X-Company-Id","X-Location-Id"}.Contains(k,StringComparer.OrdinalIgnoreCase)))
            throw new ArgumentException("Scope is assigned by this server, not request headers.");
        context.Items["role"]=role;
        // Actor de auditoria: la persona real cuando hay sesion; el rol de laboratorio si no.
        context.Items["actor"]=device is not null ? "device:"+device.Name
            : userSession is not null ? "user:"+userSession.Username : "lab-"+role;
        context.Items["sessionId"]=userSession?.SessionId;
        context.Items["deviceId"]=device?.DeviceId;
        context.Items["station"]=device?.Station;
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
ExecutionIdentity Identity(HttpContext context)=>new(scope,(string)context.Items["actor"]!,context.Items["station"] as string);
string? Station(HttpContext c)=>c.Items["station"] as string;
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
// D5.3: la raiz PUBLICA de la CA local, para instalarla en los dispositivos. Se compara su huella
// SHA-256 con la que muestra el servidor (provision-tls / diagnostico) antes de confiar en ella.
app.MapGet("/ca.crt",()=>File.Exists(LocalTls.CaCertificate) && tlsCertificate is not null
    ? Results.File(File.ReadAllBytes(LocalTls.CaCertificate),"application/x-x509-ca-cert","costina-ca.crt") : Results.NotFound());
app.MapGet("/health",async ()=>{
    await store.CheckAsync();
    if(!laboratory && !demoState.IsDemo) demoState.IsDemo=await store.IsDemoAsync(scope); // cargar la demo con el servicio en marcha se refleja sin reiniciar
    return Results.Json(new {status="ready",mode=laboratory?"local-laboratory":"installation",version=serverVersion,build=serverBuild,demo=!laboratory && demoState.IsDemo});
});
string Role(HttpContext c)=>(string)c.Items["role"]!;
app.MapGet(prefix+"/board",(Func<HttpContext,Task<IResult>>)(async c=>{
    var rows=await store.ReadAsync(Identity(c),u=>u.Board(),c.RequestAborted);
    return Json(rows.Select(r=>Affordances.Filter(r,Role(c),Station(c))).ToArray());})).WithMetadata(new RouteAccess("main","service","kitchen"));
app.MapGet(prefix+"/services/{id}",async (HttpContext c,string id)=>Json(await store.ReadAsync(Identity(c),async u=>
    {var d=await u.Dining(id); return new Versioned<DiningView>(d.Version,Affordances.Filter(d.Entity.View(true),Role(c),Station(c)));},c.RequestAborted))).WithMetadata(new RouteAccess("main","service","kitchen"));
app.MapGet(prefix+"/checkout/services/{id}",async (HttpContext c,string id)=>Json(await store.ReadAsync(Identity(c),async u=>
    {var a=await u.Account(id); return new Versioned<AccountView>(a.Version,a.Entity.ViewWithActions());},c.RequestAborted))).WithMetadata(new RouteAccess("main"));
app.MapPost(prefix+"/auth/login",(Func<HttpContext,Task<IResult>>)(async c=>{
    using var reader=new StreamReader(c.Request.Body);
    var request=JsonSerializer.Deserialize<LoginRequest>(await reader.ReadToEndAsync(c.RequestAborted),Wire.Json);
    if(request is null||string.IsNullOrWhiteSpace(request.Username)||string.IsNullOrEmpty(request.Password))
        throw new ArgumentException("Username and password are required.");
    // D5.3: tope de fallos por origen y usuario antes de abrir la LAN (429 durante el bloqueo).
    var throttle=c.RequestServices.GetRequiredService<AttemptThrottle>();
    var throttleKey=AttemptThrottle.Key(c,"login:"+request.Username);
    if(throttle.IsBlocked(throttleKey)) { c.Response.StatusCode=429; return Results.Json(new {error="too_many_attempts"}); }
    var login=await identityStore.LoginAsync(request.Username,request.Password,c.RequestAborted);
    if(login is null)
    {
        throttle.Failed(throttleKey);
        await Task.Delay(400,c.RequestAborted); // freno minimo y respuesta generica: no revela si el usuario existe
        c.Response.StatusCode=401; return Results.Json(new {error="invalid_credentials"});
    }
    throttle.Succeeded(throttleKey);
    var (token,expiresAt,role,username)=login.Value;
    return Results.Json(new {token,expiresAt,role,username});
}));
// D4.2 — emparejamiento de dispositivos. El QR lo pinta el cliente; el servidor emite el codigo.
app.MapPost(prefix+"/auth/pairings",(Func<HttpContext,Task<IResult>>)(async c=>{
    var issued=await deviceStore.CreatePairingAsync((string)c.Items["actor"]!,c.RequestAborted);
    return Results.Json(new {pairingId=issued.PairingId,code=issued.Code,expiresAt=issued.ExpiresAt});
})).WithMetadata(new RouteAccess("main"));
app.MapGet(prefix+"/auth/pairings/pending",(Func<HttpContext,Task<IResult>>)(async c=>
    Results.Json(await deviceStore.PendingAsync(c.RequestAborted)))).WithMetadata(new RouteAccess("main"));
app.MapPost(prefix+"/auth/pairings/{id}/approve",async (HttpContext c,string id)=>{
    using var reader=new StreamReader(c.Request.Body);
    var request=JsonSerializer.Deserialize<PairingDecisionRequest>(await reader.ReadToEndAsync(c.RequestAborted),Wire.Json)
        ?? throw new ArgumentException("Role and station are required.");
    if(!await deviceStore.DecideAsync(id,true,request.Role,request.Station,(string)c.Items["actor"]!,c.RequestAborted))
        throw new StoreNotFound();
    return Results.Json(new {approved=true});
}).WithMetadata(new RouteAccess("main"));
app.MapPost(prefix+"/auth/pairings/{id}/deny",async (HttpContext c,string id)=>{
    if(!await deviceStore.DecideAsync(id,false,null,null,(string)c.Items["actor"]!,c.RequestAborted)) throw new StoreNotFound();
    return Results.Json(new {approved=false});
}).WithMetadata(new RouteAccess("main"));
app.MapPost(prefix+"/auth/pairings/claim",(Func<HttpContext,Task<IResult>>)(async c=>{
    using var reader=new StreamReader(c.Request.Body);
    var request=JsonSerializer.Deserialize<PairingClaimRequest>(await reader.ReadToEndAsync(c.RequestAborted),Wire.Json);
    var throttle=c.RequestServices.GetRequiredService<AttemptThrottle>();
    var throttleKey=AttemptThrottle.Key(c,"pairing-claim");
    if(throttle.IsBlocked(throttleKey)) { c.Response.StatusCode=429; return Results.Json(new {error="too_many_attempts"}); }
    var claimed=request is null||string.IsNullOrWhiteSpace(request.Code)?null
        :await deviceStore.ClaimAsync(request.Code,request.DeviceName??"",c.RequestAborted);
    if(claimed is null)
    {
        throttle.Failed(throttleKey);
        await Task.Delay(400,c.RequestAborted);
        c.Response.StatusCode=401; return Results.Json(new {error="invalid_code"});
    }
    return Results.Json(new {pairingId=claimed.Value.PairingId,pollSecret=claimed.Value.PollSecret});
}));
app.MapPost(prefix+"/auth/pairings/{id}/collect",async (HttpContext c,string id)=>{
    using var reader=new StreamReader(c.Request.Body);
    var request=JsonSerializer.Deserialize<PairingCollectRequest>(await reader.ReadToEndAsync(c.RequestAborted),Wire.Json);
    var (status,credentials)=request is null||string.IsNullOrWhiteSpace(request.PollSecret)
        ?("unknown",(DeviceCredentials?)null)
        :await deviceStore.CollectAsync(id,request.PollSecret,c.RequestAborted);
    if(credentials is not null)
        return Results.Json(new {status="approved",deviceId=credentials.DeviceId,
            deviceToken="dev."+credentials.DeviceId+"."+credentials.Secret,
            role=credentials.Role,station=credentials.Station});
    if(status=="claimed") return Results.Json(new {status="pending"});
    if(status=="denied") { c.Response.StatusCode=403; return Results.Json(new {status="denied"}); }
    await Task.Delay(400,c.RequestAborted);
    c.Response.StatusCode=404; return Results.Json(new {error="unknown_pairing"});
});
app.MapGet(prefix+"/auth/devices",(Func<HttpContext,Task<IResult>>)(async c=>
    Results.Json(await deviceStore.DevicesAsync(c.RequestAborted)))).WithMetadata(new RouteAccess("main"));
app.MapPost(prefix+"/auth/devices/{id}/revoke",async (HttpContext c,string id)=>{
    if(!await deviceStore.RevokeAsync(id,(string)c.Items["actor"]!,c.RequestAborted)) throw new StoreNotFound();
    deviceConnections.AbortAll(id); // expulsion inmediata de las conexiones SignalR vivas
    return Results.Json(new {revoked=true});
}).WithMetadata(new RouteAccess("main"));
app.MapPost(prefix+"/auth/hub-token",(Func<HttpContext,IResult>)(c=>{
    var (token,expiresAt)=hubTickets.Issue(new HubTicket((string)c.Items["role"]!,(string)c.Items["actor"]!,
        c.Items["deviceId"] as string,c.Items["station"] as string));
    return Results.Json(new {hubToken=token,expiresAt});
})).WithMetadata(new RouteAccess("main","service","kitchen"));
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
app.MapDesktopReadRoutes(source,scope,installation,serverVersion,()=>!laboratory && demoState.IsDemo);
// Canal de notificaciones finas. El estado autoritativo se lee siempre en los GET anteriores.
object Backup()
{
    var state=app.Services.GetRequiredService<BackupHealth>().Read();
    return new {automatic=!laboratory,lastSuccessAt=state.LastSuccessAt,lastFile=state.LastFile,lastError=state.LastError,
        secondDestinationConfigured=backupRunner.CopyFolder is {Length:>0},lastReplicated=state.CopyReplicated};
}
// D5.2: diagnostico operativo, solo main. Sin datos de negocio, importes, rutas ni secretos.
app.MapGet(prefix+"/diagnostics",(Func<HttpContext,Task<IResult>>)(async c=>{
    var backlog=await store.OutboxBacklogAsync(scope,c.RequestAborted);
    var publisher=app.Services.GetRequiredService<PublisherHealth>();
    long? freeBytes=null;
    try { freeBytes=new DriveInfo(Path.GetPathRoot(ServerSettings.DataRoot)!).AvailableFreeSpace; } catch(Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException) { }
    return Results.Json(new {
        mode=laboratory?"local-laboratory":"installation",version=serverVersion,build=serverBuild,startedAt,
        runningAsService=Microsoft.Extensions.Hosting.WindowsServices.WindowsServiceHelpers.IsWindowsService(),
        publisher=new {lastSuccessAt=publisher.LastSuccessAt,lastFailureAt=publisher.LastFailureAt,consecutiveFailures=publisher.ConsecutiveFailures},
        outbox=new {pending=backlog.Pending,oldestPendingAt=backlog.OldestAt},
        dataRoot=new {freeBytes},
        tls=new {enabled=tlsCertificate is not null,port=tlsCertificate is null ? (int?)null : tlsPort,
            serverCertificateExpiresAt=tlsCertificate is null ? (DateTimeOffset?)null : new DateTimeOffset(tlsCertificate.NotAfter.ToUniversalTime()),
            names=tlsCertificate?.GetNameInfo(X509NameType.DnsName,false),caFingerprintSha256=caFingerprint},
        backup=Backup()
    });
})).WithMetadata(new RouteAccess("main"));
app.MapHub<EventsHub>(prefix+"/events").WithMetadata(new RouteAccess("main","service","kitchen"));
await app.RunAsync();
