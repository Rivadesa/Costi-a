<?php

declare(strict_types=1);
namespace App\Http\Controllers\Api\V1;
use App\Http\Api\CommandContextFactory;
use Hospitality\Application\Service\TableServiceCommandService;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

final class LifecycleController
{
    public function __construct(private readonly CommandContextFactory $contexts, private readonly TableServiceCommandService $commands) {}

    public function complete(Request $request, string $serviceId): JsonResponse
    {
        return response()->json($this->commands->close($this->contexts->fromRequest($request), $serviceId, $this->contexts->idempotencyKey($request)));
    }
    public function release(Request $request, string $serviceId): JsonResponse
    {
        $data = $request->validate(['reason' => ['required', 'string', 'max:1000']]);
        return response()->json($this->commands->releaseTable($this->contexts->fromRequest($request), $serviceId, $this->contexts->idempotencyKey($request), $data['reason']));
    }
    public function review(Request $request, string $serviceId): JsonResponse
    {
        $data = $request->validate(['status' => ['required', 'in:open,in_service,paused'], 'reason' => ['required', 'string', 'max:1000']]);
        return response()->json($this->commands->reconcileLifecycle($this->contexts->fromRequest($request), $serviceId, $this->contexts->idempotencyKey($request), $data['status'], $data['reason']));
    }
    public function closeAccount(Request $request, string $serviceId): JsonResponse
    {
        return response()->json($this->commands->closeAccount($this->contexts->fromRequest($request), $serviceId, $this->contexts->idempotencyKey($request)));
    }
    public function reopenAccount(Request $request, string $serviceId): JsonResponse
    {
        $data = $request->validate(['reason' => ['required', 'string', 'max:1000']]);
        return response()->json($this->commands->reopenAccount($this->contexts->fromRequest($request), $serviceId, $this->contexts->idempotencyKey($request), $data['reason']));
    }
}
