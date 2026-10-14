using System.Security.Cryptography;
namespace Costina.Client;

// E3.2: imagen vigente de un articulo tal como la sirve GET /catalog/images/{productId}: bytes, tipo y huella (sha256 en hexadecimal).
public sealed record ProductImage(string ProductId, string ContentType, byte[] Content, string Sha256);

// E3.2: las mismas reglas que el servidor (Catalog.Image), comprobadas ANTES de enviar y al recibir: JPEG, PNG o WebP reconocidos
// por su firma de bytes (nunca por la extension ni por lo que diga el fichero), hasta 2 MB; nunca SVG. El servidor vuelve a
// comprobarlo todo: esto solo evita mandar algo que se va a rechazar y no pintar algo que no es una imagen.
public static class ProductImages
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public const string Formats = "JPEG, PNG o WebP";

    // Tipo por la firma: JPEG FF D8 FF; PNG 89 50 4E 47 0D 0A 1A 0A; WebP "RIFF" <tamano> "WEBP". null = otra cosa.
    public static string? TypeOf(ReadOnlySpan<byte> content)
    {
        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF) return "image/jpeg";
        if (content.Length >= 8 && content[..8].SequenceEqual((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return "image/png";
        if (content.Length >= 12 && content[..4].SequenceEqual("RIFF"u8) && content.Slice(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }

    // Tamano de un fichero antes de leerlo: lo que pasa de 2 MB ni se lee.
    public static void CheckSize(long bytes)
    {
        if (bytes > MaxBytes)
            throw new ArgumentException($"La imagen ocupa {Megabytes(bytes)}; el máximo es 2 MB. Redúcela (por ejemplo a 1000 × 1000 píxeles) y vuelve a intentarlo.");
    }

    // Tipo de una imagen que se va a enviar, o ArgumentException con el motivo en castellano.
    public static string Check(byte[] content)
    {
        if (content.Length == 0) throw new ArgumentException("El fichero está vacío.");
        CheckSize(content.Length);
        return TypeOf(content) ?? throw new ArgumentException($"El fichero no es una imagen {Formats}. Otros formatos (SVG, GIF, BMP, AVIF…) no se admiten.");
    }

    public static string Sha256(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));
    // Hacia arriba con un decimal: 2 MB y un byte se dice "2,1 MB", nunca "2,0 MB" (que pareceria caber).
    public static string Megabytes(long bytes)
        => (Math.Ceiling(bytes / (1024m * 1024m) * 10m) / 10m).ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("es-ES")) + " MB";
}

// E3.2: bytes de imagenes ya leidas, por huella, en memoria y con tope (lo que no cabe expulsa a lo menos usado). Una huella
// identifica el contenido: si el catalogo trae la misma, no se vuelve a pedir. Seguro entre hilos (las miniaturas llegan en paralelo).
public sealed class ImageCache(long maxBytes = ImageCache.DefaultMaxBytes)
{
    public const long DefaultMaxBytes = 64L * 1024 * 1024;
    private readonly Dictionary<string, LinkedListNode<(string Sha, string Type, byte[] Content)>> index = new(StringComparer.Ordinal);
    private readonly LinkedList<(string Sha, string Type, byte[] Content)> order = new();   // primero = el mas reciente
    private readonly Lock gate = new();
    private long total;

    public int Count { get { lock (gate) return index.Count; } }
    public long Bytes { get { lock (gate) return total; } }

    public (string Type, byte[] Content)? Get(string sha)
    {
        lock (gate)
        {
            if (!index.TryGetValue(sha, out var node)) return null;
            order.Remove(node); order.AddFirst(node);
            return (node.Value.Type, node.Value.Content);
        }
    }

    public void Put(string sha, string type, byte[] content)
    {
        if (content.Length > maxBytes) return;
        lock (gate)
        {
            if (index.TryGetValue(sha, out var existing)) { order.Remove(existing); order.AddFirst(existing); return; }
            index[sha] = order.AddFirst((sha, type, content)); total += content.Length;
            while (total > maxBytes && order.Last is { } oldest)
            { order.RemoveLast(); index.Remove(oldest.Value.Sha); total -= oldest.Value.Content.Length; }
        }
    }

    public void Clear() { lock (gate) { index.Clear(); order.Clear(); total = 0; } }
}
