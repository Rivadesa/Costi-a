<?php

declare(strict_types=1);

namespace App\Http\Controllers\Api\V1;

use App\Http\Api\CommandContextFactory;
use Hospitality\Application\Catalog\CatalogAdminService;
use Hospitality\Application\Contracts\OperationalCatalogRepository;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

final class CatalogController
{
    public function __construct(private readonly CommandContextFactory $contexts,
        private readonly CatalogAdminService $admin, private readonly OperationalCatalogRepository $catalog) {}

    public function checkout(Request $request): JsonResponse
    {
        $c = $this->contexts->fromRequest($request);
        return response()->json(['data' => [
            'sale_categories' => $this->catalog->saleCategories($c->tenantId, $c->companyId, $c->locationId),
            'sale_items' => $this->catalog->saleItems($c->tenantId, $c->companyId, $c->locationId),
        ]]);
    }

    public function index(Request $request): JsonResponse
    {
        return response()->json(['data' => $this->admin->snapshot($this->contexts->fromRequest($request))]);
    }

    public function save(Request $request): JsonResponse
    {
        $data = $request->validate([
            'id' => ['nullable', 'string', 'max:26'],
            'revision' => ['required_with:id', 'nullable', 'string', 'size:64'],
            'price_list_id' => ['required', 'string', 'max:26'],
            'code' => ['required', 'string', 'max:64', 'regex:/^[A-Za-z0-9][A-Za-z0-9._-]*$/'],
            'name' => ['required', 'string', 'max:180'],
            'sku' => ['nullable', 'string', 'max:100'],
            'category_id' => ['nullable', 'string', 'max:26'],
            'product_type' => ['required', 'in:beverage,wine,food,extra,other'],
            'sale_unit' => ['required', 'in:unit,glass,bottle,portion,service,other'],
            'format_label' => ['nullable', 'string', 'max:80'],
            'price_cents' => ['required', 'integer', 'min:0', 'max:100000000'],
            'available' => ['required', 'boolean'],
        ]);
        $data['price_cents'] = (int) $data['price_cents'];
        $data['available'] = (bool) $data['available'];
        return response()->json(['data' => $this->admin->save($this->contexts->fromRequest($request),
            $this->contexts->idempotencyKey($request), $data)]);
    }
}
