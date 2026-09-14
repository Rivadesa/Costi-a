<?php

declare(strict_types=1);

namespace App\Http\Api;

use Hospitality\Application\Shared\CommandContext;
use Illuminate\Http\Request;

final class CommandContextFactory
{
    public function fromRequest(Request $request): CommandContext
    {
        $tenantId = $this->headerOrEnv($request, 'X-Tenant-Id', 'HOSPITALITY_TENANT_ID');
        $companyId = $this->headerOrEnv($request, 'X-Company-Id', 'HOSPITALITY_COMPANY_ID');
        $locationId = $this->headerOrEnv($request, 'X-Location-Id', 'HOSPITALITY_LOCATION_ID');
        $userId = $request->header('X-User-Id');
        $deviceId = $request->header('X-Device-Id');

        return new CommandContext(
            $tenantId,
            $companyId,
            $locationId,
            is_string($userId) && $userId !== '' ? $userId : null,
            is_string($deviceId) && $deviceId !== '' ? $deviceId : null,
        );
    }

    public function idempotencyKey(Request $request): string
    {
        $key = $request->header('Idempotency-Key');
        if (!is_string($key) || trim($key) === '') {
            throw new \InvalidArgumentException('Idempotency-Key header is required for mutations.');
        }
        if (strlen($key) > 120) {
            throw new \InvalidArgumentException('Idempotency-Key must be at most 120 characters.');
        }
        return $key;
    }

    private function headerOrEnv(Request $request, string $header, string $envKey): string
    {
        $value = $request->header($header);
        if (!is_string($value) || trim($value) === '') {
            $value = env($envKey);
        }
        if (!is_string($value) || trim($value) === '') {
            throw new \InvalidArgumentException(sprintf('%s header (or %s) is required.', $header, $envKey));
        }
        return $value;
    }
}
