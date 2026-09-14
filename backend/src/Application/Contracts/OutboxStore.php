<?php

declare(strict_types=1);

namespace Hospitality\Application\Contracts;

use Hospitality\Domain\Shared\DomainEvent;

interface OutboxStore
{
    public function append(
        string $tenantId,
        string $companyId,
        string $locationId,
        DomainEvent $event,
    ): void;
}
