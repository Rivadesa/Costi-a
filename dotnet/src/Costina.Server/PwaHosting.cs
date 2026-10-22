using System.Text.RegularExpressions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace Costina.Server;

// D6.1 (#28): el motor aloja la PWA operativa en /app/, en el MISMO origen que la API: no hay CORS que
// abrir ni otro servidor que instalar. Solo se sirven ficheros estaticos del build (publicos por
// naturaleza: el codigo de la aplicacion); los DATOS siguen exigiendo identidad en /api. Cabeceras
// estrictas: sin scripts ni estilos en linea o de terceros, sin marcos, sin permisos de dispositivo.
public static partial class PwaHosting
{
    [GeneratedRegex(@"^[A-Za-z0-9.\-:\[\]]{1,255}$")] private static partial Regex SafeHost();
    // C3c: el enrutador /app/ y las dos aplicaciones, Costina Sala (/app/sala/) y Costina Cocina (/app/cocina/), cada una en su carpeta
    // del MISMO build, servidas por los mismos ficheros estaticos y con la MISMA CSP. Sin la barra final, el navegador resolveria mal
    // las rutas relativas, el alcance del service worker y el del manifiesto: se redirige a la carpeta, como siempre con /app.
    private static readonly string[] Entries=["/app","/app/sala","/app/cocina"];

    public static bool Map(WebApplication app)
    {
        var root=Path.Combine(AppContext.BaseDirectory,"wwwroot","app");
        if(!File.Exists(Path.Combine(root,"index.html"))) return false;
        var provider=new PhysicalFileProvider(root);
        var types=new FileExtensionContentTypeProvider();
        types.Mappings[".webmanifest"]="application/manifest+json";
        app.Use(async (context,next)=>
        {
            if(HttpMethods.IsGet(context.Request.Method)
                && Array.Find(Entries,e=>context.Request.Path.Equals(e,StringComparison.OrdinalIgnoreCase)) is { } entry)
            { context.Response.Redirect(entry+"/"); return; }
            await next();
        });
        app.UseDefaultFiles(new DefaultFilesOptions{FileProvider=provider,RequestPath="/app"});
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider=provider,RequestPath="/app",ContentTypeProvider=types,ServeUnknownFileTypes=false,
            OnPrepareResponse=prepared=>
            {
                var request=prepared.Context.Request; var headers=prepared.Context.Response.Headers;
                // Safari no siempre equipara 'self' con wss:// del mismo origen: se nombra de forma explicita.
                var host=request.Host.Value ?? "";
                var sockets=SafeHost().IsMatch(host) ? " wss://"+host+" ws://"+host : "";
                // C3b: img-src admite blob: SOLO para pintar las fotos de los articulos que la propia PWA ha pedido con su token
                // (GET /catalog/images, URL.createObjectURL). Nunca en script-src, sin data:, sin unsafe-inline y sin terceros.
                headers.ContentSecurityPolicy="default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self' blob:; font-src 'self'; "
                    +"manifest-src 'self'; worker-src 'self'; connect-src 'self'"+sockets+"; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";
                headers.XContentTypeOptions="nosniff";
                headers["Referrer-Policy"]="no-referrer";
                headers["Cross-Origin-Opener-Policy"]="same-origin";
                headers["Permissions-Policy"]="camera=(), microphone=(), geolocation=(), payment=()";
                if(request.IsHttps) headers.StrictTransportSecurity="max-age=31536000";
                // Los ficheros con hash en el nombre son inmutables; el resto (index, sw.js, manifiesto) se revalida siempre.
                headers.CacheControl=request.Path.StartsWithSegments("/app/assets") ? "public, max-age=31536000, immutable" : "no-cache";
            }
        });
        return true;
    }
}
