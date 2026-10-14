"""Exporta el catalogo publico de una tienda WooCommerce (Store API, sin credenciales) al CSV que entiende
`Costina.Server import-catalog` (E3). Solo LEE la web; no escribe nada en ella.

Uso:  python tools/catalog/woocommerce-to-csv.py https://costinagourmet.com [--category 881] [--tax 21] [--out catalogo.csv]
                                                  [--images [--images-dir NOMBRE] [--workers 4]]

Columnas: reference (SKU = codigo de articulo del ERP de origen), name, category, parent, presentation, price, tax, active
y, con --images, image.
La categoria es la hoja mas profunda del producto dentro de la raiz indicada (p. ej. "Rias Baixas") y parent el tipo del que
cuelga, la categoria inmediatamente bajo la raiz ("Blancos"); asi el importador reconstruye dos niveles del arbol y distingue la
misma region en tipos distintos (E3.1). Una hoja mas honda (Vinos Dulces > Portugal > Madeira) queda bajo su tipo (Vinos Dulces > Madeira). Formato (atributo "Formato", p. ej. "0.75 L") -> presentation
"Botella 0.75 L"; sin formato -> "Unidad". El precio es el de la tienda (con impuestos). Sin stock en la web -> active=0.

Imagenes (E3.2, --images): descarga la miniatura cuadrada de cada producto (la primera imagen, 'thumbnail' de la Store API:
450 x 450 en costinagourmet.com) a una carpeta JUNTO al CSV (por defecto <nombre del CSV>-imagenes/; --images-dir cambia el
nombre, siempre dentro de la carpeta del CSV) y escribe en la columna image su nombre relativo, p. ej.
"vinos-imagenes/4535.jpg". El motor no sale a Internet: import-catalog lee esos ficheros de la carpeta del CSV.
- Nombre del fichero: la referencia en minusculas con solo letras, cifras, '.', '_' y '-' (4535.jpg); si dos referencias dan el
  mismo nombre, la de menor id de producto se queda con el y las demas llevan "-<id>". La extension sale de la FIRMA de los
  bytes (.jpg, .png, .webp), no de la URL: import-catalog rechaza un fichero cuya extension no coincide con su contenido.
- Solo se guardan JPEG, PNG o WebP de hasta 2 MB (lo mismo que acepta el motor); cualquier otra cosa cuenta como fallo y el
  informe dice que es (en costinagourmet.com, 14-10-2026: 1.017 de las 1.021 fotos, 25 MB; fallan 3 AVIF con nombre .jpg y un
  BMP, que hay que volver a subir a la tienda en JPEG).
- Producto sin imagen o con fallo -> celda vacia (import-catalog no quita la imagen que ya tenga).
- Reanudable: un fichero que ya existe con contenido valido no se vuelve a descargar (se escribe primero en .part y se renombra
  al terminar, asi que nunca queda uno a medias). Para traer de nuevo una foto cambiada en la tienda, borrar su fichero.
- Educado con la tienda: User-Agent propio, como mucho 4 descargas a la vez (--workers 1 = una a una), 30 s de espera y un
  reintento. Solo URLs http(s). Al final dice cuantas se descargaron, cuantas ya estaban, los MB, la mayor y los fallos.
"""
import argparse
import concurrent.futures
import csv
import html
import http.client
import json
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

USER_AGENT = 'costina-catalog-export/1.1 (+lectura del catalogo publico para Costina)'
MAX_IMAGE_BYTES = 2 * 1024 * 1024   # el limite del motor (Catalog.MaxImageBytes)
EXTENSIONS = ('.jpg', '.png', '.webp')
RESERVED = {'con', 'prn', 'aux', 'nul'} | {f'{d}{n}' for d in ('com', 'lpt') for n in range(1, 10)}   # nombres prohibidos en Windows


def fetch(base, path, params=None):
    url = base.rstrip('/') + '/wp-json/wc/store/v1' + path + ('?' + urllib.parse.urlencode(params) if params else '')
    with urllib.request.urlopen(urllib.request.Request(url, headers={'User-Agent': USER_AGENT}), timeout=60) as r:
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


# --- Imagenes (E3.2) ---

def image_kind(data):
    """Extension por la firma de los bytes, igual que Catalog.ImageType del motor; None si no es JPEG, PNG ni WebP."""
    if data[:3] == b'\xff\xd8\xff': return '.jpg'
    if data[:8] == b'\x89PNG\r\n\x1a\n': return '.png'
    if len(data) >= 12 and data[:4] == b'RIFF' and data[8:12] == b'WEBP': return '.webp'
    return None


