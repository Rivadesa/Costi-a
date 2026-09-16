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
