-- E2 (Hito 6, ADR-012): esquema v2 -> v3. Organizacion editable del nucleo: salas (zonas), mesas y estaciones como
-- tablas relacionales. Las mesas fixture de core.configuration (kind='table') pasan a core.tables dentro de la sala
-- 'sala'. Las estaciones se deducen de datos del NUCLEO: puestos emparejados (core.devices) y, si la instalacion es de
-- demostracion (marca lab-fixtures-v1 en core.commands), las de los fixtures. El nucleo no lee ninguna tabla de modulo.
CREATE TABLE core.zones (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 name text NOT NULL, sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, id)
);
CREATE TABLE core.tables (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 name text NOT NULL, capacity integer NOT NULL CHECK (capacity BETWEEN 1 AND 60), zone_id text NOT NULL,
 sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, id),
 FOREIGN KEY (tenant, company, location, zone_id) REFERENCES core.zones (tenant, company, location, id)
);
CREATE TABLE core.stations (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, id text NOT NULL,
 name text NOT NULL, kind text NOT NULL CHECK (kind IN ('kitchen','pass','bar','room')),
 sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, id)
);
-- Mesas existentes: una sala por ambito que tenga mesas, y las mesas con su aforo en el orden en que estaban.
INSERT INTO core.zones (tenant, company, location, id, name, sort)
 SELECT DISTINCT tenant, company, location, 'sala', 'Sala', 0 FROM core.configuration WHERE kind = 'table';
INSERT INTO core.tables (tenant, company, location, id, name, capacity, zone_id, sort)
 SELECT tenant, company, location, id, coalesce(payload->>'name', id), least(greatest(coalesce((payload->>'capacity')::integer, 4), 1), 60), 'sala',
        row_number() OVER (PARTITION BY tenant, company, location ORDER BY length(id), id) - 1
 FROM core.configuration WHERE kind = 'table';
DELETE FROM core.configuration WHERE kind = 'table';
ALTER TABLE core.configuration DROP CONSTRAINT configuration_kind_check;
ALTER TABLE core.configuration ADD CONSTRAINT configuration_kind_check CHECK (kind IN ('product'));
-- Estaciones: la de pase (la crea ademas el motor para su ambito al terminar init/upgrade/restore); las de los puestos
-- emparejados se conservan con su codigo.
INSERT INTO core.stations (tenant, company, location, id, name, kind, sort)
 SELECT DISTINCT tenant, company, location, 'pase', 'Pase', 'pass', 0 FROM core.devices
 ON CONFLICT DO NOTHING;
INSERT INTO core.stations (tenant, company, location, id, name, kind, sort)
 SELECT DISTINCT tenant, company, location, station, station,
        CASE WHEN station LIKE 'sala%' THEN 'room' WHEN station LIKE 'barra%' OR station LIKE 'bar%' THEN 'bar' ELSE 'kitchen' END, 10
 FROM core.devices WHERE station <> 'pase' AND station ~ '^[A-Za-z0-9][A-Za-z0-9_-]{0,31}$'
 ON CONFLICT DO NOTHING;
-- Instalacion de demostracion: las estaciones de sus menus ficticios (cold, hot) y una de sala.
INSERT INTO core.stations (tenant, company, location, id, name, kind, sort)
 SELECT c.tenant, c.company, c.location, s.id, s.name, s.kind, s.sort
 FROM core.commands c CROSS JOIN (VALUES ('pase','Pase','pass',0), ('cold','Cocina fría','kitchen',1), ('hot','Cocina caliente','kitchen',2), ('sala-1','Sala 1','room',3)) AS s(id, name, kind, sort)
 WHERE c.actor = 'lab-initializer' AND c.key = 'lab-fixtures-v1'
 ON CONFLICT DO NOTHING;
ALTER TABLE core.schema_version DROP CONSTRAINT schema_version_version_check;
UPDATE core.schema_version SET version = 3;
ALTER TABLE core.schema_version ADD CONSTRAINT schema_version_version_check CHECK (version = 3);
