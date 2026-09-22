-- D1 laboratory schema only. Never changes the legacy public tables.
CREATE SCHEMA IF NOT EXISTS native_d1;
CREATE TABLE IF NOT EXISTS native_d1.schema_version (version integer PRIMARY KEY CHECK (version = 1));
INSERT INTO native_d1.schema_version VALUES (1) ON CONFLICT DO NOTHING;
CREATE TABLE IF NOT EXISTS native_d1.services (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 table_id text NOT NULL, state text NOT NULL CHECK (state IN ('Open','InService','Paused','Completed','Cancelled')),
 version bigint NOT NULL CHECK (version > 0), payload_version integer NOT NULL DEFAULT 1 CHECK (payload_version = 1),
 payload jsonb NOT NULL CHECK (jsonb_typeof(payload) = 'object'),
 PRIMARY KEY (tenant, company, location, id)
);
CREATE TABLE IF NOT EXISTS native_d1.occupancies (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 service_id text NOT NULL, table_id text NOT NULL,
 state text NOT NULL CHECK (state IN ('Occupied','Released')),
 version bigint NOT NULL CHECK (version > 0), payload_version integer NOT NULL DEFAULT 1 CHECK (payload_version = 1),
 payload jsonb NOT NULL CHECK (jsonb_typeof(payload) = 'object'),
 PRIMARY KEY (tenant, company, location, id),
 UNIQUE (tenant, company, location, service_id),
 FOREIGN KEY (tenant, company, location, service_id) REFERENCES native_d1.services (tenant, company, location, id)
);
CREATE UNIQUE INDEX IF NOT EXISTS one_active_occupancy_per_table
 ON native_d1.occupancies (tenant, company, location, table_id) WHERE state = 'Occupied';
CREATE TABLE IF NOT EXISTS native_d1.accounts (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 service_id text NOT NULL, state text NOT NULL CHECK (state IN ('Open','Closed')),
 version bigint NOT NULL CHECK (version > 0), payload_version integer NOT NULL DEFAULT 1 CHECK (payload_version = 1),
 payload jsonb NOT NULL CHECK (jsonb_typeof(payload) = 'object'),
 PRIMARY KEY (tenant, company, location, id),
 UNIQUE (tenant, company, location, service_id),
 FOREIGN KEY (tenant, company, location, service_id) REFERENCES native_d1.services (tenant, company, location, id)
);
CREATE TABLE IF NOT EXISTS native_d1.commands (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, actor text NOT NULL, key text NOT NULL,
 fingerprint text NOT NULL, response text NOT NULL, created_at timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY (tenant, company, location, actor, key)
);
CREATE TABLE IF NOT EXISTS native_d1.outbox (
 id uuid PRIMARY KEY, tenant text NOT NULL, company text NOT NULL, location text NOT NULL,
 aggregate_id text NOT NULL, type text NOT NULL, occurred_at timestamptz NOT NULL, payload jsonb NOT NULL,
 published_at timestamptz NULL
);
CREATE INDEX IF NOT EXISTS outbox_unpublished
 ON native_d1.outbox (tenant, company, location, occurred_at, id) WHERE published_at IS NULL;
CREATE TABLE IF NOT EXISTS native_d1.audit (
 id uuid PRIMARY KEY, event_id uuid NOT NULL UNIQUE REFERENCES native_d1.outbox(id),
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL,
 actor text NOT NULL, aggregate_id text NOT NULL, action text NOT NULL,
 command_key text NOT NULL, occurred_at timestamptz NOT NULL, payload jsonb NOT NULL
);
CREATE TABLE IF NOT EXISTS native_d1.configuration (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL,
 kind text NOT NULL CHECK (kind IN ('table','menu','product')), id text NOT NULL, payload jsonb NOT NULL,
 PRIMARY KEY (tenant,company,location,kind,id)
);
-- D3.4: identidad estable de la instalacion (una sola fila, creada una vez). El cliente separa por
-- ella su orden durable: un pendiente de otra instalacion en el mismo puerto nunca se reenvia.
CREATE TABLE IF NOT EXISTS native_d1.installation (
 id uuid PRIMARY KEY, created_at timestamptz NOT NULL DEFAULT now(),
 single boolean NOT NULL DEFAULT true UNIQUE CHECK (single)
);
INSERT INTO native_d1.installation (id) SELECT gen_random_uuid() WHERE NOT EXISTS (SELECT 1 FROM native_d1.installation);
-- D4.1 (#26, F06): identidad RELACIONAL, nunca JSONB. Usuarios con contrasena (PBKDF2, solo el
-- hash) y sesiones con token de corta vida (solo su hash SHA-256), renovacion deslizante,
-- caducidad absoluta y revocacion individual. Las claves de rol de laboratorio conviven hasta D4.3.
CREATE TABLE IF NOT EXISTS native_d1.users (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 username text NOT NULL CHECK (username = lower(username)),
 password_hash text NOT NULL,
 role text NOT NULL CHECK (role IN ('main','service','kitchen')),
 active boolean NOT NULL DEFAULT true,
 created_at timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY (tenant, company, location, id),
 UNIQUE (tenant, company, location, username)
);
CREATE TABLE IF NOT EXISTS native_d1.sessions (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 user_id text NOT NULL, token_hash text NOT NULL UNIQUE,
 issued_at timestamptz NOT NULL DEFAULT now(),
 expires_at timestamptz NOT NULL,
 absolute_expires_at timestamptz NOT NULL,
 revoked_at timestamptz NULL,
 PRIMARY KEY (tenant, company, location, id),
 FOREIGN KEY (tenant, company, location, user_id) REFERENCES native_d1.users (tenant, company, location, id)
);
CREATE INDEX IF NOT EXISTS sessions_live ON native_d1.sessions (token_hash) WHERE revoked_at IS NULL;
-- D4.2 (#26, F06): dispositivos como identidad SEPARADA del usuario. El emparejamiento nace de un
-- codigo de un solo uso y corta caducidad aprobado por un administrador con rol y estacion; del
-- secreto del dispositivo y de los codigos solo se guardan hashes. La estacion se aplica en D4.3.
CREATE TABLE IF NOT EXISTS native_d1.devices (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 name text NOT NULL, secret_hash text NOT NULL UNIQUE,
 role text NOT NULL CHECK (role IN ('main','service','kitchen')),
 station text NOT NULL,
 approved_by text NOT NULL, created_at timestamptz NOT NULL DEFAULT now(),
 revoked_at timestamptz NULL, revoked_by text NULL,
 PRIMARY KEY (tenant, company, location, id)
);
CREATE TABLE IF NOT EXISTS native_d1.pairings (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 code_hash text NOT NULL UNIQUE, created_by text NOT NULL,
 created_at timestamptz NOT NULL DEFAULT now(), expires_at timestamptz NOT NULL,
 status text NOT NULL DEFAULT 'issued' CHECK (status IN ('issued','claimed','approved','denied','consumed')),
 device_name text NULL, poll_secret_hash text NULL, claimed_at timestamptz NULL,
 approved_role text NULL, approved_station text NULL, decided_by text NULL,
 PRIMARY KEY (tenant, company, location, id)
);
