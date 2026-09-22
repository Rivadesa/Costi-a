using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Costina.Core.Domain;
using Costina.Core.Persistence;
using Microsoft.AspNetCore.Http;

namespace Costina.Core.Hosting;

// Endpoint metadata, not string matching the raw path, defines the authorization boundary.
// ActionPolicy (ADR-012): para rutas con {action}, el modulo declara que acciones admite cada rol; el middleware
// del nucleo lo aplica igual para todos. Debe reflejar exactamente las affordances que el modulo anuncia.
public sealed record RouteAccess(params string[] Roles)
{
    public Func<string, string, bool>? ActionPolicy { get; init; }
}

// Tuberia comun de peticiones del nucleo y de los modulos: identidad del actor, lectura JSON y escritura
// idempotente (Idempotency-Key + huella del cuerpo + registro del comando en la misma transaccion, D3.4/ADR-004).
public static class Requests
{
    public static string Role(HttpContext c) => (string)c.Items["role"]!;
    public static string? Station(HttpContext c) => c.Items["station"] as string;
    public static ExecutionIdentity Identity(HttpContext c, BusinessScope scope) => new(scope, (string)c.Items["actor"]!, Station(c));
    public static IResult Json(object value) => Results.Text(Wire.Encode(value), "application/json");

    public static async Task<IResult> Write<T>(HttpContext context, PostgresStore store, BusinessScope scope,
        Func<Unit, ExecutionIdentity, T, Task<object>> work)
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync(context.RequestAborted);
        if (body.Length > 32768) throw new ArgumentException("Request too large.");
        var request = JsonSerializer.Deserialize<T>(body, Wire.Json);
        if (request is null) throw new ArgumentException("Empty command.");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(context.Request.Method + "\n" + context.Request.Path + "\n" + body)));
        var identity = Identity(context, scope);
        var response = await store.ExecuteAsync(identity, context.Request.Headers["Idempotency-Key"].ToString(), fingerprint,
            unit => work(unit, identity, request), context.RequestAborted);
        return Results.Text(response, "application/json");
    }
}
