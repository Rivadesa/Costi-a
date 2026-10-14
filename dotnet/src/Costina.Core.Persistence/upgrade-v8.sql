-- B.2 (ADR-014): esquema v7 -> v8. Conexion sencilla por la red local: HTTP sin cifrar hacia los dispositivos, apagada por
-- defecto. Es un ajuste de la INSTALACION (una sola fila), no de un ambito: lo cambia solo el responsable con la orden
-- lan-http-set (auditada) y el motor lo aplica al momento, sin reiniciar. No mueve datos.
ALTER TABLE core.installation ADD COLUMN lan_http boolean NOT NULL DEFAULT false;
ALTER TABLE core.schema_version DROP CONSTRAINT schema_version_version_check;
UPDATE core.schema_version SET version = 8;
ALTER TABLE core.schema_version ADD CONSTRAINT schema_version_version_check CHECK (version = 8);
