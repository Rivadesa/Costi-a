using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Costina.Client;

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
foreach(var sample in new[]{("9,50",950L),("9.5",950L),("0,01",1L),("150",15000L)})
    await Check("exact money "+sample.Item1,()=>{Assert(Money.Parse(sample.Item1)==sample.Item2);return Task.CompletedTask;});
foreach(var sample in new[]{"0","-1","1.234","1,234.00","1e3","NaN","9,",",50"})
    await Check("reject money "+sample,()=>Throws<ArgumentException>(()=>{Money.Parse(sample);return Task.CompletedTask;}));
foreach(var sample in new[]{"http://127.0.0.1:5088","https://costina-server.local:5443","https://192.168.1.10:5443"})
    await Check("accept endpoint "+sample,()=>{ApiClient.ValidateEndpoint(new Uri(sample));return Task.CompletedTask;});
foreach(var sample in new[]{"http://example.com:5088","http://192.168.1.10:5088","http://costina-server.local:5088","https://user@costina-server.local:5443","https://costina-server.local:5443/path","https://costina-server.local:443","ftp://costina-server.local:5443","http://127.0.0.1:5088/path","http://user@127.0.0.1:5088","http://127.0.0.1:5088/?secret=1"})
    await Check("reject endpoint "+sample,()=>Throws<ArgumentException>(()=>{ApiClient.ValidateEndpoint(new Uri(sample));return Task.CompletedTask;}));
