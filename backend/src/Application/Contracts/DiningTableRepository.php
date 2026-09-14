<?php

declare(strict_types=1);

namespace Hospitality\Application\Contracts;

interface DiningTableRepository
{
    public function existsInScope(
        string $tenantId,
        string $companyId,
        string $locationId,
        string $tableId,
    ): bool;
}
