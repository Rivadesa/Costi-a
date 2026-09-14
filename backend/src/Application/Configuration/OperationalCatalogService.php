<?php

declare(strict_types=1);

namespace Hospitality\Application\Configuration;

use Hospitality\Application\Contracts\OperationalCatalogRepository;
use Hospitality\Application\Shared\CommandContext;

final class OperationalCatalogService
{
    public function __construct(private readonly OperationalCatalogRepository $catalog)
    {
    }

    /** @return array<string, mixed> */
    public function forContext(CommandContext $context): array
    {
        return [
            'tables' => $this->catalog->tables($context->tenantId, $context->companyId, $context->locationId),
            'menus' => $this->catalog->menus($context->tenantId, $context->companyId, $context->locationId),
            'stations' => $this->catalog->stations($context->tenantId, $context->companyId, $context->locationId),
            'sale_categories' => $this->catalog->saleCategories($context->tenantId, $context->companyId, $context->locationId),
            'sale_items' => $this->catalog->saleItems($context->tenantId, $context->companyId, $context->locationId),
        ];
    }
}