def other_kind(data):
    """Que es, para el informe de fallos, un fichero que no es JPEG, PNG ni WebP (en la tienda hay AVIF con nombre .jpg y un BMP)."""
    if data[4:8] == b'ftyp': return data[8:12].decode('ascii', 'replace').strip().upper() or 'ISO'   # AVIF, HEIC...
    for magic, kind in ((b'BM', 'BMP'), (b'GIF8', 'GIF'), (b'II*\x00', 'TIFF'), (b'MM\x00*', 'TIFF'), (b'<', 'texto/SVG/HTML')):
        if data.lstrip()[:len(magic)] == magic: return kind
    return 'desconocido' if data else 'vacio'


def slug(reference, product_id):
    """Nombre de fichero seguro desde la referencia: minusculas, letras, cifras, '.', '_' y '-'; nunca vacio ni reservado."""
    name = re.sub(r'[^a-z0-9._-]+', '-', reference.lower()).strip('.-_')[:80].rstrip('.-_')
    if not name or name.split('.')[0] in RESERVED: name = f'p{product_id}' + ('-' + name if name else '')
    return name


def thumbnail_url(product):
    """URL de la miniatura cuadrada de la primera imagen ('src' si no hay miniatura); None si no tiene imagen o no es http(s)."""
    images = product.get('images') or []
    url = (images[0].get('thumbnail') or images[0].get('src')) if images else None
    return url if url and urllib.parse.urlsplit(url).scheme in ('http', 'https') else None


