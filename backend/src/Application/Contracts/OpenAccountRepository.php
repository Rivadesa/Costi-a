<?php

declare(strict_types=1);
namespace Hospitality\Application\Contracts;
interface OpenAccountRepository
{
    /** @return list<string> */
    public function ids(string $tenantId, string $companyId, string $locationId, int $offset, int $limit): array;
}
