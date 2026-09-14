<?php

declare(strict_types=1);

namespace App\Infrastructure\Persistence;

use Hospitality\Application\Contracts\OperationalCatalogRepository;
use Illuminate\Database\Query\Builder;
use Illuminate\Support\Facades\DB;

final class LaravelOperationalCatalogRepository implements OperationalCatalogRepository
{
    public function tables(string $tenantId, string $companyId, string $locationId): array
    {
        return DB::table('dining_tables as t')
            ->join('dining_areas as a', 'a.id', '=', 't.dining_area_id')
            ->where('t.tenant_id', $tenantId)
            ->where('t.company_id', $companyId)
            ->where('t.location_id', $locationId)
            ->where('t.active', true)
            ->orderBy('a.sequence')
            ->orderBy('t.sequence')
            ->get(['t.id', 't.code', 't.name', 't.capacity', 'a.name as area'])
            ->map(static fn ($row): array => [
                'id' => (string) $row->id,
                'code' => (string) $row->code,
                'name' => (string) $row->name,
                'capacity' => (int) $row->capacity,
                'area' => (string) $row->area,
            ])->all();
    }

    public function menus(string $tenantId, string $companyId, string $locationId): array
    {
        return DB::table('menu_templates as m')
            ->leftJoin('course_templates as c', 'c.menu_template_id', '=', 'm.id')
            ->where('m.tenant_id', $tenantId)
            ->where('m.company_id', $companyId)
            ->where(static function ($query) use ($locationId): void {
                $query->whereNull('m.location_id')->orWhere('m.location_id', $locationId);
            })
            ->where('m.active', true)
            ->groupBy('m.id', 'm.name', 'm.price_cents')
            ->orderBy('m.name')
            ->get([
                'm.id',
                'm.name',
                'm.price_cents',
                DB::raw('count(c.id) as course_count'),
            ])
            ->map(static fn ($row): array => [
                'id' => (string) $row->id,
                'name' => (string) $row->name,
                'price_cents' => (int) $row->price_cents,
                'course_count' => (int) $row->course_count,
            ])->all();
    }

    public function stations(string $tenantId, string $companyId, string $locationId): array
    {
        return DB::table('kitchen_stations')
            ->where('tenant_id', $tenantId)
            ->where('company_id', $companyId)
            ->where('location_id', $locationId)
            ->where('active', true)
            ->orderBy('sequence')
            ->get(['id', 'name'])
            ->map(static fn ($row): array => [
                'id' => (string) $row->id,
                'name' => (string) $row->name,
            ])->all();
    }

    public function saleCategories(string $tenantId, string $companyId, string $locationId): array
    {
        $priceList = $this->defaultPriceList($tenantId, $companyId, $locationId);
        if ($priceList === null) {
            return [];
        }

        return DB::table('product_categories as c')
            ->join('products as p', 'p.product_category_id', '=', 'c.id')
            ->join('product_prices as pp', 'pp.product_id', '=', 'p.id')
            ->where('c.tenant_id', $tenantId)
            ->where('c.active', true)
            ->where('p.tenant_id', $tenantId)
            ->where('p.active', true)
            ->where('pp.price_list_id', (string) $priceList->id)
            ->where('pp.active', true)
            ->distinct()
            ->orderBy('c.sequence')
            ->orderBy('c.name')
            ->get(['c.id', 'c.code', 'c.name', 'c.sequence'])
            ->map(static fn ($row): array => [
                'id' => (string) $row->id,
                'code' => (string) $row->code,
                'name' => (string) $row->name,
                'sequence' => (int) $row->sequence,
            ])->all();
    }

    public function saleItems(string $tenantId, string $companyId, string $locationId): array
    {
        $priceList = $this->defaultPriceList($tenantId, $companyId, $locationId);
        if ($priceList === null) {
            return [];
        }

        return $this->saleItemsQuery($tenantId, (string) $priceList->id)
            ->orderByRaw('COALESCE(c.sequence, 999999)')
            ->orderBy('p.sequence')
            ->orderBy('p.name')
            ->get($this->saleItemColumns())
            ->map(fn ($row): array => $this->mapSaleItem($row, $priceList))
            ->all();
    }

    public function saleItem(string $tenantId, string $companyId, string $locationId, string $productId): ?array
    {
        $priceList = $this->defaultPriceList($tenantId, $companyId, $locationId);
        if ($priceList === null) {
            return null;
        }

        $row = $this->saleItemsQuery($tenantId, (string) $priceList->id)
            ->where('p.id', $productId)
            ->first($this->saleItemColumns());

        return $row === null ? null : $this->mapSaleItem($row, $priceList);
    }

    private function defaultPriceList(string $tenantId, string $companyId, string $locationId): ?object
    {
        return DB::table('price_lists')
            ->where('tenant_id', $tenantId)
            ->where('company_id', $companyId)
            ->where('active', true)
            ->where('is_default', true)
            ->where(static function ($query) use ($locationId): void {
                $query->where('location_id', $locationId)->orWhereNull('location_id');
            })
            ->orderByRaw('CASE WHEN location_id = ? THEN 0 ELSE 1 END', [$locationId])
            ->orderBy('id')
            ->first(['id', 'name', 'code', 'location_id']);
    }

    private function saleItemsQuery(string $tenantId, string $priceListId): Builder
    {
        return DB::table('products as p')
            ->leftJoin('product_categories as c', 'c.id', '=', 'p.product_category_id')
            ->join('product_prices as pp', 'pp.product_id', '=', 'p.id')
            ->where('p.tenant_id', $tenantId)
            ->where('p.active', true)
            ->where('pp.price_list_id', $priceListId)
            ->where('pp.active', true)
            ->where(static function ($query): void {
                $query->whereNull('p.product_category_id')->orWhere('c.active', true);
            });
    }

    /** @return list<string> */
    private function saleItemColumns(): array
    {
        return [
            'p.id',
            'p.product_category_id as category_id',
            'c.name as category_name',
            'p.code',
            'p.sku',
            'p.name',
            'p.product_type',
            'p.sale_unit',
            'p.format_label',
            'pp.price_cents',
            'pp.currency',
        ];
    }

    /** @return array<string, mixed> */
    private function mapSaleItem(object $row, object $priceList): array
    {
        return [
            'id' => (string) $row->id,
            'category_id' => $row->category_id === null ? null : (string) $row->category_id,
            'category_name' => $row->category_name === null ? null : (string) $row->category_name,
            'code' => (string) $row->code,
            'sku' => $row->sku === null ? null : (string) $row->sku,
            'name' => (string) $row->name,
            'product_type' => (string) $row->product_type,
            'sale_unit' => (string) $row->sale_unit,
            'format_label' => $row->format_label === null ? null : (string) $row->format_label,
            'price_cents' => (int) $row->price_cents,
            'currency' => (string) $row->currency,
            'price_list_id' => (string) $priceList->id,
            'price_list_name' => (string) $priceList->name,
        ];
    }
}
