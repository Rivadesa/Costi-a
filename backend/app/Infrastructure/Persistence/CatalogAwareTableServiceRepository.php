<?php

declare(strict_types=1);

namespace App\Infrastructure\Persistence;

use Hospitality\Application\Contracts\TableServiceRepository;
use Hospitality\Domain\Service\TableService;
use Illuminate\Support\Facades\DB;

final class CatalogAwareTableServiceRepository implements TableServiceRepository
{
    public function __construct(private readonly LaravelTableServiceRepository $inner)
    {
    }

    public function get(
        string $tenantId,
        string $companyId,
        string $locationId,
        string $serviceId,
    ): TableService {
        $service = $this->inner->get($tenantId, $companyId, $locationId, $serviceId);

        $links = DB::table('consumptions')
            ->where('table_service_id', $serviceId)
            ->whereNotNull('product_id')
            ->get(['id', 'product_id', 'price_list_id'])
            ->keyBy('id');

        foreach ($service->consumptions() as $consumption) {
            $link = $links->get($consumption->id);
            if ($link === null || $link->price_list_id === null) {
                continue;
            }

            $consumption->linkCatalog((string) $link->product_id, (string) $link->price_list_id);
        }

        return $service;
    }

    public function save(TableService $service): void
    {
        $this->inner->save($service);

        foreach ($service->consumptions() as $consumption) {
            if ($consumption->productId === null || $consumption->priceListId === null) {
                continue;
            }

            DB::table('consumptions')
                ->where('id', $consumption->id)
                ->where('table_service_id', $service->id)
                ->update([
                    'product_id' => $consumption->productId,
                    'price_list_id' => $consumption->priceListId,
                ]);
        }
    }
}
