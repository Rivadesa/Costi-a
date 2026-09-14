<?php

declare(strict_types=1);

namespace Hospitality\Domain\Shared;

final readonly class DomainEvent
{
    /** @param array<string, mixed> $payload */
    public function __construct(
        public string $id,
        public string $type,
        public string $aggregateType,
        public string $aggregateId,
        public array $payload,
        public \DateTimeImmutable $occurredAt,
    ) {
    }

    /** @param array<string, mixed> $payload */
    public static function record(
        string $type,
        string $aggregateType,
        string $aggregateId,
        array $payload = [],
    ): self {
        return new self(
            Ulid::generate(),
            $type,
            $aggregateType,
            $aggregateId,
            $payload,
            new \DateTimeImmutable(),
        );
    }
}
