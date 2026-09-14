<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

final readonly class PreparationTemplate
{
    public function __construct(
        public string $id,
        public string $name,
        public string $stationId,
        public bool $required = true,
        public bool $perGuest = false,
    ) {
        if (trim($name) === '') {
            throw new \InvalidArgumentException('Preparation name cannot be empty.');
        }
        if (trim($stationId) === '') {
            throw new \InvalidArgumentException('Preparation stationId cannot be empty.');
        }
    }
}
