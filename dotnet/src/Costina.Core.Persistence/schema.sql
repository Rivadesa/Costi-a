-- Esquema del NUCLEO, version 6 (E1b..E4b, ADR-012): esquema "core" mas un esquema por modulo (schema.sql de cada modulo).
-- Idempotente sobre una base ya en v6. Una base anterior la transforman antes, encadenados y en la misma transaccion,
-- upgrade-v2.sql (native_d1 -> core + dining), upgrade-v3.sql (organizacion), upgrade-v4.sql (catalogo y tarifas),
-- upgrade-v5.sql (oferta: menus como productos + ofertas con pases y platos) y upgrade-v6.sql (carta libre: items por
-- grupo y estacion del producto). Nunca toca las tablas de "public".
CREATE SCHEMA IF NOT EXISTS core;
CREATE TABLE IF NOT EXISTS core.schema_version (version integer PRIMARY KEY CHECK (version = 6));
INSERT INTO core.schema_version VALUES (6) ON CONFLICT DO NOTHING;
-- E3: CATALOGO Y TARIFAS (impuestos, categorias jerarquicas, productos, presentaciones vendibles, tarifas, precios con
-- vigencia): datos maestros relacionales; nunca se borran, se desactivan. Precios con impuestos incluidos (PVP).
CREATE TABLE IF NOT EXISTS core.taxes (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 name text NOT NULL, rate numeric(5,2) NOT NULL CHECK (rate BETWEEN 0 AND 100), active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, id)
);
CREATE TABLE IF NOT EXISTS core.categories (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 name text NOT NULL, parent_id text NULL, color text NULL CHECK (color ~ '^#[0-9A-F]{6}$'),
 sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, id),
 FOREIGN KEY (tenant, company, location, parent_id) REFERENCES core.categories (tenant, company, location, id)
);
CREATE TABLE IF NOT EXISTS core.products (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 name text NOT NULL, category_id text NULL, tax_id text NOT NULL, reference text NULL,
 sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, id),
 FOREIGN KEY (tenant, company, location, category_id) REFERENCES core.categories (tenant, company, location, id),
 FOREIGN KEY (tenant, company, location, tax_id) REFERENCES core.taxes (tenant, company, location, id)
);
CREATE INDEX IF NOT EXISTS products_reference ON core.products (tenant, company, location, reference) WHERE reference IS NOT NULL;
CREATE TABLE IF NOT EXISTS core.presentations (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, product_id text NOT NULL, id text NOT NULL,
 name text NOT NULL, sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, product_id, id),
 FOREIGN KEY (tenant, company, location, product_id) REFERENCES core.products (tenant, company, location, id)
);
CREATE TABLE IF NOT EXISTS core.tariffs (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 name text NOT NULL, sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, id)
);
CREATE TABLE IF NOT EXISTS core.prices (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL,
 tariff_id text NOT NULL, product_id text NOT NULL, presentation_id text NOT NULL,
 valid_from date NOT NULL, price_cents bigint NOT NULL CHECK (price_cents BETWEEN 0 AND 99999999),
 PRIMARY KEY (tenant, company, location, tariff_id, product_id, presentation_id, valid_from),
 FOREIGN KEY (tenant, company, location, tariff_id) REFERENCES core.tariffs (tenant, company, location, id),
 FOREIGN KEY (tenant, company, location, product_id, presentation_id) REFERENCES core.presentations (tenant, company, location, product_id, id)
);
-- E2: ORGANIZACION editable (salas, mesas, estaciones): datos maestros relacionales; nunca se borran, se desactivan.
CREATE TABLE IF NOT EXISTS core.zones (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 name text NOT NULL, sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 tariff_id text NULL,
 PRIMARY KEY (tenant, company, location, id),
 CONSTRAINT zones_tariff_fkey FOREIGN KEY (tenant, company, location, tariff_id) REFERENCES core.tariffs (tenant, company, location, id)
);
CREATE TABLE IF NOT EXISTS core.tables (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 name text NOT NULL, capacity integer NOT NULL CHECK (capacity BETWEEN 1 AND 60), zone_id text NOT NULL,
 sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, id),
 FOREIGN KEY (tenant, company, location, zone_id) REFERENCES core.zones (tenant, company, location, id)
);
CREATE TABLE IF NOT EXISTS core.stations (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 name text NOT NULL, kind text NOT NULL CHECK (kind IN ('kitchen','pass','bar','room')),
 sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, id)
);
-- E4a: OFERTA (lo que se vende en sala): degustaciones y menus cerrados vendidos como producto del catalogo (presentacion
-- 'person'), con pases y platos (estacion de cocina) que el modulo Dining ejecuta. Nunca se borra, se desactiva.
CREATE TABLE IF NOT EXISTS core.offers (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 name text NOT NULL, kind text NOT NULL CHECK (kind IN ('tasting','set-menu','a-la-carte')), product_id text NULL,
 service text NOT NULL DEFAULT 'any' CHECK (service IN ('any','lunch','dinner')),
 valid_from date NULL, valid_to date NULL, weekdays integer NOT NULL DEFAULT 127 CHECK (weekdays BETWEEN 1 AND 127),
 sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, id),
 FOREIGN KEY (tenant, company, location, product_id) REFERENCES core.products (tenant, company, location, id)
);
CREATE TABLE IF NOT EXISTS core.offer_courses (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, offer_id text NOT NULL, id text NOT NULL,
 name text NOT NULL, sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, offer_id, id),
 FOREIGN KEY (tenant, company, location, offer_id) REFERENCES core.offers (tenant, company, location, id)
);
CREATE TABLE IF NOT EXISTS core.offer_dishes (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, offer_id text NOT NULL, course_id text NOT NULL, id text NOT NULL,
 name text NOT NULL, station_id text NOT NULL, product_id text NULL,
 sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, offer_id, course_id, id),
 FOREIGN KEY (tenant, company, location, offer_id, course_id) REFERENCES core.offer_courses (tenant, company, location, offer_id, id),
 FOREIGN KEY (tenant, company, location, station_id) REFERENCES core.stations (tenant, company, location, id),
 FOREIGN KEY (tenant, company, location, product_id) REFERENCES core.products (tenant, company, location, id)
);
-- E4b: items de una carta libre (producto + presentacion del catalogo que se puede pedir en un grupo) y estacion de
-- cocina del producto (plato); un producto sin estacion es bebida o consumo directo.
CREATE TABLE IF NOT EXISTS core.offer_items (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, offer_id text NOT NULL, course_id text NOT NULL,
 product_id text NOT NULL, presentation_id text NOT NULL, sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, offer_id, course_id, product_id, presentation_id),
 FOREIGN KEY (tenant, company, location, offer_id, course_id) REFERENCES core.offer_courses (tenant, company, location, offer_id, id),
 FOREIGN KEY (tenant, company, location, product_id, presentation_id) REFERENCES core.presentations (tenant, company, location, product_id, id)
);
ALTER TABLE core.products ADD COLUMN IF NOT EXISTS station_id text NULL;
-- Cuenta (ventas): agregado del NUCLEO. service_id es la referencia OPACA que el modulo que la abrio le dio (nunca una
-- clave foranea a una tabla de modulo: el nucleo no depende de ningun modulo); table_id es la mesa (organizacion, nucleo)
-- en la que se abrio, o '' si el origen no tiene mesa. Con ella el puesto principal lista las cuentas sin leer al modulo.
CREATE TABLE IF NOT EXISTS core.accounts (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 service_id text NOT NULL, table_id text NOT NULL DEFAULT '',
 state text NOT NULL CHECK (state IN ('Open','Closed')),
 version bigint NOT NULL CHECK (version > 0), payload_version integer NOT NULL DEFAULT 1 CHECK (payload_version = 1),
 payload jsonb NOT NULL CHECK (jsonb_typeof(payload) = 'object'),
 PRIMARY KEY (tenant, company, location, id),
 UNIQUE (tenant, company, location, service_id)
);
CREATE TABLE IF NOT EXISTS core.commands (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, actor text NOT NULL, key text NOT NULL,
 fingerprint text NOT NULL, response text NOT NULL, created_at timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY (tenant, company, location, actor, key)
);
CREATE TABLE IF NOT EXISTS core.outbox (
 id uuid PRIMARY KEY, tenant text NOT NULL, company text NOT NULL, location text NOT NULL,
 aggregate_id text NOT NULL, type text NOT NULL, occurred_at timestamptz NOT NULL, payload jsonb NOT NULL,
 published_at timestamptz NULL
);
CREATE INDEX IF NOT EXISTS outbox_unpublished
 ON core.outbox (tenant, company, location, occurred_at, id) WHERE published_at IS NULL;
