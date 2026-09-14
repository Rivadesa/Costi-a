<?php

declare(strict_types=1);

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;

return new class extends Migration {
    public function up(): void
    {
        Schema::create('table_services', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('tenant_id', 26);
            $table->char('company_id', 26);
            $table->char('location_id', 26);
            $table->char('dining_table_id', 26);
            $table->string('status', 24);
            $table->unsignedInteger('pax');
            $table->timestampTz('opened_at');
            $table->timestampTz('started_at')->nullable();
            $table->timestampTz('closed_at')->nullable();
            $table->timestampTz('cancelled_at')->nullable();
            $table->text('cancel_reason')->nullable();
            $table->unsignedInteger('version')->default(1);
            $table->char('created_by', 26)->nullable();
            $table->timestampTz('updated_at')->useCurrent();
            $table->foreign('tenant_id')->references('id')->on('tenants');
            $table->foreign('company_id')->references('id')->on('companies');
            $table->foreign('location_id')->references('id')->on('locations');
            $table->foreign('dining_table_id')->references('id')->on('dining_tables');
            $table->foreign('created_by')->references('id')->on('users');
            $table->index(['location_id', 'status']);
            $table->index(['dining_table_id', 'status']);
        });

        DB::statement("ALTER TABLE table_services ADD CONSTRAINT table_services_status_check CHECK (status IN ('prepared','open','in_service','paused','pending_payment','paid','closed','cancelled'))");
        DB::statement('ALTER TABLE table_services ADD CONSTRAINT table_services_pax_check CHECK (pax >= 1)');

        Schema::create('service_menus', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('table_service_id', 26)->unique();
            $table->char('menu_template_id', 26)->nullable();
            $table->string('menu_name', 180);
            $table->unsignedInteger('unit_price_cents');
            $table->char('currency', 3)->default('EUR');
            $table->unsignedInteger('quantity');
            $table->timestampTz('assigned_at')->useCurrent();
            $table->foreign('table_service_id')->references('id')->on('table_services')->cascadeOnDelete();
            $table->foreign('menu_template_id')->references('id')->on('menu_templates');
        });

        Schema::create('service_guests', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('table_service_id', 26);
            $table->unsignedInteger('position');
            $table->string('name', 160)->nullable();
            $table->char('customer_id', 26)->nullable();
            $table->foreign('table_service_id')->references('id')->on('table_services')->cascadeOnDelete();
            $table->unique(['table_service_id', 'position']);
        });

        Schema::create('guest_restrictions', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('service_guest_id', 26);
            $table->string('label', 160);
            $table->string('type', 20);
            $table->string('severity', 20);
            $table->text('notes')->nullable();
            $table->timestampTz('created_at')->useCurrent();
            $table->foreign('service_guest_id')->references('id')->on('service_guests')->cascadeOnDelete();
        });

        DB::statement("ALTER TABLE guest_restrictions ADD CONSTRAINT guest_restrictions_type_check CHECK (type IN ('allergy','intolerance','preference'))");
        DB::statement("ALTER TABLE guest_restrictions ADD CONSTRAINT guest_restrictions_severity_check CHECK (severity IN ('informative','important','critical'))");

        Schema::create('service_courses', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('table_service_id', 26);
            $table->char('course_template_id', 26)->nullable();
            $table->unsignedInteger('sequence');
            $table->string('name', 180);
            $table->string('status', 20);
            $table->boolean('is_extra')->default(false);
            $table->timestampTz('fired_at')->nullable();
            $table->timestampTz('ready_at')->nullable();
            $table->timestampTz('served_at')->nullable();
            $table->text('exception_reason')->nullable();
            $table->foreign('table_service_id')->references('id')->on('table_services')->cascadeOnDelete();
            $table->foreign('course_template_id')->references('id')->on('course_templates');
            $table->unique(['table_service_id', 'sequence']);
            $table->index(['table_service_id', 'status']);
        });

        DB::statement("ALTER TABLE service_courses ADD CONSTRAINT service_courses_status_check CHECK (status IN ('pending','fired','preparing','ready','served','skipped','cancelled'))");

        Schema::create('service_course_items', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('service_course_id', 26);
            $table->char('preparation_template_id', 26)->nullable();
            $table->char('station_id', 26);
            $table->string('name', 180);
            $table->unsignedInteger('quantity')->default(1);
            $table->unsignedInteger('guest_position')->nullable();
            $table->boolean('mandatory')->default(true);
            $table->string('status', 20);
            $table->timestampTz('fired_at')->nullable();
            $table->timestampTz('started_at')->nullable();
            $table->timestampTz('ready_at')->nullable();
            $table->timestampTz('cancelled_at')->nullable();
            $table->text('cancel_reason')->nullable();
            $table->text('modification_reason')->nullable();
            $table->foreign('service_course_id')->references('id')->on('service_courses')->cascadeOnDelete();
            $table->foreign('preparation_template_id')->references('id')->on('preparation_templates');
            $table->foreign('station_id')->references('id')->on('kitchen_stations');
            $table->index(['station_id', 'status']);
            $table->index('service_course_id');
        });

        DB::statement("ALTER TABLE service_course_items ADD CONSTRAINT service_course_items_status_check CHECK (status IN ('pending','fired','preparing','ready','cancelled'))");

        Schema::create('consumptions', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('table_service_id', 26);
            $table->char('product_id', 26)->nullable();
            $table->string('name', 180);
            $table->unsignedInteger('quantity');
            $table->unsignedInteger('unit_price_cents');
            $table->boolean('cancelled')->default(false);
            $table->text('cancel_reason')->nullable();
            $table->char('created_by', 26)->nullable();
            $table->timestampTz('created_at')->useCurrent();
            $table->timestampTz('cancelled_at')->nullable();
            $table->foreign('table_service_id')->references('id')->on('table_services')->cascadeOnDelete();
            $table->foreign('created_by')->references('id')->on('users');
        });

        Schema::create('payments', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('table_service_id', 26);
            $table->string('method', 40);
            $table->unsignedInteger('amount_cents');
            $table->char('currency', 3)->default('EUR');
            $table->string('external_reference', 180)->nullable();
            $table->char('recorded_by', 26)->nullable();
            $table->timestampTz('recorded_at')->useCurrent();
            $table->foreign('table_service_id')->references('id')->on('table_services');
            $table->foreign('recorded_by')->references('id')->on('users');
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('payments');
        Schema::dropIfExists('consumptions');
        Schema::dropIfExists('service_course_items');
        Schema::dropIfExists('service_courses');
        Schema::dropIfExists('guest_restrictions');
        Schema::dropIfExists('service_guests');
        Schema::dropIfExists('service_menus');
        Schema::dropIfExists('table_services');
    }
};
