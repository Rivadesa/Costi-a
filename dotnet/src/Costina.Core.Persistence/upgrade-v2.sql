-- E1b (ADR-012): esquema v1 (native_d1, monolitico: nucleo y Costiña juntos) -> v2 (core + un esquema por modulo).
-- Lo ejecuta "upgrade" (o "restore" tras restaurar una copia v1) UNA sola vez, dentro de la misma transaccion que
-- schema.sql y las concesiones: o queda entero o no queda nada. No borra ni reescribe ningun registro de negocio.
ALTER SCHEMA native_d1 RENAME TO core;
-- Tablas del modulo Dining a su esquema (los datos, indices y la clave foranea occupancies -> services viajan con ellas).
CREATE SCHEMA dining;
ALTER TABLE core.services SET SCHEMA dining;
ALTER TABLE core.occupancies SET SCHEMA dining;
-- La cuenta es del nucleo: deja de depender de la tabla del modulo y guarda la mesa en la que se abrio.
ALTER TABLE core.accounts DROP CONSTRAINT accounts_tenant_company_location_service_id_fkey;
ALTER TABLE core.accounts ADD COLUMN table_id text NOT NULL DEFAULT '';
UPDATE core.accounts a SET table_id = s.table_id FROM dining.services s
 WHERE s.tenant = a.tenant AND s.company = a.company AND s.location = a.location AND s.id = a.service_id;
-- Los menus por pases son configuracion del modulo Dining; mesas y productos siguen en el nucleo.
CREATE TABLE dining.configuration (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL,
 kind text NOT NULL CHECK (kind IN ('menu')), id text NOT NULL, payload jsonb NOT NULL,
 PRIMARY KEY (tenant,company,location,kind,id)
);
INSERT INTO dining.configuration SELECT tenant, company, location, kind, id, payload FROM core.configuration WHERE kind = 'menu';
DELETE FROM core.configuration WHERE kind = 'menu';
ALTER TABLE core.configuration DROP CONSTRAINT configuration_kind_check;
ALTER TABLE core.configuration ADD CONSTRAINT configuration_kind_check CHECK (kind IN ('table','product'));
-- Modulos activos por instalacion (NULL = todos los compilados).
ALTER TABLE core.installation ADD COLUMN modules text[] NULL;
ALTER TABLE core.schema_version DROP CONSTRAINT schema_version_version_check;
UPDATE core.schema_version SET version = 2;
ALTER TABLE core.schema_version ADD CONSTRAINT schema_version_version_check CHECK (version = 2);
