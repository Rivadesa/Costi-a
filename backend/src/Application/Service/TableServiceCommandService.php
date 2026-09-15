<?php

declare(strict_types=1);

namespace Hospitality\Application\Service;

use Hospitality\Application\Shared\CommandContext;
use Hospitality\Domain\Service\Restriction;
use Hospitality\Domain\Service\RestrictionSeverity;
use Hospitality\Domain\Service\RestrictionType;
use Hospitality\Domain\Service\TableService;
use Hospitality\Domain\Shared\Ulid;

final class TableServiceCommandService
{
    public function __construct(private readonly ServiceMutationExecutor $executor)
    {
    }

    /** @return array<string, mixed> */
    public function start(CommandContext $context, string $serviceId, string $idempotencyKey): array
    {
        return $this->executor->execute($context, $serviceId, $idempotencyKey, 'service.start', [], function (TableService $service): array {
            $service->start();
            return $this->serviceResult($service);
        });
    }

    /** @return array<string, mixed> */
    public function addGuest(CommandContext $context, string $serviceId, string $idempotencyKey, ?string $name): array
    {
        return $this->executor->execute($context, $serviceId, $idempotencyKey, 'service.guest.add', ['name' => $name], function (TableService $service) use ($name): array {
            $guest = $service->addGuest($name);
            return [
                'service_id' => $service->id,
                'guest_id' => $guest->id,
                'position' => $guest->position,
                'name' => $guest->name,
            ];
        });
    }

    /** @return array<string, mixed> */
    public function addRestriction(
        CommandContext $context,
        string $serviceId,
        string $idempotencyKey,
        string $guestId,
        string $label,
        RestrictionType $type,
        RestrictionSeverity $severity,
        ?string $notes = null,
    ): array {
        $payload = compact('guestId', 'label', 'notes') + ['type' => $type->value, 'severity' => $severity->value];

        return $this->executor->execute($context, $serviceId, $idempotencyKey, 'service.restriction.add', $payload, function (TableService $service) use ($guestId, $label, $type, $severity, $notes): array {
            $restriction = new Restriction(Ulid::generate(), $label, $type, $severity, $notes);
            $service->addRestriction($guestId, $restriction);
            return [
                'service_id' => $service->id,
                'guest_id' => $guestId,
                'restriction_id' => $restriction->id,
                'type' => $restriction->type->value,
                'severity' => $restriction->severity->value,
            ];
        });
    }

    /** @return array<string, mixed> */
    public function fireNextCourse(CommandContext $context, string $serviceId, string $idempotencyKey): array
    {
        return $this->executor->execute($context, $serviceId, $idempotencyKey, 'course.fire_next', [], function (TableService $service): array {
            $course = $service->fireNextCourse();
            return $this->courseResult($service, $course->id);
        });
    }

    /** @return array<string, mixed> */
    public function startPreparation(CommandContext $context, string $serviceId, string $idempotencyKey, string $courseId, string $itemId): array
    {
        $payload = ['course_id' => $courseId, 'item_id' => $itemId];
        return $this->executor->execute($context, $serviceId, $idempotencyKey, 'preparation.start', $payload, function (TableService $service) use ($courseId, $itemId): array {
            $service->startCourseItem($courseId, $itemId);
            return $this->courseResult($service, $courseId);
        });
    }

    /** @return array<string, mixed> */
    public function markPreparationReady(CommandContext $context, string $serviceId, string $idempotencyKey, string $courseId, string $itemId): array
    {
        $payload = ['course_id' => $courseId, 'item_id' => $itemId];
        return $this->executor->execute($context, $serviceId, $idempotencyKey, 'preparation.ready', $payload, function (TableService $service) use ($courseId, $itemId): array {
            $service->markCourseItemReady($courseId, $itemId);
            return $this->courseResult($service, $courseId);
        });
    }

    /** @return array<string, mixed> */
    public function validateCourseReady(CommandContext $context, string $serviceId, string $idempotencyKey, string $courseId): array
    {
        return $this->executor->execute($context, $serviceId, $idempotencyKey, 'course.ready', ['course_id' => $courseId], function (TableService $service) use ($courseId): array {
            $service->markCourseReady($courseId);
            return $this->courseResult($service, $courseId);
        });
    }

    /** @return array<string, mixed> */
    public function serveCourse(CommandContext $context, string $serviceId, string $idempotencyKey, string $courseId): array
    {
        return $this->executor->execute($context, $serviceId, $idempotencyKey, 'course.serve', ['course_id' => $courseId], function (TableService $service) use ($courseId): array {
            $service->serveCourse($courseId);
            return $this->courseResult($service, $courseId);
        });
    }

    /** @return array<string, mixed> */
    public function skipCourse(CommandContext $context, string $serviceId, string $idempotencyKey, string $courseId, string $reason): array
    {
        return $this->executor->execute($context, $serviceId, $idempotencyKey, 'course.skip', ['course_id' => $courseId, 'reason' => $reason], function (TableService $service) use ($courseId, $reason): array {
            $service->skipCourse($courseId, $reason);
            return $this->courseResult($service, $courseId);
        });
    }

    /** @return array<string, mixed> */
    public function substitutePreparation(
        CommandContext $context,
        string $serviceId,
        string $idempotencyKey,
        string $courseId,
        string $itemId,
        string $name,
        string $stationId,
        string $reason,
    ): array {
        $payload = compact('courseId', 'itemId', 'name', 'stationId', 'reason');
        return $this->executor->execute($context, $serviceId, $idempotencyKey, 'preparation.substitute', $payload, function (TableService $service) use ($courseId, $itemId, $name, $stationId, $reason): array {
            $service->substituteCourseItem($courseId, $itemId, $name, $stationId, $reason);
            return $this->courseResult($service, $courseId);
        });
    }

