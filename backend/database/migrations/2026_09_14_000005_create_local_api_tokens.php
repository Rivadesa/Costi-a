<?php

declare(strict_types=1);

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration {
    public function up(): void
    {
        Schema::create('api_tokens', function (Blueprint $table): void {
            $table->char('id', 26)->primary();
            $table->char('user_id', 26);
            $table->string('name', 160)->default('local-device');
            $table->char('token_hash', 64)->unique();
            $table->timestampTz('last_used_at')->nullable();
            $table->timestampTz('expires_at')->nullable();
            $table->timestampTz('revoked_at')->nullable();
            $table->timestampTz('created_at')->useCurrent();
            $table->foreign('user_id')->references('id')->on('users')->cascadeOnDelete();
            $table->index(['user_id', 'revoked_at', 'expires_at'], 'api_tokens_user_active_idx');
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('api_tokens');
    }
};
