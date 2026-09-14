<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

use Hospitality\Domain\Shared\DomainEvent;
use Hospitality\Domain\Shared\Ulid;

final class TableService
{
    public ServiceStatus $status = ServiceStatus::Open;
    private ?MenuTemplate $menu = null;
    /** @var array<string, Guest> */
    private array $guests = [];
    /** @var array<string, ServiceCourse> */
    private array $courses = [];
    /** @var array<string, Consumption> */
    private array $consumptions = [];
    /** @var array<string, Payment> */
    private array $payments = [];
    /** @var list<DomainEvent> */
    private array $events = [];

    private function __construct(
        public readonly string $id,
        public readonly string $tenantId,
        public readonly string $companyId,
        public readonly string $locationId,
        public string $tableId,
        public int $pax,
        public readonly \DateTimeImmutable $openedAt,
    ) {
        if ($pax < 1) {
            throw new \InvalidArgumentException('pax must be >= 1');
        }
    }

    public static function open(
        string $id,
        string $tenantId,
        string $companyId,
        string $locationId,
        string $tableId,
        int $pax,
        \DateTimeImmutable $openedAt,
    ): self {
        $service = new self($id, $tenantId, $companyId, $locationId, $tableId, $pax, $openedAt);
        $service->record('service.created', ['table_id' => $tableId, 'pax' => $pax]);
        return $service;
    }

    /**
     * Rebuild an existing aggregate from persistence without emitting new domain events.
     *
     * @param list<Guest> $guests
     * @param list<ServiceCourse> $courses
     * @param list<Consumption> $consumptions
     * @param list<Payment> $payments
     */
    public static function reconstitute(
        string $id,
        string $tenantId,
        string $companyId,
        string $locationId,
        string $tableId,
        int $pax,
        \DateTimeImmutable $openedAt,
        ServiceStatus $status,
        ?MenuTemplate $menu,
        array $guests,
        array $courses,
        array $consumptions,
        array $payments,
    ): self {
        $service = new self($id, $tenantId, $companyId, $locationId, $tableId, $pax, $openedAt);
        $service->status = $status;
        $service->menu = $menu;

        foreach ($guests as $guest) {
            $service->guests[$guest->id] = $guest;
        }
        foreach ($courses as $course) {
            $service->courses[$course->id] = $course;
        }
        foreach ($consumptions as $consumption) {
            $service->consumptions[$consumption->id] = $consumption;
        }
        foreach ($payments as $payment) {
            $service->payments[$payment->id] = $payment;
        }

        return $service;
    }

    public function assignMenu(MenuTemplate $menu): void
    {
        if ($this->menu !== null) {
            throw new \DomainException('A menu is already assigned to this service.');
        }
        $this->menu = $menu;
        foreach ($menu->courses as $template) {
            $course = new ServiceCourse(Ulid::generate(), $template->id, $template->sequence, $template->name, $template->preparations, $this->pax);
            $this->courses[$course->id] = $course;
        }
        $this->record('menu.assigned', ['menu_id' => $menu->id, 'menu_name' => $menu->name]);
    }

    public function addGuest(?string $name = null): Guest
    {
        $position = count($this->guests) + 1;
        if ($position > $this->pax) {
            throw new \DomainException('Cannot add more guests than service pax.');
        }
        $guest = new Guest(Ulid::generate(), $position, $name);
        $this->guests[$guest->id] = $guest;
        $this->record('service.guest_added', ['guest_id' => $guest->id, 'position' => $position]);
        return $guest;
    }

    public function addRestriction(string $guestId, Restriction $restriction): void
    {
        $guest = $this->guests[$guestId] ?? throw new \DomainException('Guest not found.');
        $guest->addRestriction($restriction);
        $this->record('service.restriction_added', [
            'guest_id' => $guestId,
            'restriction_id' => $restriction->id,
            'label' => $restriction->label,
            'type' => $restriction->type->value,
            'severity' => $restriction->severity->value,
        ]);
    }

    public function start(): void
    {
        if ($this->status !== ServiceStatus::Open) {
            throw new \DomainException('Only open services can start.');
        }
        if ($this->menu === null) {
            throw new \DomainException('A menu must be assigned before service starts.');
        }
        $this->status = ServiceStatus::InService;
        $this->record('service.started');
    }

