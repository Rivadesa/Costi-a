<?php

declare(strict_types=1);

namespace Hospitality\Application\Service;

use Hospitality\Application\Contracts\DiningTableRepository;
use Hospitality\Application\Contracts\IdempotencyStore;
use Hospitality\Application\Contracts\MenuTemplateRepository;
use Hospitality\Application\Contracts\OutboxStore;
use Hospitality\Application\Contracts\TableServiceRepository;
use Hospitality\Application\Contracts\TransactionManager;
use Hospitality\Application\Shared\CommandContext;
use Hospitality\Application\Shared\IdempotencyConflict;
use Hospitality\Application\Shared\RequestHasher;
use Hospitality\Domain\Service\TableService;
use Hospitality\Domain\Shared\Ulid;

/**
 * Opens the operational table atomically with its optional menu snapshot and
 * PAX positions. The desktop client should prefer this path so a network
 * interruption can never leave the initial service half configured.
 */
final class PreparedTableServiceSetupService
{
    public function __construct(
        private readonly TableServiceRepository $services,
        private readonly MenuTemplateRepository $menus,
        private readonly DiningTableRepository $tables,
        private readonly TransactionManager $transactions,
        private readonly IdempotencyStore $idempotency,
        private readonly OutboxStore $outbox,
    ) {
    }

    /** @return array<string, mixed> */
    public function open(
        CommandContext $context,
        string $idempotencyKey,
        string $tableId,
        int $pax,
        ?string $menuId = null,
    ): array {
        if (trim($idempotencyKey) === '') {
            throw new \InvalidArgumentException('Idempotency key is required.');
        }
        if ($pax < 1) {
            throw new \InvalidArgumentException('Pax must be at least 1.');
        }

        $requestHash = RequestHasher::hash([
            'tenant_id' => $context->tenantId,
            'company_id' => $context->companyId,
            'location_id' => $context->locationId,
            'table_id' => $tableId,
            'pax' => $pax,
            'menu_id' => $menuId,
            'initialize_guests' => true,
        ]);

        return $this->transactions->run(function () use ($context, $idempotencyKey, $tableId, $pax, $menuId, $requestHash): array {
            $existing = $this->idempotency->find($context->tenantId, $idempotencyKey);
            if ($existing !== null) {
                if ($existing->commandName !== 'service.open_prepared' || $existing->requestHash !== $requestHash) {
                    throw new IdempotencyConflict('Idempotency key was already used for a different prepared service request.');
                }
                return $existing->result;
            }

            if (!$this->tables->existsInScope($context->tenantId, $context->companyId, $context->locationId, $tableId)) {
                throw new \DomainException('Dining table not found in the requested tenant/company/location.');
            }

            $menu = $menuId === null
                ? null
                : $this->menus->get($context->tenantId, $context->companyId, $context->locationId, $menuId);

            $service = TableService::open(
                Ulid::generate(),
                $context->tenantId,
                $context->companyId,
                $context->locationId,
                $tableId,
                $pax,
                new \DateTimeImmutable(),
            );

            if ($menu !== null) {
                $service->assignMenu($menu);
            }
            for ($position = 1; $position <= $pax; $position++) {
                $service->addGuest();
            }

            $this->services->save($service);
            foreach ($service->pullEvents() as $event) {
                $this->outbox->append($context->tenantId, $context->companyId, $context->locationId, $event);
            }

            $result = [
                'service_id' => $service->id,
                'status' => $service->status->value,
                'table_id' => $service->tableId,
                'pax' => $service->pax,
                'menu_id' => $menu?->id,
                'guest_count' => count($service->guests()),
                'opened_at' => $service->openedAt->format(DATE_ATOM),
            ];

            $this->idempotency->remember(
                $context->tenantId,
                $idempotencyKey,
                'service.open_prepared',
                $requestHash,
                $result,
            );

            return $result;
        });
    }
}
