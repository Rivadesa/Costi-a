<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

final readonly class Payment
{
    public function __construct(
        public string $id,
        public string $method,
        public int $amountCents,
        public \DateTimeImmutable $recordedAt,
    ) {
        if (trim($method) === '') {
            throw new \InvalidArgumentException('Payment method cannot be empty.');
        }
        if ($amountCents <= 0) {
            throw new \InvalidArgumentException('Payment amount must be positive.');
        }
    }
}
