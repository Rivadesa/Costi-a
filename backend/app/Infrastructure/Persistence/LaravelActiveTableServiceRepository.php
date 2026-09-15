<?php

declare(strict_types=1);

namespace App\Infrastructure\Persistence;

use Hospitality\Application\Contracts\ActiveTableServiceRepository;
use Hospitality\Application\Contracts\TableServiceRepository;
use Illuminate\Support\Facades\DB;

final class LaravelActiveTableServiceRepository implements ActiveTableServiceRepository
{
    public function __construct(private readonly TableServiceRepository $services)
    {
    }

    public function listActive(
        string $tenantId,
        string $companyId,
        string $locationId,
    ): array {
        $ids = DB::table('table_services')
            ->where('tenant_id', $tenantId)
            ->where('company_id', $companyId)
            ->where('location_id', $locationId)
            ->where('occupancy_status', 'occupied')
            ->orderBy('opened_at')
            ->pluck('id');

        $result = [];
        foreach ($ids as $id) {
            $result[] = $this->services->get($tenantId, $companyId, $locationId, (string) $id);
        }
        return $result;
    }
}
