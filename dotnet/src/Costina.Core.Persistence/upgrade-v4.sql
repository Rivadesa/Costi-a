-- E3 (Hito 6, ADR-012): esquema v3 -> v4. Catalogo y tarifas del nucleo como tablas relacionales: impuestos, categorias,
-- productos, presentaciones vendibles, tarifas y precios con vigencia. Los productos fixture de core.configuration
-- (kind='product') pasan a core.products con una presentacion 'unit' (su antigua presentacion) y su precio en la tarifa
-- 'general' desde 1970-01-01. core.configuration desaparece (los menus viven en dining.configuration desde E1b).
CREATE TABLE core.taxes (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 name text NOT NULL, rate numeric(5,2) NOT NULL CHECK (rate BETWEEN 0 AND 100), active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, id)
);
CREATE TABLE core.categories (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 name text NOT NULL, parent_id text NULL, color text NULL CHECK (color ~ '^#[0-9A-F]{6}$'),
 sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, id),
 FOREIGN KEY (tenant, company, location, parent_id) REFERENCES core.categories (tenant, company, location, id)
);
CREATE TABLE core.products (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 name text NOT NULL, category_id text NULL, tax_id text NOT NULL, reference text NULL,
 sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, id),
 FOREIGN KEY (tenant, company, location, category_id) REFERENCES core.categories (tenant, company, location, id),
 FOREIGN KEY (tenant, company, location, tax_id) REFERENCES core.taxes (tenant, company, location, id)
);
CREATE INDEX products_reference ON core.products (tenant, company, location, reference) WHERE reference IS NOT NULL;
CREATE TABLE core.presentations (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, product_id text NOT NULL, id text NOT NULL,
 name text NOT NULL, sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, product_id, id),
 FOREIGN KEY (tenant, company, location, product_id) REFERENCES core.products (tenant, company, location, id)
);
CREATE TABLE core.tariffs (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 name text NOT NULL, sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, id)
);
CREATE TABLE core.prices (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL,
 tariff_id text NOT NULL, product_id text NOT NULL, presentation_id text NOT NULL,
 valid_from date NOT NULL, price_cents bigint NOT NULL CHECK (price_cents BETWEEN 0 AND 99999999),
 PRIMARY KEY (tenant, company, location, tariff_id, product_id, presentation_id, valid_from),
 FOREIGN KEY (tenant, company, location, tariff_id) REFERENCES core.tariffs (tenant, company, location, id),
 FOREIGN KEY (tenant, company, location, product_id, presentation_id) REFERENCES core.presentations (tenant, company, location, product_id, id)
);
ALTER TABLE core.zones ADD COLUMN tariff_id text NULL;
ALTER TABLE core.zones ADD CONSTRAINT zones_tariff_fkey FOREIGN KEY (tenant, company, location, tariff_id) REFERENCES core.tariffs (tenant, company, location, id);
-- Ambitos conocidos (con organizacion, productos o usuarios): impuestos por defecto y tarifa general. El motor los garantiza
-- ademas para su propio ambito al terminar init/upgrade/restore (EnsureOrganizationAsync).
CREATE TEMP TABLE scopes_v4 AS
 SELECT DISTINCT tenant, company, location FROM core.zones
 UNION SELECT DISTINCT tenant, company, location FROM core.configuration
 UNION SELECT DISTINCT tenant, company, location FROM core.users;
INSERT INTO core.tariffs (tenant, company, location, id, name, sort) SELECT tenant, company, location, 'general', 'General', 0 FROM scopes_v4;
INSERT INTO core.taxes (tenant, company, location, id, name, rate)
 SELECT s.tenant, s.company, s.location, t.id, t.name, t.rate
 FROM scopes_v4 s CROSS JOIN (VALUES ('iva-10', 'IVA 10 %', 10.00), ('iva-21', 'IVA 21 %', 21.00), ('iva-4', 'IVA 4 %', 4.00), ('iva-0', 'Exento', 0.00)) AS t(id, name, rate);
-- Productos fixture: mismo codigo, IVA 10 %, presentacion 'unit' con el nombre de la antigua presentacion, precio en general.
INSERT INTO core.products (tenant, company, location, id, name, category_id, tax_id, reference, sort, active)
 SELECT tenant, company, location, id, coalesce(payload->>'name', id), NULL, 'iva-10', NULL,
        row_number() OVER (PARTITION BY tenant, company, location ORDER BY id) - 1, coalesce((payload->>'active')::boolean, true)
 FROM core.configuration WHERE kind = 'product';
INSERT INTO core.presentations (tenant, company, location, product_id, id, name, sort, active)
 SELECT tenant, company, location, id, 'unit', coalesce(nullif(payload->>'presentation', ''), 'Unidad'), 0, true
 FROM core.configuration WHERE kind = 'product';
INSERT INTO core.prices (tenant, company, location, tariff_id, product_id, presentation_id, valid_from, price_cents)
 SELECT tenant, company, location, 'general', id, 'unit', DATE '1970-01-01', least(greatest(coalesce((payload->>'priceCents')::bigint, 0), 0), 99999999)
 FROM core.configuration WHERE kind = 'product';
DROP TABLE core.configuration;
DROP TABLE scopes_v4;
ALTER TABLE core.schema_version DROP CONSTRAINT schema_version_version_check;
UPDATE core.schema_version SET version = 4;
ALTER TABLE core.schema_version ADD CONSTRAINT schema_version_version_check CHECK (version = 4);
