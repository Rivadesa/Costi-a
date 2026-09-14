<?php

declare(strict_types=1);

namespace Hospitality\Application\Contracts;

use Hospitality\Domain\Service\TableService;

interface TableServiceRepository
{
    public function get(
        string $tenantId,
        string $companyId,
        string $locationId,
        string $serviceId,
    ): TableService;

    public function save(TableService $service): void;
}
