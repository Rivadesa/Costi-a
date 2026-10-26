using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Costina.Client;
// C1: enums del dominio (todos los valores que el servidor puede enviar) para recorrer el vocabulario.
using AccountState = Costina.Core.Domain.AccountState;
using PaymentCoverage = Costina.Core.Domain.PaymentCoverage;
using OfferKind = Costina.Core.Domain.OfferKind;
using OfferService = Costina.Core.Domain.OfferService;
using StationKind = Costina.Core.Domain.StationKind;
using DiningState = Costina.Modules.Dining.Domain.DiningState;
using CourseState = Costina.Modules.Dining.Domain.CourseState;
using PreparationState = Costina.Modules.Dining.Domain.PreparationState;
using OccupancyState = Costina.Modules.Dining.Domain.OccupancyState;
using RestrictionKind = Costina.Modules.Dining.Domain.RestrictionKind;
using RestrictionSeverity = Costina.Modules.Dining.Domain.RestrictionSeverity;
using ReviewDecision = Costina.Modules.Dining.Domain.ReviewDecision;

var results = new List<object>(); int passed=0, failed=0;
void Assert(bool value) { if(!value) throw new Exception("Assertion failed"); }
async Task Check(string name, Func<Task> test) {
    try { await test(); passed++; results.Add(new {name,passed=true}); Console.WriteLine("PASS "+name); }
    catch(Exception e) { failed++; results.Add(new {name,passed=false,error=e.GetType().Name}); Console.WriteLine("FAIL "+name+": "+e); }
}
async Task Throws<T>(Func<Task> test) where T:Exception {
    try { await test(); } catch(T) { return; } throw new Exception("Expected "+typeof(T).Name);
}
ApiClient Client(HttpMessageHandler handler) => new(new Uri("http://127.0.0.1:5088"),new string('a',40),handler);
HttpResponseMessage Response(int code,string json) => new((HttpStatusCode)code) {Content=new StringContent(json,Encoding.UTF8,"application/json")};
// Rechazo de la aplicacion que identifica el comando: el servidor D3.4 hace eco de la Idempotency-Key.
HttpResponseMessage Rejection(int code,string json,HttpRequestMessage request) {
    var response=Response(code,json); response.Headers.Add("Idempotency-Key",request.Headers.GetValues("Idempotency-Key").Single()); return response;
}
HttpResponseMessage Lookup(HttpRequestMessage request,bool found,string response="{\"version\":9}") {
    var key=Uri.UnescapeDataString(request.RequestUri!.AbsolutePath.Split("/commands/")[1]);
    return Response(200,found?"{\"key\":\""+key+"\",\"found\":true,\"response\":"+response+"}":"{\"key\":\""+key+"\",\"found\":false}");
}
foreach(var sample in new[]{("9,50",950L),("9.5",950L),("0,01",1L),("150",15000L)})
    await Check("exact money "+sample.Item1,()=>{Assert(Money.Parse(sample.Item1)==sample.Item2);return Task.CompletedTask;});
foreach(var sample in new[]{"0","-1","1.234","1,234.00","1e3","NaN","9,",",50"})
    await Check("reject money "+sample,()=>Throws<ArgumentException>(()=>{Money.Parse(sample);return Task.CompletedTask;}));
foreach(var sample in new[]{"http://127.0.0.1:5088","https://costina-server.local:5443","https://192.168.1.10:5443"})
    await Check("accept endpoint "+sample,()=>{ApiClient.ValidateEndpoint(new Uri(sample));return Task.CompletedTask;});
foreach(var sample in new[]{"http://example.com:5088","http://192.168.1.10:5088","http://costina-server.local:5088","https://user@costina-server.local:5443","https://costina-server.local:5443/path","https://costina-server.local:443","ftp://costina-server.local:5443","http://127.0.0.1:5088/path","http://user@127.0.0.1:5088","http://127.0.0.1:5088/?secret=1"})
    await Check("reject endpoint "+sample,()=>Throws<ArgumentException>(()=>{ApiClient.ValidateEndpoint(new Uri(sample));return Task.CompletedTask;}));
// D6.5: enlace del QR de emparejamiento. Solo https://NOMBRE:PUERTO sirve a un dispositivo; el codigo va en el fragmento.
await Check("pairing link carries only the code, in the fragment",()=>{
    var address=PairingLink.ParseDeviceAddress(" https://Costina-Server.local:5443 ");
    Assert(PairingLink.Build(address,"AbC123_def-456")=="https://costina-server.local:5443/app/#pair=AbC123_def-456");
    Assert(PairingLink.SuggestDeviceAddress(new Uri("https://costina-server.local:5443"))=="https://costina-server.local:5443");
    Assert(PairingLink.SuggestDeviceAddress(new Uri("http://127.0.0.1:5088"))=="");
    return Task.CompletedTask;});
foreach(var sample in new[]{"","   ","costina-server","http://127.0.0.1:5088","http://192.168.1.10:5088","https://costina-server.local:5443/app","https://user@costina-server.local:5443","https://costina-server.local:443","https://costina-server.local:5443/?x=1"})
    await Check("reject device address '"+sample+"'",()=>Throws<ArgumentException>(()=>{PairingLink.ParseDeviceAddress(sample);return Task.CompletedTask;}));
// B.2 (ADR-014): con la conexion sencilla, http://IPv4-PRIVADA:PUERTO; nunca un nombre, una IP publica o loopback.
await Check("simple connection link uses the private IPv4 over http",()=>{
    var address=PairingLink.ParseDeviceAddress(" http://192.168.0.81:5089 ",simpleConnection:true);
    Assert(PairingLink.Build(address,"AbC123_def-456")=="http://192.168.0.81:5089/app/#pair=AbC123_def-456");
    Assert(PairingLink.SimpleAddress("10.0.0.5",5089)=="http://10.0.0.5:5089");
    Assert(PairingLink.ParseDeviceAddress("https://costina-server.local:5443",simpleConnection:true).Scheme=="https");   // https sigue valiendo
    return Task.CompletedTask;});
foreach(var sample in new[]{"http://costina-server.local:5089","http://8.8.8.8:5089","http://127.0.0.1:5089","http://172.32.0.1:5089","http://192.168.0.81:80","http://192.168.0.81:5089/app","http://user@192.168.0.81:5089","http://192.168.0.81:5089/?x=1","http://[fd00::1]:5089"})
    await Check("reject simple address '"+sample+"'",()=>Throws<ArgumentException>(()=>{PairingLink.ParseDeviceAddress(sample,simpleConnection:true);return Task.CompletedTask;}));
foreach(var sample in new[]{"","corto","con espacio dentro","<img src=x onerror=1>","codigo/con/barras","código-con-acento",new string('a',129)})
    await Check("reject pairing code '"+sample[..Math.Min(sample.Length,24)]+"'",()=>Throws<ArgumentException>(()=>{PairingLink.Build(new Uri("https://costina-server.local:5443"),sample);return Task.CompletedTask;}));
// C3c: el QR abre la aplicacion de la intencion del asistente (Costina Sala o Costina Cocina); sin aplicacion, el enrutador /app/.
await Check("pairing link opens the app of the assistant's intent",()=>{
    var secure=PairingLink.ParseDeviceAddress("https://costina-server.local:5443");
    var simple=PairingLink.ParseDeviceAddress("http://192.168.0.81:5089",simpleConnection:true);
    Assert(PairingLink.Build(secure,"AbC123_def-456",PwaApp.Sala)=="https://costina-server.local:5443/app/sala/#pair=AbC123_def-456");
    Assert(PairingLink.Build(secure,"AbC123_def-456",PwaApp.Cocina)=="https://costina-server.local:5443/app/cocina/#pair=AbC123_def-456");
    Assert(PairingLink.Build(secure,"AbC123_def-456",null)=="https://costina-server.local:5443/app/#pair=AbC123_def-456");
    // El codigo corto viaja sin el espacio con el que lo ensena el PC.
    Assert(PairingLink.Build(simple,"K7M2Q9XR4T",PwaApp.Sala)=="http://192.168.0.81:5089/app/sala/#pair=K7M2Q9XR4T");
    Assert(PairingLink.Build(simple,"K7M2Q9XR4T",PwaApp.Cocina)=="http://192.168.0.81:5089/app/cocina/#pair=K7M2Q9XR4T");
    return Task.CompletedTask;});
await Check("pairing link app follows the role to approve, anything else goes to the router",()=>{
    Assert(PairingLink.AppFor("service")==PwaApp.Sala && PairingLink.AppFor("kitchen")==PwaApp.Cocina);
    foreach(var role in new[]{null,"","main","bar","Kitchen","kitchen "}) Assert(PairingLink.AppFor(role) is null);
    return Task.CompletedTask;});
await Check("pairing link refuses an unknown app",()=>Throws<ArgumentException>(()=>{PairingLink.Build(new Uri("https://costina-server.local:5443"),"AbC123_def-456",(PwaApp)7);return Task.CompletedTask;}));
// C3c: el PC ensena el codigo corto en dos grupos para teclearlo («K7M2Q 9XR4T»); el QR lo lleva sin el espacio. Cualquier otro codigo
// (el largo de un motor anterior a C3c, que no lee el cuerpo y lo devuelve siempre) se ensena tal cual, sin partirlo ni tocarlo.
// El alfabeto es el del motor (DeviceStore.ShortAlphabet): sin 0, 1, I ni O.
await Check("pairing code: the short code in two groups, anything else raw",()=>{
    Assert(PairingLink.ShortAlphabet=="23456789ABCDEFGHJKLMNPQRSTUVWXYZ");
    Assert(PairingLink.IsShortCode("K7M2Q9XR4T")&&PairingLink.DisplayCode("K7M2Q9XR4T")=="K7M2Q 9XR4T");
    Assert(PairingLink.IsShortCode("2345678ZZZ")&&PairingLink.DisplayCode("2345678ZZZ")=="23456 78ZZZ");
    foreach(var raw in new[]{"AbC123_def-456","k7m2q9xr4t","K7M2Q9XR4","K7M2Q9XR4TT","K7M2Q9XR41","K7M2Q9XR4O","K7M2Q 9XR4T","K7M2Q-9XR4T",new string('A',43),""})
        Assert(!PairingLink.IsShortCode(raw)&&PairingLink.DisplayCode(raw)==raw);
    Assert(!PairingLink.IsShortCode(null)&&PairingLink.DisplayCode(null)=="");
    return Task.CompletedTask;});
foreach(var app in new PwaApp?[]{PwaApp.Sala,PwaApp.Cocina})
    foreach(var sample in new[]{"K7M2Q 9XR4T","<img src=x onerror=1>","código-con-acento"})
        await Check("reject pairing code '"+sample+"' for "+app,()=>Throws<ArgumentException>(()=>{PairingLink.Build(new Uri("https://costina-server.local:5443"),sample,app);return Task.CompletedTask;}));
await Check("GET uses correct authenticated relative route",async()=>{
    using var client=Client(new Handler((r,_)=>{
        Assert(r.RequestUri!.AbsolutePath=="/api/native/v1/dining/board"); Assert(r.Headers.Authorization?.Scheme=="Bearer");
        return Task.FromResult(Response(200,"[]"));
    })); Assert((await client.GetAsync<BoardEntry[]>("dining/board")).Length==0);
});
await Check("503 retains command and identical replay",async()=>{
    var bodies=new List<string>();var keys=new List<string>();
    using var client=Client(new Handler(async (r,_)=>{
        bodies.Add(await r.Content!.ReadAsStringAsync());keys.Add(r.Headers.GetValues("Idempotency-Key").Single());
        return keys.Count==1?Response(503,"{\"error\":\"operation_unconfirmed\"}"):Response(200,"{\"version\":2}");
    }));
    await Throws<ApiError>(async()=>{await client.SendAsync("dining/services/test/commands/start",new {expectedVersion=1},"Start");});
    Assert(client.Pending is not null);
    await Throws<InvalidOperationException>(async()=>{await client.SendAsync("dining/services",new {pax=2},"Other");});
    await client.RetryAsync();Assert(client.Pending is null);Assert(bodies[0]==bodies[1]&&keys[0]==keys[1]&&keys.Count==2);
});
await Check("transport failure is not confirmation",async()=>{
    using var client=Client(new Handler((_,_)=>throw new HttpRequestException("simulated")));
    await Throws<HttpRequestException>(async()=>{await client.SendAsync("dining/services",new {pax=2},"Open");});Assert(client.Pending is not null);
});
// C3b (diferido de D6.7): la orden pasa a Pending SOLO despues de guardarse. Si el disco falla, no se envia, no queda en memoria para
// "Reintentar" (se mandaria sin estar guardada) ni bloquea la siguiente orden como si estuviera incierta.
await Check("a command the store could not save is never sent, kept or retried",async()=>{
    var posts=0;
    using var client=Client(new Handler((r,_)=>{posts++;return Task.FromResult(Rejection(200,"{\"version\":2}",r));}));
    var store=new FailingStore();
    client.AttachPendingStore(store);
    await Throws<IOException>(async()=>{await client.SendAsync("dining/services/x/commands/add-consumptions",new {expectedVersion=1},"Ronda");});
    Expect(client.Pending is null,"a command that could not be saved is not pending");
    await Throws<InvalidOperationException>(async()=>{await client.RetryAsync();});
    Expect(posts==0&&store.Saved==0,"nothing was sent nor stored: "+posts);
    store.Fail=false;
    await client.SendAsync("dining/services/x/commands/add-consumptions",new {expectedVersion=1},"Ronda");
    Expect(posts==1&&store.Saved==1&&store.Stored is null&&client.Pending is null,"the next command is saved, sent once and closed");
});
await Check("malformed successful response retains command",async()=>{
    using var client=Client(new Handler((_,_)=>Task.FromResult(Response(200,"{}"))));
    await Throws<JsonException>(async()=>{await client.SendAsync("dining/services",new {pax=2},"Open");});Assert(client.Pending is not null);
});
await Check("version conflict identified by key resolves rejection without auto-resubmit",async()=>{
    var methods=new List<string>();
    using var client=Client(new Handler((r,_)=>{methods.Add(r.Method.Method);return Task.FromResult(r.Method==HttpMethod.Get?Lookup(r,false):Rejection(409,"{\"error\":\"version_conflict\"}",r));}));
    await Throws<ApiError>(async()=>{await client.SendAsync("dining/services/test/commands/start",new {expectedVersion=1},"Start");});Assert(client.Pending is null);
    Assert(methods.SequenceEqual(["POST","GET"]));   // D6.6: pregunta por la clave ANTES de darla por no aplicada, y no reenvia
});
// C3b: el rechazo de una ronda dice que linea (desde 0); sin linea, o con una que no es un indice, no se inventa ninguna.
foreach(var (body,line) in new[]{("{\"error\":\"product_unavailable\",\"line\":1}",(int?)1),("{\"error\":\"use_add_dish\",\"line\":0}",0),("{\"error\":\"version_conflict\"}",null),
    ("{\"error\":\"invalid_request\",\"line\":\"2\"}",null),("{\"error\":\"invalid_request\",\"line\":-1}",null),("{\"error\":\"invalid_request\",\"line\":1.5}",null)})
    await Check("a round rejection carries the line the server names: "+body,async()=>{
        using var client=Client(new Handler((r,_)=>Task.FromResult(r.Method==HttpMethod.Get?Lookup(r,false):Rejection(422,body,r))));
        try{ await client.SendAsync("dining/services/x/commands/add-consumptions",new {expectedVersion=1},"Ronda"); throw new Exception("expected a rejection"); }
        catch(ApiError e){ Expect(e.Status==422&&e.Line==line&&client.Pending is null,"line "+e.Line+" for "+body); }
    });
