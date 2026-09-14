<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

use Hospitality\Domain\Shared\Ulid;

final class ServiceCourse
{
    public CourseStatus $status = CourseStatus::Pending;
    public ?\DateTimeImmutable $firedAt = null;
    public ?\DateTimeImmutable $readyAt = null;
    public ?\DateTimeImmutable $servedAt = null;
    public ?string $skipReason = null;

    /** @var array<string, ServiceCourseItem> */
    private array $items = [];

    /**
     * @param list<PreparationTemplate> $preparations
     */
    public function __construct(
        public readonly string $id,
        public readonly string $templateId,
        public readonly int $sequence,
        public readonly string $name,
        array $preparations,
        int $pax,
        public readonly bool $extra = false,
    ) {
        if ($sequence < 1) {
            throw new \InvalidArgumentException('Course sequence must be >= 1.');
        }
        if ($pax < 1) {
            throw new \InvalidArgumentException('Course pax must be >= 1.');
        }

        foreach ($preparations as $template) {
            if ($template->perGuest) {
                for ($guestPosition = 1; $guestPosition <= $pax; $guestPosition++) {
                    $item = new ServiceCourseItem(
                        Ulid::generate(),
                        $template->id,
                        $template->name,
                        $template->stationId,
                        1,
                        $template->required,
                        $guestPosition,
                    );
                    $this->items[$item->id] = $item;
                }
                continue;
            }

            $item = new ServiceCourseItem(
                Ulid::generate(),
                $template->id,
                $template->name,
                $template->stationId,
                $pax,
                $template->required,
            );
            $this->items[$item->id] = $item;
        }
    }

    public function fire(): void
    {
        if ($this->status !== CourseStatus::Pending) {
            throw new \DomainException('Only pending courses can be fired.');
        }

        $this->status = CourseStatus::Fired;
        $this->firedAt = new \DateTimeImmutable();
    }

    public function startItem(string $itemId): ServiceCourseItem
    {
        if (!in_array($this->status, [CourseStatus::Fired, CourseStatus::Preparing], true)) {
            throw new \DomainException('Course is not available for preparation.');
        }

        $item = $this->item($itemId);
        $item->start();
        $this->status = CourseStatus::Preparing;

        return $item;
    }

    public function markItemReady(string $itemId): ServiceCourseItem
    {
        if (!in_array($this->status, [CourseStatus::Fired, CourseStatus::Preparing], true)) {
            throw new \DomainException('Course is not being prepared.');
        }

        $item = $this->item($itemId);
        $item->markReady();
        $this->status = CourseStatus::Preparing;

        return $item;
    }

    public function validateReady(): void
    {
        if (!in_array($this->status, [CourseStatus::Fired, CourseStatus::Preparing], true)) {
            throw new \DomainException('Course cannot be validated ready from its current state.');
        }

        if (!$this->allRequiredItemsReady()) {
            throw new \DomainException('Required preparations are still pending.');
        }

        $this->status = CourseStatus::Ready;
        $this->readyAt = new \DateTimeImmutable();
    }

    public function serve(): void
    {
        if ($this->status !== CourseStatus::Ready) {
            throw new \DomainException('Only ready courses can be served.');
        }

        $this->status = CourseStatus::Served;
        $this->servedAt = new \DateTimeImmutable();
    }

    public function skip(string $reason): void
    {
        if (trim($reason) === '') {
            throw new \InvalidArgumentException('Skip reason cannot be empty.');
        }
        if (in_array($this->status, [CourseStatus::Ready, CourseStatus::Served, CourseStatus::Skipped, CourseStatus::Cancelled], true)) {
            throw new \DomainException('Course cannot be skipped from its current state.');
        }

        $this->status = CourseStatus::Skipped;
        $this->skipReason = $reason;
    }

    public function allRequiredItemsReady(): bool
    {
        foreach ($this->items as $item) {
            if ($item->required && $item->status !== CourseItemStatus::Ready) {
                return false;
            }
        }

        return true;
    }

    /** @return list<ServiceCourseItem> */
    public function items(): array
    {
        return array_values($this->items);
    }

    private function item(string $itemId): ServiceCourseItem
    {
        return $this->items[$itemId] ?? throw new \DomainException('Preparation not found.');
    }
}
