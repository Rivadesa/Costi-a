<?php

declare(strict_types=1);

namespace Hospitality\Application\Contracts;

interface OperationalCatalogRepository
{
    /** @return list<array{id:string,code:string,name:string,capacity:int,area:string}> */
    public function tables(string $tenantId, string $companyId, string $locationId): array;

    /** @return list<array{id:string,name:string,price_cents:int,course_count:int}> */
    public function menus(string $tenantId, string $companyId, string $locationId): array;

    /** @return list<array{id:string,name:string}> */
    public function stations(string $tenantId, string $companyId, string $locationId): array;

    /** @return list<array{id:string,code:string,name:string,sequence:int}> */
    public function saleCategories(string $tenantId, string $companyId, string $locationId): array;

    /**
     * @return list<array{
     *   id:string,
     *   category_id:?string,
     *   category_name:?string,
     *   code:string,
     *   sku:?string,
     *   name:string,
     *   product_type:string,
     *   sale_unit:string,
     *   format_label:?string,
     *   price_cents:int,
     *   currency:string,
     *   price_list_id:string,
     *   price_list_name:string
     * }>
     */
    public function saleItems(string $tenantId, string $companyId, string $locationId): array;

    /**
     * @return array{
     *   id:string,
     *   category_id:?string,
     *   category_name:?string,
     *   code:string,
     *   sku:?string,
     *   name:string,
     *   product_type:string,
     *   sale_unit:string,
     *   format_label:?string,
     *   price_cents:int,
     *   currency:string,
     *   price_list_id:string,
     *   price_list_name:string
     * }|null
     */
    public function saleItem(string $tenantId, string $companyId, string $locationId, string $productId): ?array;
}
