<?php

declare(strict_types=1);

namespace App\Infrastructure\Persistence;

use Hospitality\Application\Contracts\CatalogAdminRepository;
use Hospitality\Application\Shared\CommandContext;
use Hospitality\Application\Shared\ConcurrencyConflict;
use Hospitality\Application\Shared\RequestHasher;
use Hospitality\Domain\Shared\Ulid;
use Illuminate\Support\Facades\DB;

final class LaravelCatalogAdminRepository implements CatalogAdminRepository
{
    public function lockContext(CommandContext $context): void
    {
        DB::select('SELECT pg_advisory_xact_lock(hashtextextended(?, 0))', ['catalog-admin:'.$context->tenantId]);
    }

    public function snapshot(CommandContext $context): array
    {
        $list = $this->priceList($context);
        $rows = DB::table('products as p')->join('product_prices as pp', 'pp.product_id', '=', 'p.id')
            ->where('p.tenant_id', $context->tenantId)->where('pp.price_list_id', $list->id)
            ->orderBy('p.name')->orderBy('p.id')
            ->get(['p.*', 'pp.price_cents', 'pp.currency', 'pp.active as available', 'pp.updated_at as price_updated_at']);
        return [
            'price_list' => ['id' => $list->id, 'name' => $list->name, 'location_id' => $list->location_id],
            'categories' => DB::table('product_categories')->where('tenant_id', $context->tenantId)
                ->where('active', true)->orderBy('sequence')->get(['id', 'name'])->toArray(),
            'products' => $rows->map(fn ($row) => $this->map($row, (string) $list->id))->all(),
        ];
    }

    public function saveProduct(CommandContext $context, array $data): array
    {
        $list = $this->priceList($context, true);
        if (($data['price_list_id'] ?? null) !== (string) $list->id) {
            throw new ConcurrencyConflict('La tarifa activa ha cambiado. Recarga el catálogo.');
        }
        $id = $data['id'] ?? null;
        $before = null;
        if ($id !== null) {
            // Lock product and price independently; no nullable outer-join locks.
            $product = DB::table('products')->where('tenant_id', $context->tenantId)->where('id', $id)->lockForUpdate()->first();
            $price = DB::table('product_prices')->where('product_id', $id)->where('price_list_id', $list->id)->lockForUpdate()->first();
            if ($product === null || $price === null) {
                throw new \DomainException('Artículo no encontrado en el catálogo autorizado.');
            }
            $row = (object) (get_object_vars($product) + [
                'price_cents' => $price->price_cents, 'currency' => $price->currency,
                'available' => $price->active, 'price_updated_at' => $price->updated_at,
            ]);
            if ((string) $price->currency !== 'EUR') throw new \DomainException('Esta versión solo permite editar tarifas en EUR.');
            $before = $this->map($row, (string) $list->id);
            if (!hash_equals($before['revision'], (string) ($data['revision'] ?? ''))) {
                throw new ConcurrencyConflict('Otro usuario ha modificado el artículo. Recarga antes de guardar.');
            }
        }
        $categoryId = $data['category_id'] ?? null;
        if ($categoryId !== null && !DB::table('product_categories')->where('id', $categoryId)
            ->where('tenant_id', $context->tenantId)->where('active', true)->exists()) {
            throw new \DomainException('Categoría no disponible en este catálogo.');
        }
        $duplicate = DB::table('products')->where('tenant_id', $context->tenantId)->whereRaw('lower(code) = lower(?)', [$data['code']]);
        if ($id !== null) $duplicate->where('id', '<>', $id);
        if ($duplicate->exists()) throw new \DomainException('El código de artículo ya existe.');

        $now = now();
        $productData = ['code' => $data['code'], 'name' => $data['name'],
            'product_category_id' => $categoryId, 'sku' => $data['sku'] ?? null,
            'product_type' => $data['product_type'], 'sale_unit' => $data['sale_unit'],
            'format_label' => $data['format_label'] ?? null, 'updated_at' => $now];
        if ($id === null) {
            $id = Ulid::generate();
            DB::table('products')->insert($productData + ['id' => $id, 'tenant_id' => $context->tenantId,
                'active' => true, 'sequence' => 0, 'created_at' => $now]);
            DB::table('product_prices')->insert(['id' => Ulid::generate(), 'product_id' => $id,
                'price_list_id' => $list->id, 'price_cents' => $data['price_cents'], 'currency' => 'EUR',
                'active' => $data['available'], 'created_at' => $now, 'updated_at' => $now]);
        } else {
            DB::table('products')->where('id', $id)->where('tenant_id', $context->tenantId)->update($productData + ['catalog_version' => DB::raw('catalog_version + 1')]);
            DB::table('product_prices')->where('product_id', $id)->where('price_list_id', $list->id)
                ->update(['price_cents' => $data['price_cents'], 'active' => $data['available'], 'updated_at' => $now]);
        }
        $row = DB::table('products as p')->join('product_prices as pp', 'pp.product_id', '=', 'p.id')
            ->where('p.id', $id)->where('p.tenant_id', $context->tenantId)->where('pp.price_list_id', $list->id)
            ->first(['p.*', 'pp.price_cents', 'pp.currency', 'pp.active as available', 'pp.updated_at as price_updated_at']);
        $after = $this->map($row, (string) $list->id);
        DB::table('audit_log')->insert(['id' => Ulid::generate(), 'tenant_id' => $context->tenantId,
            'company_id' => $context->companyId, 'location_id' => $context->locationId, 'user_id' => $context->userId,
            'action' => $before === null ? 'catalog.product.created' : 'catalog.product.updated',
            'entity_type' => 'product', 'entity_id' => $id,
            'before_data' => $before === null ? null : json_encode($before, JSON_THROW_ON_ERROR),
            'after_data' => json_encode($after, JSON_THROW_ON_ERROR),
            'metadata' => json_encode(['device_id' => $context->deviceId], JSON_THROW_ON_ERROR), 'occurred_at' => $now]);
        return $after;
    }

    private function priceList(CommandContext $context, bool $lock = false): object
    {
        $query = DB::table('price_lists')->where('tenant_id', $context->tenantId)
            ->where('company_id', $context->companyId)->where('active', true)->where('is_default', true)
            ->where(fn ($q) => $q->where('location_id', $context->locationId)->orWhereNull('location_id'))
            ->orderByRaw('CASE WHEN location_id = ? THEN 0 ELSE 1 END', [$context->locationId])->orderBy('id');
        if ($lock) $query->lockForUpdate();
        return $query->first() ?? throw new \DomainException('Configura primero una tarifa activa para este establecimiento.');
    }

    private function map(object $row, string $listId): array
    {
        $data = ['id' => (string) $row->id, 'price_list_id' => $listId, 'code' => (string) $row->code,
            'name' => (string) $row->name, 'sku' => $row->sku, 'category_id' => $row->product_category_id,
            'product_type' => (string) $row->product_type, 'sale_unit' => (string) $row->sale_unit,
            'format_label' => $row->format_label, 'price_cents' => (int) $row->price_cents,
            'currency' => (string) $row->currency, 'available' => (bool) $row->available,
            'product_active' => (bool) $row->active];
        $data['revision'] = RequestHasher::hash($data + ['version' => (int) $row->catalog_version, 'product_updated_at' => $row->updated_at,
            'price_updated_at' => $row->price_updated_at]);
        return $data;
    }
}
