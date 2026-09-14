<?php

declare(strict_types=1);

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration {
    public function up(): void
    {
        Schema::create('tenants', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->string('name', 160);
            $table->timestampsTz();
        });

        Schema::create('companies', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('tenant_id', 26);
            $table->string('legal_name', 180);
            $table->string('trade_name', 180)->nullable();
            $table->string('tax_id', 32)->nullable();
            $table->boolean('active')->default(true);
            $table->timestampsTz();
            $table->foreign('tenant_id')->references('id')->on('tenants');
            $table->index('tenant_id');
        });

        Schema::create('locations', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('tenant_id', 26);
            $table->char('company_id', 26);
            $table->string('name', 160);
            $table->string('timezone', 64)->default('Europe/Madrid');
            $table->boolean('active')->default(true);
            $table->timestampsTz();
            $table->foreign('tenant_id')->references('id')->on('tenants');
            $table->foreign('company_id')->references('id')->on('companies');
            $table->index('company_id');
        });

        Schema::create('users', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('tenant_id', 26);
            $table->string('display_name', 160);
            $table->string('email')->nullable();
            $table->string('password_hash')->nullable();
            $table->boolean('active')->default(true);
            $table->timestampsTz();
            $table->foreign('tenant_id')->references('id')->on('tenants');
            $table->unique(['tenant_id', 'email']);
        });

        Schema::create('roles', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('tenant_id', 26);
            $table->string('name', 100);
            $table->jsonb('permissions')->default('[]');
            $table->foreign('tenant_id')->references('id')->on('tenants');
            $table->unique(['tenant_id', 'name']);
        });

        Schema::create('user_roles', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('user_id', 26);
            $table->char('role_id', 26);
            $table->char('company_id', 26)->nullable();
            $table->char('location_id', 26)->nullable();
            $table->foreign('user_id')->references('id')->on('users')->cascadeOnDelete();
            $table->foreign('role_id')->references('id')->on('roles')->cascadeOnDelete();
            $table->foreign('company_id')->references('id')->on('companies');
            $table->foreign('location_id')->references('id')->on('locations');
            $table->unique(['user_id', 'role_id', 'company_id', 'location_id'], 'user_roles_scope_unique');
        });

        Schema::create('dining_areas', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('tenant_id', 26);
            $table->char('company_id', 26);
            $table->char('location_id', 26);
            $table->string('name', 120);
            $table->integer('sequence')->default(0);
            $table->boolean('active')->default(true);
            $table->foreign('tenant_id')->references('id')->on('tenants');
            $table->foreign('company_id')->references('id')->on('companies');
            $table->foreign('location_id')->references('id')->on('locations');
        });

        Schema::create('dining_tables', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('tenant_id', 26);
            $table->char('company_id', 26);
            $table->char('location_id', 26);
            $table->char('dining_area_id', 26);
            $table->string('code', 40);
            $table->string('name', 100);
            $table->unsignedInteger('capacity');
            $table->integer('sequence')->default(0);
            $table->boolean('active')->default(true);
            $table->foreign('tenant_id')->references('id')->on('tenants');
            $table->foreign('company_id')->references('id')->on('companies');
            $table->foreign('location_id')->references('id')->on('locations');
            $table->foreign('dining_area_id')->references('id')->on('dining_areas');
            $table->unique(['location_id', 'code']);
        });

        Schema::create('kitchen_stations', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('tenant_id', 26);
            $table->char('company_id', 26);
            $table->char('location_id', 26);
            $table->string('name', 120);
            $table->integer('sequence')->default(0);
            $table->boolean('active')->default(true);
            $table->foreign('tenant_id')->references('id')->on('tenants');
            $table->foreign('company_id')->references('id')->on('companies');
            $table->foreign('location_id')->references('id')->on('locations');
            $table->unique(['location_id', 'name']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('kitchen_stations');
        Schema::dropIfExists('dining_tables');
        Schema::dropIfExists('dining_areas');
        Schema::dropIfExists('user_roles');
        Schema::dropIfExists('roles');
        Schema::dropIfExists('users');
        Schema::dropIfExists('locations');
        Schema::dropIfExists('companies');
        Schema::dropIfExists('tenants');
    }
};