await Check("GET uses correct authenticated relative route",async()=>{
    using var client=Client(new Handler((r,_)=>{
        Assert(r.RequestUri!.AbsolutePath=="/api/native/v1/board"); Assert(r.Headers.Authorization?.Scheme=="Bearer");
        return Task.FromResult(Response(200,"[]"));
    })); Assert((await client.GetAsync<BoardEntry[]>("board")).Length==0);
});
await Check("503 retains command and identical replay",async()=>{
    var bodies=new List<string>();var keys=new List<string>();
    using var client=Client(new Handler(async (r,_)=>{
        bodies.Add(await r.Content!.ReadAsStringAsync());keys.Add(r.Headers.GetValues("Idempotency-Key").Single());
        return keys.Count==1?Response(503,"{\"error\":\"operation_unconfirmed\"}"):Response(200,"{\"version\":2}");
    }));
    await Throws<ApiError>(async()=>{await client.SendAsync("services/test/commands/start",new {expectedVersion=1},"Start");});
    Assert(client.Pending is not null);
    await Throws<InvalidOperationException>(async()=>{await client.SendAsync("services",new {pax=2},"Other");});
    await client.RetryAsync();Assert(client.Pending is null);Assert(bodies[0]==bodies[1]&&keys[0]==keys[1]&&keys.Count==2);
});
await Check("transport failure is not confirmation",async()=>{
    using var client=Client(new Handler((_,_)=>throw new HttpRequestException("simulated")));
    await Throws<HttpRequestException>(async()=>{await client.SendAsync("services",new {pax=2},"Open");});Assert(client.Pending is not null);
});
await Check("malformed successful response retains command",async()=>{
    using var client=Client(new Handler((_,_)=>Task.FromResult(Response(200,"{}"))));
    await Throws<JsonException>(async()=>{await client.SendAsync("services",new {pax=2},"Open");});Assert(client.Pending is not null);
});
await Check("version conflict identified by key resolves rejection without auto-resubmit",async()=>{
    using var client=Client(new Handler((r,_)=>Task.FromResult(Rejection(409,"{\"error\":\"version_conflict\"}",r))));
    await Throws<ApiError>(async()=>{await client.SendAsync("services/test/commands/start",new {expectedVersion=1},"Start");});Assert(client.Pending is null);
});
await Check("401 preserves uncertain mutation",async()=>{
    using var client=Client(new Handler((_,_)=>Task.FromResult(Response(401,"{}"))));
    await Throws<ApiError>(async()=>{await client.SendAsync("services",new {pax=2},"Open");});Assert(client.Pending is not null);
});
// D3.4 (F04): solo un rechazo definitivo reconocible, que identifica ESTE comando, cierra la incertidumbre.
await Check("409 without echoed key retains the command",async()=>{
    using var client=Client(new Handler((_,_)=>Task.FromResult(Response(409,"{\"error\":\"version_conflict\"}"))));
    await Throws<ApiError>(async()=>{await client.SendAsync("services/test/commands/start",new {expectedVersion=1},"Start");});Assert(client.Pending is not null);
});
await Check("409 echoing another key retains the command",async()=>{
    using var client=Client(new Handler((_,_)=>{var r=Response(409,"{\"error\":\"version_conflict\"}");r.Headers.Add("Idempotency-Key","otra");return Task.FromResult(r);}));
    await Throws<ApiError>(async()=>{await client.SendAsync("services/test/commands/start",new {expectedVersion=1},"Start");});Assert(client.Pending is not null);
});
await Check("transient storage_conflict retains the command",async()=>{
    using var client=Client(new Handler((r,_)=>Task.FromResult(Rejection(409,"{\"error\":\"storage_conflict\"}",r))));
    await Throws<ApiError>(async()=>{await client.SendAsync("services/test/commands/start",new {expectedVersion=1},"Start");});Assert(client.Pending is not null);
});
await Check("non-JSON 403 retains the command",async()=>{
    using var client=Client(new Handler((r,_)=>{var html=new HttpResponseMessage(HttpStatusCode.Forbidden){Content=new StringContent("<html>forbidden</html>",Encoding.UTF8,"text/html")};
        html.Headers.Add("Idempotency-Key",r.Headers.GetValues("Idempotency-Key").Single());return Task.FromResult(html);}));
    await Throws<ApiError>(async()=>{await client.SendAsync("services/test/commands/start",new {expectedVersion=1},"Start");});Assert(client.Pending is not null);
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
    await Throws<ApiError>(async()=>{await client.SendAsync("services/x/commands/start",new {expectedVersion=1},"Start");});
    Assert(store.Stored is not null&&store.Stored.Body==client.Pending!.Body&&store.Stored.Key==client.Pending.Key);
    await client.RetryAsync();Assert(store.Stored is null&&client.Pending is null&&store.ClearedKeys.Single()==store.LastSavedKey);
});
await Check("explicit rejection clears the durable command",async()=>{
    var store=new MemoryStore();
    using var client=Client(new Handler((r,_)=>Task.FromResult(Rejection(409,"{\"error\":\"version_conflict\"}",r))));
    client.AttachPendingStore(store);
    await Throws<ApiError>(async()=>{await client.SendAsync("services/x/commands/start",new {expectedVersion=1},"Start");});
    Assert(store.Stored is null&&client.Pending is null);
});
await Check("restored command from a previous process replays identical bytes and key",async()=>{
    var store=new MemoryStore{Stored=new PendingCommand("key-abc","services/x/commands/start","{\"expectedVersion\":7}","Start")};
    string? sentBody=null,sentKey=null;
    using var client=Client(new Handler(async(r,_)=>{
        sentBody=await r.Content!.ReadAsStringAsync();sentKey=r.Headers.GetValues("Idempotency-Key").Single();
        return Response(200,"{\"version\":8}");
    }));
    client.AttachPendingStore(store);
    Assert(client.Pending is {Key:"key-abc"});
    await Throws<InvalidOperationException>(async()=>{await client.SendAsync("services",new {pax=2},"Otra");});
    await client.RetryAsync();
    Assert(sentBody=="{\"expectedVersion\":7}"&&sentKey=="key-abc"&&store.Stored is null);
});
await Check("attaching a store with a live pending persists it",async()=>{
    using var client=Client(new Handler((_,_)=>Task.FromResult(Response(503,"{}"))));
    await Throws<ApiError>(async()=>{await client.SendAsync("services",new {pax=2},"Open");});
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
    await Throws<InvalidOperationException>(async()=>{await client.SendAsync("services",new {pax=2},"Open");});
    Assert(!store.Discarded);
    await Throws<InvalidOperationException>(()=>{client.DiscardBlocked();return Task.CompletedTask;});   // sin consultar no se descarta
    Assert(await client.ReconcileAsync()=="confirmed"&&client.Blocked is null&&store.Discarded&&store.ClearedKeys.Count==0);
});
await Check("unknown reconciliation keeps the block until an explicit discard that quarantines",async()=>{
    var store=new MemoryStore{Forced=new PendingLoad(PendingOutcome.Unreadable,null,"k10","dañado")};
    using var client=Client(new Handler((_,_)=>Task.FromResult(Response(200,"{\"key\":\"k10\",\"found\":false}"))));
    client.AttachPendingStore(store);
    Assert(await client.ReconcileAsync()=="unknown"&&client.Blocked is not null);
    await Throws<InvalidOperationException>(async()=>{await client.SendAsync("services",new {pax=2},"Open");});
    client.DiscardBlocked();
    Assert(client.Blocked is null&&store.Discarded);
});
await Check("unavailable durable file blocks and cannot be discarded",async()=>{
    var store=new MemoryStore{Forced=new PendingLoad(PendingOutcome.Unavailable,null,null,"bloqueado")};
    using var client=Client(new Handler((_,_)=>Task.FromResult(Response(200,"{\"version\":1}"))));
    client.AttachPendingStore(store);
    await Throws<InvalidOperationException>(async()=>{await client.SendAsync("services",new {pax=2},"Open");});
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