    public function fireNextCourse(): ServiceCourse
    {
        $this->assertOperable();
        foreach ($this->orderedCourses() as $course) {
            if ($course->status === CourseStatus::Pending) {
                $course->fire();
                $this->record('course.fired', ['course_id' => $course->id, 'sequence' => $course->sequence, 'name' => $course->name]);
                return $course;
            }
            if (in_array($course->status, [CourseStatus::Fired, CourseStatus::Preparing, CourseStatus::Ready], true)) {
                throw new \DomainException('Cannot fire the next course while the current course is unfinished.');
            }
        }
        throw new \DomainException('There are no pending courses.');
    }

    public function startCourseItem(string $courseId, string $itemId): void
    {
        $course = $this->course($courseId);
        $item = $course->startItem($itemId);
        $this->record('course_item.started', [
            'course_id' => $courseId,
            'item_id' => $itemId,
            'station_id' => $item->stationId,
            'guest_position' => $item->guestPosition,
        ]);
    }

    public function markCourseItemReady(string $courseId, string $itemId): void
    {
        $course = $this->course($courseId);
        $item = $course->markItemReady($itemId);
        $this->record('course_item.ready', [
            'course_id' => $courseId,
            'item_id' => $itemId,
            'station_id' => $item->stationId,
            'guest_position' => $item->guestPosition,
        ]);
    }

    public function markCourseReady(string $courseId): void
    {
        $course = $this->course($courseId);
        $course->validateReady();
        $this->record('course.ready', ['course_id' => $courseId]);
    }

    public function serveCourse(string $courseId): void
    {
        $course = $this->course($courseId);
        $course->serve();
        $this->record('course.served', ['course_id' => $courseId]);
    }

    public function skipCourse(string $courseId, string $reason): void
    {
        $course = $this->course($courseId);
        $course->skip($reason);
        $this->record('course.skipped', ['course_id' => $courseId, 'reason' => $reason]);
    }

    public function substituteCourseItem(string $courseId, string $itemId, string $name, string $stationId, string $reason): void
    {
        $course = $this->course($courseId);
        $item = $course->substituteItem($itemId, $name, $stationId, $reason);
        $this->record('course_item.substituted', [
            'course_id' => $courseId,
            'item_id' => $itemId,
            'name' => $item->name,
            'station_id' => $item->stationId,
            'reason' => $reason,
        ]);
    }

    /** @param list<PreparationTemplate> $preparations */
    public function addExtraCourse(string $name, array $preparations = []): ServiceCourse
    {
        $sequence = count($this->courses) + 1;
        $course = new ServiceCourse(Ulid::generate(), 'extra', $sequence, $name, $preparations, $this->pax, true);
        $this->courses[$course->id] = $course;
        $this->record('course.extra_added', ['course_id' => $course->id, 'name' => $name]);
        return $course;
    }

    public function pause(?string $reason = null): void
    {
        if ($this->status !== ServiceStatus::InService) {
            throw new \DomainException('Only an active service can be paused.');
        }
        $this->status = ServiceStatus::Paused;
        $this->record('service.paused', ['reason' => $reason]);
    }

    public function resume(): void
    {
        if ($this->status !== ServiceStatus::Paused) {
            throw new \DomainException('Only a paused service can resume.');
        }
        $this->status = ServiceStatus::InService;
        $this->record('service.resumed');
    }

    public function moveTable(string $tableId): void
    {
        $before = $this->tableId;
        $this->tableId = $tableId;
        $this->record('service.table_changed', ['from' => $before, 'to' => $tableId]);
    }

    public function addConsumption(string $name, int $quantity, int $unitPriceCents): Consumption
    {
        if ($quantity < 1 || $unitPriceCents < 0) {
            throw new \InvalidArgumentException('Invalid consumption quantity/price.');
        }
        if (in_array($this->status, [ServiceStatus::Closed, ServiceStatus::Cancelled], true)) {
            throw new \DomainException('Cannot add consumption to a closed service.');
        }
        $consumption = new Consumption(Ulid::generate(), $name, $quantity, $unitPriceCents);
        $this->consumptions[$consumption->id] = $consumption;
        $this->record('consumption.added', ['consumption_id' => $consumption->id, 'name' => $name, 'quantity' => $quantity, 'unit_price_cents' => $unitPriceCents]);
        return $consumption;
    }

    public function cancelConsumption(string $consumptionId, string $reason): void
    {
        $consumption = $this->consumptions[$consumptionId] ?? throw new \DomainException('Consumption not found.');
        $consumption->cancel($reason);
        $this->record('consumption.cancelled', ['consumption_id' => $consumptionId, 'reason' => $reason]);
    }

    public function subtotalCents(): int
    {
        $menuTotal = $this->menu ? $this->menu->priceCents * $this->pax : 0;
        $consumptionTotal = array_sum(array_map(fn (Consumption $c) => $c->totalCents(), $this->consumptions));
        return $menuTotal + $consumptionTotal;
    }

