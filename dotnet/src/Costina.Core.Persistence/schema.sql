-- Esquema del NUCLEO, version 3 (E1b/E2, ADR-012): esquema "core" mas un esquema por modulo (schema.sql de cada modulo).
-- Idempotente sobre una base ya en v3. Una base anterior la transforman antes, encadenados y en la misma transaccion,
-- upgrade-v2.sql (native_d1 -> core + dining) y upgrade-v3.sql (organizacion relacional). Nunca toca las tablas de "public".
CREATE SCHEMA IF NOT EXISTS core;
CREATE TABLE IF NOT EXISTS core.schema_version (version integer PRIMARY KEY CHECK (version = 3));
INSERT INTO core.schema_version VALUES (3) ON CONFLICT DO NOTHING;
-- E2: ORGANIZACION editable (salas, mesas, estaciones): datos maestros relacionales; nunca se borran, se desactivan.
CREATE TABLE IF NOT EXISTS core.zones (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 name text NOT NULL, sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, id)
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
-- Configuracion del nucleo: catalogo (productos) hasta E3. Las mesas viven en core.tables (E2); los menus en dining.configuration.
CREATE TABLE IF NOT EXISTS core.configuration (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL,
 kind text NOT NULL CHECK (kind IN ('product')), id text NOT NULL, payload jsonb NOT NULL,
 PRIMARY KEY (tenant,company,location,kind,id)
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
