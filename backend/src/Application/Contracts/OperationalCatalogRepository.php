<?php

declare(strict_types=1);

namespace Hospitality\Application\Contracts;

interface OperationalCatalogRepository
{
    /** @return list<array{id:string,code:string,name:string,capacity:int,area:string}> */
    public function tables(string $tenantId, string $companyId, string $locationId): array;

    /** @return list<array{id:string,name:string,price_cents:int,course_count:int}> */
    public function menus(string $tenantId, string $companyId, string $locationId): array;

    /** @return list<array{id:string,name:string}> */
    public function stations(string $tenantId, string $companyId, string $locationId): array;
}
