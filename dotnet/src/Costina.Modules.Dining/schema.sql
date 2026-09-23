-- Esquema del modulo Dining (E1b, ADR-012): SOLO sus tablas. Lo aplica el nucleo tras el suyo, en la misma transaccion,
-- para todos los modulos compilados (activos o no: desactivar un modulo nunca hace desaparecer sus datos).
-- Idempotente. Una base v1 llega aqui ya transformada por upgrade-v2.sql (estas sentencias no hacen nada entonces).
CREATE SCHEMA IF NOT EXISTS dining;
CREATE TABLE IF NOT EXISTS dining.services (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 table_id text NOT NULL, state text NOT NULL CHECK (state IN ('Open','InService','Paused','Completed','Cancelled')),
 version bigint NOT NULL CHECK (version > 0), payload_version integer NOT NULL DEFAULT 1 CHECK (payload_version = 1),
 payload jsonb NOT NULL CHECK (jsonb_typeof(payload) = 'object'),
 PRIMARY KEY (tenant, company, location, id)
);
CREATE TABLE IF NOT EXISTS dining.occupancies (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 service_id text NOT NULL, table_id text NOT NULL,
 state text NOT NULL CHECK (state IN ('Occupied','Released')),
 version bigint NOT NULL CHECK (version > 0), payload_version integer NOT NULL DEFAULT 1 CHECK (payload_version = 1),
 payload jsonb NOT NULL CHECK (jsonb_typeof(payload) = 'object'),
 PRIMARY KEY (tenant, company, location, id),
 UNIQUE (tenant, company, location, service_id),
 FOREIGN KEY (tenant, company, location, service_id) REFERENCES dining.services (tenant, company, location, id)
);
CREATE UNIQUE INDEX IF NOT EXISTS one_active_occupancy_per_table
 ON dining.occupancies (tenant, company, location, table_id) WHERE state = 'Occupied';
-- Menus por pases: configuracion del modulo (mesas y productos son del nucleo: organizacion E2 y catalogo E3, relacionales).
CREATE TABLE IF NOT EXISTS dining.configuration (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL,
 kind text NOT NULL CHECK (kind IN ('menu')), id text NOT NULL, payload jsonb NOT NULL,
 PRIMARY KEY (tenant,company,location,kind,id)
);