class _HttpOnlyRedirects(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        if urllib.parse.urlsplit(newurl).scheme not in ('http', 'https'): raise urllib.error.URLError(f'redireccion no http(s): {newurl}')
        return super().redirect_request(req, fp, code, msg, headers, newurl)


_opener = urllib.request.build_opener(_HttpOnlyRedirects)


def download(url):
    """Bytes de la imagen (hasta 2 MB) con 30 s de espera y un reintento ante un fallo de red o un 5xx/429."""
    for attempt in (1, 2):
        try:
            with _opener.open(urllib.request.Request(url, headers={'User-Agent': USER_AGENT, 'Accept': 'image/*'}), timeout=30) as r:
                data = r.read(MAX_IMAGE_BYTES + 1)
            if len(data) > MAX_IMAGE_BYTES: raise ValueError('mayor de 2 MB')
            return data
        except urllib.error.HTTPError as e:
            if attempt == 2 or (e.code < 500 and e.code != 429): raise
        except (urllib.error.URLError, http.client.HTTPException, TimeoutError, ConnectionError):
            if attempt == 2: raise
        time.sleep(2)


def existing(folder, name):
    """Fichero ya descargado con ese nombre y contenido valido (cualquiera de las tres extensiones), o None."""
    for ext in EXTENSIONS:
        path = os.path.join(folder, name + ext)
        try:
            if os.path.isfile(path) and not os.path.islink(path) and 0 < os.path.getsize(path) <= MAX_IMAGE_BYTES:
                with open(path, 'rb') as f:
                    if image_kind(f.read(12)) == ext: return name + ext
        except OSError:
            pass
    return None


def fetch_image(folder, name, url):
    """(fichero, descargada) o lanza una excepcion con el motivo. Escribe en .part y renombra: nunca deja un fichero a medias."""
    found = existing(folder, name)
    if found: return found, False
    data = download(url)
    ext = image_kind(data)
    if not data or not ext: raise ValueError(f'no es JPEG, PNG ni WebP: {other_kind(data)}')
    part = os.path.join(folder, name + ext + '.part')
    with open(part, 'wb') as f: f.write(data)
    os.replace(part, os.path.join(folder, name + ext))
    return name + ext, True


def images(rows, out, folder_name, workers):
    """Descarga las miniaturas y rellena la columna image de cada fila. Devuelve los fallos [(referencia, motivo)]."""
    csv_folder = os.path.dirname(os.path.abspath(out))
    folder = os.path.abspath(os.path.join(csv_folder, folder_name))
    try: inside = os.path.commonpath([csv_folder, folder]) == csv_folder and folder != csv_folder
    except ValueError: inside = False   # otra unidad
    if not inside:
        raise SystemExit(f'--images-dir debe ser una carpeta DENTRO de la del CSV ({csv_folder})')
    os.makedirs(folder, exist_ok=True)
    relative = os.path.relpath(folder, csv_folder).replace(os.sep, '/')
    # Nombres estables entre ejecuciones (reanudar): ante una colision, el menor id de producto se queda con el nombre limpio.
    wanted = sorted((r for r in rows if r['_url']), key=lambda r: (slug(r['reference'], r['_id']), r['_id']))
    taken = set()
    for r in wanted:
        name = slug(r['reference'], r['_id'])
        while name in taken: name = f"{name}-{r['_id']}"
        r['_name'] = name; taken.add(name)
    failures, downloaded, reused = [], 0, 0
    with concurrent.futures.ThreadPoolExecutor(max_workers=max(1, min(4, workers))) as pool:
        jobs = {pool.submit(fetch_image, folder, r['_name'], r['_url']): r for r in wanted}
        for done, job in enumerate(concurrent.futures.as_completed(jobs), 1):
            r = jobs[job]
            try:
                file, new = job.result()
                r['image'] = relative + '/' + file
                downloaded += new; reused += not new
            except Exception as e:   # un fallo no para la exportacion: celda vacia y se informa al final
                failures.append((r['reference'], f"{e if isinstance(e, ValueError) else type(e).__name__ + ': ' + str(e)} ({r['_url']})"))
            if done % 100 == 0: print(f'  imagenes: {done}/{len(wanted)}', file=sys.stderr)
    files = [os.path.join(folder, r['image'].rsplit('/', 1)[1]) for r in rows if r.get('image')]
    sizes = sorted(((os.path.getsize(p), p) for p in files), reverse=True)
    total = sum(s for s, _ in sizes)
    print(f'{len(files)} imagenes en {folder} ({downloaded} descargadas, {reused} ya estaban; {total / 1048576:.1f} MB'
          + (f'; la mayor {os.path.basename(sizes[0][1])}, {sizes[0][0] / 1024:.0f} KB' if sizes else '') + ')')
    print(f'{sum(1 for r in rows if not r["_url"])} productos sin imagen en la tienda; {len(failures)} fallos')
    for reference, reason in sorted(failures): print(f'  FALLO {reference}: {reason}', file=sys.stderr)
    return failures


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('base'); parser.add_argument('--category', type=int, default=None, help='id de la categoria raiz (todas si se omite)')
    parser.add_argument('--tax', default='21', help='tipo o codigo de impuesto para todas las filas (21 por defecto)')
    parser.add_argument('--out', default='catalogo.csv')
    parser.add_argument('--images', action='store_true', help='descarga la miniatura de cada producto junto al CSV y rellena la columna image')
    parser.add_argument('--images-dir', default=None, help='carpeta de las imagenes dentro de la del CSV (<nombre del CSV>-imagenes por defecto)')
    parser.add_argument('--workers', type=int, default=4, help='descargas a la vez, de 1 a 4 (4 por defecto)')
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
        name_of = lambda cid: html.unescape(cats[cid]['name']) if cid in cats else ''   # la Store API entrega entidades HTML (&#8217;, &amp;)
        parent = cats[leaf]['parent'] if leaf else None
        while parent and parent in cats and cats[parent]['parent'] in allowed and cats[parent]['parent'] != args.category:
            parent = cats[parent]['parent']   # el tipo bajo la raiz, no un nivel intermedio (E3.1)
        fmt = next((a['terms'][0]['name'] for a in p.get('attributes', []) if a.get('name', '').lower() == 'formato' and a.get('terms')), None)
        price = int(p['prices']['price']) / (10 ** int(p['prices'].get('currency_minor_unit', 2)))
        rows.append({'reference': p.get('sku') or str(p['id']), 'name': html.unescape(p['name']), 'category': name_of(leaf) if leaf else '',
                     'parent': name_of(parent) if parent and parent != args.category else '',
                     'presentation': 'Botella ' + fmt if fmt else 'Unidad', 'price': f'{price:.2f}'.replace('.', ','),
                     'tax': args.tax, 'active': '1' if p.get('is_in_stock', True) else '0',
                     'image': '', '_id': p['id'], '_url': thumbnail_url(p)})
    fields = ['reference', 'name', 'category', 'parent', 'presentation', 'price', 'tax', 'active']
    if args.images:
        # Sin --images el CSV no cambia (misma cabecera y misma huella de importacion que antes de E3.2).
        images(rows, args.out, args.images_dir or os.path.splitext(os.path.basename(args.out))[0] + '-imagenes', args.workers)
        fields.append('image')
    with open(args.out, 'w', newline='', encoding='utf8') as f:
        writer = csv.DictWriter(f, fieldnames=fields, delimiter=';', extrasaction='ignore')
        writer.writeheader(); writer.writerows(rows)
    print(f'{len(rows)} productos escritos en {args.out}')


if __name__ == '__main__':
    sys.exit(main())