    /** @return array<string, mixed> */
    public function pause(CommandContext $context, string $serviceId, string $idempotencyKey, ?string $reason = null): array
    {
        return $this->executor->execute($context, $serviceId, $idempotencyKey, 'service.pause', ['reason' => $reason], function (TableService $service) use ($reason): array {
            $service->pause($reason);
            return $this->serviceResult($service);
        });
    }

    /** @return array<string, mixed> */
    public function resume(CommandContext $context, string $serviceId, string $idempotencyKey): array
    {
        return $this->executor->execute($context, $serviceId, $idempotencyKey, 'service.resume', [], function (TableService $service): array {
            $service->resume();
            return $this->serviceResult($service);
        });
    }

    /** @return array<string, mixed> */
    public function addConsumption(CommandContext $context, string $serviceId, string $idempotencyKey, string $name, int $quantity, int $unitPriceCents): array
    {
        $payload = ['name' => $name, 'quantity' => $quantity, 'unit_price_cents' => $unitPriceCents];
        return $this->executor->execute($context, $serviceId, $idempotencyKey, 'consumption.add', $payload, function (TableService $service) use ($name, $quantity, $unitPriceCents): array {
            $consumption = $service->addConsumption($name, $quantity, $unitPriceCents);
            return [
                'service_id' => $service->id,
                'consumption_id' => $consumption->id,
                'subtotal_cents' => $service->subtotalCents(),
            ];
        });
    }

    /** @return array<string, mixed> */
    public function cancelConsumption(CommandContext $context, string $serviceId, string $idempotencyKey, string $consumptionId, string $reason): array
    {
        $payload = ['consumption_id' => $consumptionId, 'reason' => $reason];
        return $this->executor->execute($context, $serviceId, $idempotencyKey, 'consumption.cancel', $payload, function (TableService $service) use ($consumptionId, $reason): array {
            $service->cancelConsumption($consumptionId, $reason);
            return [
                'service_id' => $service->id,
                'consumption_id' => $consumptionId,
                'subtotal_cents' => $service->subtotalCents(),
            ];
        });
    }

    /** @return array<string, mixed> */
    public function recordPayment(CommandContext $context, string $serviceId, string $idempotencyKey, string $method, int $amountCents): array
    {
        $payload = ['method' => $method, 'amount_cents' => $amountCents];
        return $this->executor->execute($context, $serviceId, $idempotencyKey, 'payment.record', $payload, function (TableService $service) use ($method, $amountCents): array {
            $payment = $service->recordPayment($method, $amountCents);
            return [
                'service_id' => $service->id,
                'payment_id' => $payment->id,
                'paid_cents' => $service->paidCents(),
                'subtotal_cents' => $service->subtotalCents(),
                'service_status' => $service->status->value,
            ];
        });
    }

    /** @return array<string, mixed> */
    public function close(CommandContext $context, string $serviceId, string $idempotencyKey): array
    {
        return $this->executor->execute($context, $serviceId, $idempotencyKey, 'service.complete', [], function (TableService $service): array {
            $service->close();
            return $this->serviceResult($service);
        });
    }

    public function releaseTable(CommandContext $context, string $serviceId, string $key, string $reason): array
    {
        return $this->executor->execute($context, $serviceId, $key, 'table.release', ['reason' => $reason], function (TableService $service) use ($reason): array {
            $service->releaseTable($reason);
            return ['service_id' => $service->id, 'occupancy_status' => $service->occupancy->value];
        });
    }

    public function closeAccount(CommandContext $context, string $serviceId, string $key): array
    {
        return $this->executor->execute($context, $serviceId, $key, 'account.close', [], function (TableService $service): array {
            $service->closeAccount();
            return ['service_id' => $service->id, 'account_closed_at' => $service->accountClosedAt?->format(DATE_ATOM)];
        });
    }

    public function reopenAccount(CommandContext $context, string $serviceId, string $key, string $reason): array
    {
        return $this->executor->execute($context, $serviceId, $key, 'account.reopen', ['reason' => $reason], function (TableService $service) use ($reason): array {
            $service->reopenAccount($reason);
            return ['service_id' => $service->id, 'account_closed_at' => null];
        });
    }

    public function reconcileLifecycle(CommandContext $context, string $serviceId, string $key, string $status, string $reason): array
    {
        return $this->executor->execute($context, $serviceId, $key, 'service.lifecycle_review', compact('status', 'reason'), function (TableService $service) use ($status, $reason): array {
            $service->reconcileLifecycle(\Hospitality\Domain\Service\ServiceStatus::from($status), $reason);
            return ['service_id' => $service->id, 'status' => $service->status->value, 'lifecycle_review_required' => false];
        });
    }

    /** @return array<string, mixed> */
    private function serviceResult(TableService $service): array
    {
        return [
            'service_id' => $service->id,
            'status' => $service->status->value,
            'table_id' => $service->tableId,
            'pax' => $service->pax,
            'occupancy_status' => $service->occupancy->value,
        ];
    }

    /** @return array<string, mixed> */
    private function courseResult(TableService $service, string $courseId): array
    {
        foreach ($service->courses() as $course) {
            if ($course->id !== $courseId) {
                continue;
            }

            return [
                'service_id' => $service->id,
                'course_id' => $course->id,
                'course_sequence' => $course->sequence,
                'course_name' => $course->name,
                'course_status' => $course->status->value,
                'items' => array_map(static fn ($item): array => [
                    'id' => $item->id,
                    'station_id' => $item->stationId,
                    'guest_position' => $item->guestPosition,
                    'status' => $item->status->value,
                ], $course->items()),
            ];
        }

        throw new \LogicException('Course result requested for unknown course.');
    }
}
