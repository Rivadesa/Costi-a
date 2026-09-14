<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

final class ServiceCourseItem
{
    public CourseItemStatus $status = CourseItemStatus::Pending;
    public ?\DateTimeImmutable $firedAt = null;
    public ?\DateTimeImmutable $startedAt = null;
    public ?\DateTimeImmutable $readyAt = null;
    public ?\DateTimeImmutable $cancelledAt = null;
    public ?string $cancelReason = null;
    public ?string $modificationReason = null;

    public function __construct(
        public readonly string $id,
        public readonly string $preparationTemplateId,
        public string $name,
        public string $stationId,
        public int $quantity = 1,
        public ?int $guestPosition = null,
        public bool $mandatory = true,
    ) {
        if ($quantity < 1) {
            throw new \InvalidArgumentException('quantity must be >= 1');
        }
    }

    public function fire(): void
    {
        if ($this->status !== CourseItemStatus::Pending) {
            throw new \DomainException('Only pending course items can be fired.');
        }
        $this->status = CourseItemStatus::Fired;
        $this->firedAt = new \DateTimeImmutable();
    }

    public function start(): void
    {
        if ($this->status !== CourseItemStatus::Fired) {
            throw new \DomainException('Only fired course items can start preparation.');
        }
        $this->status = CourseItemStatus::Preparing;
        $this->startedAt = new \DateTimeImmutable();
    }

    public function markReady(): void
    {
        if (!in_array($this->status, [CourseItemStatus::Fired, CourseItemStatus::Preparing], true)) {
            throw new \DomainException('Only fired/preparing course items can be marked ready.');
        }
        $this->status = CourseItemStatus::Ready;
        $this->readyAt = new \DateTimeImmutable();
    }

    public function cancel(string $reason): void
    {
        if ($this->status === CourseItemStatus::Ready) {
            throw new \DomainException('A ready course item cannot be cancelled.');
        }
        if ($this->status === CourseItemStatus::Cancelled) {
            throw new \DomainException('Course item already cancelled.');
        }
        if (trim($reason) === '') {
            throw new \InvalidArgumentException('Cancellation reason cannot be empty.');
        }
        $this->status = CourseItemStatus::Cancelled;
        $this->cancelReason = $reason;
        $this->cancelledAt = new \DateTimeImmutable();
    }

    public function substitute(string $name, string $stationId, string $reason): void
    {
        if (in_array($this->status, [CourseItemStatus::Ready, CourseItemStatus::Cancelled], true)) {
            throw new \DomainException('Ready/cancelled course items cannot be substituted.');
        }
        if (trim($name) === '' || trim($stationId) === '' || trim($reason) === '') {
            throw new \InvalidArgumentException('Substitution name, station and reason are required.');
        }
        $this->name = $name;
        $this->stationId = $stationId;
        $this->modificationReason = $reason;
    }

    public static function reconstitute(
        string $id,
        string $preparationTemplateId,
        string $name,
        string $stationId,
        int $quantity,
        ?int $guestPosition,
        bool $mandatory,
        CourseItemStatus $status,
        ?\DateTimeImmutable $firedAt,
        ?\DateTimeImmutable $startedAt,
        ?\DateTimeImmutable $readyAt,
        ?\DateTimeImmutable $cancelledAt,
        ?string $cancelReason,
        ?string $modificationReason,
    ): self {
        $item = new self($id, $preparationTemplateId, $name, $stationId, $quantity, $guestPosition, $mandatory);
        $item->status = $status;
        $item->firedAt = $firedAt;
        $item->startedAt = $startedAt;
        $item->readyAt = $readyAt;
        $item->cancelledAt = $cancelledAt;
        $item->cancelReason = $cancelReason;
        $item->modificationReason = $modificationReason;
        return $item;
    }
}