// D6.6: un rechazo habla de ESTE intento. Respuesta perdida -> puesto re-emparejado con otro rol -> el reintento recibe 403 con eco.
await Check("a rejected retry whose key the server has is closed as APPLIED with the stored response",async()=>{
    var store=new MemoryStore(); var posts=0;
    using var client=Client(new Handler((r,_)=>{
        if(r.Method==HttpMethod.Get) return Task.FromResult(Lookup(r,true));
        return ++posts==1?throw new HttpRequestException("lost response"):Task.FromResult(Rejection(403,"{\"error\":\"forbidden\"}",r));
    }));
    client.AttachPendingStore(store);
    await Throws<HttpRequestException>(async()=>{await client.SendAsync("dining/services/x/commands/pause",new {expectedVersion=1},"Pause");});
    Assert(client.Pending is not null&&store.Stored is not null);
    var applied=await client.RetryAsync();
    Assert(applied.GetProperty("version").GetInt64()==9); Assert(client.Pending is null&&store.Stored is null);
});
foreach(var lookup in new Func<HttpRequestMessage,HttpResponseMessage>[]{_=>Response(500,"{}"),_=>Response(401,"{}"),_=>Response(200,"<html>proxy</html>"),_=>Response(200,"{\"key\":\"otra\",\"found\":false}")})
    await Check("a rejection that cannot be verified against the server record retains the command",async()=>{
        using var client=Client(new Handler((r,_)=>Task.FromResult(r.Method==HttpMethod.Get?lookup(r):Rejection(409,"{\"error\":\"version_conflict\"}",r))));
        await Throws<ApiError>(async()=>{await client.SendAsync("dining/services/x/commands/start",new {expectedVersion=1},"Start");});Assert(client.Pending is not null);
    });
