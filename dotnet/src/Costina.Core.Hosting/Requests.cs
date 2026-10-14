using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Costina.Core.Domain;
using Costina.Core.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

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

    // Tope del cuerpo de una orden: el mismo que Kestrel aplica a todo el motor (Program.cs). Una ruta solo lo sube de forma
    // explicita y para una accion concreta (E3.2: la imagen de un articulo, CatalogOperations.ImageCommandBody).
    public const int MaxCommandBody = 32768;

    public static async Task<IResult> Write<T>(HttpContext context, PostgresStore store, BusinessScope scope,
        Func<Unit, ExecutionIdentity, T, Task<object>> work, int maxBody = MaxCommandBody)
    {
        // Kestrel rechaza un cuerpo mayor que su tope al empezar a leerlo; para esta peticion se ajusta ANTES de leer nada.
        if (maxBody != MaxCommandBody && context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize = maxBody;
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync(context.RequestAborted);
        if (body.Length > maxBody) throw new ArgumentException("Request too large.");
        var request = JsonSerializer.Deserialize<T>(body, Wire.Json);
        if (request is null) throw new ArgumentException("Empty command.");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(context.Request.Method + "\n" + context.Request.Path + "\n" + body)));
        var identity = Identity(context, scope);
        var response = await store.ExecuteAsync(identity, context.Request.Headers["Idempotency-Key"].ToString(), fingerprint,
            unit => work(unit, identity, request), context.RequestAborted);
        return Results.Text(response, "application/json");
    }
}
