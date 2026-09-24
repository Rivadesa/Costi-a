-- E4b (Hito 6, ADR-012): esquema v5 -> v6. Carta libre: items por grupo (producto + presentacion pedibles) y estacion de
-- cocina del producto. No mueve datos.
CREATE TABLE core.offer_items (
 tenant text NOT NULL, company text NOT NULL, location text NOT NULL, offer_id text NOT NULL, course_id text NOT NULL,
 product_id text NOT NULL, presentation_id text NOT NULL, sort integer NOT NULL DEFAULT 0, active boolean NOT NULL DEFAULT true,
 PRIMARY KEY (tenant, company, location, offer_id, course_id, product_id, presentation_id),
 FOREIGN KEY (tenant, company, location, offer_id, course_id) REFERENCES core.offer_courses (tenant, company, location, offer_id, id),
 FOREIGN KEY (tenant, company, location, product_id, presentation_id) REFERENCES core.presentations (tenant, company, location, product_id, id)
);
ALTER TABLE core.products ADD COLUMN station_id text NULL;
ALTER TABLE core.products ADD CONSTRAINT products_station_fkey FOREIGN KEY (tenant, company, location, station_id) REFERENCES core.stations (tenant, company, location, id);
ALTER TABLE core.schema_version DROP CONSTRAINT schema_version_version_check;
UPDATE core.schema_version SET version = 6;
ALTER TABLE core.schema_version ADD CONSTRAINT schema_version_version_check CHECK (version = 6);
