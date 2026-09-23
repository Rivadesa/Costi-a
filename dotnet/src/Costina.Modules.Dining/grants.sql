-- Concesiones del rol de EJECUCION sobre el esquema del modulo Dining (E1b, ADR-011/012): las aplica el nucleo tras
-- las suyas, solo si costina_runtime existe. Mismo criterio que el nucleo: sin DDL, sin borrar, escritura solo en
-- las tablas que el modulo guarda; la configuracion (menus) se lee y, hasta E4, la siembran los fixtures.
REVOKE ALL ON SCHEMA dining FROM PUBLIC;
REVOKE ALL ON ALL TABLES IN SCHEMA dining FROM PUBLIC;
REVOKE ALL ON ALL TABLES IN SCHEMA dining FROM costina_runtime;
GRANT USAGE ON SCHEMA dining TO costina_runtime;
GRANT SELECT ON ALL TABLES IN SCHEMA dining TO costina_runtime;
GRANT INSERT, UPDATE ON dining.services, dining.occupancies TO costina_runtime
