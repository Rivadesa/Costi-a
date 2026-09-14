<?php

declare(strict_types=1);

namespace Hospitality\Application\Shared;

final readonly class IdempotencyRecord
{
    /** @param array<string, mixed> $result */
    public function __construct(
        public string $tenantId,
        public string $idempotencyKey,
        public string $commandName,
        public string $requestHash,
        public array $result,
    ) {
    }
}
