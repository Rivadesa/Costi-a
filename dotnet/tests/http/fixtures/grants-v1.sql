-- Concesiones v1 (native_d1) tal como las aplicaban los binarios hasta 0.26.0-e1. FIXTURE de pruebas junto a schema-v1.sql. Nunca cambia.
-- D5.1 (#27): privilegios minimos del rol de EJECUCION. Lo aplica el propietario del esquema tras
-- schema.sql y solo si el rol costina_runtime existe (lo crea "provision"). El motor en marcha no
-- tiene DDL, no borra nada y no puede reescribir comandos ni auditoria; las columnas de estado que
-- necesita cambiar se conceden de forma explicita. Tabla nueva = revisar este fichero.
REVOKE ALL ON SCHEMA native_d1 FROM PUBLIC;
REVOKE ALL ON ALL TABLES IN SCHEMA native_d1 FROM PUBLIC;
REVOKE ALL ON ALL TABLES IN SCHEMA native_d1 FROM costina_runtime;
GRANT USAGE ON SCHEMA native_d1 TO costina_runtime;
GRANT SELECT ON ALL TABLES IN SCHEMA native_d1 TO costina_runtime;
GRANT INSERT, UPDATE ON native_d1.services, native_d1.occupancies, native_d1.accounts TO costina_runtime;
GRANT INSERT, UPDATE ON native_d1.sessions, native_d1.devices, native_d1.pairings TO costina_runtime;
GRANT INSERT ON native_d1.commands, native_d1.audit, native_d1.outbox, native_d1.users TO costina_runtime;
GRANT UPDATE (published_at) ON native_d1.outbox TO costina_runtime
