<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

final readonly class Restriction
{
    public function __construct(
        public string $id,
        public string $label,
        public RestrictionType $type,
        public RestrictionSeverity $severity,
        public ?string $notes = null,
    ) {
        if (trim($label) === '') {
            throw new \InvalidArgumentException('Restriction label cannot be empty.');
        }
    }
}
