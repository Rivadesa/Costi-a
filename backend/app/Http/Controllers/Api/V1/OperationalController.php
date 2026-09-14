<?php

declare(strict_types=1);

namespace App\Http\Controllers\Api\V1;

use App\Http\Api\CommandContextFactory;
use Hospitality\Application\ServiceBoard\OperationalReadService;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

final class OperationalController
{
    public function __construct(
        private readonly CommandContextFactory $contexts,
        private readonly OperationalReadService $reads,
    ) {
    }

    public function board(Request $request): JsonResponse
    {
        return response()->json([
            'data' => $this->reads->serviceBoard($this->contexts->fromRequest($request)),
        ]);
    }

    public function kds(Request $request, string $stationId): JsonResponse
    {
        return response()->json([
            'station_id' => $stationId,
            'data' => $this->reads->stationQueue($this->contexts->fromRequest($request), $stationId),
        ]);
    }
}
