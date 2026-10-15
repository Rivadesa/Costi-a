using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Costina.Core.Domain;
using Costina.Core.Persistence;

namespace Costina.Server;

// E3: importacion del catalogo desde un fichero CSV (exportacion de Verial, de la tienda web o alta masiva a mano).
// Cabecera obligatoria; columnas reconocidas: code, reference, name, category, parent, presentation, price, tax, active, image (E3.2).
// Separador ';' o ',' (se detecta en la cabecera), UTF-8 (con o sin BOM), campos entrecomillados admitidos.
// Nunca borra ni desactiva: crea lo que no existe (categorias, productos, presentaciones) y actualiza nombre, categoria,
// impuesto y referencia; el precio se fija en la tarifa GENERAL desde hoy solo si cambia. Todo en UNA transaccion e
// idempotente por la huella del fichero (misma clave de comando -> misma respuesta, sin volver a aplicar nada).
// E3.2: columna opcional 'image' = nombre de un fichero JPEG, PNG o WebP DENTRO de la carpeta del CSV (puede ir en una subcarpeta;
// nunca una ruta absoluta, '..' ni un enlace). Se valida igual que una imagen subida (firma de bytes, 2 MB) y solo se fija si su
// huella difiere de la guardada; una celda vacia nunca quita la imagen que ya hay. El motor no sale a Internet: las fotos las
// descarga antes tools/catalog/woocommerce-to-csv.py --images. La huella de la importacion cubre el CSV Y cada imagen citada.
public sealed record ImportRow(int Line,string Code,string? Reference,string Name,string? Category,string? Parent,string Presentation,long PriceCents,string Tax,bool Active,string? Image=null);
// E3.3: Kept = productos existentes cuyo fichero traia otra familia o IVA y se dejaron como estaban en Costina.
public sealed record ImportSummary(int Rows,int ProductsCreated,int ProductsUpdated,int CategoriesCreated,int PresentationsCreated,int PricesSet,int ImagesSet=0,int Kept=0);
// Imagen de una fila ya validada: fichero dentro de la carpeta del CSV, su tipo por la firma y su huella. Los bytes no se guardan
// en memoria (un catalogo grande no cabe): se vuelven a leer en la transaccion solo si hay que fijarla, y deben seguir siendo los mismos.
public sealed record ImportImage(int Line,string Code,string Name,string Path,string ContentType,string Sha256);

