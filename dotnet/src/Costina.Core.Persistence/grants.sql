-- D5.1 (#27): privilegios minimos del rol de EJECUCION sobre el esquema del NUCLEO. Lo aplica el propietario del
-- esquema tras schema.sql y solo si el rol costina_runtime existe (lo crea "provision"). El motor en marcha no
-- tiene DDL, no borra nada y no puede reescribir comandos ni auditoria; las columnas de estado que necesita
-- cambiar se conceden de forma explicita. Tabla nueva = revisar este fichero. Cada modulo trae el suyo (E1b, ADR-012).
REVOKE ALL ON SCHEMA core FROM PUBLIC;
REVOKE ALL ON ALL TABLES IN SCHEMA core FROM PUBLIC;
REVOKE ALL ON ALL TABLES IN SCHEMA core FROM costina_runtime;
GRANT USAGE ON SCHEMA core TO costina_runtime;
GRANT SELECT ON ALL TABLES IN SCHEMA core TO costina_runtime;
GRANT INSERT, UPDATE ON core.accounts TO costina_runtime;
GRANT INSERT, UPDATE ON core.zones, core.tables, core.stations TO costina_runtime;
GRANT INSERT, UPDATE ON core.taxes, core.categories, core.products, core.presentations, core.tariffs, core.prices TO costina_runtime;
GRANT INSERT, UPDATE ON core.offers, core.offer_courses, core.offer_dishes TO costina_runtime;
GRANT INSERT, UPDATE ON core.sessions, core.devices, core.pairings TO costina_runtime;
GRANT INSERT ON core.commands, core.audit, core.outbox, core.users TO costina_runtime;
GRANT UPDATE (published_at) ON core.outbox TO costina_runtime
