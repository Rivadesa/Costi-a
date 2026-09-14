<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

final readonly class CourseTemplate
{
    /** @param list<PreparationTemplate> $preparations */
    public function __construct(
        public string $id,
        public int $sequence,
        public string $name,
        public array $preparations,
    ) {
        if ($sequence < 1) {
            throw new \InvalidArgumentException('Course sequence must be >= 1.');
        }
        if (trim($name) === '') {
            throw new \InvalidArgumentException('Course name cannot be empty.');
        }
    }
}
