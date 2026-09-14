<?php

declare(strict_types=1);

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;

return new class extends Migration {
    public function up(): void
    {
        Schema::create('idempotency_keys', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('tenant_id', 26);
            $table->string('idempotency_key', 120);
            $table->string('command_name', 120);
            $table->string('request_hash', 128);
            $table->unsignedSmallInteger('response_status')->nullable();
            $table->jsonb('response_body')->nullable();
            $table->timestampTz('created_at')->useCurrent();
            $table->timestampTz('expires_at')->nullable();
            $table->foreign('tenant_id')->references('id')->on('tenants');
            $table->unique(['tenant_id', 'idempotency_key']);
        });

        Schema::create('outbox_events', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('tenant_id', 26);
            $table->char('company_id', 26);
            $table->char('location_id', 26)->nullable();
            $table->string('aggregate_type', 80);
            $table->char('aggregate_id', 26);
            $table->string('event_type', 120);
            $table->jsonb('payload')->default('{}');
            $table->timestampTz('occurred_at');
            $table->timestampTz('available_at')->useCurrent();
            $table->timestampTz('published_at')->nullable();
            $table->unsignedInteger('attempts')->default(0);
            $table->text('last_error')->nullable();
            $table->foreign('tenant_id')->references('id')->on('tenants');
            $table->foreign('company_id')->references('id')->on('companies');
            $table->foreign('location_id')->references('id')->on('locations');
            $table->index(['aggregate_type', 'aggregate_id', 'occurred_at'], 'outbox_aggregate_idx');
        });
        DB::statement('CREATE INDEX outbox_pending_idx ON outbox_events (available_at, occurred_at) WHERE published_at IS NULL');

        Schema::create('audit_log', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('tenant_id', 26);
            $table->char('company_id', 26);
            $table->char('location_id', 26)->nullable();
            $table->char('user_id', 26)->nullable();
            $table->string('action', 120);
            $table->string('entity_type', 80);
            $table->char('entity_id', 26);
            $table->jsonb('before_data')->nullable();
            $table->jsonb('after_data')->nullable();
            $table->jsonb('metadata')->default('{}');
            $table->timestampTz('occurred_at')->useCurrent();
            $table->foreign('tenant_id')->references('id')->on('tenants');
            $table->foreign('company_id')->references('id')->on('companies');
            $table->foreign('location_id')->references('id')->on('locations');
            $table->foreign('user_id')->references('id')->on('users');
            $table->index(['entity_type', 'entity_id', 'occurred_at'], 'audit_entity_idx');
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('audit_log');
        Schema::dropIfExists('outbox_events');
        Schema::dropIfExists('idempotency_keys');
    }
};
