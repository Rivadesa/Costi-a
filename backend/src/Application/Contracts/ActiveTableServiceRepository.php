<?php

declare(strict_types=1);

namespace Hospitality\Application\Contracts;

use Hospitality\Domain\Service\TableService;

interface ActiveTableServiceRepository
{
    /** @return list<TableService> */
    public function listActive(
        string $tenantId,
        string $companyId,
        string $locationId,
    ): array;
}
