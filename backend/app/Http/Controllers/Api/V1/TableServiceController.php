<?php

declare(strict_types=1);

namespace App\Http\Controllers\Api\V1;

use App\Http\Api\CommandContextFactory;
use Hospitality\Application\Service\TableServiceCommandService;
use Hospitality\Application\Service\TableServiceQueryService;
use Hospitality\Application\Service\TableServiceSetupService;
use Hospitality\Domain\Service\RestrictionSeverity;
use Hospitality\Domain\Service\RestrictionType;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

final class TableServiceController
{
    public function __construct(
        private readonly CommandContextFactory $contexts,
        private readonly TableServiceSetupService $setup,
        private readonly TableServiceCommandService $commands,
        private readonly TableServiceQueryService $queries,
    ) {
    }

    public function open(Request $request): JsonResponse
    {
        $data = $request->validate([
            'table_id' => ['required', 'string', 'max:26'],
            'pax' => ['required', 'integer', 'min:1', 'max:100'],
        ]);

        $result = $this->setup->open(
            $this->contexts->fromRequest($request),
            $this->contexts->idempotencyKey($request),
            $data['table_id'],
            (int) $data['pax'],
        );

        return response()->json($result, 201);
    }

    public function show(Request $request, string $serviceId): JsonResponse
    {
        return response()->json(
            $this->queries->detail($this->contexts->fromRequest($request), $serviceId),
        );
    }

    public function assignMenu(Request $request, string $serviceId): JsonResponse
    {
        $data = $request->validate(['menu_id' => ['required', 'string', 'max:26']]);
        return response()->json($this->setup->assignMenu(
            $this->contexts->fromRequest($request),
            $serviceId,
            $this->contexts->idempotencyKey($request),
            $data['menu_id'],
        ));
    }

    public function addGuest(Request $request, string $serviceId): JsonResponse
    {
        $data = $request->validate(['name' => ['nullable', 'string', 'max:160']]);
        return response()->json($this->commands->addGuest(
            $this->contexts->fromRequest($request),
            $serviceId,
            $this->contexts->idempotencyKey($request),
            $data['name'] ?? null,
        ));
    }

    public function addRestriction(Request $request, string $serviceId, string $guestId): JsonResponse
    {
        $data = $request->validate([
            'label' => ['required', 'string', 'max:160'],
            'type' => ['required', 'string', 'in:allergy,intolerance,preference'],
            'severity' => ['required', 'string', 'in:informative,important,critical'],
            'notes' => ['nullable', 'string', 'max:2000'],
        ]);

        return response()->json($this->commands->addRestriction(
            $this->contexts->fromRequest($request),
            $serviceId,
            $this->contexts->idempotencyKey($request),
            $guestId,
            $data['label'],
            RestrictionType::from($data['type']),
            RestrictionSeverity::from($data['severity']),
            $data['notes'] ?? null,
        ));
    }

    public function start(Request $request, string $serviceId): JsonResponse
    {
        return response()->json($this->commands->start(
            $this->contexts->fromRequest($request),
            $serviceId,
            $this->contexts->idempotencyKey($request),
        ));
    }

    public function fireNextCourse(Request $request, string $serviceId): JsonResponse
    {
        return response()->json($this->commands->fireNextCourse(
            $this->contexts->fromRequest($request),
            $serviceId,
            $this->contexts->idempotencyKey($request),
        ));
    }

    public function startPreparation(Request $request, string $serviceId, string $courseId, string $itemId): JsonResponse
    {
        return response()->json($this->commands->startPreparation(
            $this->contexts->fromRequest($request),
            $serviceId,
            $this->contexts->idempotencyKey($request),
            $courseId,
            $itemId,
        ));
    }

    public function readyPreparation(Request $request, string $serviceId, string $courseId, string $itemId): JsonResponse
    {
        return response()->json($this->commands->markPreparationReady(
            $this->contexts->fromRequest($request),
            $serviceId,
            $this->contexts->idempotencyKey($request),
            $courseId,
            $itemId,
        ));
    }

    public function validateCourseReady(Request $request, string $serviceId, string $courseId): JsonResponse
    {
        return response()->json($this->commands->validateCourseReady(
            $this->contexts->fromRequest($request),
            $serviceId,
            $this->contexts->idempotencyKey($request),
            $courseId,
        ));
    }

    public function serveCourse(Request $request, string $serviceId, string $courseId): JsonResponse
    {
        return response()->json($this->commands->serveCourse(
            $this->contexts->fromRequest($request),
            $serviceId,
            $this->contexts->idempotencyKey($request),
            $courseId,
        ));
    }

    public function skipCourse(Request $request, string $serviceId, string $courseId): JsonResponse
    {
        $data = $request->validate(['reason' => ['required', 'string', 'max:1000']]);
        return response()->json($this->commands->skipCourse(
            $this->contexts->fromRequest($request),
            $serviceId,
            $this->contexts->idempotencyKey($request),
            $courseId,
            $data['reason'],
        ));
    }

    public function substitutePreparation(Request $request, string $serviceId, string $courseId, string $itemId): JsonResponse
    {
        $data = $request->validate([
            'name' => ['required', 'string', 'max:180'],
            'station_id' => ['required', 'string', 'max:26'],
            'reason' => ['required', 'string', 'max:1000'],
        ]);
        return response()->json($this->commands->substitutePreparation(
            $this->contexts->fromRequest($request),
            $serviceId,
            $this->contexts->idempotencyKey($request),
            $courseId,
            $itemId,
            $data['name'],
            $data['station_id'],
            $data['reason'],
        ));
    }

    public function pause(Request $request, string $serviceId): JsonResponse
    {
        $data = $request->validate(['reason' => ['nullable', 'string', 'max:1000']]);
        return response()->json($this->commands->pause(
            $this->contexts->fromRequest($request),
            $serviceId,
            $this->contexts->idempotencyKey($request),
            $data['reason'] ?? null,
        ));
    }

    public function resume(Request $request, string $serviceId): JsonResponse
    {
        return response()->json($this->commands->resume(
            $this->contexts->fromRequest($request),
            $serviceId,
            $this->contexts->idempotencyKey($request),
        ));
    }

    public function addConsumption(Request $request, string $serviceId): JsonResponse
    {
        $data = $request->validate([
            'name' => ['required', 'string', 'max:180'],
            'quantity' => ['required', 'integer', 'min:1', 'max:999'],
            'unit_price_cents' => ['required', 'integer', 'min:0'],
        ]);
        return response()->json($this->commands->addConsumption(
            $this->contexts->fromRequest($request),
            $serviceId,
            $this->contexts->idempotencyKey($request),
            $data['name'],
            (int) $data['quantity'],
            (int) $data['unit_price_cents'],
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
        return response()->json($this->commands->close(
            $this->contexts->fromRequest($request),
            $serviceId,
            $this->contexts->idempotencyKey($request),
        ));
    }
}
