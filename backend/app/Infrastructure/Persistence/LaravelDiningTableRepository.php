<?php

declare(strict_types=1);

namespace App\Infrastructure\Persistence;

use Hospitality\Application\Contracts\DiningTableRepository;
use Illuminate\Support\Facades\DB;

final class LaravelDiningTableRepository implements DiningTableRepository
{
    public function existsInScope(
        string $tenantId,
        string $companyId,
        string $locationId,
        string $tableId,
    ): bool {
        return DB::table('dining_tables')
            ->where('id', $tableId)
            ->where('tenant_id', $tenantId)
            ->where('company_id', $companyId)
            ->where('location_id', $locationId)
            ->where('active', true)
            ->exists();
    }
}
