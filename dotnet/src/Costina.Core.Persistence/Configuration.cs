namespace Costina.Core.Persistence;

// Configuracion del NUCLEO que sigue como fixture JSONB: el catalogo (productos), editable desde E3.
// La organizacion (salas, mesas, estaciones) es relacional desde E2: Costina.Core.Domain.Organization y OrganizationUnit.
public sealed record ProductDefinition(string Id,string Name,string Presentation,long PriceCents,bool Active);
