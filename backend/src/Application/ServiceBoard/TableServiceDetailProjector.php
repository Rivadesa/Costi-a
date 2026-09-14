<?php

declare(strict_types=1);

namespace Hospitality\Application\ServiceBoard;

use Hospitality\Domain\Service\TableService;

final class TableServiceDetailProjector
{
    /** @return array<string, mixed> */
    public function project(TableService $service): array
    {
        $menu = $service->menu();

        return [
            'id' => $service->id,
            'table_id' => $service->tableId,
            'pax' => $service->pax,
            'status' => $service->status->value,
            'opened_at' => $service->openedAt->format(DATE_ATOM),
            'menu' => $menu === null ? null : [
                'id' => $menu->id,
                'name' => $menu->name,
                'unit_price_cents' => $menu->priceCents,
            ],
            'guests' => array_map(static fn ($guest): array => [
                'id' => $guest->id,
                'position' => $guest->position,
                'name' => $guest->name,
                'restrictions' => array_map(static fn ($restriction): array => [
                    'id' => $restriction->id,
                    'label' => $restriction->label,
                    'type' => $restriction->type->value,
                    'severity' => $restriction->severity->value,
                    'notes' => $restriction->notes,
                ], $guest->restrictions()),
            ], $service->guests()),
            'courses' => array_map(static fn ($course): array => [
                'id' => $course->id,
                'sequence' => $course->sequence,
                'name' => $course->name,
                'status' => $course->status->value,
                'extra' => $course->extra,
                'fired_at' => $course->firedAt?->format(DATE_ATOM),
                'ready_at' => $course->readyAt?->format(DATE_ATOM),
                'served_at' => $course->servedAt?->format(DATE_ATOM),
                'exception_reason' => $course->exceptionReason,
                'items' => array_map(static fn ($item): array => [
                    'id' => $item->id,
                    'name' => $item->name,
                    'station_id' => $item->stationId,
                    'quantity' => $item->quantity,
                    'guest_position' => $item->guestPosition,
                    'mandatory' => $item->mandatory,
                    'status' => $item->status->value,
                    'fired_at' => $item->firedAt?->format(DATE_ATOM),
                    'started_at' => $item->startedAt?->format(DATE_ATOM),
                    'ready_at' => $item->readyAt?->format(DATE_ATOM),
                    'modification_reason' => $item->modificationReason,
                ], $course->items()),
            ], $service->courses()),
            'consumptions' => array_map(static fn ($consumption): array => [
                'id' => $consumption->id,
                'name' => $consumption->name,
                'quantity' => $consumption->quantity,
                'unit_price_cents' => $consumption->unitPriceCents,
                'cancelled' => $consumption->isCancelled(),
                'cancel_reason' => $consumption->cancelReason,
            ], $service->consumptions()),
            'payments' => array_map(static fn ($payment): array => [
                'id' => $payment->id,
                'method' => $payment->method,
                'amount_cents' => $payment->amountCents,
                'recorded_at' => $payment->recordedAt->format(DATE_ATOM),
            ], $service->payments()),
            'subtotal_cents' => $service->subtotalCents(),
            'paid_cents' => $service->paidCents(),
            'balance_cents' => max(0, $service->subtotalCents() - $service->paidCents()),
        ];
    }
}