await Check("401 preserves uncertain mutation",async()=>{
    using var client=Client(new Handler((_,_)=>Task.FromResult(Response(401,"{}"))));
    await Throws<ApiError>(async()=>{await client.SendAsync("dining/services",new {pax=2},"Open");});Assert(client.Pending is not null);
});
// D3.4 (F04): solo un rechazo definitivo reconocible, que identifica ESTE comando, cierra la incertidumbre.
await Check("409 without echoed key retains the command",async()=>{
    using var client=Client(new Handler((_,_)=>Task.FromResult(Response(409,"{\"error\":\"version_conflict\"}"))));
    await Throws<ApiError>(async()=>{await client.SendAsync("dining/services/test/commands/start",new {expectedVersion=1},"Start");});Assert(client.Pending is not null);
});
await Check("409 echoing another key retains the command",async()=>{
    using var client=Client(new Handler((_,_)=>{var r=Response(409,"{\"error\":\"version_conflict\"}");r.Headers.Add("Idempotency-Key","otra");return Task.FromResult(r);}));
    await Throws<ApiError>(async()=>{await client.SendAsync("dining/services/test/commands/start",new {expectedVersion=1},"Start");});Assert(client.Pending is not null);
});
await Check("transient storage_conflict retains the command",async()=>{
    using var client=Client(new Handler((r,_)=>Task.FromResult(Rejection(409,"{\"error\":\"storage_conflict\"}",r))));
    await Throws<ApiError>(async()=>{await client.SendAsync("dining/services/test/commands/start",new {expectedVersion=1},"Start");});Assert(client.Pending is not null);
});
await Check("non-JSON 403 retains the command",async()=>{
    using var client=Client(new Handler((r,_)=>{var html=new HttpResponseMessage(HttpStatusCode.Forbidden){Content=new StringContent("<html>forbidden</html>",Encoding.UTF8,"text/html")};
        html.Headers.Add("Idempotency-Key",r.Headers.GetValues("Idempotency-Key").Single());return Task.FromResult(html);}));
    await Throws<ApiError>(async()=>{await client.SendAsync("dining/services/test/commands/start",new {expectedVersion=1},"Start");});Assert(client.Pending is not null);
});
await Check("device rows and session accept an older server without online or stationName",()=>{
    var web=new JsonSerializerOptions(JsonSerializerDefaults.Web);
    var online=JsonSerializer.Deserialize<DeviceRowDto>("{\"id\":\"d1\",\"name\":\"tablet\",\"role\":\"service\",\"station\":\"sala-1\",\"approvedBy\":\"user:jefa\",\"createdAt\":\"2026-10-06T10:00:00Z\",\"revokedAt\":null,\"online\":true}",web)!;
    var older=JsonSerializer.Deserialize<DeviceRowDto>("{\"id\":\"d2\",\"name\":\"kds\",\"role\":\"kitchen\",\"station\":\"cold\",\"approvedBy\":\"user:jefa\",\"createdAt\":\"2026-10-06T10:00:00Z\",\"revokedAt\":\"2026-10-06T11:00:00Z\"}",web)!;
    Assert(online.Online&&online.Active&&online.StateLabel=="Conectado"&&online.RoleLabel=="Costiña Sala"&&online.ToString().EndsWith("aprobado por jefa",StringComparison.Ordinal));
    Assert(!older.Online&&!older.Active&&older.StateLabel.StartsWith("Quitado el ",StringComparison.Ordinal)&&older.RoleLabel=="Costiña Cocina");
    var named=JsonSerializer.Deserialize<SessionInfo>("{\"role\":\"kitchen\",\"tenantId\":\"t\",\"companyId\":\"c\",\"locationId\":\"l\",\"station\":\"cold\",\"stationName\":\"Cocina fría\"}",web)!;
    var unnamed=JsonSerializer.Deserialize<SessionInfo>("{\"role\":\"kitchen\",\"tenantId\":\"t\",\"companyId\":\"c\",\"locationId\":\"l\",\"station\":\"cold\"}",web)!;
    Assert(named.StationName=="Cocina fría"&&unnamed.StationName is null);
    return Task.CompletedTask;
});
await Check("operational DTO excludes financial fields",()=>{
    var types=new[]{typeof(DiningDto),typeof(CourseDto),typeof(PreparationDto),typeof(BoardEntry)};
    Assert(types.SelectMany(t=>t.GetProperties()).All(p=>!new[]{"Price","Balance","Payments","Account","TotalCents"}.Any(x=>p.Name.Contains(x,StringComparison.OrdinalIgnoreCase))));
    return Task.CompletedTask;
});
await Check("durable store saves before first attempt and clears only the confirmed key",async()=>{
    var store=new MemoryStore();var calls=0;
    using var client=Client(new Handler((_,_)=>Task.FromResult(++calls==1?Response(503,"{\"error\":\"operation_unconfirmed\"}"):Response(200,"{\"version\":2}"))));
    client.AttachPendingStore(store);
    await Throws<ApiError>(async()=>{await client.SendAsync("dining/services/x/commands/start",new {expectedVersion=1},"Start");});
    Assert(store.Stored is not null&&store.Stored.Body==client.Pending!.Body&&store.Stored.Key==client.Pending.Key);
    await client.RetryAsync();Assert(store.Stored is null&&client.Pending is null&&store.ClearedKeys.Single()==store.LastSavedKey);
});
await Check("explicit rejection clears the durable command",async()=>{
    var store=new MemoryStore();
    using var client=Client(new Handler((r,_)=>Task.FromResult(r.Method==HttpMethod.Get?Lookup(r,false):Rejection(409,"{\"error\":\"version_conflict\"}",r))));
    client.AttachPendingStore(store);
    await Throws<ApiError>(async()=>{await client.SendAsync("dining/services/x/commands/start",new {expectedVersion=1},"Start");});
    Assert(store.Stored is null&&client.Pending is null);
});
await Check("restored command from a previous process replays identical bytes and key",async()=>{
    var store=new MemoryStore{Stored=new PendingCommand("key-abc","dining/services/x/commands/start","{\"expectedVersion\":7}","Start")};
    string? sentBody=null,sentKey=null;
    using var client=Client(new Handler(async(r,_)=>{
        sentBody=await r.Content!.ReadAsStringAsync();sentKey=r.Headers.GetValues("Idempotency-Key").Single();
        return Response(200,"{\"version\":8}");
    }));
    client.AttachPendingStore(store);
    Assert(client.Pending is {Key:"key-abc"});
    await Throws<InvalidOperationException>(async()=>{await client.SendAsync("dining/services",new {pax=2},"Otra");});
    await client.RetryAsync();
    Assert(sentBody=="{\"expectedVersion\":7}"&&sentKey=="key-abc"&&store.Stored is null);
});
await Check("attaching a store with a live pending persists it",async()=>{
    using var client=Client(new Handler((_,_)=>Task.FromResult(Response(503,"{}"))));
    await Throws<ApiError>(async()=>{await client.SendAsync("dining/services",new {pax=2},"Open");});
    var store=new MemoryStore();client.AttachPendingStore(store);
    Assert(store.Stored is not null&&store.Stored.Key==client.Pending!.Key);
});
// D3.4 (F02): un fichero ilegible bloquea (fallo cerrado) hasta conciliar con el servidor; la evidencia no se destruye.
await Check("unreadable durable file blocks mutations and server confirmation lifts the block",async()=>{
    var store=new MemoryStore{Forced=new PendingLoad(PendingOutcome.Unreadable,null,"k9","dañado")};
    using var client=Client(new Handler((r,_)=>{
        Assert(r.Method==HttpMethod.Get&&r.RequestUri!.AbsolutePath=="/api/native/v1/commands/k9");
        return Task.FromResult(Response(200,"{\"key\":\"k9\",\"found\":true,\"response\":{\"version\":3}}"));
    }));
    client.AttachPendingStore(store);
    Assert(client.Blocked is {Outcome:PendingOutcome.Unreadable,Key:"k9"}&&client.Pending is null);
    await Throws<InvalidOperationException>(async()=>{await client.SendAsync("dining/services",new {pax=2},"Open");});
    Assert(!store.Discarded);
    await Throws<InvalidOperationException>(()=>{client.DiscardBlocked();return Task.CompletedTask;});   // sin consultar no se descarta
    Assert(await client.ReconcileAsync()=="confirmed"&&client.Blocked is null&&store.Discarded&&store.ClearedKeys.Count==0);
});
await Check("unknown reconciliation keeps the block until an explicit discard that quarantines",async()=>{
    var store=new MemoryStore{Forced=new PendingLoad(PendingOutcome.Unreadable,null,"k10","dañado")};
    using var client=Client(new Handler((_,_)=>Task.FromResult(Response(200,"{\"key\":\"k10\",\"found\":false}"))));
    client.AttachPendingStore(store);
    Assert(await client.ReconcileAsync()=="unknown"&&client.Blocked is not null);
    await Throws<InvalidOperationException>(async()=>{await client.SendAsync("dining/services",new {pax=2},"Open");});
    client.DiscardBlocked();
    Assert(client.Blocked is null&&store.Discarded);
});
await Check("unavailable durable file blocks and cannot be discarded",async()=>{
    var store=new MemoryStore{Forced=new PendingLoad(PendingOutcome.Unavailable,null,null,"bloqueado")};
    using var client=Client(new Handler((_,_)=>Task.FromResult(Response(200,"{\"version\":1}"))));
    client.AttachPendingStore(store);
    await Throws<InvalidOperationException>(async()=>{await client.SendAsync("dining/services",new {pax=2},"Open");});
    await Throws<InvalidOperationException>(()=>client.ReconcileAsync());
    await Throws<InvalidOperationException>(()=>{client.DiscardBlocked();return Task.CompletedTask;});
    Assert(client.Blocked is not null&&!store.Discarded);
});
await Check("unreadable file without key can only be discarded explicitly",async()=>{
    var store=new MemoryStore{Forced=new PendingLoad(PendingOutcome.Unreadable,null,null,"cabecera dañada")};
    using var client=Client(new Handler((_,_)=>Task.FromResult(Response(200,"{\"version\":1}"))));
    client.AttachPendingStore(store);
    await Throws<InvalidOperationException>(()=>client.ReconcileAsync());
    client.DiscardBlocked();Assert(client.Blocked is null&&store.Discarded);
});
// D6.7: el servidor guarda la idempotencia por actor. La orden de OTRO actor con el mismo rol nunca se envia ni se consulta (diria
// que no consta) y solo sale con un descarte explicito; la que no anota su actor (0.40.0) se consulta pero nunca se reenvia.
await Check("another actor's durable order blocks, is never sent nor asked about, and leaves only by explicit discard",async()=>{
    var store=new MemoryStore{Forced=new PendingLoad(PendingOutcome.OtherActor,new PendingCommand("k-ana","erp/catalog/commands/glass-sale-set","{}","Vender por copas"),"k-ana","","user:ana")};
    var requests=new List<string>();
    using var client=Client(new Handler((r,_)=>{requests.Add(r.Method+" "+r.RequestUri!.AbsolutePath);return Task.FromResult(Response(200,"{\"version\":1}"));}));
    client.AttachPendingStore(store);
    Assert(client.Pending is null&&client.Blocked is {Outcome:PendingOutcome.OtherActor,Key:"k-ana",Actor:"user:ana"});
    await Throws<InvalidOperationException>(()=>client.RetryAsync());
    await Throws<InvalidOperationException>(()=>client.ReconcileAsync());
    await Throws<InvalidOperationException>(async()=>{await client.SendAsync("dining/services",new {pax=2},"Open");});
    Assert(requests.Count==0&&!store.Discarded);
    client.DiscardBlocked();   // sin consultar: consultarla no serviria
    Assert(client.Blocked is null&&store.Discarded&&requests.Count==0);
    await client.SendAsync("dining/services",new {pax=2},"Open");
    Assert(requests.Single()=="POST /api/native/v1/dining/services");
});
await Check("a durable order without its author is asked about but never resent",async()=>{
    foreach(var found in new[]{false,true})
    {
        var store=new MemoryStore{Forced=new PendingLoad(PendingOutcome.UnknownActor,new PendingCommand("k-old","dining/services","{\"pax\":2}","Abrir mesa"),"k-old","Sin actor.")};
        var requests=new List<string>();
        using var client=Client(new Handler((r,_)=>{requests.Add(r.Method+" "+r.RequestUri!.AbsolutePath);return Task.FromResult(Lookup(r,found));}));
        client.AttachPendingStore(store);
        Assert(client.Pending is null&&client.Blocked is {Outcome:PendingOutcome.UnknownActor,Key:"k-old"});
        await Throws<InvalidOperationException>(()=>client.RetryAsync());
        await Throws<InvalidOperationException>(()=>{client.DiscardBlocked();return Task.CompletedTask;});   // sin consultar no se descarta
        Assert(await client.ReconcileAsync()==(found?"confirmed":"unknown")&&requests.SequenceEqual(["GET /api/native/v1/commands/k-old"]));
        if(found) Assert(client.Blocked is null&&store.Discarded);   // consta con esta sesion: se aplico (D6.6)
        else { Assert(client.Blocked is not null); client.DiscardBlocked(); Assert(client.Blocked is null&&store.Discarded); }
        Assert(requests.All(r=>r.StartsWith("GET ",StringComparison.Ordinal)));
    }
});
await Check("the author of a stored order is said in words, never as a raw actor",()=>{
    Assert(Labels.Author("user:ana")=="«ana»"&&Labels.Author("device:pc-barra")=="el dispositivo «pc-barra»"&&Labels.Author(null)=="alguien sin identificar"&&Labels.Author("raro")=="«raro»");
    var person=Labels.OtherActorPending("user:ana","Vender por copas Albariño","k1");
    var device=Labels.OtherActorPending("device:pc-barra",null,null);
    var legacy=Labels.UnknownActorPending("Abrir mesa","k2","Se guardó con una versión anterior de Costiña, que no anotaba quién la hizo.");
    Assert(person.StartsWith("ORDEN SIN CONFIRMAR DE OTRA PERSONA, hecha por «ana»: «Vender por copas Albariño» (identificador k1). No se reenvía desde tu sesión",StringComparison.Ordinal)
        &&person.Contains("pide a «ana» que entre como responsable en este PC",StringComparison.Ordinal)&&person.Contains("descártala",StringComparison.Ordinal));
    Assert(device.StartsWith("ORDEN SIN CONFIRMAR DE OTRO DISPOSITIVO, hecha por el dispositivo «pc-barra». No se reenvía",StringComparison.Ordinal)&&device.Contains("conéctalo como puesto",StringComparison.Ordinal));
    Assert(legacy.StartsWith("ORDEN ANTERIOR SIN AUTOR ANOTADO: «Abrir mesa» (identificador k2). Se guardó con una versión anterior",StringComparison.Ordinal)&&legacy.Contains("Consulta al servidor",StringComparison.Ordinal));
    Assert(new[]{person,device,legacy,Labels.UnknownActorNotFound}.All(t=>!t.Contains("user:",StringComparison.Ordinal)&&!t.Contains("device:",StringComparison.Ordinal)));
    return Task.CompletedTask;
});
await Check("auth login parses result and maps generic rejection",async()=>{
    using var auth=new AuthClient(new Uri("http://127.0.0.1:5088"),new Handler((r,_)=>{
        Assert(r.RequestUri!.AbsolutePath=="/api/native/v1/auth/login");
        return Task.FromResult(Response(200,"{\"token\":\"t-1\",\"expiresAt\":\"2026-09-17T12:00:00Z\",\"role\":\"main\",\"username\":\"jefe\"}"));
    }));
    var login=await auth.LoginAsync("jefe","una-contrasena-larga");
    Assert(login.Token=="t-1"&&login.Role=="main"&&login.Username=="jefe");
});
await Check("auth login failure raises generic credentials error",async()=>{
    using var auth=new AuthClient(new Uri("http://127.0.0.1:5088"),new Handler((_,_)=>Task.FromResult(Response(401,"{\"error\":\"invalid_credentials\"}"))));
    try { await auth.LoginAsync("jefe","mala"); throw new Exception("expected"); }
    catch(ApiError e){ Assert(e.Status==401&&e.Code=="invalid_credentials"); }
});
await Check("pairing collect distinguishes pending, denied and approved",async()=>{
    var phase=0;
    using var auth=new AuthClient(new Uri("http://127.0.0.1:5088"),new Handler((_,_)=>Task.FromResult(phase switch{
        0=>Response(200,"{\"status\":\"pending\"}"),
        1=>Response(403,"{\"status\":\"denied\"}"),
        _=>Response(200,"{\"status\":\"approved\",\"deviceId\":\"d1\",\"deviceToken\":\"dev.d1.s\",\"role\":\"service\",\"station\":\"sala-1\"}")})));
    Assert((await auth.CollectAsync("p","s")).Status=="pending"); phase=1;
    Assert((await auth.CollectAsync("p","s")).Status=="denied"); phase=2;
    var approved=await auth.CollectAsync("p","s");
    Assert(approved.DeviceToken=="dev.d1.s"&&approved.Station=="sala-1");
});
await Check("raw identity POST never touches the uncertain command",async()=>{
    using var client=Client(new Handler((_,_)=>Task.FromResult(Response(200,"{\"revoked\":true}"))));
    _=await client.PostRawAsync("auth/devices/x/revoke",new{});
    Assert(client.Pending is null);
});
// E2: organizacion editable. Lectura tipada y comando por la tuberia incierta (Idempotency-Key, orden persistida antes de enviar).
await Check("organization read decodes zones with tables and stations",async()=>{
    using var client=Client(new Handler((r,_)=>{
        Assert(r.RequestUri!.AbsolutePath=="/api/native/v1/organization");
        return Task.FromResult(Response(200,"{\"zones\":[{\"id\":\"sala\",\"name\":\"Sala\",\"sort\":0,\"active\":true,\"tables\":[{\"id\":\"M1\",\"name\":\"Mesa 1\",\"capacity\":12,\"zoneId\":\"sala\",\"sort\":0,\"active\":true}]}],\"stations\":[{\"id\":\"pase\",\"name\":\"Pase\",\"kind\":\"Pass\",\"sort\":0,\"active\":true}]}"));
    }));
    var org=await client.GetAsync<OrganizationDto>("organization");
    Assert(org.Zones.Single().Tables.Single().Capacity==12&&org.Stations.Single().KindLabel=="Pase"&&org.Zones[0].ToString().Contains("1 mesa activa ·"));
    Assert(org.Zones[0].Tables[0].ToString()=="Mesa 1 · 12 plazas · activa"&&org.Stations[0].ToString()=="Pase · Pase · activa");   // C1: nombres, no codigos
});
await Check("organization command travels with an idempotency key and is persisted before sending",async()=>{
    var store=new MemoryStore(); string? key=null;
    using var client=new ApiClient(new Uri("http://127.0.0.1:5088"),new string('a',40),new Handler((r,_)=>{
        Assert(r.RequestUri!.AbsolutePath=="/api/native/v1/organization/commands/table-create"); key=r.Headers.GetValues("Idempotency-Key").Single();
        Assert(store.LastSavedKey==key);   // durable ANTES del primer intento
        var ok=Response(200,"{\"id\":\"T1\",\"name\":\"Terraza 1\",\"capacity\":4,\"zoneId\":\"terraza\",\"sort\":0,\"active\":true}"); ok.Headers.Add("Idempotency-Key",key); return Task.FromResult(ok);
    }));
    client.AttachPendingStore(store);
    var result=await client.SendAsync("organization/commands/table-create",new{id="T1",name="Terraza 1",capacity=4,zoneId="terraza",sort=0},"Crear mesa T1");
    Assert(result.GetProperty("id").GetString()=="T1"&&client.Pending is null&&store.ClearedKeys.Single()==key);
});
// E3: catalogo y tarifas. Lectura tipada (precios por tarifa con vigencia) y price-set por la tuberia incierta.
await Check("catalog read decodes taxes, categories, products with presentations and prices, and tariffs",async()=>{
    using var client=Client(new Handler((r,_)=>{
        Assert(r.RequestUri!.AbsolutePath=="/api/native/v1/erp/catalog");
        return Task.FromResult(Response(200,"{\"taxes\":[{\"id\":\"iva-10\",\"name\":\"IVA 10 %\",\"rate\":10.00,\"active\":true}],\"categories\":[{\"id\":\"vinos\",\"name\":\"Vinos\",\"parentId\":\"bebidas\",\"color\":\"#8C1D1D\",\"sort\":1,\"active\":true}],"
            +"\"products\":[{\"id\":\"wine\",\"name\":\"Vino\",\"categoryId\":\"vinos\",\"taxId\":\"iva-21\",\"reference\":\"3754\",\"sort\":0,\"active\":true,\"presentations\":[{\"id\":\"glass\",\"name\":\"Copa\",\"sort\":0,\"active\":true,\"prices\":[{\"tariffId\":\"general\",\"validFrom\":\"1970-01-01\",\"priceCents\":950}]}]}],"
            +"\"tariffs\":[{\"id\":\"general\",\"name\":\"General\",\"sort\":0,\"active\":true}],\"today\":\"2026-09-23\"}"));
    }));
    var catalog=await client.GetAsync<CatalogDto>("erp/catalog");
    Assert(catalog.Taxes.Single().Rate==10m&&catalog.Products.Single().Presentations.Single().Prices.Single().PriceCents==950&&catalog.Tariffs.Single().ToString().Contains("activa")&&catalog.Today=="2026-09-23");
});
await Check("price-set travels with an idempotency key, is persisted before sending and closes on the echoed key",async()=>{
    var store=new MemoryStore(); string? key=null;
    using var client=new ApiClient(new Uri("http://127.0.0.1:5088"),new string('a',40),new Handler((r,_)=>{
        Assert(r.RequestUri!.AbsolutePath=="/api/native/v1/erp/catalog/commands/price-set"); key=r.Headers.GetValues("Idempotency-Key").Single();
        Assert(store.LastSavedKey==key);
        var ok=Response(200,"{\"tariffId\":\"general\",\"productId\":\"wine\",\"presentationId\":\"glass\",\"validFrom\":\"2026-09-23\",\"priceCents\":1000,\"changed\":true}"); ok.Headers.Add("Idempotency-Key",key); return Task.FromResult(ok);
    }));
    client.AttachPendingStore(store);
    var result=await client.SendAsync("erp/catalog/commands/price-set",new{tariffId="general",productId="wine",presentationId="glass",validFrom="",priceCents=1000},"Fijar precio");
    Assert(result.GetProperty("changed").GetBoolean()&&client.Pending is null&&store.ClearedKeys.Single()==key);
    var choice=JsonSerializer.Deserialize<ProductChoice>("{\"id\":\"wine\",\"name\":\"Vino\",\"presentation\":\"Copa\",\"priceCents\":950,\"presentationId\":\"glass\",\"categoryName\":\"Vinos\",\"tariffId\":\"general\"}",new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    var legacy=JsonSerializer.Deserialize<ProductChoice>("{\"id\":\"water\",\"name\":\"Agua\",\"presentation\":\"Botella\",\"priceCents\":400}",new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    Assert(choice.ToString().StartsWith("Vinos · Vino · Copa")&&legacy.PresentationId is null&&legacy.ToString().StartsWith("Agua · Botella"));
});
// E3.2: imagenes de los articulos. Los catalogos traen la huella (opcional: un servidor anterior no la envia); los bytes se piden
// aparte con la credencial y se guardan por huella; poner y quitar son ordenes normales de la tuberia incierta.
var web=new JsonSerializerOptions(JsonSerializerDefaults.Web);
byte[] png=[0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,0,0,0,13,(byte)'I',(byte)'H',(byte)'D',(byte)'R',0,0,0,1,0,0,0,1,8,6,0,0,0,0x1F,0x15,0xC4,0x89];
var pngSha=ProductImages.Sha256(png);
await Check("catalog DTOs carry imageSha when the server sends it and stay null for an older server",()=>{
    var product=JsonSerializer.Deserialize<ProductDto>("{\"id\":\"wine\",\"name\":\"Vino\",\"taxId\":\"iva-21\",\"sort\":0,\"active\":true,\"presentations\":[],\"imageSha\":\""+pngSha+"\"}",web)!;
    var older=JsonSerializer.Deserialize<ProductDto>("{\"id\":\"wine\",\"name\":\"Vino\",\"taxId\":\"iva-21\",\"sort\":0,\"active\":true,\"presentations\":[],\"stationId\":\"hot\"}",web)!;
    var choice=JsonSerializer.Deserialize<ProductChoice>("{\"id\":\"wine\",\"name\":\"Vino\",\"presentation\":\"Copa\",\"priceCents\":950,\"imageSha\":\""+pngSha+"\"}",web)!;
    var plain=JsonSerializer.Deserialize<ProductChoice>("{\"id\":\"wine\",\"name\":\"Vino\",\"presentation\":\"Copa\",\"priceCents\":950}",web)!;
    Expect(product.ImageSha==pngSha&&older.ImageSha is null&&older.StationId=="hot","product imageSha");
    Expect(choice.ImageSha==pngSha&&plain.ImageSha is null&&choice.ToString()==plain.ToString(),"product choice imageSha (never painted as text)");
    // La organizacion trae las tarifas para elegir la de cada sala; un servidor anterior no: null, y la sala sigue decodificando.
    var organization=JsonSerializer.Deserialize<OrganizationDto>("{\"zones\":[{\"id\":\"terraza\",\"name\":\"Terraza\",\"sort\":1,\"active\":true,\"tables\":[],\"tariffId\":\"terraza\"}],\"stations\":[],"
        +"\"tariffs\":[{\"id\":\"general\",\"name\":\"General\",\"sort\":0,\"active\":true},{\"id\":\"terraza\",\"name\":\"Terraza\",\"sort\":1,\"active\":true}]}",web)!;
    var legacy=JsonSerializer.Deserialize<OrganizationDto>("{\"zones\":[{\"id\":\"sala\",\"name\":\"Sala\",\"sort\":0,\"active\":true,\"tables\":[]}],\"stations\":[]}",web)!;
    Expect(organization.Tariffs!.Length==2&&organization.Zones[0].TariffId=="terraza"&&legacy.Tariffs is null&&legacy.Zones[0].TariffId is null,"organization tariffs");
    Expect(organization.Zones[0].ToString()=="Terraza · 0 mesas activas · activa"&&(organization.Zones[0] with {TariffName="Terraza"}).ToString()=="Terraza · tarifa Terraza · 0 mesas activas · activa","zone row names its own tariff");
    Expect(!JsonSerializer.Serialize(organization.Zones[0] with {TariffName="Terraza"},web).Contains("tariffName"),"the tariff name is never sent back");
    return Task.CompletedTask;});
// C2: GET /configuration trae las estaciones (todos los roles, tambien las desactivadas) para escribir su nombre; un servidor
// anterior no las envia: Stations null y se sigue viendo el codigo, sin inventar nada.
await Check("configuration stations give names to every role and an older server keeps the codes",async()=>{
    const string tables="\"tables\":[{\"id\":\"M1\",\"name\":\"Mesa 1\",\"capacity\":4,\"zoneId\":\"sala\",\"zoneName\":\"Sala\"}]";
    var json="{"+tables+",\"offers\":[],\"menus\":[],\"stations\":[{\"id\":\"pase\",\"name\":\"Pase\",\"kind\":\"pass\",\"active\":true},"
        +"{\"id\":\"hot\",\"name\":\"Cocina caliente\",\"kind\":\"kitchen\",\"active\":true},{\"id\":\"barra\",\"name\":\"Barra alta\",\"kind\":\"bar\",\"active\":false}]}";
    using var client=Client(new Handler((r,_)=>Task.FromResult(Response(200,r.RequestUri!.AbsolutePath.EndsWith("/configuration",StringComparison.Ordinal)?json:"{}"))));
    var current=await client.GetAsync<Configuration>("configuration");
    var stations=current.Stations??throw new Exception("stations missing");
    Expect(stations.Length==3&&stations[2] is {Id:"barra",Kind:"bar",Active:false},"stations decoded, inactive ones included");
    Expect(stations.Select(s=>s.KindLabel).SequenceEqual(["Pase","Cocina","Barra"])&&stations[1].ToString()=="Cocina caliente","kind labels and the name as text");
    var names=new Names(current);
    Expect(names.Station("hot")=="Cocina caliente"&&names.Station("barra")=="Barra alta"&&names.Station("cold")=="cold"&&names.Table("M1")=="Mesa 1","names from the configuration (unknown code shown raw)");
    var older=JsonSerializer.Deserialize<Configuration>("{"+tables+",\"menus\":[]}",web)!;
    Expect(older.Stations is null&&new Names(older).Station("hot")=="hot"&&new Names(older).Table("M1")=="Mesa 1","older server: no stations, codes stay");
    Expect(new StationChoice("x","Rara","grill").KindLabel=="grill","an unknown kind is shown raw");
});
await Check("product-image-set takes the type from the bytes, sends them in base64, is persisted before sending and closes on the echoed key",async()=>{
    var store=new MemoryStore(); string? key=null; JsonElement sent=default;
    using var client=new ApiClient(new Uri("http://127.0.0.1:5088"),new string('a',40),new Handler(async (r,_)=>{
        Assert(r.Method==HttpMethod.Post&&r.RequestUri!.AbsolutePath=="/api/native/v1/erp/catalog/commands/product-image-set"); key=r.Headers.GetValues("Idempotency-Key").Single();
        Assert(store.LastSavedKey==key);   // durable ANTES del primer intento, como toda orden
        sent=JsonSerializer.Deserialize<JsonElement>(await r.Content!.ReadAsStringAsync());
        var ok=Response(200,"{\"productId\":\"wine\",\"sha256\":\""+pngSha+"\",\"bytes\":"+png.Length+",\"contentType\":\"image/png\",\"changed\":true}"); ok.Headers.Add("Idempotency-Key",key); return ok;
    }));
    client.AttachPendingStore(store);
    var result=await client.SetProductImageAsync("wine",png,"Cambiar imagen de Vino");
    Expect(sent.GetProperty("productId").GetString()=="wine"&&sent.GetProperty("contentType").GetString()=="image/png"&&Convert.FromBase64String(sent.GetProperty("data").GetString()!).SequenceEqual(png),"body: "+sent);
    Expect(sent.EnumerateObject().Select(p=>p.Name).Order().SequenceEqual(["contentType","data","productId"]),"only productId, contentType and data travel");
    Expect(result.GetProperty("changed").GetBoolean()&&client.Pending is null&&store.ClearedKeys.Single()==key&&store.Stored is null,"closed on the echoed key");
    Expect(client.Images.Get(pngSha) is {Type:"image/png"},"the confirmed image is cached by its sha: the catalog re-read does not download it again");
});
await Check("an image that is not JPEG, PNG or WebP, is empty or passes 2 MB is never sent",async()=>{
    var calls=0;
    using var client=Client(new Handler((_,_)=>{calls++;return Task.FromResult(Response(200,"{}"));}));
    var tooBig=new byte[ProductImages.MaxBytes+1]; png.CopyTo(tooBig,0);
    foreach(var bad in new[]{"<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray(),"<html><body>no</body></html>"u8.ToArray(),"GIF89a\u0001\u0000"u8.ToArray(),
        "RIFF\u0000\u0000\u0000\u0000WAVEfmt "u8.ToArray(),new byte[]{0xFF,0xD8},Array.Empty<byte>(),tooBig})
        await Throws<ArgumentException>(()=>client.SetProductImageAsync("wine",bad,"Cambiar imagen"));
    Expect(calls==0&&client.Pending is null,"nothing reached the server and nothing is pending");
    Expect(ProductImages.TypeOf([0xFF,0xD8,0xFF,0xE0])=="image/jpeg"&&ProductImages.TypeOf("RIFF\u0010\u0000\u0000\u0000WEBPVP8 "u8)=="image/webp"&&ProductImages.TypeOf(png)=="image/png","signatures");
    try { ProductImages.CheckSize(ProductImages.MaxBytes+1); throw new Exception("2 MB + 1 must be refused"); }
    catch(ArgumentException e) { Expect(e.Message.StartsWith("La imagen ocupa 2,1 MB; el máximo es 2 MB.",StringComparison.Ordinal),"size message: "+e.Message); }
    ProductImages.CheckSize(ProductImages.MaxBytes);   // exactamente 2 MB si vale
});
await Check("product-image-remove travels as a normal command with only the product",async()=>{
    string? body=null;
    using var client=Client(new Handler(async (r,_)=>{
        Assert(r.RequestUri!.AbsolutePath=="/api/native/v1/erp/catalog/commands/product-image-remove");
        body=await r.Content!.ReadAsStringAsync();
        var ok=Response(200,"{\"productId\":\"wine\",\"changed\":true}"); ok.Headers.Add("Idempotency-Key",r.Headers.GetValues("Idempotency-Key").Single()); return ok;
    }));
    await client.RemoveProductImageAsync("wine","Quitar imagen de Vino");
    Expect(body=="{\"productId\":\"wine\"}"&&client.Pending is null,"remove body: "+body);
});
await Check("an image is read with the bearer, checked (type, signature, size, hash) and asked only once per sha",async()=>{
    var gets=0; var mode="ok";
    using var client=Client(new Handler((r,_)=>{
        gets++;
        Assert(r.Method==HttpMethod.Get&&r.Headers.Authorization?.Scheme=="Bearer");
        if(r.RequestUri!.AbsolutePath=="/api/native/v1/catalog/images/none") return Task.FromResult(Response(404,"{\"error\":\"not_found\"}"));
        Assert(r.RequestUri!.AbsolutePath=="/api/native/v1/catalog/images/wine");
        var body=mode=="svg"?"<svg/>"u8.ToArray():png;
        var response=new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(body)};
        response.Content.Headers.ContentType=new System.Net.Http.Headers.MediaTypeHeaderValue(mode=="type"?"image/jpeg":mode=="svg"?"image/svg+xml":"image/png");
        response.Headers.ETag=new System.Net.Http.Headers.EntityTagHeaderValue("\""+(mode=="etag"?new string('0',64):pngSha)+"\"");
        return Task.FromResult(response);
    }));
    foreach(var bad in new[]{"type","svg","etag"})
    {
        mode=bad;
        await Throws<InvalidDataException>(()=>client.GetProductImageAsync("wine",pngSha));
        Expect(client.Images.Count==0,"a refused image is never cached: "+bad);
    }
    mode="ok";
    var first=await client.GetProductImageAsync("wine",pngSha);
    Expect(first is {ContentType:"image/png"}&&first.Sha256==pngSha&&first.Content.SequenceEqual(png),"image read");
    var before=gets;
    var again=await client.GetProductImageAsync("wine",pngSha);
    Expect(gets==before&&again!.Content.SequenceEqual(png),"the same sha is served from memory");
    Expect(await client.GetProductImageAsync("none","otra") is null,"404 = no image");
    var cache=new ImageCache(10); cache.Put("a","image/png",new byte[6]); cache.Put("b","image/png",new byte[6]);
    Expect(cache.Get("a") is null&&cache.Get("b") is not null&&cache.Bytes==6,"the cache keeps a ceiling and drops the least used");
});
// E2: la mesa operativa lleva su sala; un servidor anterior sin ese campo sigue decodificando.
await Check("table choice shows its zone when the server sends it",()=>{
    var withZone=JsonSerializer.Deserialize<TableChoice>("{\"id\":\"M1\",\"name\":\"Mesa 1\",\"capacity\":12,\"zoneId\":\"sala\",\"zoneName\":\"Sala\"}",new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    var legacy=JsonSerializer.Deserialize<TableChoice>("{\"id\":\"M1\",\"name\":\"Mesa 1\",\"capacity\":12}",new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    Assert(withZone.ToString()=="Sala · Mesa 1"&&legacy.ToString()=="Mesa 1"); return Task.CompletedTask;
});
// C1 (plan 2026-10-14, seccion 4): vocabulario. TODOS los valores que el dominio puede enviar tienen su texto en castellano,
// distinto del valor crudo; una orden en minusculas (fire-next, a-la-carte, adapt) da el mismo texto que la lectura; un valor
// DESCONOCIDO vuelve tal cual: nunca se rebaja (una restriccion nueva no puede salir como "Preferencia (leve)").
void Expect(bool ok,string what){ if(!ok) throw new Exception(what); }
static string Kebab(string name)=>string.Concat(name.Select((c,i)=>i>0&&char.IsUpper(c)?"-"+char.ToLowerInvariant(c):char.ToLowerInvariant(c).ToString()));
static string[] Lower<T>() where T:struct,Enum=>Enum.GetNames<T>().Select(n=>n.ToLowerInvariant()).ToArray();
string[] actions=["open","start","fire-next","serve","pause","resume","complete","cancel-unstarted","skip","ready","choose","add-dish",
    "preparation-start","preparation-ready","review-preparation","declare-restriction","remove-restriction","release",
    "add-product","add-consumption","payment","void-charge","refund","close","reopen",
    // E4e: las ordenes de grupo (un plato agrupado, o el menu de un comensal, en una orden).
    "preparations-start","preparations-ready","review-preparations"];
var vocabulary=new (string Family,string[] Codes,Func<string,string> Label)[]{
    ("servicio",Enum.GetNames<DiningState>(),Labels.Dining),
    ("pase",Enum.GetNames<CourseState>(),Labels.Course),
    ("elaboracion",Enum.GetNames<PreparationState>(),Labels.Preparation),
    ("ocupacion",Enum.GetNames<OccupancyState>(),Labels.Occupancy),
    ("cuenta",Enum.GetNames<AccountState>(),Labels.Account),
    ("cobertura",Enum.GetNames<PaymentCoverage>(),Labels.Coverage),
    ("tipo de restriccion",Enum.GetNames<RestrictionKind>(),Labels.RestrictionKind),
    ("tipo de restriccion (orden)",Lower<RestrictionKind>(),Labels.RestrictionKind),
    ("gravedad",Enum.GetNames<RestrictionSeverity>(),Labels.Severity),
    ("gravedad (orden)",Lower<RestrictionSeverity>(),Labels.Severity),
    ("revision de cocina",Enum.GetNames<ReviewDecision>(),Labels.Review),
    ("revision de cocina (orden)",Lower<ReviewDecision>(),Labels.Review),
    ("tipo de estacion",Enum.GetNames<StationKind>(),Labels.StationKind),
    ("tipo de estacion (orden)",Lower<StationKind>(),Labels.StationKind),
    ("tipo de oferta",Enum.GetNames<OfferKind>().Select(Kebab).ToArray(),Labels.OfferKind),
    ("tipo de oferta (enum)",Enum.GetNames<OfferKind>(),Labels.OfferKind),
    ("servicio de la oferta",Lower<OfferService>(),Labels.OfferService),
    ("metodo de pago",["card","cash","other"],Labels.PaymentMethod),
    ("rol de dispositivo",["service","kitchen","main"],Labels.DeviceRole),
    ("rol de sesion",["main","service","kitchen"],Labels.SessionRole),
    ("tiempo real",["connected","reconnecting","closed","unavailable","inactive"],Labels.Realtime),
    ("estado de mesa (plano)",[FloorPlan.Free,FloorPlan.Seated,FloorPlan.Kitchen,FloorPlan.Ready,FloorPlan.Eating,FloorPlan.Paused,FloorPlan.Finished,FloorPlan.Cancelled],Labels.TableState),
    ("accion",actions,Labels.Action),
    ("vista",["HomeTab","ServiceTab","CheckoutTab","CatalogTab","OffersTab","OrganizationTab","DevicesTab"],Labels.View)};
foreach(var (family,codes,label) in vocabulary)
    await Check("vocabulary: every "+family+" value has a Spanish label",()=>{
        Expect(codes.Length>0,family+": no values to check");
        foreach(var code in codes)
        {
            var text=label(code);
            Expect(text.Trim().Length>0&&!string.Equals(text,code,StringComparison.OrdinalIgnoreCase),$"{family}: '{code}' has no Spanish label ('{text}')");
            Expect(label("valor-desconocido-"+code)=="valor-desconocido-"+code,$"{family}: an unknown value must come back raw");
        }
        return Task.CompletedTask;});
await Check("vocabulary: the exact words of the plan table",()=>{
    Expect(string.Join("|",Enum.GetNames<DiningState>().Select(Labels.Dining))=="Sentados|En servicio|En pausa|Terminado|Cancelado","servicio");
    Expect(string.Join("|",Enum.GetNames<CourseState>().Select(Labels.Course))=="Sin enviar|En cocina|En preparación|Listo para servir|Servido|Omitido","pase");
    Expect(string.Join("|",Enum.GetNames<PreparationState>().Select(Labels.Preparation))=="Sin enviar|Por empezar|En preparación|Lista","elaboracion");
    Expect(string.Join("|",Enum.GetNames<AccountState>().Select(Labels.Account))=="Abierta|Cerrada","cuenta");
    Expect(string.Join("|",Enum.GetNames<PaymentCoverage>().Select(Labels.Coverage))=="Sin pagar|Pago parcial|Pagada|Pagado de más","cobertura");
    Expect(string.Join("|",new[]{"card","cash","other"}.Select(Labels.PaymentMethod))=="Tarjeta|Efectivo|Otro","pago");
    Expect(string.Join("|",new[]{"tasting","set-menu","a-la-carte"}.Select(Labels.OfferKind))=="Menú degustación|Menú con elección|Carta","oferta");
    Expect(string.Join("|",new[]{"any","lunch","dinner"}.Select(Labels.OfferService))=="Comida y cena|Comida|Cena","servicio de la oferta");
    // C3c (plan 2026-10-22, seccion 3): los dispositivos son las dos aplicaciones, «Costiña Sala» y «Costiña Cocina» (sustituyen a
    // «Comandero» y «Pantalla de cocina»); el antiguo con rol principal sigue diciendolo. La sesion de ESTE PC es un puesto: de sala o de
    // cocina, nunca una de las aplicaciones de los moviles. Los tipos de dispositivo no estan en vocabulary-cases.json: su copia en la
    // PWA (apps.ts, KINDS) se comprueba con vitest.
    Expect(string.Join("|",new[]{"service","kitchen","main"}.Select(Labels.DeviceRole))=="Costiña Sala|Costiña Cocina|Principal (antiguo)","tipo de dispositivo");
    Expect(string.Join("|",new[]{"main","service","kitchen"}.Select(Labels.SessionRole))=="Responsable|Puesto de sala|Puesto de cocina","rol");
    Expect(string.Join("|",new[]{"connected","reconnecting","closed"}.Select(Labels.Realtime))=="Tiempo real activo|Reconectando…|Sin tiempo real, reintentando","tiempo real");
    Expect(Labels.Guest(null)=="Toda la mesa"&&Labels.Guest(3)=="Comensal 3","comensal");
    Expect(string.Join("|",new[]{"free","seated","kitchen","ready","eating","paused","finished","cancelled"}.Select(Labels.TableState))
        =="Libre|Sentados|En cocina|Listo para servir|Comiendo|En pausa|Terminado, por liberar|Cancelado, por liberar","estado de mesa");
    Expect(Labels.Presence(true,null)=="Conectado"&&Labels.Presence(false,null)=="Sin conexión","presencia");
    var removedAt=new DateTimeOffset(2026,10,14,21,30,0,TimeSpan.Zero);
    Expect(Labels.Presence(true,removedAt)=="Quitado el "+removedAt.ToLocalTime().ToString("dd/MM HH:mm",System.Globalization.CultureInfo.InvariantCulture),"quitado, en hora local");
    return Task.CompletedTask;});
await Check("vocabulary: an unknown restriction is shown raw, never as a mild preference",()=>{
    var odd=new GuestRestrictionDto("r1",2,"Anaphylaxis","cacahuete","Critical");
    Expect(odd.KindLabel=="Anaphylaxis"&&odd.SeverityLabel=="Critical"&&odd.ToString()=="Comensal 2 · Anaphylaxis cacahuete (Critical)","unknown restriction: "+odd);
    var known=new GuestRestrictionDto("r2",null,"Allergy","gluten","Severe");
    Expect(known.ToString()=="Toda la mesa · ALERGIA gluten (grave)","known restriction: "+known);
    var prep=new PreparationDto("p1","Plato","hot",1,null,true,"Fired",null,[odd,known]);
    Expect(prep.RestrictionsText.Contains("⚠ Anaphylaxis cacahuete (Critical)")&&!prep.RestrictionsText.Contains("leve")&&!prep.RestrictionsText.Contains("Preferencia"),"preparation text: "+prep.RestrictionsText);
    Expect(Labels.RestrictionKind("")==""&&Labels.Severity("Grave!")=="Grave!","raw, even when odd");
    return Task.CompletedTask;});
await Check("vocabulary: server rejections in clear Spanish; an unknown one keeps its code",()=>{
    foreach(var code in new[]{"version_conflict","table_occupied","account_not_balanced","unfinished_courses","restrictions_unreviewed","restrictions_unacknowledged",
        "wrong_station","account_closed","account_not_closed","refund_exceeds_credit","forbidden","choice_missing","course_empty","no_pending_course","active_course",
        "service_finished","not_found","duplicate_code","table_in_use","reserved_station","reserved_tariff","offer_unavailable","price_missing","image_invalid","image_too_large",
        "station_kind_mismatch","device_role_not_allowed","storage_conflict","operation_unconfirmed","request_failed","too_many_items",
        // C3b: los diferidos de E3.3.1 y los de la ronda (add-consumptions).
        "reserved_category","nothing_to_do","product_ids_required","use_add_dish","too_many_lines","duplicate_consumption",
        // E4e (ADR-016): rehacer lo que no ha salido y abrir con un tipo o una gravedad que el servidor no conoce.
        "nothing_to_remake","invalid_restriction"})
    {
        var text=Labels.Rejection(code);
        Expect(text!=code&&!text.Contains("(código",StringComparison.Ordinal)&&!text.Contains('_'),"rejection without a sentence: "+code+" -> "+text);
    }
    Expect(Labels.Rejection("brand_new_rule")=="El servidor rechazó la operación (código brand_new_rule).","unknown rejection: "+Labels.Rejection("brand_new_rule"));
    return Task.CompletedTask;});
// C1 (revision): el aviso de una orden fallida sale del MISMO estado que la orden: si sigue pendiente (5xx, conflicto de
// almacenamiento) no se sabe si se aplico y nunca se dice "rechazo"; un rechazo definitivo (ya cerrado) si es un rechazo.
await Check("vocabulary: an uncertain failure is never told as a rejection",async()=>{
    foreach(var (status,body) in new[]{(409,"{\"error\":\"storage_conflict\"}"),(503,"{\"error\":\"brand_new_rule\"}"),(502,"<html>gateway</html>")})
    {
        using var client=Client(new Handler((r,_)=>Task.FromResult(Rejection(status,body,r))));
        var error=(ApiError?)null;
        try { await client.SendAsync("dining/services/test/commands/start",new {expectedVersion=1},"Start"); } catch(ApiError e) { error=e; }
        Expect(error is not null&&client.Pending is not null,"an inconclusive answer keeps the command: "+status);
        var text=Labels.Failure(error!.Code,error.Status,client.Pending is not null);
        Expect(text=="El servidor no confirmó la operación: puede haberse aplicado o no (código "+error.Code+").","uncertain failure: "+text);
    }
    using var definitive=Client(new Handler((r,_)=>Task.FromResult(r.Method==HttpMethod.Get?Lookup(r,false):Rejection(409,"{\"error\":\"version_conflict\"}",r))));
    var rejected=(ApiError?)null;
    try { await definitive.SendAsync("dining/services/test/commands/start",new {expectedVersion=1},"Start"); } catch(ApiError e) { rejected=e; }
    Expect(rejected is not null&&definitive.Pending is null,"a definitive rejection closes the command");
    Expect(Labels.Failure(rejected!.Code,rejected.Status,definitive.Pending is not null)==Labels.Rejection("version_conflict"),"definitive rejection: "+Labels.Failure(rejected.Code,rejected.Status,false));
    Expect(Labels.Failure("request_failed",502,false)=="El servidor no dio una respuesta reconocible. (HTTP 502)"&&!Labels.Rejection("storage_conflict").Contains("no pudo guardar",StringComparison.Ordinal),"read failure and storage conflict wording");
});
await Check("vocabulary: course states carry the skip reason, the missing choice and the empty group",()=>{
    var chosen=new PreparationDto("sopa-1","Sopa","hot",1,1,true,"Pending");
    Expect(new CourseDto("c","Primeros","Skipped",null,null,null,"sin hambre",[]).StateLabel=="Omitido: sin hambre","skip reason");
    Expect(new CourseDto("c","Entrantes","Pending",null,null,null,null,[],Optional:true).StateLabel=="Sin platos pedidos","empty carta group");
    Expect(new CourseDto("c","Primeros","Pending",null,null,null,null,[],ChoiceRequired:true).StateLabel=="Falta elegir plato","nobody chose");
    var partly=new CourseDto("c","Primeros","Pending",null,null,null,null,[chosen],ChoiceRequired:true);
    Expect(Labels.Course(partly,2)=="Falta elegir plato"&&Labels.Course(partly,1)=="Sin enviar","with the headcount, a partial choice is still missing");
    Expect(new CourseDto("c","Primeros","Fired",null,null,null,null,[chosen]).StateLabel=="En cocina","fired");
    var at=new DateTimeOffset(2026,10,14,11,5,0,TimeSpan.Zero);
    var served=new CourseDto("c","Primeros","Served",at,at,at,null,[]);
    Expect(served.ServedAtLabel==at.ToLocalTime().ToString("HH:mm",System.Globalization.CultureInfo.InvariantCulture),"served time in LOCAL time: "+served.ServedAtLabel);
    Expect(TimeZoneInfo.Local.GetUtcOffset(at)==TimeSpan.Zero||served.ServedAtLabel!="11:05","served time must not be the UTC clock");
    Expect(new CourseDto("c","Primeros","Pending",null,null,null,null,[]).ServedAtLabel=="","not served yet");
    return Task.CompletedTask;});
await Check("vocabulary: actors without prefixes, plurals, active and names with the code as fallback",()=>{
    Expect(Labels.Actor("user:jefa")=="jefa"&&Labels.Actor("device:pc-barra")=="pc-barra"&&Labels.Actor("lab-main")=="laboratorio"&&Labels.Actor(null)==""&&Labels.Actor("otro")=="otro","actor");
    Expect(Labels.Plural(1,"mesa activa","mesas activas")=="1 mesa activa"&&Labels.Plural(0,"plato","platos")=="0 platos"&&Labels.Plural(3,"plato","platos")=="3 platos","plural");
    Expect(Labels.Active(true,true)=="activa"&&Labels.Active(false,true)=="desactivada"&&Labels.Active(true,false)=="activo"&&Labels.Active(false,false)=="desactivado","active");
    var names=new Names([new TableChoice("M1","Mesa 1",4,"sala","Sala")],[new StationDto("cold","Cocina fría","Kitchen",1,false)]);
    Expect(names.Table("M1")=="Mesa 1"&&names.Table("M9")=="M9"&&names.Station("cold")=="Cocina fría"&&names.Station("hot")=="hot"&&Names.Empty.Table(null)=="","names");
    Expect(new AccountChoice("s1","M1","Open").StateLabel=="Abierta"&&new OfferChoice("c","Carta de ensayo","a-la-carte",[]).ToString()=="Carta de ensayo · Carta","account and offer choices");
    Expect(new BoardEntry(1,new DiningDto("s1","M1",1,"InService",[]),new OccupancyDto("o1","M1","s1","Occupied",null),1).ToString()=="M1 · 1 persona · En servicio","board entry fallback");
    return Task.CompletedTask;});
// C2: plano del comedor. El estado de la tarjeta, su tiempo, el avance de los pases y el aviso de restricciones salen SOLO de lo
// leido (presentacion); un estado o un tipo de restriccion desconocidos salen tal cual y nunca se rebajan.
await Check("floor plan: tile state, time, progress and alert come from the read data, unknown values raw",()=>{
    var now=new DateTimeOffset(2026,10,15,20,40,0,TimeSpan.Zero);
    CourseDto C(string state,int? fired=null,int? ready=null,int? served=null,PreparationDto[]? preps=null,bool choice=false,bool optional=false)
        =>new("c-"+state,state,state,fired is {} f?now.AddMinutes(-f):null,ready is {} r?now.AddMinutes(-r):null,served is {} s?now.AddMinutes(-s):null,null,preps??[],null,choice,optional);
    DiningDto D(string state,params CourseDto[] courses)=>new("s1","M1",4,state,courses);
    Expect(FloorPlan.State(null)=="free"&&Labels.TableState(FloorPlan.State(null))=="Libre","free");
    Expect(FloorPlan.State(D("Open",C("Pending")))=="seated"&&FloorPlan.State(D("InService",C("Pending"),C("Pending")))=="seated","seated: open, or running with nothing sent yet");
    Expect(FloorPlan.State(D("InService",C("Served",30,25,20),C("Fired",12),C("Pending")))=="kitchen","in the kitchen");
    Expect(FloorPlan.State(D("InService",C("Served",30,25,20),C("Ready",12,3)))=="ready","ready beats the rest");
    Expect(FloorPlan.State(D("InService",C("Served",30,25,20),C("Skipped"),C("Pending")))=="eating","eating between courses");
    Expect(FloorPlan.State(D("Paused",C("Ready",12,3)))=="paused"&&FloorPlan.State(D("Completed",C("Served")))=="finished"&&FloorPlan.State(D("Cancelled",C("Pending")))=="cancelled","paused, finished, cancelled");
    Expect(FloorPlan.State(D("Closing"))=="Closing"&&Labels.TableState("Closing")=="Closing"&&FloorPlan.Label("Closing")=="Closing","an unknown service state is shown raw");
    Expect(FloorPlan.State(D("InService",C("Plating",5)))=="Plating","an unknown course state is shown raw, never softened to 'Comiendo'");
    // Revision C2: solo los codigos de FloorPlan se rotulan (comparacion exacta); un estado del servidor que se parece a uno de ellos
    // (Labels no distingue mayusculas) sigue saliendo tal cual: un pase "Cancelled" no es una mesa cancelada.
    Expect(FloorPlan.State(D("InService",C("Cancelled")))=="Cancelled"&&FloorPlan.Label(FloorPlan.State(D("InService",C("Cancelled"))))=="Cancelled"
        &&FloorPlan.Label(FloorPlan.State(D("Ready")))=="Ready"&&FloorPlan.Label(FloorPlan.State(D("Finished")))=="Finished"&&!FloorPlan.IsCode("Ready"),
        "a raw state that looks like a tile code stays raw: "+FloorPlan.Label(FloorPlan.State(D("InService",C("Cancelled")))));
    Expect(FloorPlan.Codes.All(c=>FloorPlan.Label(c)==Labels.TableState(c)&&FloorPlan.Label(c)!=c),"every tile code has its Spanish label");
    Expect(FloorPlan.Since(D("InService",C("Served",30,25,20),C("Fired",12)))==now.AddMinutes(-12)&&FloorPlan.Since(D("InService",C("Ready",12,3)))==now.AddMinutes(-3)
        &&FloorPlan.Since(D("InService",C("Served",40,35,30),C("Served",20,15,8),C("Pending")))==now.AddMinutes(-8)&&FloorPlan.Since(D("Open",C("Pending"))) is null,"time of the state shown (never invented)");
    Expect(Labels.Minutes(TimeSpan.FromMinutes(12.7))=="12 min"&&Labels.Minutes(TimeSpan.FromMinutes(65))=="1 h 05 min"&&Labels.Minutes(TimeSpan.FromMinutes(-2))=="0 min","minutes");
    var running=D("InService",C("Served",30,25,20),C("Skipped"),C("Ready",12,3),C("Fired",2),C("Pending"));
    Expect(string.Join(",",FloorPlan.Steps(running))=="done,done,ready,kitchen,pending"&&FloorPlan.Done(running)==2&&FloorPlan.Steps(null).Length==0,"course progress");
    GuestRestrictionDto allergy=new("r1",2,"Allergy","frutos secos","Severe"),mild=new("r2",3,"Intolerance","lactosa","Mild"),odd=new("r3",1,"Anaphylaxis","cacahuete","Critical"),
        severeIntolerance=new("r4",null,"Intolerance","gluten","Severe"),oddSeverity=new("r5",null,"Preference","picante","Extreme");
    Expect(FloorPlan.Alert(D("InService"))==""&&!FloorPlan.AlertIsAlarming(D("InService")),"no restrictions, no alert");
    Expect(FloorPlan.Alert(D("InService") with {Restrictions=[mild,allergy]})=="Alergia"&&FloorPlan.AlertIsAlarming(D("InService") with {Restrictions=[mild,allergy]}),"an allergy is named");
    Expect(FloorPlan.Alert(D("InService") with {Restrictions=[mild]})=="Restricción"&&!FloorPlan.AlertIsAlarming(D("InService") with {Restrictions=[mild]}),"a mild intolerance is a plain restriction");
    // Revision C2: la palabra de la tarjeta nombra lo mas alarmante y un valor desconocido sale tal cual (con su gravedad): el rojo y el
    // ambar nunca llevan la misma palabra, y un tipo nuevo nunca se rebaja a la palabra generica "Restriccion".
    string A(params GuestRestrictionDto[] list)=>FloorPlan.Alert(D("InService") with {Restrictions=list});
    Expect(A(odd)=="Anaphylaxis (Critical)"&&FloorPlan.AlertIsAlarming(D("InService") with {Restrictions=[odd]})&&odd.Alarming,"an unknown kind is shown raw with its severity, never softened: "+A(odd));
    Expect(A(severeIntolerance)=="Intolerancia (grave)"&&A(oddSeverity)=="Preferencia (Extreme)"&&A(mild,severeIntolerance)=="Intolerancia (grave)","a severe or unknown-severity intolerance or preference names kind and severity: "+A(oddSeverity));
    Expect(A(allergy,odd)=="Alergia, Anaphylaxis (Critical)"&&A(mild,allergy,severeIntolerance)=="Alergia"&&A(new GuestRestrictionDto("r6",1,"Allergy","sésamo","Critical"))=="Alergia (Critical)",
        "an allergy is named, an unknown kind next to it too, and an unknown allergy severity raw: "+A(allergy,odd));
    Expect(A(mild,new GuestRestrictionDto("r7",2,"Preference","picante","Moderate"))=="Restricción"&&!FloorPlan.AlertIsAlarming(D("InService") with {Restrictions=[mild]}),"only mild or moderate ones are a plain restriction");
    Expect(severeIntolerance.Alarming&&oddSeverity.Alarming&&!mild.Alarming&&allergy.Alarming,"severe or unknown severity is alarming");
    Expect(FloorPlan.Alert(D("InService") with {Restrictions=[allergy],RestrictionsPendingAck=true})=="Alergia · por revisar"&&FloorPlan.AlertIsAlarming(D("InService") with {Restrictions=[mild],RestrictionsPendingAck=true})
        &&FloorPlan.Alert(D("InService") with {RestrictionsPendingAck=true})=="Restricción · por revisar"&&FloorPlan.Alert(D("InService") with {Restrictions=[odd],RestrictionsPendingAck=true})=="Anaphylaxis (Critical) · por revisar"
        &&FloorPlan.AlertWord(D("InService") with {Restrictions=[allergy],RestrictionsPendingAck=true})=="Alergia","a change kitchen has not reviewed is said, after the word");
    var ready=new PreparationDto("p1","Merluza","hot",1,1,true,"Ready"); var fired=new PreparationDto("p2","Merluza","hot",1,2,true,"Fired");
    // E4e: cuenta PLATOS (las dos merluzas son un plato, que no esta listo hasta que lo estan las dos).
    Expect(FloorPlan.CourseDetail(C("Fired",12,preps:[ready,fired]),2)=="En cocina desde las "+Labels.Clock(now.AddMinutes(-12))+" · 0 de 1 plato listo","kitchen detail: "+FloorPlan.CourseDetail(C("Fired",12,preps:[ready,fired]),2));
    Expect(FloorPlan.CourseDetail(C("Served",30,25,20),2)=="Servido a las "+Labels.Clock(now.AddMinutes(-20))&&FloorPlan.CourseDetail(C("Ready",12,3),2)=="Listo desde las "+Labels.Clock(now.AddMinutes(-3)),"served and ready");
    Expect(FloorPlan.CourseDetail(C("Pending",preps:[ready],choice:true),4)=="Han elegido 1 de 4 personas"&&FloorPlan.CourseDetail(C("Pending",optional:true),2)=="Se piden platos de la carta"
        &&FloorPlan.CourseDetail(C("Pending",preps:[ready,fired],optional:true),2)=="2 platos pedidos"&&FloorPlan.CourseDetail(C("Pending",preps:[ready]),2)=="1 plato","pending detail");
    Expect(FloorPlan.CourseDetail(new CourseDto("c","Primeros","Skipped",null,null,null,"sin hambre",[]),2)=="Motivo: sin hambre"&&FloorPlan.CourseDetail(C("Plating"),2)=="","skipped and unknown");
    return Task.CompletedTask;});
// C3a: tablas de casos COMUNES con el comandero (dotnet/tests/fixtures, copiadas junto al ejecutable). frontend/pwa/tests/floor.test.ts y
// labels.test.ts recorren las MISMAS contra floor.ts y labels.ts: si FloorPlan o Labels cambian y la PWA no (o al reves), falla una de las
// dos. Cada caso compara solo los campos que trae; un campo o una familia que este lado no conoce es un fallo, nunca se salta.
var sharedCases=Path.Combine(AppContext.BaseDirectory,"fixtures");
static string Shown(JsonElement value)=>value.ValueKind switch{
    JsonValueKind.Array=>"["+string.Join(",",value.EnumerateArray().Select(Shown))+"]",
    JsonValueKind.True=>"true",JsonValueKind.False=>"false",JsonValueKind.Null=>"null",JsonValueKind.Number=>value.GetRawText(),_=>value.GetString()!};
using var floorCases=JsonDocument.Parse(File.ReadAllText(Path.Combine(sharedCases,"floorplan-cases.json")));
var floorNow=DateTimeOffset.Parse(floorCases.RootElement.GetProperty("now").GetString()!,System.Globalization.CultureInfo.InvariantCulture);
// Los estados que la tabla espera (no los de los casos que pasan): un codigo sin caso es un hueco aunque todo lo demas pase.
var tileStates=floorCases.RootElement.GetProperty("tiles").EnumerateArray().Where(t=>t.TryGetProperty("state",out _))
    .Select(t=>t.GetProperty("state").GetString()!).ToHashSet(StringComparer.Ordinal);
foreach(var tile in floorCases.RootElement.GetProperty("tiles").EnumerateArray())
    await Check("floor plan (shared cases): "+tile.GetProperty("case").GetString(),()=>{
        var service=tile.GetProperty("service").ValueKind==JsonValueKind.Null?null:tile.GetProperty("service").Deserialize<DiningDto>(ApiClient.Json)!;
        var actual=new Dictionary<string,string>(StringComparer.Ordinal){
            ["state"]=FloorPlan.State(service),["label"]=FloorPlan.Label(FloorPlan.State(service)),["elapsed"]=FloorPlan.Elapsed(service,floorNow),
            ["progress"]=FloorPlan.Progress(service),["steps"]="["+string.Join(",",FloorPlan.Steps(service))+"]",["alert"]=FloorPlan.Alert(service),
            ["alertWord"]=FloorPlan.AlertWord(service),["alarming"]=FloorPlan.AlertIsAlarming(service)?"true":"false"};
        var compared=0;
        foreach(var field in tile.EnumerateObject().Where(f=>f.Name is not ("case" or "service")))
        {
            Expect(actual.TryGetValue(field.Name,out var got),"unknown field in the shared cases: "+field.Name);
            Expect(got==Shown(field.Value),$"{field.Name} = '{got}', the shared case says '{Shown(field.Value)}'");
            compared++;
        }
        Expect(compared>0,"a shared case that compares nothing");
        return Task.CompletedTask;});
await Check("floor plan (shared cases): every tile code is covered",()=>{
    Expect(FloorPlan.Codes.All(tileStates.Contains),"codes without a shared case: "+string.Join(",",FloorPlan.Codes.Where(c=>!tileStates.Contains(c))));
    return Task.CompletedTask;});
foreach(var row in floorCases.RootElement.GetProperty("courses").EnumerateArray())
    await Check("floor plan course (shared cases): "+row.GetProperty("case").GetString(),()=>{
        var course=row.GetProperty("course").Deserialize<CourseDto>(ApiClient.Json)!; var pax=row.GetProperty("pax").GetInt32();
        string At(DateTimeOffset? at,string field)=>at is { } value?Labels.Clock(value):"{"+field+"}";
        var detail=row.GetProperty("detail").GetString()!.Replace("{firedAt}",At(course.FiredAt,"firedAt"),StringComparison.Ordinal)
            .Replace("{readyAt}",At(course.ReadyAt,"readyAt"),StringComparison.Ordinal).Replace("{servedAt}",At(course.ServedAt,"servedAt"),StringComparison.Ordinal);
        Expect(Labels.Course(course,pax)==row.GetProperty("label").GetString(),$"label = '{Labels.Course(course,pax)}'");
        Expect(FloorPlan.CourseDetail(course,pax)==detail,$"detail = '{FloorPlan.CourseDetail(course,pax)}', the shared case says '{detail}'");
        return Task.CompletedTask;});
using var vocabularyCases=JsonDocument.Parse(File.ReadAllText(Path.Combine(sharedCases,"vocabulary-cases.json")));
var sharedFamilies=new Dictionary<string,Func<string,string>>(StringComparer.Ordinal){
    ["dining"]=Labels.Dining,["course"]=Labels.Course,["preparation"]=Labels.Preparation,["tableState"]=Labels.TableState,["offerKind"]=Labels.OfferKind,
    ["stationKind"]=Labels.StationKind,["restrictionKind"]=Labels.RestrictionKind,["severity"]=Labels.Severity,["review"]=Labels.Review,["action"]=Labels.Action};
foreach(var family in vocabularyCases.RootElement.GetProperty("families").EnumerateObject())
    await Check("vocabulary (shared cases): "+family.Name,()=>{
        Expect(sharedFamilies.TryGetValue(family.Name,out var label),"unknown family in the shared cases: "+family.Name);
        foreach(var row in family.Value.EnumerateObject())
            Expect(label!(row.Name)==row.Value.GetString(),$"'{row.Name}' = '{label!(row.Name)}', the shared case says '{row.Value.GetString()}'");
        return Task.CompletedTask;});
// Y al reves: cada valor que el dominio puede enviar en una familia comun tiene su fila. Un valor nuevo con palabra solo en Labels.cs
// (el puesto en castellano, el comandero en crudo) falla aqui; con su fila en la tabla, vitest obliga a ponerla en labels.ts.
// Las acciones de caja no tienen palabra en el comandero (ADR-007): no se piden.
string[] checkoutActions=["add-product","payment","void-charge","refund","close","reopen"];
var domainValues=new Dictionary<string,string[]>(StringComparer.Ordinal){
    ["dining"]=Enum.GetNames<DiningState>(),["course"]=Enum.GetNames<CourseState>(),["preparation"]=Enum.GetNames<PreparationState>(),
    ["tableState"]=FloorPlan.Codes,["offerKind"]=Enum.GetNames<OfferKind>().Select(Kebab).ToArray(),
    ["stationKind"]=[..Enum.GetNames<StationKind>(),..Lower<StationKind>()],["restrictionKind"]=Enum.GetNames<RestrictionKind>(),
    ["severity"]=Enum.GetNames<RestrictionSeverity>(),["review"]=Enum.GetNames<ReviewDecision>(),["action"]=actions.Except(checkoutActions).ToArray()};
await Check("vocabulary (shared cases): every value the domain can send has its row",()=>{
    Expect(domainValues.Keys.Order().SequenceEqual(sharedFamilies.Keys.Order()),"a shared family without its domain values (or the reverse)");
    var families=vocabularyCases.RootElement.GetProperty("families");
    var missing=domainValues.SelectMany(pair=>families.TryGetProperty(pair.Key,out var rows)
        ? pair.Value.Where(code=>!rows.TryGetProperty(code,out _)).Select(code=>pair.Key+"."+code) : [pair.Key+" (family)"]).ToList();
    Expect(missing.Count==0,"values without a row in vocabulary-cases.json: "+string.Join(", ",missing));
    return Task.CompletedTask;});
await Check("vocabulary (shared cases): guests, restrictions, minutes and plurals",()=>{
    var root=vocabularyCases.RootElement;
    foreach(var row in root.GetProperty("guests").EnumerateArray())
    {
        int? position=row.GetProperty("position").ValueKind==JsonValueKind.Null?null:row.GetProperty("position").GetInt32();
        Expect(Labels.Guest(position)==row.GetProperty("text").GetString(),"guest: "+Labels.Guest(position));
    }
    foreach(var row in root.GetProperty("restrictions").EnumerateArray())
    {
        var text=row.GetProperty("restriction").Deserialize<GuestRestrictionDto>(ApiClient.Json)!.ToString();
        Expect(text==row.GetProperty("text").GetString(),"restriction: "+text);
    }
    foreach(var row in root.GetProperty("minutes").EnumerateArray())
    {
        var text=Labels.Minutes(TimeSpan.FromMinutes(row.GetProperty("minutes").GetDouble()));
        Expect(text==row.GetProperty("text").GetString(),"minutes: "+text);
    }
    foreach(var row in root.GetProperty("plural").EnumerateArray())
    {
        var text=Labels.Plural(row.GetProperty("count").GetInt32(),row.GetProperty("one").GetString()!,row.GetProperty("many").GetString()!);
        Expect(text==row.GetProperty("text").GetString(),"plural: "+text);
    }
    return Task.CompletedTask;});
// E4e: lo que la sala y el puesto leen de la decision de cocina sobre un plato («Por revisar en cocina», «Adaptado: …»). La MISMA tabla
// la recorre vitest contra reviewOutcome (labels.ts). Un campo que este lado no conoce es un fallo.
await Check("vocabulary (shared cases): what the room reads of a kitchen review",()=>{
    var rows=vocabularyCases.RootElement.GetProperty("reviews").EnumerateArray().ToArray();
    Expect(rows.Length>0,"no review cases");
    foreach(var row in rows)
    {
        var unknown=row.EnumerateObject().Select(p=>p.Name).Where(name=>name is not ("case" or "preparation" or "text")).ToArray();
        Expect(unknown.Length==0,"unknown field in the shared cases: "+string.Join(",",unknown));
        var text=Labels.ReviewOutcome(row.GetProperty("preparation").Deserialize<PreparationDto>(ApiClient.Json)!);
        Expect(text==row.GetProperty("text").GetString(),$"{row.GetProperty("case").GetString()}: '{text}', the shared case says '{row.GetProperty("text").GetString()}'");
    }
    return Task.CompletedTask;});
// E4e: platos agrupados de la ficha del Comedor («Ostra × 7») con las lineas de los comensales aparte, escritas. Solo presentacion: el
// boton de un plato solo lleva lo que el servidor anuncia en cada elaboracion.
await Check("dish groups: same name and station, first appearance order, exceptions apart and written",()=>{
    GuestRestrictionDto shellfish=new("r1",3,"Allergy","marisco","Severe"),coriander=new("r2",2,"Preference","cilantro","Mild");
    PreparationDto P(string id,string name,string station,int? guest,string state,string[]? actions=null,GuestRestrictionDto[]? restrictions=null,bool pending=false,PreparationReviewDto? review=null)
        =>new(id,name,station,1,guest,true,state,actions??[],restrictions??[],pending,review);
    var course=new CourseDto("p1","Aperitivos","Pending",null,null,null,null,[
        P("ostra-1","Ostra","cold",1,"Pending",["review-preparation"]),P("pan-1","Pan","pase",1,"Pending"),
        P("ostra-2","Ostra","cold",2,"Pending",null,[coriander],review:new("Unaffected","no lleva cilantro",DateTimeOffset.UnixEpoch)),
        P("ostra-3","Ostra","cold",3,"Pending",["review-preparation"],[shellfish],true),
        P("pan-3","Pan","pase",3,"Pending",["review-preparation"],[shellfish],review:new("Adapt","pan de maíz",DateTimeOffset.UnixEpoch)),
        P("pan-hot","Pan","hot",null,"Pending")]);
    var groups=DishGroups.Of(course);
    Expect(string.Join(" | ",groups.Select(g=>DishGroups.Title(g)+" ["+g.StationId+"]"))=="Ostra × 3 [cold] | Pan × 2 [pase] | Pan [hot]","groups: "+string.Join(" | ",groups.Select(DishGroups.Title)));
    Expect(string.Join(" | ",groups[0].Exceptions.Select(DishGroups.ExceptionLine))=="Ostra · Comensal 2 · ⚠ Preferencia cilantro (leve) · No le afecta: no lleva cilantro | Ostra · Comensal 3 · ⚠ ALERGIA marisco (grave) · Por revisar en cocina",
        "exception lines: "+string.Join(" | ",groups[0].Exceptions.Select(DishGroups.ExceptionLine)));
    Expect(DishGroups.ExceptionLine(course.Preparations[4])=="Pan · Comensal 3 · ⚠ ALERGIA marisco (grave) · Adaptado: pan de maíz","an adapted line keeps the dish name: "+DishGroups.ExceptionLine(course.Preparations[4]));
    Expect(!DishGroups.ExceptionAlarming(course.Preparations[2])&&DishGroups.ExceptionAlarming(course.Preparations[3])&&DishGroups.ExceptionAlarming(course.Preparations[4]),
        "red for a pending review or an alarming restriction; a reviewed mild preference is not");
    Expect(groups[0].Announcing("review-preparation").SequenceEqual(["ostra-1","ostra-3"])&&groups[2].Announcing("review-preparation").Length==0,"a group button carries only what announces the single action");
    Expect(DishGroups.State(groups[0])=="Sin enviar"&&!groups[0].AllReady,"state of the dish: "+DishGroups.State(groups[0]));
    var mixed=new CourseDto("p2","Mar","Preparing",null,null,null,null,[P("z-1","Zamburiña","hot",1,"Ready"),P("z-2","Zamburiña","hot",2,"Preparing"),P("z-3","Zamburiña","hot",3,"Ready")]);
    Expect(DishGroups.State(DishGroups.Of(mixed)[0])=="2 de 3 listas"&&DishGroups.Of(mixed with {Preparations=[..mixed.Preparations.Select(p=>p with {State="Ready"})]})[0].AllReady,
        "mixed states say how many are ready: "+DishGroups.State(DishGroups.Of(mixed)[0]));
    Expect(DishGroups.Of(course with {Preparations=[]}).Length==0,"a course without preparations has no dishes");
    return Task.CompletedTask;});
// C3b: tabla de casos COMUN del selector «Bodega y bebidas» y de la ronda. frontend/pwa/tests/drinks.test.ts recorre la MISMA contra
// search.ts, drinks.ts y round.ts: si CatalogSearch, DrinksPicker o Round cambian y la PWA no (o al reves), falla una de las dos.
// Un campo, una orden de ronda o un resultado que este lado no conoce es un fallo, nunca se salta.
using var drinkCases=JsonDocument.Parse(File.ReadAllText(Path.Combine(sharedCases,"drinks-cases.json")));
var drinksRoot=drinkCases.RootElement;
var drinkProducts=DrinksPicker.Products(drinksRoot.GetProperty("catalog").Deserialize<CatalogItem[]>(ApiClient.Json)!);
static string? TextOrNull(JsonElement value)=>value.ValueKind==JsonValueKind.Null?null:value.GetString();
static void Only(JsonElement row,params string[] fields){
    var unknown=row.EnumerateObject().Select(p=>p.Name).Where(name=>!fields.Contains(name)).ToArray();
    if(unknown.Length>0) throw new Exception("unknown field in the shared cases: "+string.Join(",",unknown));
}
static void Same(object? actual,JsonElement expected,string what){
    var got=JsonSerializer.SerializeToElement(actual,ApiClient.Json);
    if(JsonElement.DeepEquals(got,expected)) return;
    var readable=new JsonSerializerOptions(ApiClient.Json){Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping};   // solo el mensaje
    throw new Exception($"{what} = {JsonSerializer.Serialize(actual,readable)}, the shared case says {expected.GetRawText()}");
}
string[] Ids(IEnumerable<DrinkProduct>? products)=>products is null?null!:products.Select(p=>p.Id).ToArray();
RoundItem ItemOf(JsonElement key){
    var parts=key.GetString()!.Split('/');
    var product=drinkProducts.FirstOrDefault(p=>p.Id==parts[0]); var button=product?.Buttons.FirstOrDefault(b=>b.PresentationId==parts[1]);
    if(product is null||button is null) throw new Exception("item not in the shared catalogue: "+key.GetString());
    return DrinksPicker.Item(product,button);
}
var drinkSections=new[]{"about","catalog","fold","products","families","subfamilies","inFamily","glass","search","consumptions","atThisTable","round"};
await Check("drinks picker (shared cases): every section of the table is known here",()=>{ Only(drinksRoot,drinkSections); return Task.CompletedTask; });
foreach(var row in drinksRoot.GetProperty("fold").EnumerateArray())
    await Check("drinks picker (shared cases): fold «"+row.GetProperty("text").GetString()+"»",()=>{
        Only(row,"text","folded"); Expect(CatalogSearch.Fold(row.GetProperty("text").GetString())==row.GetProperty("folded").GetString(),"fold = "+CatalogSearch.Fold(row.GetProperty("text").GetString()));
        return Task.CompletedTask;});
await Check("drinks picker (shared cases): one row per product of the bodega section, A to Z, the glass first and «Añadir» if only one",()=>{
    var rows=drinksRoot.GetProperty("products").EnumerateArray().ToArray();
    Same(Ids(drinkProducts),JsonSerializer.SerializeToElement(rows.Select(r=>r.GetProperty("id").GetString()).ToArray()),"products");
    for(var i=0;i<rows.Length;i++)
    {
        Only(rows[i],"id","name","family","subfamily","detail","glass","buttons");
        var p=drinkProducts[i];
        Same(new{id=p.Id,name=p.Name,family=p.Family,subfamily=p.Subfamily,detail=p.Detail,glass=p.Glass,buttons=p.Buttons.Select(b=>new[]{b.PresentationId,b.Label})},rows[i],"product "+p.Id);
    }
    return Task.CompletedTask;});
await Check("drinks picker (shared cases): family chips, A to Z, with «Otros» last",()=>{
    Same(DrinksPicker.Families(drinkProducts).Select(f=>new{family=f.Family,label=f.Label,count=f.Count}),drinksRoot.GetProperty("families"),"families");
    return Task.CompletedTask;});
foreach(var row in drinksRoot.GetProperty("subfamilies").EnumerateArray())
    await Check("drinks picker (shared cases): subfamilies: "+row.GetProperty("case").GetString(),()=>{
        Only(row,"case","family","expect");
        Same(DrinksPicker.Subfamilies(drinkProducts,TextOrNull(row.GetProperty("family"))).Select(s=>new object[]{s.Name,s.Count}),row.GetProperty("expect"),"subfamilies");
        return Task.CompletedTask;});
foreach(var row in drinksRoot.GetProperty("inFamily").EnumerateArray())
    await Check("drinks picker (shared cases): in a family: "+row.GetProperty("case").GetString(),()=>{
        Only(row,"case","family","subfamily","expect");
        Same(Ids(DrinksPicker.InFamily(drinkProducts,TextOrNull(row.GetProperty("family")),TextOrNull(row.GetProperty("subfamily")))),row.GetProperty("expect"),"in family");
        return Task.CompletedTask;});
await Check("drinks picker (shared cases): «Por copa», the products the server marks with a glass",()=>{
    Same(Ids(DrinksPicker.ByGlass(drinkProducts)),drinksRoot.GetProperty("glass"),"by glass");
    return Task.CompletedTask;});
foreach(var row in drinksRoot.GetProperty("search").EnumerateArray())
    await Check("drinks picker (shared cases): search: "+row.GetProperty("case").GetString(),()=>{
        Only(row,"case","query","expect","details");
        var query=row.GetProperty("query").GetString();
        var found=DrinksPicker.Search(drinkProducts,query);
        Same(found is null?null:Ids(found),row.GetProperty("expect"),"search «"+query+"»");
        Expect(CatalogSearch.IsSearching(query)==(row.GetProperty("expect").ValueKind!=JsonValueKind.Null),"searching «"+query+"»");
        if(row.TryGetProperty("details",out var details)) Same(found!.Select(p=>DrinksPicker.Detail(p,query)),details,"details «"+query+"»");
        return Task.CompletedTask;});
await Check("drinks picker (shared cases): «En esta mesa», what the table already has, once, in order, if still sold in the bodega",()=>{
    var repeats=DrinksPicker.AtThisTable(drinksRoot.GetProperty("consumptions").Deserialize<ConsumptionDto[]>(ApiClient.Json)!,drinkProducts);
    foreach(var row in drinksRoot.GetProperty("atThisTable").EnumerateArray()) Only(row,"item","name","detail");
    Same(repeats.Select(r=>new{item=r.ProductId+"/"+r.PresentationId,name=r.Name,detail=r.Detail}),drinksRoot.GetProperty("atThisTable"),"at this table");
    var items=drinksRoot.GetProperty("atThisTable").EnumerateArray().Select(r=>ItemOf(r.GetProperty("item"))).ToArray();
    Expect(repeats.Select(r=>DrinksPicker.Item(r.Product,r.Button)).SequenceEqual(items),"a repeat adds the same item as its button");
    return Task.CompletedTask;});
var roundFields=new Dictionary<string,string[]>(StringComparer.Ordinal){
    ["add"]=["item","result"],["addMany"]=["count","result"],["increment"]=["line","result"],["decrement"]=["line","result"],["remove"]=["line","result"],
    ["quantity"]=["line","value","result"],["guest"]=["line","value","result"],["canSend"]=["locked","result"],["begin"]=["version","table","result"],
    ["settle"]=["kind","code","line"],["state"]=["lines","texts","units","summary","sendLabel","sending","conflict","problem","count","unitsOf"]};
var settleKinds=new Dictionary<string,RoundOutcome>(StringComparer.Ordinal){
    ["confirmed"]=RoundOutcome.Confirmed,["unconfirmed"]=RoundOutcome.Unconfirmed,["not_sent"]=RoundOutcome.NotSent,["rejected"]=RoundOutcome.Rejected};
static string Added(RoundAdd result)=>result switch{RoundAdd.Added=>"added",RoundAdd.More=>"more",RoundAdd.Full=>"full",RoundAdd.Max=>"max",RoundAdd.Sending=>"sending",_=>result.ToString()};
object? RoundState(Round round,string field)=>field switch{
    "lines"=>round.Lines.Select(l=>$"{l.ProductId}/{l.PresentationId} × {l.Quantity} · {l.GuestLabel}").ToArray(),
    "texts"=>round.Lines.Select(l=>l.Text).ToArray(),
    "units"=>round.Units,"summary"=>round.Summary,"sendLabel"=>round.SendLabel,"sending"=>round.Sending,"conflict"=>round.Conflict,
    "problem"=>round.Problem is null?null:new object?[]{round.Problem.Code,round.Problem.Line},
    "count"=>round.Lines.Count,
    _=>throw new Exception("unknown round state in the shared cases: "+field)};
foreach(var row in drinksRoot.GetProperty("round").EnumerateArray())
    await Check("round (shared cases): "+row.GetProperty("case").GetString(),()=>{
        Only(row,"case","steps");
        var round=new Round(); var index=0;
        foreach(var step in row.GetProperty("steps").EnumerateArray())
        {
            var op=step.GetProperty("op").GetString()!; var at=$"step {index++} ({op})";
            Expect(roundFields.TryGetValue(op,out var fields),"unknown round step in the shared cases: "+op);
            Only(step,["op",..fields!]);
            int Line()=>step.GetProperty("line").GetInt32();
            object? result=null;
            switch(op)
            {
                case "add": result=Added(round.Add(ItemOf(step.GetProperty("item")))); break;
                case "addMany":
                    for(var n=1;n<=step.GetProperty("count").GetInt32();n++) result=Added(round.Add(new RoundItem($"x{n}","unit",$"Producto {n}","Unidad")));
                    break;
                case "increment": result=round.Increment(Line()); break;
                case "decrement": result=round.Decrement(Line()); break;
                case "remove": result=round.Remove(Line()); break;
                // En el puesto la cantidad es un int: un valor no entero nunca llega a la ronda (el campo no lo admite).
                case "quantity": result=step.GetProperty("value").TryGetInt32(out var quantity)&&round.SetQuantity(Line(),quantity); break;
                case "guest": result=round.SetGuest(Line(),step.GetProperty("value").ValueKind==JsonValueKind.Null?null:step.GetProperty("value").GetInt32()); break;
                case "canSend": result=round.CanSend(step.GetProperty("locked").GetBoolean()); break;
                case "begin":
                    var sent=round.Begin(step.GetProperty("version").GetInt64(),step.GetProperty("table").GetString()!);
                    result=sent is null?null:new{body=sent.Body,description=sent.Description};
                    break;
                case "settle":
                    Expect(settleKinds.TryGetValue(step.GetProperty("kind").GetString()!,out var kind),"unknown settle kind in the shared cases: "+step.GetProperty("kind").GetString());
                    round.Settle(kind,step.TryGetProperty("code",out var code)?code.GetString():null,step.TryGetProperty("line",out var line)&&line.ValueKind!=JsonValueKind.Null?line.GetInt32():null);
                    break;
                case "state":
                    foreach(var field in step.EnumerateObject().Where(f=>f.Name!="op"))
                    {
                        if(field.Name!="unitsOf") { Same(RoundState(round,field.Name),field.Value,$"{at}: {field.Name}"); continue; }
                        foreach(var pair in field.Value.EnumerateArray())
                            Expect(round.UnitsOf(pair[0].GetString()!)==pair[1].GetInt32(),$"{at}: unitsOf {pair[0].GetString()} = {round.UnitsOf(pair[0].GetString()!)}");
                    }
                    break;
            }
            if(step.TryGetProperty("result",out var expected)) Same(result,expected,at);
        }
        return Task.CompletedTask;});
await Check("round: the limits are the server ones and the draft knows no store (never persisted, never a queue)",()=>{
    Expect(Round.MaxLines==30&&Round.MaxQuantity==99,"limits");
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
    var held=typeof(Round).GetFields(flags).Select(f=>f.FieldType).Concat(typeof(Round).GetProperties(flags).Select(p=>p.PropertyType)).ToArray();
    Expect(held.All(t=>!typeof(IPendingStore).IsAssignableFrom(t)&&!typeof(ApiClient).IsAssignableFrom(t)&&!typeof(Stream).IsAssignableFrom(t)&&!typeof(FileSystemInfo).IsAssignableFrom(t)),
        "the round holds a store, a client or a file: "+string.Join(",",held.Select(t=>t.Name)));
    Expect(typeof(Round).GetConstructors().All(c=>c.GetParameters().Length==0),"the round is built from nothing");
    return Task.CompletedTask;});
await Check("drinks picker: words of the picker and the round, without checkout words",()=>{
    var shown=new[]{Round.Send,Round.SendAgain,DrinksPicker.Add,DrinksPicker.OtherFamily,Labels.Rejection("use_add_dish"),Labels.Rejection("too_many_lines"),Labels.Rejection("duplicate_consumption")}
        .Concat(drinkProducts.SelectMany(p=>p.Buttons.Select(b=>b.Label).Append(p.Detail))).ToArray();
    var found=shown.Where(text=>System.Text.RegularExpressions.Regex.IsMatch(text,"€|precio|saldo|pago|cuenta",System.Text.RegularExpressions.RegexOptions.IgnoreCase)).ToArray();
    Expect(found.Length==0,"checkout words: "+string.Join(" | ",found));
    return Task.CompletedTask;});
Directory.CreateDirectory("artifacts/desktop");
await File.WriteAllTextAsync("artifacts/desktop/client-checks.json",JsonSerializer.Serialize(new {passed,failed,results},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Client checks: {passed} passed; {failed} failed");return failed==0?0:1;
sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send):HttpMessageHandler {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>send(request,ct);
}
// C3b: un almacen cuyo disco falla al guardar (lleno, sin permisos): la orden no se guarda.
sealed class FailingStore:IPendingStore {
    public bool Fail=true; public int Saved; public PendingCommand? Stored;
    public void Save(PendingCommand command){if(Fail) throw new IOException("disco lleno (simulado)");Saved++;Stored=command;}
    public PendingLoad Load()=>Stored is null?new(PendingOutcome.Absent,null,null,""):new(PendingOutcome.Restored,Stored,Stored.Key,"");
    public void Clear(string key){if(Stored?.Key==key)Stored=null;}
    public void Discard(){Stored=null;}
}
sealed class MemoryStore:IPendingStore {
    public PendingCommand? Stored; public PendingLoad? Forced; public bool Discarded; public string? LastSavedKey;
    public List<string> ClearedKeys=[];
    public void Save(PendingCommand command){Stored=command;LastSavedKey=command.Key;}
    public PendingLoad Load()=>Forced??(Stored is null?new(PendingOutcome.Absent,null,null,""):new(PendingOutcome.Restored,Stored,Stored.Key,""));
    public void Clear(string key){ClearedKeys.Add(key);if(Stored?.Key==key)Stored=null;}
    public void Discard(){Discarded=true;Forced=null;Stored=null;}
}
