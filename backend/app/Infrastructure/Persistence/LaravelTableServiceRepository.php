<?php

declare(strict_types=1);

namespace App\Infrastructure\Persistence;

use Hospitality\Application\Contracts\TableServiceRepository;
use Hospitality\Application\Shared\ConcurrencyConflict;
use Hospitality\Domain\Service\Consumption;
use Hospitality\Domain\Service\CourseItemStatus;
use Hospitality\Domain\Service\CourseStatus;
use Hospitality\Domain\Service\CourseTemplate;
use Hospitality\Domain\Service\Guest;
use Hospitality\Domain\Service\MenuTemplate;
use Hospitality\Domain\Service\Payment;
use Hospitality\Domain\Service\Restriction;
use Hospitality\Domain\Service\RestrictionSeverity;
use Hospitality\Domain\Service\RestrictionType;
use Hospitality\Domain\Service\ServiceCourse;
use Hospitality\Domain\Service\ServiceCourseItem;
use Hospitality\Domain\Service\ServiceStatus;
use Hospitality\Domain\Service\TableService;
use Hospitality\Domain\Shared\Ulid;
use Illuminate\Support\Facades\DB;

final class LaravelTableServiceRepository implements TableServiceRepository
{
    /** @var \WeakMap<TableService, int> */
    private \WeakMap $loadedVersions;

    /** @var \WeakMap<TableService, array{started_at:mixed,closed_at:mixed,cancelled_at:mixed,cancel_reason:mixed}> */
    private \WeakMap $lifecycle;

    public function __construct()
    {
        $this->loadedVersions = new \WeakMap();
        $this->lifecycle = new \WeakMap();
    }

    public function get(
        string $tenantId,
        string $companyId,
        string $locationId,
        string $serviceId,
    ): TableService {
        $row = DB::table('table_services')
            ->where('id', $serviceId)
            ->where('tenant_id', $tenantId)
            ->where('company_id', $companyId)
            ->where('location_id', $locationId)
            ->first();

        if ($row === null) {
            throw new \DomainException('Table service not found in the requested tenant/company/location.');
        }

        $courses = $this->loadCourses($serviceId);
        $guests = $this->loadGuests($serviceId);
        $consumptions = $this->loadConsumptions($serviceId);
        $payments = $this->loadPayments($serviceId);
        $menu = $this->loadMenu($serviceId, $courses);

        $service = TableService::reconstitute(
            (string) $row->id,
            (string) $row->tenant_id,
            (string) $row->company_id,
            (string) $row->location_id,
            (string) $row->dining_table_id,
            (int) $row->pax,
            $this->dateTime($row->opened_at) ?? throw new \RuntimeException('Service opened_at is required.'),
            ServiceStatus::from((string) $row->status),
            $menu,
            $guests,
            $courses,
            $consumptions,
            $payments,
        );

        $this->loadedVersions[$service] = (int) $row->version;
        $this->lifecycle[$service] = [
            'started_at' => $row->started_at,
            'closed_at' => $row->closed_at,
            'cancelled_at' => $row->cancelled_at,
            'cancel_reason' => $row->cancel_reason,
        ];

        return $service;
    }

