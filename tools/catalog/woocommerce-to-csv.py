"""Exporta el catalogo publico de una tienda WooCommerce (Store API, sin credenciales) al CSV que entiende
`Costina.Server import-catalog` (E3). Solo LEE la web; no escribe nada en ella.

Uso:  python tools/catalog/woocommerce-to-csv.py https://costinagourmet.com [--category 881] [--tax 21] [--out catalogo.csv]

Columnas: reference (SKU = codigo de articulo del ERP de origen), name, category, parent, presentation, price, tax, active.
La categoria es la hoja mas profunda del producto dentro de la raiz indicada (p. ej. "Rias Baixas") y parent su padre
("Blancos"); asi el importador reconstruye dos niveles del arbol. Formato (atributo "Formato", p. ej. "0.75 L") -> presentation
"Botella 0.75 L"; sin formato -> "Unidad". El precio es el de la tienda (con impuestos). Sin stock en la web -> active=0.
"""
import argparse
import csv
import json
import sys
import urllib.parse
import urllib.request


def fetch(base, path, params=None):
    url = base.rstrip('/') + '/wp-json/wc/store/v1' + path + ('?' + urllib.parse.urlencode(params) if params else '')
    with urllib.request.urlopen(urllib.request.Request(url, headers={'User-Agent': 'costina-catalog-export/1.0'}), timeout=60) as r:
        return json.loads(r.read().decode('utf8'))


def categories(base):
    result = {}
    page = 1
    while True:
        batch = fetch(base, '/products/categories', {'per_page': 100, 'page': page})
        if not batch: break
        for c in batch: result[c['id']] = c
        if len(batch) < 100: break
        page += 1
    return result


def under(cats, root):
    """Ids de las categorias que cuelgan de la raiz (incluida)."""
    ids = {root}
    changed = True
    while changed:
        changed = False
        for c in cats.values():
            if c['parent'] in ids and c['id'] not in ids: ids.add(c['id']); changed = True
    return ids


def products(base, category):
    page = 1
    while True:
        batch = fetch(base, '/products', {'per_page': 100, 'page': page, 'category': category})
        if not batch: return
        yield from batch
        if len(batch) < 100: return
        page += 1


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('base'); parser.add_argument('--category', type=int, default=None, help='id de la categoria raiz (todas si se omite)')
    parser.add_argument('--tax', default='21', help='tipo o codigo de impuesto para todas las filas (21 por defecto)')
    parser.add_argument('--out', default='catalogo.csv')
    args = parser.parse_args()
    cats = categories(args.base)
    allowed = under(cats, args.category) if args.category else set(cats)
    depth = {}
    def level(cid):
        if cid not in depth: depth[cid] = 0 if cats[cid]['parent'] in (0, None) or cats[cid]['parent'] not in cats else level(cats[cid]['parent']) + 1
        return depth[cid]
    seen = set(); rows = []
    for p in products(args.base, args.category) if args.category else products(args.base, ''):
        if p['id'] in seen: continue
        seen.add(p['id'])
        own = [c['id'] for c in p.get('categories', []) if c['id'] in allowed and c['id'] != args.category]
        leaf = max(own, key=level) if own else None
        name_of = lambda cid: cats[cid]['name'] if cid in cats else ''
        parent = cats[leaf]['parent'] if leaf else None
        fmt = next((a['terms'][0]['name'] for a in p.get('attributes', []) if a.get('name', '').lower() == 'formato' and a.get('terms')), None)
        price = int(p['prices']['price']) / (10 ** int(p['prices'].get('currency_minor_unit', 2)))
        rows.append({'reference': p.get('sku') or str(p['id']), 'name': p['name'], 'category': name_of(leaf) if leaf else '',
                     'parent': name_of(parent) if parent and parent != args.category else '',
                     'presentation': 'Botella ' + fmt if fmt else 'Unidad', 'price': f'{price:.2f}'.replace('.', ','),
                     'tax': args.tax, 'active': '1' if p.get('is_in_stock', True) else '0'})
    with open(args.out, 'w', newline='', encoding='utf8') as f:
        writer = csv.DictWriter(f, fieldnames=['reference', 'name', 'category', 'parent', 'presentation', 'price', 'tax', 'active'], delimiter=';')
        writer.writeheader(); writer.writerows(rows)
    print(f'{len(rows)} productos escritos en {args.out}')


if __name__ == '__main__':
    sys.exit(main())