public static class CatalogImport
{
    public static string Fingerprint(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));
    // Sin imagenes, la huella de siempre (un fichero anterior repite su misma clave). Con imagenes, la del CSV mas la de cada imagen
    // citada, en orden: una foto cambiada con el CSV igual es OTRA importacion, no una repeticion que "no cambia nada".
    public static string Fingerprint(byte[] content,IReadOnlyList<ImportImage> images) => images.Count==0 ? Fingerprint(content)
        : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Fingerprint(content)+"\n"+string.Join("\n",images.Select(i=>i.Line+":"+i.Sha256)))));

    // Tipo DECLARADO por la extension; el dominio exige despues que coincida con la firma de los bytes.
    private static string ImageTypeOf(string path) => System.IO.Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp",
        _ => throw new RuleViolation("image_invalid","the image must be a .jpg, .jpeg, .png or .webp file.")
    };

    // El nombre es RELATIVO a la carpeta del CSV y nunca sale de ella: ni ruta absoluta, ni unidad o flujo alternativo (':'),
    // ni '..', ni enlaces (fichero o carpeta intermedia). Un fichero de catalogo no puede hacer leer al motor nada de fuera.
    public static string ImagePath(string folder,string name)
    {
        var relative=name.Trim().Replace('\\','/');
        if(relative.Length is 0 or >255 || relative.Contains(':') || relative.StartsWith('/') || System.IO.Path.IsPathRooted(relative)
            || relative.Any(char.IsControl) || relative.Split('/').Any(s=>s==".."))
            throw new ArgumentException($"image '{name}' must be a file name inside the CSV's folder (no absolute path, drive or '..').");
        // La raiz de una unidad ("E:\", un CSV en una memoria USB) ya acaba en separador y no se recorta: no se le anade otro.
        var folderPath=System.IO.Path.GetFullPath(folder);
        var root=System.IO.Path.EndsInDirectorySeparator(folderPath) ? folderPath : folderPath+System.IO.Path.DirectorySeparatorChar;
        var full=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,relative.Replace('/',System.IO.Path.DirectorySeparatorChar)));
        var comparison=OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if(!full.StartsWith(root,comparison)) throw new ArgumentException($"image '{name}' is outside the CSV's folder.");
        if(!File.Exists(full)) throw new ArgumentException($"image file '{name}' was not found in the CSV's folder.");
        for(var current=full;current.Length>root.Length;current=System.IO.Path.GetDirectoryName(current)!)
            if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) throw new ArgumentException($"image '{name}' goes through a link: copy the file instead.");
        return full;
    }

    // Valida cada imagen citada ANTES de abrir la transaccion (un error dice su linea y no se aplica nada) y calcula su huella.
    public static List<ImportImage> Images(IReadOnlyList<ImportRow> rows,string folder)
    {
        var images=new List<ImportImage>();
        foreach(var row in rows.Where(r=>r.Image is not null))
        {
            try
            {
                var path=ImagePath(folder,row.Image!);
                var type=ImageTypeOf(path);
                Catalog.ImageSize(new FileInfo(path).Length);
                var image=Catalog.Image(row.Code,type,File.ReadAllBytes(path));
                images.Add(new(row.Line,row.Code,row.Image!,path,image.ContentType,image.Sha256));
            }
            catch(Exception e) when(e is RuleViolation or ArgumentException or IOException or UnauthorizedAccessException)
            {
                throw new ArgumentException($"Line {row.Line} ({row.Code}): {e.Message}");
            }
        }
        return images;
    }

    public static List<ImportRow> Parse(string text)
    {
        var lines=text.TrimStart('﻿').Split('\n').Select(l=>l.TrimEnd('\r')).ToList();
        if(lines.Count==0 || string.IsNullOrWhiteSpace(lines[0])) throw new ArgumentException("The file has no header line.");
        var separator=lines[0].Count(c=>c==';')>=lines[0].Count(c=>c==',') ? ';' : ',';
        var header=Split(lines[0],separator).Select(h=>h.Trim().ToLowerInvariant()).ToList();
        int Column(string name) => header.IndexOf(name);
        if(Column("name")<0 || Column("price")<0) throw new ArgumentException("The header must contain at least 'name' and 'price'.");
        var rows=new List<ImportRow>();
        for(var i=1;i<lines.Count;i++)
        {
            if(string.IsNullOrWhiteSpace(lines[i])) continue;
            var fields=Split(lines[i],separator);
            string? Field(string name) { var c=Column(name); return c>=0 && c<fields.Count ? fields[c].Trim() : null; }
            string? Optional(string name) => string.IsNullOrWhiteSpace(Field(name)) ? null : Field(name);
            var name=Optional("name") ?? throw new ArgumentException($"Line {i+1}: name is missing.");
            var reference=Optional("reference");
            var code=Optional("code") ?? Catalog.Slug(reference ?? name);
            var presentation=Optional("presentation") ?? "Unidad";
            var price=Optional("price") ?? throw new ArgumentException($"Line {i+1}: price is missing.");
            var activeText=(Optional("active") ?? "1").ToLowerInvariant();
            rows.Add(new(i+1,code,reference,name,Optional("category"),Optional("parent"),presentation,Cents(price,i+1),Optional("tax") ?? "10",
                activeText is not ("0" or "false" or "no" or "n"),Optional("image")));
        }
        return rows;
    }

    // "12,50", "12.50", "1.234,50" y "12" -> centimos.
    private static long Cents(string text,int line)
    {
        var t=text.Replace(" ","").Replace("€","");
        if(t.Contains(',') && t.Contains('.')) t=t.Replace(".","").Replace(',','.');
        else if(t.Contains(',')) t=t.Replace(',','.');
        if(!decimal.TryParse(t,NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var value) || value<0)
            throw new ArgumentException($"Line {line}: price '{text}' is not a valid amount.");
        return (long)decimal.Round(value*100,0,MidpointRounding.AwayFromZero);
    }

    private static List<string> Split(string line,char separator)
    {
        var fields=new List<string>(); var current=new StringBuilder(); var quoted=false;
        for(var i=0;i<line.Length;i++)
        {
            var c=line[i];
            if(quoted)
            {
                if(c=='"' && i+1<line.Length && line[i+1]=='"') { current.Append('"'); i++; }
                else if(c=='"') quoted=false;
                else current.Append(c);
            }
            else if(c=='"') quoted=true;
            else if(c==separator) { fields.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        fields.Add(current.ToString());
        return fields;
    }

    public static async Task<ImportSummary> Apply(Unit unit,IReadOnlyList<ImportRow> rows,DateOnly today,IReadOnlyList<ImportImage>? images=null)
    {
        var taxes=await unit.Taxes();
        var categories=(await unit.Categories()).ToDictionary(c=>c.Id,StringComparer.Ordinal);
        var products=(await unit.Products()).ToDictionary(p=>p.Id,StringComparer.Ordinal);
        var imageOf=(images ?? []).ToDictionary(i=>i.Line);
        int created=0,updated=0,newCategories=0,newPresentations=0,prices=0,newImages=0,kept=0;
        // E3.1: una categoria se reconoce por NOMBRE y PADRE, no solo por el nombre: en una bodega real la misma region cuelga
        // de varios tipos (Rioja en Tintos y en Blancos) y por nombre se mezclaban. Sin padre en el fichero, o para el propio padre
        // (cuyo abuelo no se conoce), vale cualquier categoria con ese nombre, la raiz primero. Una nueva toma el codigo del nombre
        // y, si ya esta usado, el del padre y el nombre ("blancos-rioja") o un sufijo.
        async Task<string?> EnsureCategory(string? name,string? parentName,int line)
        {
            if(name is null) return null;
            var parentId=parentName is null ? null : await EnsureCategory(parentName,null,line);
            var same=categories.Values.Where(c=>string.Equals(c.Name,name.Trim(),StringComparison.OrdinalIgnoreCase));
            var match=parentName is not null ? same.FirstOrDefault(c=>c.ParentId==parentId)
                : same.OrderBy(c=>c.ParentId is null ? 0 : 1).ThenBy(c=>c.Id,StringComparer.Ordinal).FirstOrDefault();
            if(match is not null) return match.Id;
            var id=Catalog.Slug(name);
            if(categories.ContainsKey(id) && parentName is not null) id=Catalog.Slug(parentName+" "+name);
            var stem=id.Length>Organization.MaxCodeLength-3 ? id[..(Organization.MaxCodeLength-3)].TrimEnd('-') : id;
            for(var n=2;categories.ContainsKey(id);n++) id=stem+"-"+n;
            var category=Catalog.Category(id,name,parentId,null,categories.Count);
            Catalog.Depth(id,parentId,x=>categories.GetValueOrDefault(x)?.ParentId);
            await unit.InsertCategory(category); categories[id]=category; newCategories++;
            return id;
        }
        foreach(var row in rows)
        {
            try
            {
                var tax=taxes.FirstOrDefault(t=>t.Id==row.Tax) ?? taxes.FirstOrDefault(t=>t.Active && t.Rate==RateOf(row.Tax))
                    ?? throw new ArgumentException($"tax '{row.Tax}' does not exist (use a tax code or a rate such as 10 or 21).");
                var categoryId=await EnsureCategory(row.Category,row.Parent,row.Line);
                if(products.TryGetValue(row.Code,out var existing))
                {
                    // E3.3: a un producto que ya existe solo se le actualizan nombre, referencia, precio e imagen: su familia, su IVA y su
                    // estacion son de Costina (lo que el promotor movio o corrigio no lo deshace la tienda). Se cuenta lo que traia distinto.
                    if((categoryId is not null && categoryId!=existing.CategoryId) || tax.Id!=existing.TaxId) kept++;
                    var product=Catalog.Product(existing.Id,row.Name,existing.CategoryId,existing.TaxId,row.Reference??existing.Reference,existing.Sort,existing.Active,existing.StationId);
                    if(product!=existing) { await unit.UpdateProduct(product); products[product.Id]=product; updated++; }
                }
                else
                {
                    var product=Catalog.Product(row.Code,row.Name,categoryId,tax.Id,row.Reference,products.Count,row.Active);
                    await unit.InsertProduct(product); products[product.Id]=product; created++;
                }
                var presentationId=row.Presentation.Equals("Unidad",StringComparison.OrdinalIgnoreCase) ? Catalog.DefaultPresentation : Catalog.Slug(row.Presentation);
                var presentations=await unit.Presentations(row.Code);
                if(presentations.All(p=>p.Id!=presentationId))
                {
                    await unit.InsertPresentation(Catalog.Presentation(row.Code,presentationId,row.Presentation,presentations.Count)); newPresentations++;
                }
                long? current=null;
                try { current=(await unit.PriceFor(Catalog.GeneralTariff,row.Code,presentationId,today)).Cents; } catch(RuleViolation) { }
                if(current!=row.PriceCents && await unit.SetPrice(Catalog.Price(Catalog.GeneralTariff,row.Code,presentationId,today,row.PriceCents))) prices++;
                // E3.2: la imagen solo se fija si su huella difiere de la guardada (activa o no: una imagen quitada a mano sigue quitada
                // mientras el fichero traiga la misma, como un producto desactivado a mano). Sin imagen en la fila no se toca nada.
                if(imageOf.TryGetValue(row.Line,out var file) && (await unit.ImageOf(row.Code))?.Sha256!=file.Sha256)
                {
                    var image=Catalog.Image(row.Code,file.ContentType,await File.ReadAllBytesAsync(file.Path));
                    if(image.Sha256!=file.Sha256) throw new ArgumentException($"image '{file.Name}' changed while importing; run the import again.");
                    if(await unit.SetImage(image)) newImages++;
                }
            }
            catch(Exception e) when(e is RuleViolation or ArgumentException or StoreConflict or StoreNotFound or IOException or UnauthorizedAccessException)
            {
                throw new ArgumentException($"Line {row.Line} ({row.Code}): {e.Message}");
            }
        }
        return new(rows.Count,created,updated,newCategories,newPresentations,prices,newImages,kept);
    }
    private static decimal? RateOf(string text)
        => decimal.TryParse(text.Replace(',','.').TrimEnd('%').Trim(),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var rate) ? rate : null;
}
