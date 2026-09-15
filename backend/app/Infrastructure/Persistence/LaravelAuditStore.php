<?php

declare(strict_types=1);
namespace App\Infrastructure\Persistence;
use Hospitality\Application\Contracts\AuditStore;
use Hospitality\Application\Shared\CommandContext;
use Hospitality\Domain\Shared\DomainEvent;
use Illuminate\Support\Facades\DB;

final class LaravelAuditStore implements AuditStore
{
    public function append(CommandContext $context, DomainEvent $event): void
    {
        DB::table('audit_log')->insert([
            'id' => $event->id, 'tenant_id' => $context->tenantId,
            'company_id' => $context->companyId, 'location_id' => $context->locationId,
            'user_id' => $context->userId, 'action' => $event->type,
            'entity_type' => $event->aggregateType, 'entity_id' => $event->aggregateId,
            'after_data' => json_encode($event->payload, JSON_THROW_ON_ERROR),
            'metadata' => json_encode(['device_id' => $context->deviceId], JSON_THROW_ON_ERROR),
            'occurred_at' => $event->occurredAt,
        ]);
    }
}
