<?php

declare(strict_types=1);

namespace Hospitality\Application\Contracts;

use Hospitality\Application\Shared\CommandContext;

interface CatalogAdminRepository
{
    /** @return array<string, mixed> */
    public function snapshot(CommandContext $context): array;

    /** Serialize low-volume administrative writes; call inside a transaction. */
    public function lockContext(CommandContext $context): void;

    /** @param array<string, mixed> $data @return array<string, mixed> */
    public function saveProduct(CommandContext $context, array $data): array;
}
