<?php

declare(strict_types=1);

namespace Hospitality\Application\Contracts;

use Hospitality\Application\Shared\IdempotencyRecord;

interface IdempotencyStore
{
    public function find(string $tenantId, string $idempotencyKey): ?IdempotencyRecord;

    /** @param array<string, mixed> $result */
    public function remember(
        string $tenantId,
        string $idempotencyKey,
        string $commandName,
        string $requestHash,
        array $result,
    ): void;
}
