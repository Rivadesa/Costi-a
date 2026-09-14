<?php

declare(strict_types=1);

namespace Hospitality\Application\ServiceBoard;

use Hospitality\Application\Contracts\ActiveTableServiceRepository;
use Hospitality\Application\Contracts\KitchenStationRepository;
use Hospitality\Application\Kitchen\KdsProjector;
use Hospitality\Application\Shared\CommandContext;

final class OperationalReadService
{
    public function __construct(
        private readonly ActiveTableServiceRepository $services,
        private readonly KitchenStationRepository $stations,
        private readonly ServiceBoardProjector $board,
        private readonly KdsProjector $kds,
    ) {
    }

    /** @return list<array<string, mixed>> */
    public function serviceBoard(CommandContext $context): array
    {
        return $this->board->project($this->active($context));
    }

    /** @return list<array<string, mixed>> */
    public function stationQueue(CommandContext $context, string $stationId): array
    {
        if (!$this->stations->existsInScope($context->tenantId, $context->companyId, $context->locationId, $stationId)) {
            throw new \DomainException('Kitchen station not found in the requested tenant/company/location.');
        }

        return $this->kds->stationQueue($this->active($context), $stationId);
    }

    private function active(CommandContext $context): array
    {
        return $this->services->listActive($context->tenantId, $context->companyId, $context->locationId);
    }
}
