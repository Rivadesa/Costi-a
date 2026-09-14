-- Hospitality OS / Costi-a · V1A PostgreSQL reference schema
-- Local-primary database. This is the reference model for Laravel migrations.
-- ULIDs are char(26) to remain sortable/portable.

CREATE TABLE tenants (
    id char(26) PRIMARY KEY,
    name varchar(160) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE companies (
    id char(26) PRIMARY KEY,
    tenant_id char(26) NOT NULL REFERENCES tenants(id),
    legal_name varchar(180) NOT NULL,
    trade_name varchar(180),
    tax_id varchar(32),
    active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX companies_tenant_idx ON companies(tenant_id);

CREATE TABLE locations (
    id char(26) PRIMARY KEY,
    tenant_id char(26) NOT NULL REFERENCES tenants(id),
    company_id char(26) NOT NULL REFERENCES companies(id),
    name varchar(160) NOT NULL,
    timezone varchar(64) NOT NULL DEFAULT 'Europe/Madrid',
    active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX locations_company_idx ON locations(company_id);

CREATE TABLE users (
    id char(26) PRIMARY KEY,
    tenant_id char(26) NOT NULL REFERENCES tenants(id),
    display_name varchar(160) NOT NULL,
    email varchar(255),
    password_hash varchar(255),
    active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, email)
);

CREATE TABLE roles (
    id char(26) PRIMARY KEY,
    tenant_id char(26) NOT NULL REFERENCES tenants(id),
    name varchar(100) NOT NULL,
    permissions jsonb NOT NULL DEFAULT '[]'::jsonb,
    UNIQUE (tenant_id, name)
);

CREATE TABLE user_roles (
    id char(26) PRIMARY KEY,
    user_id char(26) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role_id char(26) NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    company_id char(26) REFERENCES companies(id),
    location_id char(26) REFERENCES locations(id),
    UNIQUE (user_id, role_id, company_id, location_id)
);

CREATE TABLE dining_areas (
    id char(26) PRIMARY KEY,
    tenant_id char(26) NOT NULL REFERENCES tenants(id),
    company_id char(26) NOT NULL REFERENCES companies(id),
    location_id char(26) NOT NULL REFERENCES locations(id),
    name varchar(120) NOT NULL,
    sequence integer NOT NULL DEFAULT 0,
    active boolean NOT NULL DEFAULT true
);

CREATE TABLE dining_tables (
    id char(26) PRIMARY KEY,
    tenant_id char(26) NOT NULL REFERENCES tenants(id),
    company_id char(26) NOT NULL REFERENCES companies(id),
    location_id char(26) NOT NULL REFERENCES locations(id),
    dining_area_id char(26) NOT NULL REFERENCES dining_areas(id),
    code varchar(40) NOT NULL,
    name varchar(100) NOT NULL,
    capacity integer NOT NULL CHECK (capacity >= 1),
    sequence integer NOT NULL DEFAULT 0,
    active boolean NOT NULL DEFAULT true,
    UNIQUE (location_id, code)
);

CREATE TABLE kitchen_stations (
    id char(26) PRIMARY KEY,
    tenant_id char(26) NOT NULL REFERENCES tenants(id),
    company_id char(26) NOT NULL REFERENCES companies(id),
    location_id char(26) NOT NULL REFERENCES locations(id),
    name varchar(120) NOT NULL,
    sequence integer NOT NULL DEFAULT 0,
    active boolean NOT NULL DEFAULT true,
    UNIQUE (location_id, name)
);

CREATE TABLE menu_templates (
    id char(26) PRIMARY KEY,
    tenant_id char(26) NOT NULL REFERENCES tenants(id),
    company_id char(26) NOT NULL REFERENCES companies(id),
    location_id char(26) REFERENCES locations(id),
    name varchar(180) NOT NULL,
    price_cents integer NOT NULL CHECK (price_cents >= 0),
    currency char(3) NOT NULL DEFAULT 'EUR',
    active boolean NOT NULL DEFAULT true,
    version integer NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE course_templates (
    id char(26) PRIMARY KEY,
    menu_template_id char(26) NOT NULL REFERENCES menu_templates(id) ON DELETE CASCADE,
    sequence integer NOT NULL CHECK (sequence >= 1),
    name varchar(180) NOT NULL,
    active boolean NOT NULL DEFAULT true,
    UNIQUE (menu_template_id, sequence)
);

CREATE TABLE preparation_templates (
    id char(26) PRIMARY KEY,
    course_template_id char(26) NOT NULL REFERENCES course_templates(id) ON DELETE CASCADE,
    station_id char(26) NOT NULL REFERENCES kitchen_stations(id),
    name varchar(180) NOT NULL,
    quantity_mode varchar(20) NOT NULL CHECK (quantity_mode IN ('per_guest','fixed')),
    fixed_quantity integer NOT NULL DEFAULT 1 CHECK (fixed_quantity >= 1),
    mandatory boolean NOT NULL DEFAULT true,
    sequence integer NOT NULL DEFAULT 0
);

CREATE TABLE table_services (
    id char(26) PRIMARY KEY,
    tenant_id char(26) NOT NULL REFERENCES tenants(id),
    company_id char(26) NOT NULL REFERENCES companies(id),
    location_id char(26) NOT NULL REFERENCES locations(id),
    dining_table_id char(26) NOT NULL REFERENCES dining_tables(id),
    status varchar(24) NOT NULL CHECK (status IN ('prepared','open','in_service','paused','pending_payment','paid','closed','cancelled')),
    pax integer NOT NULL CHECK (pax >= 1),
    opened_at timestamptz NOT NULL,
    started_at timestamptz,
    closed_at timestamptz,
    cancelled_at timestamptz,
    cancel_reason text,
    version integer NOT NULL DEFAULT 1,
    created_by char(26) REFERENCES users(id),
    updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX table_services_location_status_idx ON table_services(location_id, status);
CREATE INDEX table_services_table_status_idx ON table_services(dining_table_id, status);

-- Snapshot assigned to a concrete service. Template changes never rewrite history.
CREATE TABLE service_menus (
    id char(26) PRIMARY KEY,
    table_service_id char(26) NOT NULL UNIQUE REFERENCES table_services(id) ON DELETE CASCADE,
    menu_template_id char(26) REFERENCES menu_templates(id),
    menu_name varchar(180) NOT NULL,
    unit_price_cents integer NOT NULL CHECK (unit_price_cents >= 0),
    currency char(3) NOT NULL DEFAULT 'EUR',
    quantity integer NOT NULL CHECK (quantity >= 1),
    assigned_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE service_guests (
    id char(26) PRIMARY KEY,
    table_service_id char(26) NOT NULL REFERENCES table_services(id) ON DELETE CASCADE,
    position integer NOT NULL CHECK (position >= 1),
    name varchar(160),
    customer_id char(26), -- future CRM reference; intentionally no V1A FK
    UNIQUE (table_service_id, position)
);

CREATE TABLE guest_restrictions (
    id char(26) PRIMARY KEY,
    service_guest_id char(26) NOT NULL REFERENCES service_guests(id) ON DELETE CASCADE,
    label varchar(160) NOT NULL,
    type varchar(20) NOT NULL CHECK (type IN ('allergy','intolerance','preference')),
    severity varchar(20) NOT NULL CHECK (severity IN ('informative','important','critical')),
    notes text,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE service_courses (
    id char(26) PRIMARY KEY,
    table_service_id char(26) NOT NULL REFERENCES table_services(id) ON DELETE CASCADE,
    course_template_id char(26) REFERENCES course_templates(id),
    sequence integer NOT NULL CHECK (sequence >= 1),
    name varchar(180) NOT NULL,
    status varchar(20) NOT NULL CHECK (status IN ('pending','fired','preparing','ready','served','skipped','cancelled')),
    is_extra boolean NOT NULL DEFAULT false,
    fired_at timestamptz,
    ready_at timestamptz,
    served_at timestamptz,
    exception_reason text,
    UNIQUE (table_service_id, sequence)
);
CREATE INDEX service_courses_status_idx ON service_courses(table_service_id, status);

-- One actionable unit in a kitchen station. guest_position is populated for per-guest items.
CREATE TABLE service_course_items (
    id char(26) PRIMARY KEY,
    service_course_id char(26) NOT NULL REFERENCES service_courses(id) ON DELETE CASCADE,
    preparation_template_id char(26) REFERENCES preparation_templates(id),
    station_id char(26) NOT NULL REFERENCES kitchen_stations(id),
    name varchar(180) NOT NULL,
    quantity integer NOT NULL DEFAULT 1 CHECK (quantity >= 1),
    guest_position integer,
    mandatory boolean NOT NULL DEFAULT true,
    status varchar(20) NOT NULL CHECK (status IN ('pending','fired','preparing','ready','cancelled')),
    fired_at timestamptz,
    started_at timestamptz,
    ready_at timestamptz,
    cancelled_at timestamptz,
    cancel_reason text,
    modification_reason text
);
CREATE INDEX service_course_items_station_status_idx ON service_course_items(station_id, status);
CREATE INDEX service_course_items_course_idx ON service_course_items(service_course_id);

CREATE TABLE consumptions (
    id char(26) PRIMARY KEY,
    table_service_id char(26) NOT NULL REFERENCES table_services(id) ON DELETE CASCADE,
    product_id char(26), -- PIM module introduced later
    name varchar(180) NOT NULL,
    quantity integer NOT NULL CHECK (quantity >= 1),
    unit_price_cents integer NOT NULL CHECK (unit_price_cents >= 0),
    cancelled boolean NOT NULL DEFAULT false,
    cancel_reason text,
    created_by char(26) REFERENCES users(id),
    created_at timestamptz NOT NULL DEFAULT now(),
    cancelled_at timestamptz
);

CREATE TABLE payments (
    id char(26) PRIMARY KEY,
    table_service_id char(26) NOT NULL REFERENCES table_services(id),
    method varchar(40) NOT NULL,
    amount_cents integer NOT NULL CHECK (amount_cents > 0),
    currency char(3) NOT NULL DEFAULT 'EUR',
    external_reference varchar(180),
    recorded_by char(26) REFERENCES users(id),
    recorded_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE idempotency_keys (
    id char(26) PRIMARY KEY,
    tenant_id char(26) NOT NULL REFERENCES tenants(id),
    idempotency_key varchar(120) NOT NULL,
    command_name varchar(120) NOT NULL,
    request_hash varchar(128),
    response_status integer,
    response_body jsonb,
    created_at timestamptz NOT NULL DEFAULT now(),
    expires_at timestamptz,
    UNIQUE (tenant_id, idempotency_key)
);

-- Durable handoff written in the same DB transaction as the domain change.
CREATE TABLE outbox_events (
    id char(26) PRIMARY KEY,
    tenant_id char(26) NOT NULL REFERENCES tenants(id),
    company_id char(26) NOT NULL REFERENCES companies(id),
    location_id char(26) REFERENCES locations(id),
    aggregate_type varchar(80) NOT NULL,
    aggregate_id char(26) NOT NULL,
    event_type varchar(120) NOT NULL,
    payload jsonb NOT NULL DEFAULT '{}'::jsonb,
    occurred_at timestamptz NOT NULL,
    available_at timestamptz NOT NULL DEFAULT now(),
    published_at timestamptz,
    attempts integer NOT NULL DEFAULT 0,
    last_error text
);
CREATE INDEX outbox_pending_idx ON outbox_events(available_at, occurred_at) WHERE published_at IS NULL;

CREATE TABLE audit_log (
    id char(26) PRIMARY KEY,
    tenant_id char(26) NOT NULL REFERENCES tenants(id),
    company_id char(26) NOT NULL REFERENCES companies(id),
    location_id char(26) REFERENCES locations(id),
    user_id char(26) REFERENCES users(id),
    action varchar(120) NOT NULL,
    entity_type varchar(80) NOT NULL,
    entity_id char(26) NOT NULL,
    before_data jsonb,
    after_data jsonb,
    metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
    occurred_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX audit_entity_idx ON audit_log(entity_type, entity_id, occurred_at);

-- V1B fiscal boundary:
-- table_services/accounts/payments are operational concepts, not fiscal documents.
-- Future fiscal tables reference a closed settlement/service snapshot without rewriting operational history.
