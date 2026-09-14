<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

final class Guest
{
    /** @var array<string, Restriction> */
    private array $restrictions = [];

    public function __construct(
        public readonly string $id,
        public readonly int $position,
        public ?string $name = null,
    ) {
        if ($position < 1) {
            throw new \InvalidArgumentException('Guest position must be >= 1.');
        }
    }

    public function addRestriction(Restriction $restriction): void
    {
        $this->restrictions[$restriction->id] = $restriction;
    }

    /** @return list<Restriction> */
    public function restrictions(): array
    {
        return array_values($this->restrictions);
    }

    public function hasCriticalRestriction(): bool
    {
        foreach ($this->restrictions as $restriction) {
            if ($restriction->severity === RestrictionSeverity::Critical) {
                return true;
            }
        }

        return false;
    }
}
