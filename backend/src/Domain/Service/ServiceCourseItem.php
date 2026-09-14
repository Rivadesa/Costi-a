<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

final class ServiceCourseItem
{
    public CourseItemStatus $status = CourseItemStatus::Pending;
    public ?\DateTimeImmutable $startedAt = null;
    public ?\DateTimeImmutable $readyAt = null;

    public function __construct(
        public readonly string $id,
        public readonly string $templateId,
        public readonly string $name,
        public readonly string $stationId,
        public readonly int $quantity,
        public readonly bool $required,
        public readonly ?int $guestPosition = null,
    ) {
        if ($quantity < 1) {
            throw new \InvalidArgumentException('Preparation quantity must be >= 1.');
        }
    }

    public function start(): void
    {
        if ($this->status !== CourseItemStatus::Pending) {
            throw new \DomainException('Only pending preparations can start.');
        }

        $this->status = CourseItemStatus::Started;
        $this->startedAt = new \DateTimeImmutable();
    }

    public function markReady(): void
    {
        if ($this->status !== CourseItemStatus::Started) {
            throw new \DomainException('Preparation must be started before it can be ready.');
        }

        $this->status = CourseItemStatus::Ready;
        $this->readyAt = new \DateTimeImmutable();
    }
}
