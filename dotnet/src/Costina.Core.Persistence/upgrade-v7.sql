-- E3.2 (Hito 6, ADR-012): esquema v6 -> v7. Imagen vigente de cada articulo del catalogo (una por producto), en la base:
-- entra sola en las copias y en la restauracion con su huella, como las demas tablas. Nunca se borra: quitarla la marca
-- inactiva y una nueva la sustituye. Solo JPEG, PNG y WebP de hasta 2 MB (lo valida el dominio por la firma de los bytes;
-- la tabla lo repite) y la huella siempre es la de su contenido. No mueve datos.
CREATE TABLE core.product_images (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, product_id text NOT NULL,
 content bytea NOT NULL CHECK (octet_length(content) BETWEEN 1 AND 2097152),
 content_type text NOT NULL CHECK (content_type IN ('image/jpeg','image/png','image/webp')),
 sha256 text NOT NULL CHECK (sha256 = encode(sha256(content), 'hex')),
 bytes integer NOT NULL CHECK (bytes = octet_length(content)),
 active boolean NOT NULL DEFAULT true,
 updated_at timestamptz NOT NULL DEFAULT now(), updated_by text NOT NULL,
 PRIMARY KEY (tenant, company, location, product_id),
 FOREIGN KEY (tenant, company, location, product_id) REFERENCES core.products (tenant, company, location, id)
);
-- Ya vienen comprimidas: PostgreSQL no intenta comprimirlas otra vez (se guardan fuera de linea tal cual).
ALTER TABLE core.product_images ALTER COLUMN content SET STORAGE EXTERNAL;
ALTER TABLE core.schema_version DROP CONSTRAINT schema_version_version_check;
UPDATE core.schema_version SET version = 7;
ALTER TABLE core.schema_version ADD CONSTRAINT schema_version_version_check CHECK (version = 7);