    public function save(TableService $service): void
    {
        $existing = DB::table('table_services')
            ->where('id', $service->id)
            ->where('tenant_id', $service->tenantId)
            ->where('company_id', $service->companyId)
            ->where('location_id', $service->locationId)
            ->first();

        $now = now();
        $meta = $this->lifecycle[$service] ?? [
            'started_at' => null,
            'closed_at' => null,
            'cancelled_at' => null,
            'cancel_reason' => null,
        ];

        $startedAt = $meta['started_at'];
        if ($startedAt === null && !in_array($service->status, [ServiceStatus::Prepared, ServiceStatus::Open], true)) {
            $startedAt = $now;
        }

        $closedAt = $meta['closed_at'];
        if ($closedAt === null && $service->status === ServiceStatus::Closed) {
            $closedAt = $now;
        }

        $cancelledAt = $meta['cancelled_at'];
        if ($cancelledAt === null && $service->status === ServiceStatus::Cancelled) {
            $cancelledAt = $now;
        }

        if ($existing === null) {
            DB::table('table_services')->insert([
                'id' => $service->id,
                'tenant_id' => $service->tenantId,
                'company_id' => $service->companyId,
                'location_id' => $service->locationId,
                'dining_table_id' => $service->tableId,
                'status' => $service->status->value,
                'pax' => $service->pax,
                'opened_at' => $service->openedAt,
                'started_at' => $startedAt,
                'closed_at' => $closedAt,
                'cancelled_at' => $cancelledAt,
                'cancel_reason' => $meta['cancel_reason'],
                'version' => 1,
                'created_by' => null,
                'updated_at' => $now,
            ]);
            $this->loadedVersions[$service] = 1;
        } else {
            if (!isset($this->loadedVersions[$service])) {
                throw new ConcurrencyConflict('Existing TableService must be loaded by this repository before it can be saved.');
            }

            $expectedVersion = $this->loadedVersions[$service];
            $nextVersion = $expectedVersion + 1;

            $affected = DB::table('table_services')
                ->where('id', $service->id)
                ->where('tenant_id', $service->tenantId)
                ->where('company_id', $service->companyId)
                ->where('location_id', $service->locationId)
                ->where('version', $expectedVersion)
                ->update([
                    'dining_table_id' => $service->tableId,
                    'status' => $service->status->value,
                    'pax' => $service->pax,
                    'started_at' => $startedAt,
                    'closed_at' => $closedAt,
                    'cancelled_at' => $cancelledAt,
                    'cancel_reason' => $meta['cancel_reason'],
                    'version' => $nextVersion,
                    'updated_at' => $now,
                ]);

            if ($affected !== 1) {
                throw new ConcurrencyConflict('TableService changed on another terminal. Reload and retry the command.');
            }

            $this->loadedVersions[$service] = $nextVersion;
        }

        $this->lifecycle[$service] = [
            'started_at' => $startedAt,
            'closed_at' => $closedAt,
            'cancelled_at' => $cancelledAt,
            'cancel_reason' => $meta['cancel_reason'],
        ];

        $this->saveMenu($service);
        $this->saveGuests($service);
        $this->saveCourses($service);
        $this->saveConsumptions($service);
        $this->savePayments($service);
    }

    /** @return list<ServiceCourse> */
    private function loadCourses(string $serviceId): array
    {
        $courseRows = DB::table('service_courses')
            ->where('table_service_id', $serviceId)
            ->orderBy('sequence')
            ->get();

        if ($courseRows->isEmpty()) {
            return [];
        }

        $courseIds = $courseRows->pluck('id')->all();
        $itemsByCourse = [];

        foreach (DB::table('service_course_items')->whereIn('service_course_id', $courseIds)->get() as $row) {
            $item = ServiceCourseItem::reconstitute(
                (string) $row->id,
                (string) ($row->preparation_template_id ?? 'snapshot'),
                (string) $row->name,
                (string) $row->station_id,
                (int) $row->quantity,
                $row->guest_position === null ? null : (int) $row->guest_position,
                (bool) $row->mandatory,
                CourseItemStatus::from((string) $row->status),
                $this->dateTime($row->fired_at),
                $this->dateTime($row->started_at),
                $this->dateTime($row->ready_at),
                $this->dateTime($row->cancelled_at),
                $row->cancel_reason === null ? null : (string) $row->cancel_reason,
                $row->modification_reason === null ? null : (string) $row->modification_reason,
            );
            $itemsByCourse[(string) $row->service_course_id][] = $item;
        }

        $courses = [];
        foreach ($courseRows as $row) {
            $courses[] = ServiceCourse::reconstitute(
                (string) $row->id,
                (string) ($row->course_template_id ?? 'extra'),
                (int) $row->sequence,
                (string) $row->name,
                (bool) $row->is_extra,
                CourseStatus::from((string) $row->status),
                $this->dateTime($row->fired_at),
                $this->dateTime($row->ready_at),
                $this->dateTime($row->served_at),
                $row->exception_reason === null ? null : (string) $row->exception_reason,
                $itemsByCourse[(string) $row->id] ?? [],
            );
        }

        return $courses;
    }

