namespace Costina.Client;

// C3b (plan 2026-10-15, secciones 2 y 4.4): la RONDA de «Añadir consumicion», sin WPF. Espejo de frontend/pwa/src/round.ts: las dos
// implementaciones recorren la tabla de casos comun dotnet/tests/fixtures/drinks-cases.json (ClientChecks y vitest).
// Es un BORRADOR EN MEMORIA, nunca una cola: no conoce el almacen de la orden incierta (IPendingStore) ni ningun fichero, se ve como
// «Sin enviar» y cerrar el programa la pierde (D6.1: nunca se recupera como si estuviera enviada). «Enviar» es UNA sola orden
// add-consumptions con todas las lineas y la version de la ficha LEIDA (F01); mientras haya una orden incierta (D6.7) se puede llenar,
// pero no enviar. Reglas:
//  - un toque suma 1, para toda la mesa; el mismo producto y presentacion para el mismo comensal es la misma linea;
//  - cantidades de 1 a 99 y como mucho 30 lineas (los limites del servidor); el comensal es opcional (null = toda la mesa) y dos lineas
//    que quedan iguales se juntan si la suma no pasa de 99;
//  - mientras va la orden no se toca; al volver: confirmada, la ronda se vacia; sin confirmar, lo enviado ya ES la orden incierta (la
//    muestra su panel y se reintenta identica) y la ronda se vacia para no mandarlo dos veces; rechazada, la ronda sigue entera con el
//    codigo (y la linea, si el servidor la dice); con version_conflict se ofrece «La mesa ha cambiado: enviar de nuevo», que es otra
//    orden, con su clave y la version releida; no salida (no se pudo guardar u otra orden ocupaba el hueco), la ronda sigue igual.

// Lo que la ronda necesita de un boton: que se vende (producto y presentacion) y como se nombra.
public sealed record RoundItem(string ProductId, string PresentationId, string Name, string Presentation);
public sealed record RoundLine(string ProductId, string PresentationId, string Name, string Presentation, int Quantity, int? GuestPosition)
{
    public string Text => DrinksPicker.LineText(Name, PresentationId, Presentation);
    public string GuestLabel => Labels.Guest(GuestPosition);
}
public enum RoundAdd { Added, More, Full, Max, Sending }
public enum RoundOutcome { Confirmed, Unconfirmed, NotSent, Rejected }
public sealed record RoundProblem(string Code, int? Line);
// Cuerpo de add-consumptions (sin dinero: el servidor fija el importe con la tarifa de la sala) y descripcion de la orden pendiente.
public sealed record RoundLineBody(string ProductId, string PresentationId, int Quantity, int? GuestPosition);
public sealed record RoundBody(long ExpectedVersion, RoundLineBody[] Lines);
public sealed record RoundSend(RoundBody Body, string Description);

public sealed class Round
{
    public const int MaxLines = 30;
    public const int MaxQuantity = 99;
    public const string Send = "Enviar";
    public const string SendAgain = "La mesa ha cambiado: enviar de nuevo";

    private readonly List<RoundLine> lines = [];
    public IReadOnlyList<RoundLine> Lines => lines;
    public bool Sending { get; private set; }
    public bool Conflict { get; private set; }
    public RoundProblem? Problem { get; private set; }

    public int Units => lines.Sum(line => line.Quantity);
    public string Summary => Units == 0 ? "" : $"Ronda · {Units} sin enviar";
    public string SendLabel => Conflict ? SendAgain : Send;
    public int UnitsOf(string productId) => lines.Where(line => line.ProductId == productId).Sum(line => line.Quantity);

