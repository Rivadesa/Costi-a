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

final class TableServiceSetupService
{
    public function __construct(
        private readonly TableServiceRepository $services,
        private readonly MenuTemplateRepository $menus,
        private readonly DiningTableRepository $tables,
        private readonly TransactionManager $transactions,
        private readonly IdempotencyStore $idempotency,
        private readonly OutboxStore $outbox,
        private readonly ServiceMutationExecutor $executor,
    ) {
    }

    /** @return array<string, mixed> */
    public function open(
        CommandContext $context,
        string $idempotencyKey,
        string $tableId,
        int $pax,
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
        ]);

        return $this->transactions->run(function () use ($context, $idempotencyKey, $tableId, $pax, $requestHash): array {
            $existing = $this->idempotency->find($context->tenantId, $idempotencyKey);
            if ($existing !== null) {
                if ($existing->commandName !== 'service.open' || $existing->requestHash !== $requestHash) {
                    throw new IdempotencyConflict('Idempotency key was already used for a different service.open request.');
                }
                return $existing->result;
            }

            if (!$this->tables->existsInScope($context->tenantId, $context->companyId, $context->locationId, $tableId)) {
                throw new \DomainException('Dining table not found in the requested tenant/company/location.');
            }

            $service = TableService::open(
                Ulid::generate(),
                $context->tenantId,
                $context->companyId,
                $context->locationId,
                $tableId,
                $pax,
                new \DateTimeImmutable(),
            );

            $this->services->save($service);
            foreach ($service->pullEvents() as $event) {
                $this->outbox->append($context->tenantId, $context->companyId, $context->locationId, $event);
            }

            $result = [
                'service_id' => $service->id,
                'status' => $service->status->value,
                'table_id' => $service->tableId,
                'pax' => $service->pax,
                'opened_at' => $service->openedAt->format(DATE_ATOM),
            ];

            $this->idempotency->remember(
                $context->tenantId,
                $idempotencyKey,
                'service.open',
                $requestHash,
                $result,
            );

            return $result;
        });
    }

    /** @return array<string, mixed> */
    public function assignMenu(
        CommandContext $context,
        string $serviceId,
        string $idempotencyKey,
        string $menuId,
    ): array {
        $menu = $this->menus->get($context->tenantId, $context->companyId, $context->locationId, $menuId);

        return $this->executor->execute(
            $context,
            $serviceId,
            $idempotencyKey,
            'service.menu.assign',
            ['menu_id' => $menuId],
            static function (TableService $service) use ($menu): array {
                $service->assignMenu($menu);
                return [
                    'service_id' => $service->id,
                    'menu_id' => $menu->id,
                    'menu_name' => $menu->name,
                    'menu_price_cents' => $menu->priceCents,
                    'course_count' => count($service->courses()),
                ];
            },
        );
    }
}
