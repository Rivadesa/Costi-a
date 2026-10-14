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
foreach(var sample in new[]{"","corto","con espacio dentro","<img src=x onerror=1>","codigo/con/barras","código-con-acento",new string('a',129)})
    await Check("reject pairing code '"+sample[..Math.Min(sample.Length,24)]+"'",()=>Throws<ArgumentException>(()=>{PairingLink.Build(new Uri("https://costina-server.local:5443"),sample);return Task.CompletedTask;}));
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
    Assert(online.Online&&online.Active&&online.StateLabel=="Conectado"&&online.RoleLabel=="Comandero"&&online.ToString().EndsWith("aprobado por jefa",StringComparison.Ordinal));
    Assert(!older.Online&&!older.Active&&older.StateLabel.StartsWith("Quitado el ",StringComparison.Ordinal)&&older.RoleLabel=="Pantalla de cocina");
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
    "add-product","add-consumption","payment","void-charge","refund","close","reopen"];
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
    Expect(string.Join("|",new[]{"tasting","set-menu","a-la-carte"}.Select(Labels.OfferKind))=="Degustación|Menú cerrado|Carta","oferta");
    Expect(string.Join("|",new[]{"any","lunch","dinner"}.Select(Labels.OfferService))=="Comida y cena|Comida|Cena","servicio de la oferta");
    Expect(string.Join("|",new[]{"main","service","kitchen"}.Select(Labels.SessionRole))=="Responsable|Puesto de sala|Pantalla de cocina","rol");
    Expect(string.Join("|",new[]{"connected","reconnecting","closed"}.Select(Labels.Realtime))=="Tiempo real activo|Reconectando…|Sin tiempo real, reintentando","tiempo real");
    Expect(Labels.Guest(null)=="Toda la mesa"&&Labels.Guest(3)=="Comensal 3","comensal");
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
        "service_finished","not_found","duplicate_code","table_in_use","reserved_station","reserved_tariff","offer_unavailable","price_missing",
        "station_kind_mismatch","device_role_not_allowed","storage_conflict","operation_unconfirmed","request_failed"})
    {
        var text=Labels.Rejection(code);
        Expect(text!=code&&!text.Contains("(código",StringComparison.Ordinal)&&!text.Contains('_'),"rejection without a sentence: "+code+" -> "+text);
    }
    Expect(Labels.Rejection("brand_new_rule")=="El servidor rechazó la operación (código brand_new_rule).","unknown rejection: "+Labels.Rejection("brand_new_rule"));
    return Task.CompletedTask;});
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
Directory.CreateDirectory("artifacts/desktop");
await File.WriteAllTextAsync("artifacts/desktop/client-checks.json",JsonSerializer.Serialize(new {passed,failed,results},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Client checks: {passed} passed; {failed} failed");return failed==0?0:1;
sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send):HttpMessageHandler {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>send(request,ct);
}
sealed class MemoryStore:IPendingStore {
    public PendingCommand? Stored; public PendingLoad? Forced; public bool Discarded; public string? LastSavedKey;
    public List<string> ClearedKeys=[];
    public void Save(PendingCommand command){Stored=command;LastSavedKey=command.Key;}
    public PendingLoad Load()=>Forced??(Stored is null?new(PendingOutcome.Absent,null,null,""):new(PendingOutcome.Restored,Stored,Stored.Key,""));
    public void Clear(string key){ClearedKeys.Add(key);if(Stored?.Key==key)Stored=null;}
    public void Discard(){Discarded=true;Forced=null;Stored=null;}
}