CREATE TABLE IF NOT EXISTS core.audit (
 id uuid PRIMARY KEY, event_id uuid NOT NULL UNIQUE REFERENCES core.outbox(id),
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL,
 actor text NOT NULL, aggregate_id text NOT NULL, action text NOT NULL,
 command_key text NOT NULL, occurred_at timestamptz NOT NULL, payload jsonb NOT NULL
);
-- D3.4: identidad estable de la instalacion (una sola fila, creada una vez). El cliente separa por
-- ella su orden durable: un pendiente de otra instalacion en el mismo puerto nunca se reenvia.
-- E1b: modules = modulos ACTIVOS en esta instalacion; NULL = todos los compilados en el binario.
CREATE TABLE IF NOT EXISTS core.installation (
 id uuid PRIMARY KEY, created_at timestamptz NOT NULL DEFAULT now(),
 single boolean NOT NULL DEFAULT true UNIQUE CHECK (single),
 modules text[] NULL
);
INSERT INTO core.installation (id) SELECT gen_random_uuid() WHERE NOT EXISTS (SELECT 1 FROM core.installation);
-- D4.1 (#26, F06): identidad RELACIONAL, nunca JSONB. Usuarios con contrasena (PBKDF2, solo el
-- hash) y sesiones con token de corta vida (solo su hash SHA-256), renovacion deslizante,
-- caducidad absoluta y revocacion individual.
CREATE TABLE IF NOT EXISTS core.users (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 username text NOT NULL CHECK (username = lower(username)),
 password_hash text NOT NULL,
 role text NOT NULL CHECK (role IN ('main','service','kitchen')),
 active boolean NOT NULL DEFAULT true,
 created_at timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY (tenant, company, location, id),
 UNIQUE (tenant, company, location, username)
);
CREATE TABLE IF NOT EXISTS core.sessions (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 user_id text NOT NULL, token_hash text NOT NULL UNIQUE,
 issued_at timestamptz NOT NULL DEFAULT now(),
 expires_at timestamptz NOT NULL,
 absolute_expires_at timestamptz NOT NULL,
 revoked_at timestamptz NULL,
 PRIMARY KEY (tenant, company, location, id),
 FOREIGN KEY (tenant, company, location, user_id) REFERENCES core.users (tenant, company, location, id)
);
CREATE INDEX IF NOT EXISTS sessions_live ON core.sessions (token_hash) WHERE revoked_at IS NULL;
-- D4.2 (#26, F06): dispositivos como identidad SEPARADA del usuario. El emparejamiento nace de un
-- codigo de un solo uso y corta caducidad aprobado por un administrador con rol y estacion; del
-- secreto del dispositivo y de los codigos solo se guardan hashes. La estacion se aplica en D4.3.
CREATE TABLE IF NOT EXISTS core.devices (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 name text NOT NULL, secret_hash text NOT NULL UNIQUE,
 role text NOT NULL CHECK (role IN ('main','service','kitchen')),
 station text NOT NULL,
 approved_by text NOT NULL, created_at timestamptz NOT NULL DEFAULT now(),
 revoked_at timestamptz NULL, revoked_by text NULL,
 PRIMARY KEY (tenant, company, location, id)
);
CREATE TABLE IF NOT EXISTS core.pairings (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 code_hash text NOT NULL UNIQUE, created_by text NOT NULL,
 created_at timestamptz NOT NULL DEFAULT now(), expires_at timestamptz NOT NULL,
 status text NOT NULL DEFAULT 'issued' CHECK (status IN ('issued','claimed','approved','denied','consumed')),
 device_name text NULL, poll_secret_hash text NULL, claimed_at timestamptz NULL,
 approved_role text NULL, approved_station text NULL, decided_by text NULL,
 PRIMARY KEY (tenant, company, location, id)
);
