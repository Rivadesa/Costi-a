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
    public ?string $exceptionReason = null;

    /** @var array<string, ServiceCourseItem> */
    private array $items = [];

    /** @param list<PreparationTemplate> $preparations */
    public function __construct(
        public readonly string $id,
        public readonly string $templateCourseId,
        public readonly int $sequence,
        public string $name,
        array $preparations,
        int $pax,
        public bool $extra = false,
    ) {
        if ($sequence < 1) {
            throw new \InvalidArgumentException('course sequence must be >= 1');
        }
        if ($pax < 1) {
            throw new \InvalidArgumentException('pax must be >= 1');
        }

        foreach ($preparations as $preparation) {
            if ($preparation->quantityMode === PreparationQuantityMode::PerGuest) {
                for ($position = 1; $position <= $pax; $position++) {
                    $item = new ServiceCourseItem(
                        Ulid::generate(),
                        $preparation->id,
                        $preparation->name,
                        $preparation->stationId,
                        1,
                        $position,
                        $preparation->mandatory,
                    );
                    $this->items[$item->id] = $item;
                }
            } else {
                $item = new ServiceCourseItem(
                    Ulid::generate(),
                    $preparation->id,
                    $preparation->name,
                    $preparation->stationId,
                    $preparation->fixedQuantity,
                    null,
                    $preparation->mandatory,
                );
                $this->items[$item->id] = $item;
            }
        }
    }

    public function fire(): void
    {
        if ($this->status !== CourseStatus::Pending) {
            throw new \DomainException('Only pending courses can be fired.');
        }
        $this->status = CourseStatus::Fired;
        $this->firedAt = new \DateTimeImmutable();
        foreach ($this->items as $item) {
            $item->fire();
        }
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
            throw new \DomainException('Course is not available for preparation.');
        }
        $item = $this->item($itemId);
        $item->markReady();
        if ($this->status === CourseStatus::Fired) {
            $this->status = CourseStatus::Preparing;
        }
        return $item;
    }

    public function validateReady(): void
    {
        if (!in_array($this->status, [CourseStatus::Fired, CourseStatus::Preparing], true)) {
            throw new \DomainException('Only fired/preparing courses can be validated ready.');
        }
        if (!$this->allMandatoryItemsReady()) {
            throw new \DomainException('All mandatory preparations must be ready before validating the course.');
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
        if (!in_array($this->status, [CourseStatus::Pending, CourseStatus::Fired], true)) {
            throw new \DomainException('This course can no longer be skipped.');
        }
        foreach ($this->items as $item) {
            if ($item->status === CourseItemStatus::Fired) {
                $item->cancel('Course skipped: ' . $reason);
            }
        }
        $this->status = CourseStatus::Skipped;
        $this->exceptionReason = $reason;
    }

    public function substituteItem(string $itemId, string $name, string $stationId, string $reason): ServiceCourseItem
    {
        $item = $this->item($itemId);
        $item->substitute($name, $stationId, $reason);
        return $item;
    }

    /** @return list<ServiceCourseItem> */
    public function items(): array
    {
        return array_values($this->items);
    }

    /** @return list<ServiceCourseItem> */
    public function itemsForStation(string $stationId): array
    {
        return array_values(array_filter(
            $this->items,
            fn (ServiceCourseItem $item): bool => $item->stationId === $stationId,
        ));
    }

    public function allMandatoryItemsReady(): bool
    {
        foreach ($this->items as $item) {
            if (!$item->mandatory) {
                continue;
            }
            if (!in_array($item->status, [CourseItemStatus::Ready, CourseItemStatus::Cancelled], true)) {
                return false;
            }
        }
        return true;
    }

    /** @param list<ServiceCourseItem> $items */
    public static function reconstitute(
        string $id,
        string $templateCourseId,
        int $sequence,
        string $name,
        bool $extra,
        CourseStatus $status,
        ?\DateTimeImmutable $firedAt,
        ?\DateTimeImmutable $readyAt,
        ?\DateTimeImmutable $servedAt,
        ?string $exceptionReason,
        array $items,
    ): self {
        $course = new self($id, $templateCourseId, $sequence, $name, [], 1, $extra);
        $course->status = $status;
        $course->firedAt = $firedAt;
        $course->readyAt = $readyAt;
        $course->servedAt = $servedAt;
        $course->exceptionReason = $exceptionReason;
        foreach ($items as $item) {
            $course->items[$item->id] = $item;
        }
        return $course;
    }

    private function item(string $itemId): ServiceCourseItem
    {
        return $this->items[$itemId] ?? throw new \DomainException('Course item not found.');
    }
}
