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
foreach(var sample in new[]{("9,50",950L),("9.5",950L),("0,01",1L),("150",15000L)})
    await Check("exact money "+sample.Item1,()=>{Assert(Money.Parse(sample.Item1)==sample.Item2);return Task.CompletedTask;});
foreach(var sample in new[]{"0","-1","1.234","1,234.00","1e3","NaN","9,",",50"})
    await Check("reject money "+sample,()=>Throws<ArgumentException>(()=>{Money.Parse(sample);return Task.CompletedTask;}));
foreach(var sample in new[]{"http://example.com:5088","http://127.0.0.1:5088/path","http://user@127.0.0.1:5088","http://127.0.0.1:5088/?secret=1"})
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
await Check("version conflict resolves rejection without auto-resubmit",async()=>{
    using var client=Client(new Handler((_,_)=>Task.FromResult(Response(409,"{\"error\":\"version_conflict\"}"))));
    await Throws<ApiError>(async()=>{await client.SendAsync("services/test/commands/start",new {expectedVersion=1},"Start");});Assert(client.Pending is null);
});
await Check("401 preserves uncertain mutation",async()=>{
    using var client=Client(new Handler((_,_)=>Task.FromResult(Response(401,"{}"))));
    await Throws<ApiError>(async()=>{await client.SendAsync("services",new {pax=2},"Open");});Assert(client.Pending is not null);
});
await Check("operational DTO excludes financial fields",()=>{
    var types=new[]{typeof(DiningDto),typeof(CourseDto),typeof(PreparationDto),typeof(BoardEntry)};
    Assert(types.SelectMany(t=>t.GetProperties()).All(p=>!new[]{"Price","Balance","Payments","Account","TotalCents"}.Any(x=>p.Name.Contains(x,StringComparison.OrdinalIgnoreCase))));
    return Task.CompletedTask;
});
Directory.CreateDirectory("artifacts/desktop");
await File.WriteAllTextAsync("artifacts/desktop/client-checks.json",JsonSerializer.Serialize(new {passed,failed,results},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Client checks: {passed} passed; {failed} failed");return failed==0?0:1;
sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send):HttpMessageHandler {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>send(request,ct);
}
