<?php

declare(strict_types=1);

namespace App\Http\Controllers\Api\V1;

use App\Http\Api\CommandContextFactory;
use Hospitality\Application\Configuration\OperationalCatalogService;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

final class ConfigurationController
{
    public function __construct(
        private readonly CommandContextFactory $contexts,
        private readonly OperationalCatalogService $catalog,
    ) {
    }

    public function show(Request $request): JsonResponse
    {
        return response()->json([
            'data' => $this->catalog->forContext($this->contexts->fromRequest($request)),
        ]);
    }
}
