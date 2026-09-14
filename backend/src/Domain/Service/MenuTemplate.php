<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

final readonly class MenuTemplate
{
    /** @param list<CourseTemplate> $courses */
    public function __construct(
        public string $id,
        public string $name,
        public int $priceCents,
        public array $courses,
    ) {
        if (trim($name) === '') {
            throw new \InvalidArgumentException('Menu name cannot be empty.');
        }
        if ($priceCents < 0) {
            throw new \InvalidArgumentException('Menu price cannot be negative.');
        }
        if ($courses === []) {
            throw new \InvalidArgumentException('Menu must contain at least one course.');
        }
    }
}
