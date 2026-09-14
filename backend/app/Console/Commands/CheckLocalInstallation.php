<?php

declare(strict_types=1);

namespace App\Console\Commands;

use Illuminate\Console\Command;
use Illuminate\Support\Facades\DB;

final class CheckLocalInstallation extends Command
{
    protected $signature = 'hospitality:check-installation';
    protected $description = 'Check the configured local context without changing business data';

    public function handle(): int
    {
        try {
            $ready = DB::table('locations as l')
                ->join('companies as c', 'c.id', '=', 'l.company_id')
                ->join('tenants as t', 't.id', '=', 'l.tenant_id')
                ->where('l.id', config('hospitality.location_id'))
                ->where('c.id', config('hospitality.company_id'))
                ->where('t.id', config('hospitality.tenant_id'))
                ->whereColumn('c.tenant_id', 'l.tenant_id')
                ->where('l.active', true)->where('c.active', true)->exists();
        } catch (\Throwable) {
            $this->error('Local database is unavailable or not initialized. Run the documented initialization/update procedure.');
            return self::FAILURE;
        }
        if (!$ready) {
            $this->error('Configured tenant/company/location was not found or is inactive. No data was changed.');
            return self::FAILURE;
        }
        $this->info('Local installation context is ready.');
        return self::SUCCESS;
    }
}
