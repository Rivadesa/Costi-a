using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Costina.Core.Domain;
using Costina.Core.Persistence;

namespace Costina.Server;

// E3: importacion del catalogo desde un fichero CSV (exportacion de Verial, de la tienda web o alta masiva a mano).
// Cabecera obligatoria; columnas reconocidas: code, reference, name, category, parent, presentation, price, tax, active.
// Separador ';' o ',' (se detecta en la cabecera), UTF-8 (con o sin BOM), campos entrecomillados admitidos.
// Nunca borra ni desactiva: crea lo que no existe (categorias, productos, presentaciones) y actualiza nombre, categoria,
// impuesto y referencia; el precio se fija en la tarifa GENERAL desde hoy solo si cambia. Todo en UNA transaccion e
// idempotente por la huella del fichero (misma clave de comando -> misma respuesta, sin volver a aplicar nada).
public sealed record ImportRow(int Line,string Code,string? Reference,string Name,string? Category,string? Parent,string Presentation,long PriceCents,string Tax,bool Active);
public sealed record ImportSummary(int Rows,int ProductsCreated,int ProductsUpdated,int CategoriesCreated,int PresentationsCreated,int PricesSet);

public static class CatalogImport
{
    public static string Fingerprint(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

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
                activeText is not ("0" or "false" or "no" or "n")));
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

    public static async Task<ImportSummary> Apply(Unit unit,IReadOnlyList<ImportRow> rows,DateOnly today)
    {
        var taxes=await unit.Taxes();
        var categories=(await unit.Categories()).ToDictionary(c=>c.Id,StringComparer.Ordinal);
        var products=(await unit.Products()).ToDictionary(p=>p.Id,StringComparer.Ordinal);
        int created=0,updated=0,newCategories=0,newPresentations=0,prices=0;
        async Task<string?> EnsureCategory(string? name,string? parentName,int line)
        {
            if(name is null) return null;
            var parentId=await EnsureCategory(parentName,null,line);
            var id=Catalog.Slug(name);
            if(!categories.ContainsKey(id))
            {
                var category=Catalog.Category(id,name,parentId,null,categories.Count);
                Catalog.Depth(id,parentId,x=>categories.GetValueOrDefault(x)?.ParentId);
                await unit.InsertCategory(category); categories[id]=category; newCategories++;
            }
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
                    var product=Catalog.Product(existing.Id,row.Name,categoryId??existing.CategoryId,tax.Id,row.Reference??existing.Reference,existing.Sort,existing.Active);
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
            }
            catch(Exception e) when(e is RuleViolation or ArgumentException or StoreConflict or StoreNotFound)
            {
                throw new ArgumentException($"Line {row.Line} ({row.Code}): {e.Message}");
            }
        }
        return new(rows.Count,created,updated,newCategories,newPresentations,prices);
    }
    private static decimal? RateOf(string text)
        => decimal.TryParse(text.Replace(',','.').TrimEnd('%').Trim(),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var rate) ? rate : null;
}