    /** @return list<Guest> */
    private function loadGuests(string $serviceId): array
    {
        $guestRows = DB::table('service_guests')
            ->where('table_service_id', $serviceId)
            ->orderBy('position')
            ->get();

        if ($guestRows->isEmpty()) {
            return [];
        }

        $restrictionRows = DB::table('guest_restrictions')
            ->whereIn('service_guest_id', $guestRows->pluck('id')->all())
            ->get();
        $restrictionsByGuest = [];

        foreach ($restrictionRows as $row) {
            $restrictionsByGuest[(string) $row->service_guest_id][] = new Restriction(
                (string) $row->id,
                (string) $row->label,
                RestrictionType::from((string) $row->type),
                RestrictionSeverity::from((string) $row->severity),
                $row->notes === null ? null : (string) $row->notes,
            );
        }

        $guests = [];
        foreach ($guestRows as $row) {
            $guest = new Guest(
                (string) $row->id,
                (int) $row->position,
                $row->name === null ? null : (string) $row->name,
            );
            foreach ($restrictionsByGuest[(string) $row->id] ?? [] as $restriction) {
                $guest->addRestriction($restriction);
            }
            $guests[] = $guest;
        }

        return $guests;
    }

    /** @return list<Consumption> */
    private function loadConsumptions(string $serviceId): array
    {
        $consumptions = [];
        foreach (DB::table('consumptions')->where('table_service_id', $serviceId)->orderBy('created_at')->get() as $row) {
            $consumption = new Consumption(
                (string) $row->id,
                (string) $row->name,
                (int) $row->quantity,
                (int) $row->unit_price_cents,
            );
            $consumption->cancelledAt = $this->dateTime($row->cancelled_at);
            $consumption->cancelReason = $row->cancel_reason === null ? null : (string) $row->cancel_reason;
            $consumptions[] = $consumption;
        }
        return $consumptions;
    }

    /** @return list<Payment> */
    private function loadPayments(string $serviceId): array
    {
        $payments = [];
        foreach (DB::table('payments')->where('table_service_id', $serviceId)->orderBy('recorded_at')->get() as $row) {
            $payments[] = new Payment(
                (string) $row->id,
                (string) $row->method,
                (int) $row->amount_cents,
                $this->dateTime($row->recorded_at) ?? throw new \RuntimeException('Payment recorded_at is required.'),
            );
        }
        return $payments;
    }

    /** @param list<ServiceCourse> $courses */
    private function loadMenu(string $serviceId, array $courses): ?MenuTemplate
    {
        $row = DB::table('service_menus')->where('table_service_id', $serviceId)->first();
        if ($row === null) {
            return null;
        }
        if ($courses === []) {
            throw new \RuntimeException('Assigned menu snapshot has no service courses.');
        }

        $courseTemplates = array_map(
            static fn (ServiceCourse $course): CourseTemplate => new CourseTemplate(
                $course->templateCourseId,
                $course->sequence,
                $course->name,
                [],
            ),
            $courses,
        );

        return new MenuTemplate(
            (string) ($row->menu_template_id ?? $serviceId),
            (string) $row->menu_name,
            (int) $row->unit_price_cents,
            $courseTemplates,
        );
    }

    private function saveMenu(TableService $service): void
    {
        $menu = $service->menu();
        if ($menu === null) {
            return;
        }

        DB::table('service_menus')->upsert([[
            'id' => Ulid::generate(),
            'table_service_id' => $service->id,
            'menu_template_id' => $menu->id,
            'menu_name' => $menu->name,
            'unit_price_cents' => $menu->priceCents,
            'currency' => 'EUR',
            'quantity' => $service->pax,
            'assigned_at' => now(),
        ]], ['table_service_id'], ['menu_template_id', 'menu_name', 'unit_price_cents', 'currency', 'quantity']);
    }

