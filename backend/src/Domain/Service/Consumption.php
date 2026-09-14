<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

final class Consumption
{
    public ?\DateTimeImmutable $cancelledAt = null;
    public ?string $cancelReason = null;
    public ?string $productId = null;
    public ?string $priceListId = null;

    public function __construct(
        public readonly string $id,
        public readonly string $name,
        public readonly int $quantity,
        public readonly int $unitPriceCents,
    ) {
        if (trim($name) === '') {
            throw new \InvalidArgumentException('Consumption name cannot be empty.');
        }
        if ($quantity < 1) {
            throw new \InvalidArgumentException('Consumption quantity must be >= 1.');
        }
        if ($unitPriceCents < 0) {
            throw new \InvalidArgumentException('Consumption unit price cannot be negative.');
        }
    }

    public function linkCatalog(string $productId, string $priceListId): void
    {
        if (trim($productId) === '' || trim($priceListId) === '') {
            throw new \InvalidArgumentException('Catalog product and price list ids are required.');
        }

        if ($this->productId !== null && ($this->productId !== $productId || $this->priceListId !== $priceListId)) {
            throw new \DomainException('Consumption is already linked to a different catalog item or price list.');
        }

        $this->productId = $productId;
        $this->priceListId = $priceListId;
    }

    public function cancel(string $reason): void
    {
        if ($this->cancelledAt !== null) {
            throw new \DomainException('Consumption is already cancelled.');
        }
        if (trim($reason) === '') {
            throw new \InvalidArgumentException('Cancellation reason cannot be empty.');
        }

        $this->cancelledAt = new \DateTimeImmutable();
        $this->cancelReason = $reason;
    }

    public function isCancelled(): bool
    {
        return $this->cancelledAt !== null;
    }

    public function totalCents(): int
    {
        return $this->isCancelled() ? 0 : $this->quantity * $this->unitPriceCents;
    }
}
