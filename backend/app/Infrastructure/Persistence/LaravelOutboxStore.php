<?php

declare(strict_types=1);

namespace App\Infrastructure\Persistence;

use Hospitality\Application\Contracts\OutboxStore;
use Hospitality\Domain\Shared\DomainEvent;
use Illuminate\Support\Facades\DB;

final class LaravelOutboxStore implements OutboxStore
{
    public function append(
        string $tenantId,
        string $companyId,
        string $locationId,
        DomainEvent $event,
    ): void {
        DB::table('outbox_events')->insert([
            'id' => $event->id,
            'tenant_id' => $tenantId,
            'company_id' => $companyId,
            'location_id' => $locationId,
            'aggregate_type' => $event->aggregateType,
            'aggregate_id' => $event->aggregateId,
            'event_type' => $event->type,
            'payload' => json_encode($event->payload, JSON_THROW_ON_ERROR),
            'occurred_at' => $event->occurredAt,
            'available_at' => now(),
            'published_at' => null,
            'attempts' => 0,
            'last_error' => null,
        ]);
    }
}
