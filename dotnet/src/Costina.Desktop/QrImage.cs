using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;

namespace Costina.Desktop;

// D6.5: QR visual del enlace de emparejamiento. QRCoder (MIT) calcula la matriz con su
// correccion de errores — no es algo que convenga reimplementar — y PngByteQRCode la pinta sin System.Drawing.
// Nivel M y zona de silencio incluida: legible por la camara de un movil a la distancia de una pantalla.
public static class QrImage
{
    public static ImageSource Render(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        using var renderer = new PngByteQRCode(data);
        using var stream = new MemoryStream(renderer.GetGraphic(8));
        var image = new BitmapImage();
        image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit();
        image.Freeze();
        return image;
    }
}
