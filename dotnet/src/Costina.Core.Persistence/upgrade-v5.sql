-- E4a (Hito 6, ADR-012): esquema v4 -> v5. OFERTA del nucleo: ofertas (degustacion / menu cerrado), pases y platos como
-- tablas relacionales. Los menus JSONB del modulo Dining (dining.configuration, kind='menu') pasan a: un producto-menu del
-- catalogo (categoria 'menus', IVA 10 %, presentacion 'person' con su precio en la tarifa general) y una oferta 'tasting'
-- con sus pases y platos (estacion de cada elaboracion). Como en upgrade-v2, el nucleo mueve datos de un modulo durante
-- una migracion (nunca en tiempo de ejecucion).
CREATE TABLE core.offers (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 name text NOT NULL, kind text NOT NULL CHECK (kind IN ('tasting','set-menu','a-la-carte')), product_id text NULL,
 service text NOT NULL DEFAULT 'any' CHECK (service IN ('any','lunch','dinner')),
 valid_from date NULL, valid_to date NULL, weekdays integer NOT NULL DEFAULT 127 CHECK (weekdays BETWEEN 1 AND 127),
 sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, id),
 FOREIGN KEY (tenant, company, location, product_id) REFERENCES core.products (tenant, company, location, id)
);
CREATE TABLE core.offer_courses (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, offer_id text NOT NULL, id text NOT NULL,
 name text NOT NULL, sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, offer_id, id),
 FOREIGN KEY (tenant, company, location, offer_id) REFERENCES core.offers (tenant, company, location, id)
);
CREATE TABLE core.offer_dishes (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, offer_id text NOT NULL, course_id text NOT NULL, id text NOT NULL,
 name text NOT NULL, station_id text NOT NULL, product_id text NULL,
 sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, offer_id, course_id, id),
 FOREIGN KEY (tenant, company, location, offer_id, course_id) REFERENCES core.offer_courses (tenant, company, location, offer_id, id),
 FOREIGN KEY (tenant, company, location, station_id) REFERENCES core.stations (tenant, company, location, id),
 FOREIGN KEY (tenant, company, location, product_id) REFERENCES core.products (tenant, company, location, id)
);
-- Menus JSONB del modulo -> catalogo + oferta. Estaciones que no existan se crean como cocina (nunca falla por datos antiguos).
CREATE TEMP TABLE menus_v5 AS
 SELECT tenant, company, location, id, payload FROM dining.configuration WHERE kind = 'menu';
INSERT INTO core.categories (tenant, company, location, id, name, parent_id, color, sort, active)
 SELECT DISTINCT tenant, company, location, 'menus', 'Menús', NULL, NULL, 99, true FROM menus_v5
 ON CONFLICT DO NOTHING;
INSERT INTO core.products (tenant, company, location, id, name, category_id, tax_id, reference, sort, active)
 SELECT tenant, company, location, 'menu-' || lower(id), coalesce(payload->>'name', id), 'menus', 'iva-10', NULL, 90, true FROM menus_v5
 ON CONFLICT DO NOTHING;
INSERT INTO core.presentations (tenant, company, location, product_id, id, name, sort, active)
 SELECT tenant, company, location, 'menu-' || lower(id), 'person', 'Por persona', 0, true FROM menus_v5
 ON CONFLICT DO NOTHING;
INSERT INTO core.prices (tenant, company, location, tariff_id, product_id, presentation_id, valid_from, price_cents)
 SELECT tenant, company, location, 'general', 'menu-' || lower(id), 'person', DATE '1970-01-01',
        least(greatest(coalesce((payload->>'unitPriceCents')::bigint, 0), 0), 99999999) FROM menus_v5
 ON CONFLICT DO NOTHING;
INSERT INTO core.stations (tenant, company, location, id, name, kind, sort)
 SELECT DISTINCT m.tenant, m.company, m.location, p->>'stationId', p->>'stationId', 'kitchen', 20
 FROM menus_v5 m, jsonb_array_elements(m.payload->'courses') c, jsonb_array_elements(c->'preparations') p
 WHERE p->>'stationId' ~ '^[A-Za-z0-9][A-Za-z0-9_-]{0,31}$'
 ON CONFLICT DO NOTHING;
INSERT INTO core.offers (tenant, company, location, id, name, kind, product_id, service, sort, active)
 SELECT tenant, company, location, id, coalesce(payload->>'name', id), 'tasting', 'menu-' || lower(id), 'any',
        row_number() OVER (PARTITION BY tenant, company, location ORDER BY id) - 1, true FROM menus_v5;
INSERT INTO core.offer_courses (tenant, company, location, offer_id, id, name, sort, active)
 SELECT m.tenant, m.company, m.location, m.id, c.value->>'id', coalesce(c.value->>'name', c.value->>'id'), c.ordinality - 1, true
 FROM menus_v5 m, jsonb_array_elements(m.payload->'courses') WITH ORDINALITY c;
INSERT INTO core.offer_dishes (tenant, company, location, offer_id, course_id, id, name, station_id, product_id, sort, active)
 SELECT m.tenant, m.company, m.location, m.id, c.value->>'id', p.value->>'id', coalesce(p.value->>'name', p.value->>'id'), p.value->>'stationId', NULL, p.ordinality - 1, true
 FROM menus_v5 m, jsonb_array_elements(m.payload->'courses') c, jsonb_array_elements(c.value->'preparations') WITH ORDINALITY p
 WHERE p.value->>'stationId' ~ '^[A-Za-z0-9][A-Za-z0-9_-]{0,31}$';
DELETE FROM dining.configuration WHERE kind = 'menu';
DROP TABLE menus_v5;
ALTER TABLE core.schema_version DROP CONSTRAINT schema_version_version_check;
UPDATE core.schema_version SET version = 5;
ALTER TABLE core.schema_version ADD CONSTRAINT schema_version_version_check CHECK (version = 5);
