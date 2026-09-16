namespace Costina.Server;

// Endpoint metadata, not string matching the raw path, defines the authorization boundary.
public sealed record RouteAccess(params string[] Roles);
