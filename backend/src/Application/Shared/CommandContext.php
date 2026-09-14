<?php

declare(strict_types=1);

namespace Hospitality\Application\Shared;

final readonly class CommandContext
{
    public function __construct(
        public string $tenantId,
        public string $companyId,
        public string $locationId,
        public ?string $userId = null,
        public ?string $deviceId = null,
    ) {
    }
}
