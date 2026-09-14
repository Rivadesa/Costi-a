<?php

declare(strict_types=1);

namespace Hospitality\Application\Catalog;

use Hospitality\Application\Contracts\CatalogAdminRepository;
use Hospitality\Application\Contracts\IdempotencyStore;
use Hospitality\Application\Contracts\OutboxStore;
use Hospitality\Application\Contracts\TransactionManager;
use Hospitality\Application\Shared\CommandContext;
use Hospitality\Application\Shared\IdempotencyConflict;
use Hospitality\Application\Shared\RequestHasher;
use Hospitality\Domain\Shared\DomainEvent;

final class CatalogAdminService
{
    public function __construct(
        private readonly CatalogAdminRepository $catalog,
        private readonly TransactionManager $transactions,
        private readonly IdempotencyStore $idempotency,
        private readonly OutboxStore $outbox,
    ) {}

    public function snapshot(CommandContext $context): array
    {
        return $this->catalog->snapshot($context);
    }

    /** @param array<string, mixed> $data @return array<string, mixed> */
    public function save(CommandContext $context, string $key, array $data): array
    {
        if (trim($key) === '' || strlen($key) > 120) {
            throw new \InvalidArgumentException('Idempotency-Key debe tener entre 1 y 120 caracteres.');
        }
        $data['code'] = strtoupper(trim((string) ($data['code'] ?? '')));
        $data['name'] = trim((string) ($data['name'] ?? ''));
        if ($data['code'] === '' || $data['name'] === '' || !is_int($data['price_cents'] ?? null)
            || $data['price_cents'] < 0 || $data['price_cents'] > 100000000
            || !is_bool($data['available'] ?? null)
            || !in_array($data['product_type'] ?? null, ['beverage', 'wine', 'food', 'extra', 'other'], true)
            || !in_array($data['sale_unit'] ?? null, ['unit', 'glass', 'bottle', 'portion', 'service', 'other'], true)) {
            throw new \InvalidArgumentException('Artículo, formato o precio no válidos.');
        }
        $hash = RequestHasher::hash(['company' => $context->companyId, 'location' => $context->locationId,
            'actor' => $context->userId, 'payload' => $data]);
        return $this->transactions->run(function () use ($context, $key, $data, $hash): array {
            $this->catalog->lockContext($context);
            $existing = $this->idempotency->find($context->tenantId, $key);
            if ($existing !== null) {
                if ($existing->commandName !== 'catalog.product.save' || $existing->requestHash !== $hash) {
                    throw new IdempotencyConflict('La clave de reintento pertenece a otra operación.');
                }
                return $existing->result;
            }
            $result = $this->catalog->saveProduct($context, $data);
            $this->outbox->append($context->tenantId, $context->companyId, $context->locationId,
                DomainEvent::record('catalog.product.saved', 'product', $result['id'],
                    ['price_list_id' => $result['price_list_id'], 'revision' => $result['revision']]));
            $this->idempotency->remember($context->tenantId, $key, 'catalog.product.save', $hash, $result);
            return $result;
        });
    }
}
