<?php

declare(strict_types=1);

namespace Hospitality\Application\Checkout;

use Hospitality\Domain\Service\TableService;

final class CheckoutDetailProjector
{
    /** @return array<string, mixed> */
    public function project(TableService $service): array
    {
        $menu = $service->menu();

        return [
            'service_id' => $service->id,
            'table_id' => $service->tableId,
            'pax' => $service->pax,
            'status' => $service->status->value,
            'opened_at' => $service->openedAt->format(DATE_ATOM),
            'menu' => $menu === null ? null : [
                'id' => $menu->id,
                'name' => $menu->name,
                'unit_price_cents' => $menu->priceCents,
                'quantity' => $service->pax,
                'total_cents' => $menu->priceCents * $service->pax,
            ],
            'consumptions' => array_map(static fn ($consumption): array => [
                'id' => $consumption->id,
                'product_id' => $consumption->productId,
                'price_list_id' => $consumption->priceListId,
                'name' => $consumption->name,
                'quantity' => $consumption->quantity,
                'unit_price_cents' => $consumption->unitPriceCents,
                'total_cents' => $consumption->totalCents(),
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