    public RoundAdd Add(RoundItem item)
    {
        if (Sending) return RoundAdd.Sending;
        var index = lines.FindIndex(l => l.ProductId == item.ProductId && l.PresentationId == item.PresentationId && l.GuestPosition is null);
        if (index >= 0)
        {
            if (lines[index].Quantity >= MaxQuantity) return RoundAdd.Max;
            lines[index] = lines[index] with { Quantity = lines[index].Quantity + 1 }; Touched(); return RoundAdd.More;
        }
        if (lines.Count >= MaxLines) return RoundAdd.Full;
        lines.Add(new(item.ProductId, item.PresentationId, item.Name, item.Presentation, 1, null));
        Touched(); return RoundAdd.Added;
    }

    public bool Increment(int index) => Editable(index) is { } line && SetQuantity(index, line.Quantity + 1);

    // En 1, quita la linea (el boton es la papelera).
    public bool Decrement(int index) => Editable(index) is { } line && (line.Quantity <= 1 ? Remove(index) : SetQuantity(index, line.Quantity - 1));

    public bool Remove(int index)
    {
        if (Editable(index) is null) return false;
        lines.RemoveAt(index); Touched(); return true;
    }

    public bool SetQuantity(int index, int quantity)
    {
        if (Editable(index) is not { } line || quantity is < 1 or > MaxQuantity) return false;
        lines[index] = line with { Quantity = quantity }; Touched(); return true;
    }

    // null = toda la mesa; un comensal es un numero desde 1 (que exista en la mesa lo comprueba el servidor: 422 con el indice de la linea).
    public bool SetGuest(int index, int? guestPosition)
    {
        if (Editable(index) is not { } line || guestPosition is < 1) return false;
        var changed = line with { GuestPosition = guestPosition };
        lines[index] = changed;
        var twin = -1;
        for (var i = 0; i < lines.Count && twin < 0; i++) if (i != index && Same(lines[i], changed)) twin = i;
        if (twin >= 0 && lines[twin].Quantity + changed.Quantity <= MaxQuantity)
        {
            lines[twin] = lines[twin] with { Quantity = lines[twin].Quantity + changed.Quantity };
            lines.RemoveAt(index);
        }
        Touched(); return true;
    }

    // locked: hay una orden incierta, una bloqueada o otra en curso (D6.7): se puede llenar, pero no enviar.
    public bool CanSend(bool locked) => !locked && !Sending && lines.Count > 0;

    // La orden de la ronda entera, con la version de la mesa LEIDA. Desde aqui hasta Settle la ronda no se toca.
    public RoundSend? Begin(long expectedVersion, string tableName)
    {
        if (Sending || lines.Count == 0) return null;
        Sending = true;
        var parts = lines.Select(l => $"{l.Quantity} × {l.Text}" + (l.GuestPosition is null ? "" : $" ({l.GuestLabel})"));
        return new(new(expectedVersion, lines.Select(l => new RoundLineBody(l.ProductId, l.PresentationId, l.Quantity, l.GuestPosition)).ToArray()),
            $"Ronda para {tableName}: {string.Join(", ", parts)}");
    }

    public void Settle(RoundOutcome outcome, string? code = null, int? line = null)
    {
        if (!Sending) return;
        Sending = false;
        switch (outcome)
        {
            case RoundOutcome.Confirmed or RoundOutcome.Unconfirmed: lines.Clear(); Conflict = false; Problem = null; break;
            case RoundOutcome.Rejected:
                var rejected = code ?? "request_failed";
                Conflict = rejected == "version_conflict"; Problem = new(rejected, line); break;
        }
    }

    public void Clear() { if (!Sending) { lines.Clear(); Touched(); } }

    private RoundLine? Editable(int index) => Sending || index < 0 || index >= lines.Count ? null : lines[index];
    private static bool Same(RoundLine a, RoundLine b) => a.ProductId == b.ProductId && a.PresentationId == b.PresentationId && a.GuestPosition == b.GuestPosition;
    // Cualquier cambio retira el aviso del ultimo rechazo: su indice de linea ya puede ser otro y la orden siguiente sera otra.
    private void Touched() { Conflict = false; Problem = null; }
}
