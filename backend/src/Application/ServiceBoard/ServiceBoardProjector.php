<?php

declare(strict_types=1);

namespace Hospitality\Application\ServiceBoard;

use Hospitality\Domain\Service\RestrictionSeverity;
use Hospitality\Domain\Service\TableService;

final class ServiceBoardProjector
{
    /**
     * @param list<TableService> $services
     * @return list<array<string, mixed>>
     */
    public function project(array $services): array
    {
        return array_map(fn (TableService $service): array => $this->row($service), $services);
    }

    /** @return array<string, mixed> */
    public function row(TableService $service): array
    {
        $course = $service->currentCourse();
        $critical = [];

        foreach ($service->guests() as $guest) {
            foreach ($guest->restrictions() as $restriction) {
                if ($restriction->severity === RestrictionSeverity::Critical) {
                    $critical[] = sprintf('PAX %d · %s', $guest->position, $restriction->label);
                }
            }
        }

        return [
            'service_id' => $service->id,
            'table_id' => $service->tableId,
            'pax' => $service->pax,
            'service_status' => $service->status->value,
            'course' => $course ? [
                'id' => $course->id,
                'sequence' => $course->sequence,
                'name' => $course->name,
                'status' => $course->status->value,
                'fired_at' => $course->firedAt?->format(DATE_ATOM),
                'ready_at' => $course->readyAt?->format(DATE_ATOM),
                'served_at' => $course->servedAt?->format(DATE_ATOM),
            ] : null,
            'critical_restrictions' => $critical,
            'subtotal_cents' => $service->subtotalCents(),
            'paid_cents' => $service->paidCents(),
        ];
    }
}
