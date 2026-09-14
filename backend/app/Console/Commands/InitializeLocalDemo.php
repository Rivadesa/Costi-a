<?php

declare(strict_types=1);

namespace App\Console\Commands;

use Database\Seeders\RetiroDemoSeeder;
use Illuminate\Console\Command;
use Illuminate\Support\Facades\DB;

/** Explicit development initialization; never called by the normal server boot. */
final class InitializeLocalDemo extends Command
{
    protected $signature = 'hospitality:initialize-demo';
    protected $description = 'Initialize an empty local test installation without overwriting existing data';

    public function handle(): int
    {
        if (!app()->environment(['local', 'testing'])) {
            $this->error('Demo initialization is only allowed in local/testing environments.');
            return self::FAILURE;
        }

        try {
            $created = DB::transaction(function (): bool {
                // Serialize concurrent attempts on this database. The seed shares this transaction.
                DB::select('SELECT pg_advisory_xact_lock(20260914, 1)');
                $tenants = DB::table('tenants')->pluck('id');
                if ($tenants->count() === 1 && trim((string) $tenants->first()) === RetiroDemoSeeder::TENANT_ID) {
                    return false;
                }
                if ($tenants->isNotEmpty()) {
                    throw new \RuntimeException('Database is not empty. Refusing to load demo data into an existing installation.');
                }
                $exit = $this->call('db:seed', ['--force' => true, '--no-ansi' => true]);
                if ($exit !== self::SUCCESS) {
                    throw new \RuntimeException('Demo initialization failed; database changes have been rolled back.');
                }
                return true;
            });
        } catch (\Throwable $exception) {
            $this->error($exception->getMessage());
            return self::FAILURE;
        }

        $this->info($created ? 'Local demo initialized.' : 'Existing demo detected. No accounts, catalog, users or configuration were overwritten.');
        return self::SUCCESS;
    }
}
