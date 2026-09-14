<?php

declare(strict_types=1);

namespace Hospitality\Application\Service;

use Hospitality\Application\Contracts\OperationalCatalogRepository;
use Hospitality\Application\Shared\CommandContext;
use Hospitality\Domain\Service\TableService;

final class CatalogConsumptionService
{
    public function __construct(
        private readonly ServiceMutationExecutor $executor,
        private readonly OperationalCatalogRepository $catalog,
    ) {
    }

    /** @return array<string, mixed> */
    public function add(
        CommandContext $context,
        string $serviceId,
        string $idempotencyKey,
        string $productId,
        int $quantity,
    ): array {
        if ($quantity < 1) {
            throw new \InvalidArgumentException('Consumption quantity must be >= 1.');
        }

        $payload = [
            'product_id' => $productId,
            'quantity' => $quantity,
        ];

        return $this->executor->execute(
            $context,
            $serviceId,
            $idempotencyKey,
            'consumption.catalog.add',
            $payload,
            function (TableService $service) use ($context, $productId, $quantity): array {
                $item = $this->catalog->saleItem(
                    $context->tenantId,
                    $context->companyId,
                    $context->locationId,
                    $productId,
                );

                if ($item === null) {
                    throw new \DomainException('Product is not active or has no price in the current restaurant price list.');
                }

                if (($item['currency'] ?? 'EUR') !== 'EUR') {
                    throw new \DomainException('V1A provisional accounts currently support EUR only.');
                }

                $consumption = $service->addConsumption(
                    (string) $item['name'],
                    $quantity,
                    (int) $item['price_cents'],
                );
                $consumption->linkCatalog((string) $item['id'], (string) $item['price_list_id']);

                return [
                    'service_id' => $service->id,
                    'consumption_id' => $consumption->id,
                    'product_id' => $consumption->productId,
                    'price_list_id' => $consumption->priceListId,
                    'name' => $consumption->name,
                    'quantity' => $consumption->quantity,
                    'unit_price_cents' => $consumption->unitPriceCents,
                    'subtotal_cents' => $service->subtotalCents(),
                ];
            },
        );
    }
}
