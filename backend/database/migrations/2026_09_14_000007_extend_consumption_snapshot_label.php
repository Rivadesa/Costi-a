<?php

declare(strict_types=1);

use Illuminate\Database\Migrations\Migration;
use Illuminate\Support\Facades\DB;

return new class extends Migration {
    public function up(): void
    {
        // Product name + presentation + format are frozen in the historic account label.
        DB::statement('ALTER TABLE consumptions ALTER COLUMN name TYPE VARCHAR(320)');
        DB::statement('ALTER TABLE products ADD COLUMN catalog_version BIGINT NOT NULL DEFAULT 1 CHECK (catalog_version >= 1)');
    }

    public function down(): void
    {
        if (DB::table('consumptions')->whereRaw('char_length(name) > 180')->exists()) {
            throw new \RuntimeException('Refusing to truncate historic consumption descriptions.');
        }
        DB::statement('ALTER TABLE consumptions ALTER COLUMN name TYPE VARCHAR(180)');
        DB::statement('ALTER TABLE products DROP COLUMN catalog_version');
    }
};
