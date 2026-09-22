namespace Costina.Server;

// Peticiones de IDENTIDAD (nucleo): sin Idempotency-Key ni orden incierta (D4.3b).
public sealed record LoginRequest(string Username,string Password);
public sealed record PairingClaimRequest(string Code,string? DeviceName);
public sealed record PairingDecisionRequest(string? Role,string? Station);
public sealed record PairingCollectRequest(string PollSecret);
