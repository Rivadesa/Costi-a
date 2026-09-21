namespace Costina.Client;

// D6.5: enlace que lleva el QR de emparejamiento. Lo lee la CAMARA del dispositivo (sin escaner propio) y abre
// la PWA en https://EQUIPO:PUERTO/app/#pair=CODIGO. El codigo viaja en el FRAGMENTO: el navegador nunca lo envia
// al servidor ni queda en sus logs (D6.1). Sin token, sin credenciales: solo el codigo de un uso y 5 minutos.
public static class PairingLink
{
    // Direccion con la que un DISPOSITIVO alcanza al servidor. Si este cliente ya habla por la LAN, es la suya;
    // conectado por loopback (mismo equipo que el servidor) no hay nombre que proponer: lo escribe el administrador.
    public static string SuggestDeviceAddress(Uri endpoint)
        => endpoint.IsAbsoluteUri && endpoint.Scheme == "https" ? endpoint.GetLeftPart(UriPartial.Authority) : "";

    // Misma regla que D5.3 (ApiClient.ValidateEndpoint), sin la excepcion de loopback: una tablet nunca llega a 127.0.0.1.
    public static Uri ParseDeviceAddress(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || !Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri))
            throw new ArgumentException("Escribe la dirección con la que los dispositivos llegan al servidor: https://NOMBRE:PUERTO.");
        ApiClient.ValidateEndpoint(uri);
        if (uri.Scheme != "https")
            throw new ArgumentException("Los dispositivos solo llegan al servidor por https://NOMBRE:PUERTO; 127.0.0.1 solo vale en el propio servidor.");
        return uri;
    }

    // Mismo alfabeto cerrado que acepta la PWA (pairing.ts): nada que no sea un codigo entra en una URL.
    public static bool IsCode(string? code)
        => code is { Length: >= 8 and <= 128 } && code.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');

    public static string Build(Uri deviceAddress, string code)
    {
        if (!IsCode(code)) throw new ArgumentException("Código de emparejamiento no válido.");
        return deviceAddress.GetLeftPart(UriPartial.Authority) + "/app/#pair=" + code;
    }
}
