using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Costina.Desktop;

// E3.2: fotos de los articulos en el puesto. Los bytes los pide ApiClient (con la credencial; una vez por huella); aqui se
// decodifican FUERA del hilo de la ventana, a la medida en que se pintan, y se congelan. Las miniaturas de la lista se guardan por
// huella; la foto grande de la ficha, solo las ultimas. Nunca bloquea la ventana: como mucho cuatro descargas a la vez y la ultima
// pedida va primero (lo que acaba de aparecer al desplazar la lista). Un fallo deja el hueco con su icono: la lista sigue.
public sealed class ImageLoader(Func<string, string, Task<byte[]?>> fetch, Dispatcher dispatcher)
{
    // Pixeles de decodificacion: el doble de lo pintado (32 y 140 px) para pantallas con escala.
    public const int ThumbPixels = 64, PhotoPixels = 280, MaxParallel = 4, MaxPhotos = 8, MaxThumbs = 4000;
    private readonly Dictionary<string, BitmapSource> thumbs = new(StringComparer.Ordinal);
    private readonly LinkedList<(string Sha, BitmapSource Image)> photos = new();
    private readonly LinkedList<(string ProductId, string Sha)> queue = new();   // primero = el ultimo pedido
    private readonly Dictionary<string, List<Action<BitmapSource?>>> waiting = new(StringComparer.Ordinal);
    private int running;

    // Solo desde el hilo de la ventana (todo el estado de aqui vive en el).
    public BitmapSource? Thumbnail(string sha) => thumbs.GetValueOrDefault(sha);
    public BitmapSource? Photo(string sha) => photos.FirstOrDefault(p => p.Sha == sha).Image;
    public int ThumbnailCount => thumbs.Count;
    public int Waiting => queue.Count + running;   // miniaturas en cola o descargandose

    // Miniatura de la lista: se encola (la mas reciente delante) y done llega en el hilo de la ventana (null = no se pudo).
    public void RequestThumbnail(string productId, string sha, Action<BitmapSource?> done)
    {
        if (thumbs.TryGetValue(sha, out var ready)) { done(ready); return; }
        if (waiting.TryGetValue(sha, out var others)) { others.Add(done); return; }
        waiting[sha] = [done];
        queue.AddFirst((productId, sha));
        Pump();
    }

    // Foto de la ficha: sin cola (es la que se esta mirando). Se llama y se espera en el hilo de la ventana; descarga y decodificacion
    // van fuera de el. Un fallo de red o de permisos llega como excepcion. Found = el servidor la tenia (Image null con Found: este
    // equipo no sabe decodificarla).
    public async Task<(BitmapSource? Image, bool Found)> LoadPhotoAsync(string productId, string sha)
    {
        if (Photo(sha) is { } cached) return (cached, true);
        var (image, found) = await Task.Run(async () =>
        {
            var content = await fetch(productId, sha);
            return (Decode(content, PhotoPixels), content is not null);
        });
        if (image is not null)
        {
            photos.AddFirst((sha, image));
            while (photos.Count > MaxPhotos) photos.RemoveLast();
        }
        return (image, found);
    }

    // Al desconectar o cambiar de servidor. Lo que ya estaba descargandose termina sin avisar a nadie.
    public void Clear() { thumbs.Clear(); photos.Clear(); queue.Clear(); waiting.Clear(); }

    private void Pump()
    {
        while (running < MaxParallel && queue.First is { } next)
        {
            queue.RemoveFirst(); running++;
            var (productId, sha) = next.Value;
            _ = Task.Run(async () =>
            {
                BitmapSource? image = null;
                try { image = Decode(await fetch(productId, sha), ThumbPixels); }
                catch (Exception e) when (e is not OutOfMemoryException) { image = null; }
                await dispatcher.InvokeAsync(() => Finish(sha, image));
            });
        }
    }

    private void Finish(string sha, BitmapSource? image)
    {
        running--;
        if (image is not null && thumbs.Count < MaxThumbs) thumbs[sha] = image;
        if (waiting.Remove(sha, out var callbacks)) foreach (var done in callbacks) done(image);
        Pump();
    }

    // Decodifica a la medida (lado mayor = pixels, sin agrandar) y congela: se puede pintar desde cualquier hilo. Lo que WIC no sabe
    // leer (por ejemplo WebP en un Windows 10 sin su extension) da null y la pantalla lo dice.
    public static BitmapSource? Decode(byte[]? content, int pixels)
    {
        if (content is null || content.Length == 0) return null;
        try
        {
            using var probe = new MemoryStream(content, writable: false);
            var frame = BitmapDecoder.Create(probe, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
            using var stream = new MemoryStream(content, writable: false);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            if (frame.PixelWidth >= frame.PixelHeight) image.DecodePixelWidth = Math.Min(pixels, frame.PixelWidth);
            else image.DecodePixelHeight = Math.Min(pixels, frame.PixelHeight);
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception e) when (e is NotSupportedException or FileFormatException or ArgumentException or InvalidOperationException or IOException
            or OverflowException or System.Runtime.InteropServices.COMException) { return null; }
    }
}
