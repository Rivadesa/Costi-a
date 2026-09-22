namespace Costina.Core.Persistence;

// Configuracion del NUCLEO (organizacion y catalogo). Hoy fixtures en core.configuration; editables desde E2/E3.
public sealed record TableDefinition(string Id,string Name,int Capacity);
public sealed record ProductDefinition(string Id,string Name,string Presentation,long PriceCents,bool Active);
