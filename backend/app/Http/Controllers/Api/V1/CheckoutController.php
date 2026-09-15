<?php

declare(strict_types=1);

namespace App\Http\Controllers\Api\V1;

use App\Http\Api\CommandContextFactory;
use Hospitality\Application\Checkout\CheckoutQueryService;
use Hospitality\Application\Service\CatalogConsumptionService;
use Hospitality\Application\Service\TableServiceCommandService;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

final class CheckoutController
{
    public function __construct(
        private readonly CommandContextFactory $contexts,
        private readonly CheckoutQueryService $queries,
        private readonly CatalogConsumptionService $catalogConsumptions,
        private readonly TableServiceCommandService $commands,
    ) {
    }

    public function show(Request $request, string $serviceId): JsonResponse
    {
        return response()->json(
            $this->queries->detail($this->contexts->fromRequest($request), $serviceId),
        );
    }

    public function addConsumption(Request $request, string $serviceId): JsonResponse
    {
        $data = $request->validate([
            'product_id' => ['required', 'string', 'max:26'],
            'quantity' => ['required', 'integer', 'min:1', 'max:999'],
        ]);

        return response()->json($this->catalogConsumptions->add(
            $this->contexts->fromRequest($request),
            $serviceId,
            $this->contexts->idempotencyKey($request),
            $data['product_id'],
            (int) $data['quantity'],
        ));
    }

    public function cancelConsumption(Request $request, string $serviceId, string $consumptionId): JsonResponse
    {
        $data = $request->validate(['reason' => ['required', 'string', 'max:1000']]);

        return response()->json($this->commands->cancelConsumption(
            $this->contexts->fromRequest($request),
            $serviceId,
            $this->contexts->idempotencyKey($request),
            $consumptionId,
            $data['reason'],
        ));
    }

    public function recordPayment(Request $request, string $serviceId): JsonResponse
    {
        $data = $request->validate([
            'method' => ['required', 'string', 'max:40'],
            'amount_cents' => ['required', 'integer', 'min:1'],
        ]);

        return response()->json($this->commands->recordPayment(
            $this->contexts->fromRequest($request),
            $serviceId,
            $this->contexts->idempotencyKey($request),
            $data['method'],
            (int) $data['amount_cents'],
        ));
    }

    public function close(Request $request, string $serviceId): JsonResponse
    {
        return response()->json(['error' => 'lifecycle_upgrade_required',
            'message' => 'Use separate complete, release-table and close-account actions.', 'retryable' => false], 409);
    }

    public function index(Request $request): JsonResponse
    {
        $data = $request->validate(['page' => ['sometimes', 'integer', 'min:1', 'max:100000']]);
        return response()->json($this->queries->openAccounts($this->contexts->fromRequest($request), (int) ($data['page'] ?? 1)));
    }
}
