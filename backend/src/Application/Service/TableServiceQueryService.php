<?php

declare(strict_types=1);

namespace Hospitality\Application\Service;

use Hospitality\Application\Contracts\TableServiceRepository;
use Hospitality\Application\ServiceBoard\TableServiceDetailProjector;
use Hospitality\Application\Shared\CommandContext;

final class TableServiceQueryService
{
    public function __construct(
        private readonly TableServiceRepository $services,
        private readonly TableServiceDetailProjector $projector,
    ) {
    }

    /** @return array<string, mixed> */
    public function detail(CommandContext $context, string $serviceId): array
    {
        $service = $this->services->get(
            $context->tenantId,
            $context->companyId,
            $context->locationId,
            $serviceId,
        );

        return $this->projector->project($service);
    }
}
