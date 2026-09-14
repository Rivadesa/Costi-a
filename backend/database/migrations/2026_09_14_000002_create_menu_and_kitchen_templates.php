<?php

declare(strict_types=1);

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration {
    public function up(): void
    {
        Schema::create('menu_templates', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('tenant_id', 26);
            $table->char('company_id', 26);
            $table->char('location_id', 26)->nullable();
            $table->string('name', 180);
            $table->unsignedInteger('price_cents');
            $table->char('currency', 3)->default('EUR');
            $table->boolean('active')->default(true);
            $table->unsignedInteger('version')->default(1);
            $table->timestampsTz();
            $table->foreign('tenant_id')->references('id')->on('tenants');
            $table->foreign('company_id')->references('id')->on('companies');
            $table->foreign('location_id')->references('id')->on('locations');
            $table->index(['tenant_id', 'company_id', 'active']);
        });

        Schema::create('course_templates', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('menu_template_id', 26);
            $table->unsignedInteger('sequence');
            $table->string('name', 180);
            $table->boolean('active')->default(true);
            $table->foreign('menu_template_id')->references('id')->on('menu_templates')->cascadeOnDelete();
            $table->unique(['menu_template_id', 'sequence']);
        });

        Schema::create('preparation_templates', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('course_template_id', 26);
            $table->char('station_id', 26);
            $table->string('name', 180);
            $table->string('quantity_mode', 20);
            $table->unsignedInteger('fixed_quantity')->default(1);
            $table->boolean('mandatory')->default(true);
            $table->integer('sequence')->default(0);
            $table->foreign('course_template_id')->references('id')->on('course_templates')->cascadeOnDelete();
            $table->foreign('station_id')->references('id')->on('kitchen_stations');
            $table->index(['station_id', 'course_template_id']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('preparation_templates');
        Schema::dropIfExists('course_templates');
        Schema::dropIfExists('menu_templates');
    }
};
