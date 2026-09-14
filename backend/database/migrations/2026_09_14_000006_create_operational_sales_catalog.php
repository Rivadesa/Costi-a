<?php

declare(strict_types=1);

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;

return new class extends Migration {
    public function up(): void
    {
        Schema::create('product_categories', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('tenant_id', 26);
            $table->string('code', 64);
            $table->string('name', 160);
            $table->unsignedInteger('sequence')->default(0);
            $table->boolean('active')->default(true);
            $table->timestampsTz();

            $table->foreign('tenant_id')->references('id')->on('tenants');
            $table->unique(['tenant_id', 'code']);
            $table->index(['tenant_id', 'active', 'sequence']);
        });

        Schema::create('products', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('tenant_id', 26);
            $table->char('product_category_id', 26)->nullable();
            $table->string('code', 64);
            $table->string('sku', 100)->nullable();
            $table->string('name', 180);
            $table->string('product_type', 30)->default('other');
            $table->string('sale_unit', 30)->default('unit');
            $table->string('format_label', 80)->nullable();
            $table->boolean('active')->default(true);
            $table->unsignedInteger('sequence')->default(0);
            $table->timestampsTz();

            $table->foreign('tenant_id')->references('id')->on('tenants');
            $table->foreign('product_category_id')->references('id')->on('product_categories');
            $table->unique(['tenant_id', 'code']);
            $table->index(['tenant_id', 'active', 'sequence']);
            $table->index(['product_category_id', 'active']);
        });

        DB::statement("ALTER TABLE products ADD CONSTRAINT products_type_check CHECK (product_type IN ('beverage','wine','food','extra','other'))");
        DB::statement("ALTER TABLE products ADD CONSTRAINT products_sale_unit_check CHECK (sale_unit IN ('unit','glass','bottle','portion','service','other'))");

        Schema::create('price_lists', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('tenant_id', 26);
            $table->char('company_id', 26);
            $table->char('location_id', 26)->nullable();
            $table->string('code', 64);
            $table->string('name', 160);
            $table->boolean('is_default')->default(false);
            $table->boolean('active')->default(true);
            $table->timestampsTz();

            $table->foreign('tenant_id')->references('id')->on('tenants');
            $table->foreign('company_id')->references('id')->on('companies');
            $table->foreign('location_id')->references('id')->on('locations');
            $table->unique(['company_id', 'code']);
            $table->index(['tenant_id', 'company_id', 'location_id', 'active', 'is_default']);
        });

        Schema::create('product_prices', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('product_id', 26);
            $table->char('price_list_id', 26);
            $table->unsignedInteger('price_cents');
            $table->char('currency', 3)->default('EUR');
            $table->boolean('active')->default(true);
            $table->timestampsTz();

            $table->foreign('product_id')->references('id')->on('products');
            $table->foreign('price_list_id')->references('id')->on('price_lists');
            $table->unique(['product_id', 'price_list_id']);
            $table->index(['price_list_id', 'active']);
        });

        Schema::table('consumptions', function (Blueprint $table): void {
            $table->char('price_list_id', 26)->nullable()->after('product_id');
            $table->foreign('product_id')->references('id')->on('products');
            $table->foreign('price_list_id')->references('id')->on('price_lists');
            $table->index('product_id');
        });
    }

    public function down(): void
    {
        Schema::table('consumptions', function (Blueprint $table): void {
            $table->dropForeign(['product_id']);
            $table->dropForeign(['price_list_id']);
            $table->dropIndex(['product_id']);
            $table->dropColumn('price_list_id');
        });

        Schema::dropIfExists('product_prices');
        Schema::dropIfExists('price_lists');
        Schema::dropIfExists('products');
        Schema::dropIfExists('product_categories');
    }
};
