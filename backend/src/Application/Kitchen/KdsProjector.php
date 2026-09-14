<?php

declare(strict_types=1);

namespace Hospitality\Application\Kitchen;

use Hospitality\Domain\Service\CourseStatus;
use Hospitality\Domain\Service\TableService;

final class KdsProjector
{
    /**
     * @param list<TableService> $services
     * @return list<array<string, mixed>>
     */
    public function stationQueue(array $services, string $stationId): array
    {
        $queue = [];

        foreach ($services as $service) {
            $course = $service->currentCourse();
            if ($course === null || !in_array($course->status, [CourseStatus::Fired, CourseStatus::Preparing, CourseStatus::Ready], true)) {
                continue;
            }

            $items = $course->itemsForStation($stationId);
            if ($items === []) {
                continue;
            }

            $projectedItems = [];
            foreach ($items as $item) {
                $restrictions = [];
                if ($item->guestPosition !== null) {
                    foreach ($service->restrictionsForGuestPosition($item->guestPosition) as $restriction) {
                        $restrictions[] = [
                            'label' => $restriction->label,
                            'type' => $restriction->type->value,
                            'severity' => $restriction->severity->value,
                        ];
                    }
                }

                $projectedItems[] = [
                    'id' => $item->id,
                    'name' => $item->name,
                    'quantity' => $item->quantity,
                    'guest_position' => $item->guestPosition,
                    'status' => $item->status->value,
                    'mandatory' => $item->mandatory,
                    'restrictions' => $restrictions,
                    'modification_reason' => $item->modificationReason,
                ];
            }

            $queue[] = [
                'service_id' => $service->id,
                'table_id' => $service->tableId,
                'pax' => $service->pax,
                'course_id' => $course->id,
                'course_sequence' => $course->sequence,
                'course_name' => $course->name,
                'course_status' => $course->status->value,
                'fired_at' => $course->firedAt?->format(DATE_ATOM),
                'items' => $projectedItems,
            ];
        }

        usort($queue, fn (array $a, array $b): int => strcmp($a['fired_at'] ?? '', $b['fired_at'] ?? ''));

        return $queue;
    }
}
