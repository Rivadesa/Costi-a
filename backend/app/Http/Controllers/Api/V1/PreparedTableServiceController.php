<?php

declare(strict_types=1);

namespace App\Http\Controllers\Api\V1;

use App\Http\Api\CommandContextFactory;
use Hospitality\Application\Service\PreparedTableServiceSetupService;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

final class PreparedTableServiceController
{
    public function __construct(
        private readonly CommandContextFactory $contexts,
        private readonly PreparedTableServiceSetupService $setup,
    ) {
    }

    public function open(Request $request): JsonResponse
    {
        $data = $request->validate([
            'table_id' => ['required', 'string', 'max:26'],
            'pax' => ['required', 'integer', 'min:1', 'max:100'],
            'menu_id' => ['nullable', 'string', 'max:26'],
        ]);

        return response()->json($this->setup->open(
            $this->contexts->fromRequest($request),
            $this->contexts->idempotencyKey($request),
            $data['table_id'],
            (int) $data['pax'],
            $data['menu_id'] ?? null,
        ), 201);
    }
}
