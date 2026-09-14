<?php

declare(strict_types=1);

namespace Hospitality\Application\Contracts;

use Hospitality\Domain\Service\MenuTemplate;

interface MenuTemplateRepository
{
    public function get(
        string $tenantId,
        string $companyId,
        string $locationId,
        string $menuId,
    ): MenuTemplate;
}
