<?php

declare(strict_types=1);

namespace Hospitality\Application\Checkout;

use Hospitality\Application\Contracts\TableServiceRepository;
use Hospitality\Application\Shared\CommandContext;

final class CheckoutQueryService
{
    public function __construct(
        private readonly TableServiceRepository $services,
        private readonly CheckoutDetailProjector $projector,
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