    private function saveGuests(TableService $service): void
    {
        foreach ($service->guests() as $guest) {
            DB::table('service_guests')->upsert([[
                'id' => $guest->id,
                'table_service_id' => $service->id,
                'position' => $guest->position,
                'name' => $guest->name,
                'customer_id' => null,
            ]], ['id'], ['position', 'name']);

            foreach ($guest->restrictions() as $restriction) {
                DB::table('guest_restrictions')->upsert([[
                    'id' => $restriction->id,
                    'service_guest_id' => $guest->id,
                    'label' => $restriction->label,
                    'type' => $restriction->type->value,
                    'severity' => $restriction->severity->value,
                    'notes' => $restriction->notes,
                    'created_at' => now(),
                ]], ['id'], ['label', 'type', 'severity', 'notes']);
            }
        }
    }

    private function saveCourses(TableService $service): void
    {
        foreach ($service->courses() as $course) {
            DB::table('service_courses')->upsert([[
                'id' => $course->id,
                'table_service_id' => $service->id,
                'course_template_id' => $course->extra ? null : $course->templateCourseId,
                'sequence' => $course->sequence,
                'name' => $course->name,
                'status' => $course->status->value,
                'is_extra' => $course->extra,
                'fired_at' => $course->firedAt,
                'ready_at' => $course->readyAt,
                'served_at' => $course->servedAt,
                'exception_reason' => $course->exceptionReason,
            ]], ['id'], ['sequence', 'name', 'status', 'is_extra', 'fired_at', 'ready_at', 'served_at', 'exception_reason']);

            foreach ($course->items() as $item) {
                DB::table('service_course_items')->upsert([[
                    'id' => $item->id,
                    'service_course_id' => $course->id,
                    'preparation_template_id' => $course->extra ? null : $item->preparationTemplateId,
                    'station_id' => $item->stationId,
                    'name' => $item->name,
                    'quantity' => $item->quantity,
                    'guest_position' => $item->guestPosition,
                    'mandatory' => $item->mandatory,
                    'status' => $item->status->value,
                    'fired_at' => $item->firedAt,
                    'started_at' => $item->startedAt,
                    'ready_at' => $item->readyAt,
                    'cancelled_at' => $item->cancelledAt,
                    'cancel_reason' => $item->cancelReason,
                    'modification_reason' => $item->modificationReason,
                ]], ['id'], ['station_id', 'name', 'quantity', 'guest_position', 'mandatory', 'status', 'fired_at', 'started_at', 'ready_at', 'cancelled_at', 'cancel_reason', 'modification_reason']);
            }
        }
    }

    private function saveConsumptions(TableService $service): void
    {
        foreach ($service->consumptions() as $consumption) {
            DB::table('consumptions')->upsert([[
                'id' => $consumption->id,
                'table_service_id' => $service->id,
                'product_id' => null,
                'name' => $consumption->name,
                'quantity' => $consumption->quantity,
                'unit_price_cents' => $consumption->unitPriceCents,
                'cancelled' => $consumption->isCancelled(),
                'cancel_reason' => $consumption->cancelReason,
                'created_by' => null,
                'created_at' => now(),
                'cancelled_at' => $consumption->cancelledAt,
            ]], ['id'], ['name', 'quantity', 'unit_price_cents', 'cancelled', 'cancel_reason', 'cancelled_at']);
        }
    }

    private function savePayments(TableService $service): void
    {
        foreach ($service->payments() as $payment) {
            DB::table('payments')->upsert([[
                'id' => $payment->id,
                'table_service_id' => $service->id,
                'method' => $payment->method,
                'amount_cents' => $payment->amountCents,
                'currency' => 'EUR',
                'external_reference' => null,
                'recorded_by' => null,
                'recorded_at' => $payment->recordedAt,
            ]], ['id'], ['method', 'amount_cents', 'recorded_at']);
        }
    }

    private function dateTime(mixed $value): ?\DateTimeImmutable
    {
        if ($value === null) {
            return null;
        }
        if ($value instanceof \DateTimeImmutable) {
            return $value;
        }
        if ($value instanceof \DateTimeInterface) {
            return \DateTimeImmutable::createFromInterface($value);
        }
        return new \DateTimeImmutable((string) $value);
    }
}
