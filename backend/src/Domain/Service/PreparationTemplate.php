<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

final readonly class PreparationTemplate
{
    public function __construct(
        public string $id,
        public string $name,
        public string $stationId,
        public PreparationQuantityMode $quantityMode = PreparationQuantityMode::PerGuest,
        public int $fixedQuantity = 1,
        public bool $mandatory = true,
    ) {
        if (trim($name) === '') {
            throw new \InvalidArgumentException('Preparation name cannot be empty.');
        }
        if (trim($stationId) === '') {
            throw new \InvalidArgumentException('Preparation stationId cannot be empty.');
        }
        if ($fixedQuantity < 1) {
            throw new \InvalidArgumentException('fixedQuantity must be >= 1');
        }
    }
}