    public function recordPayment(string $method, int $amountCents): Payment
    {
        if ($amountCents <= 0) {
            throw new \InvalidArgumentException('Payment amount must be positive.');
        }
        if (in_array($this->status, [ServiceStatus::Closed, ServiceStatus::Cancelled], true)) {
            throw new \DomainException('Cannot record payment on a closed/cancelled service.');
        }
        $payment = new Payment(Ulid::generate(), $method, $amountCents, new \DateTimeImmutable());
        $this->payments[$payment->id] = $payment;
        $this->record('payment.recorded', ['payment_id' => $payment->id, 'method' => $method, 'amount_cents' => $amountCents]);
        if ($this->paidCents() >= $this->subtotalCents()) {
            $this->status = ServiceStatus::Paid;
        } else {
            $this->status = ServiceStatus::PendingPayment;
        }
        return $payment;
    }

    public function paidCents(): int
    {
        return array_sum(array_map(fn (Payment $p) => $p->amountCents, $this->payments));
    }

    public function close(): void
    {
        if ($this->status !== ServiceStatus::Paid) {
            throw new \DomainException('Service must be fully paid before closing.');
        }
        if ($this->hasActiveCourse()) {
            throw new \DomainException('Cannot close service with active kitchen courses.');
        }
        $this->status = ServiceStatus::Closed;
        $this->record('service.closed');
    }

    public function cancel(string $reason): void
    {
        if (trim($reason) === '') {
            throw new \InvalidArgumentException('Cancellation reason cannot be empty.');
        }
        if ($this->status === ServiceStatus::Closed) {
            throw new \DomainException('Closed services cannot be operationally cancelled.');
        }
        $this->status = ServiceStatus::Cancelled;
        $this->record('service.cancelled', ['reason' => $reason]);
    }

    public function menu(): ?MenuTemplate
    {
        return $this->menu;
    }

    public function currentCourse(): ?ServiceCourse
    {
        $ordered = $this->orderedCourses();
        foreach ($ordered as $course) {
            if (in_array($course->status, [CourseStatus::Fired, CourseStatus::Preparing, CourseStatus::Ready], true)) {
                return $course;
            }
        }

        $progressed = array_values(array_filter(
            $ordered,
            fn (ServiceCourse $course): bool => in_array($course->status, [CourseStatus::Served, CourseStatus::Skipped], true),
        ));
        if ($progressed !== []) {
            return $progressed[array_key_last($progressed)];
        }

        return $ordered[0] ?? null;
    }

    /** @return list<Restriction> */
    public function restrictionsForGuestPosition(int $position): array
    {
        foreach ($this->guests as $guest) {
            if ($guest->position === $position) {
                return $guest->restrictions();
            }
        }
        return [];
    }

    /** @return list<Guest> */
    public function guests(): array
    {
        return array_values($this->guests);
    }

    /** @return list<ServiceCourse> */
    public function courses(): array
    {
        return $this->orderedCourses();
    }

    /** @return list<Consumption> */
    public function consumptions(): array
    {
        return array_values($this->consumptions);
    }

    /** @return list<Payment> */
    public function payments(): array
    {
        return array_values($this->payments);
    }

    /** @return list<DomainEvent> */
    public function pullEvents(): array
    {
        $events = $this->events;
        $this->events = [];
        return $events;
    }

    private function course(string $courseId): ServiceCourse
    {
        return $this->courses[$courseId] ?? throw new \DomainException('Course not found.');
    }

    /** @return list<ServiceCourse> */
    private function orderedCourses(): array
    {
        $courses = array_values($this->courses);
        usort($courses, fn (ServiceCourse $a, ServiceCourse $b) => $a->sequence <=> $b->sequence);
        return $courses;
    }

    private function hasActiveCourse(): bool
    {
        foreach ($this->courses as $course) {
            if (in_array($course->status, [CourseStatus::Fired, CourseStatus::Preparing, CourseStatus::Ready], true)) {
                return true;
            }
        }
        return false;
    }

    private function assertOperable(): void
    {
        if (!in_array($this->status, [ServiceStatus::InService, ServiceStatus::Paused], true)) {
            throw new \DomainException('Service is not active.');
        }
        if ($this->status === ServiceStatus::Paused) {
            throw new \DomainException('A paused service cannot fire a course.');
        }
    }

    /** @param array<string, mixed> $payload */
    private function record(string $type, array $payload = []): void
    {
        $this->events[] = DomainEvent::record($type, 'table_service', $this->id, $payload);
    }
}
