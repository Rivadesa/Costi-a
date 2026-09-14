<?php

declare(strict_types=1);

namespace Hospitality\Application\Contracts;

interface KitchenStationRepository
{
    public function existsInScope(
        string $tenantId,
        string $companyId,
        string $locationId,
        string $stationId,
    ): bool;
}
