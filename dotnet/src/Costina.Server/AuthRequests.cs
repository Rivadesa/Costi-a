namespace Costina.Server;

// Peticiones de IDENTIDAD (nucleo): sin Idempotency-Key ni orden incierta (D4.3b).
public sealed record LoginRequest(string Username,string Password);
// C3c: Typed=true pide el codigo CORTO para teclear (iPhone/iPad, desde el icono). Opcional: sin cuerpo o sin Typed, el largo.
public sealed record PairingIssueRequest(bool? Typed);
public sealed record PairingClaimRequest(string Code,string? DeviceName);
public sealed record PairingDecisionRequest(string? Role,string? Station);
public sealed record PairingCollectRequest(string PollSecret);
