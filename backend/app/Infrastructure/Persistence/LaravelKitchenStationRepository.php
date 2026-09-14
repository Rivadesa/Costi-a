<?php

declare(strict_types=1);

namespace App\Infrastructure\Persistence;

use Hospitality\Application\Contracts\KitchenStationRepository;
use Illuminate\Support\Facades\DB;

final class LaravelKitchenStationRepository implements KitchenStationRepository
{
    public function existsInScope(
        string $tenantId,
        string $companyId,
        string $locationId,
        string $stationId,
    ): bool {
        return DB::table('kitchen_stations')
            ->where('id', $stationId)
            ->where('tenant_id', $tenantId)
            ->where('company_id', $companyId)
            ->where('location_id', $locationId)
            ->where('active', true)
            ->exists();
    }
}
