<?php

declare(strict_types=1);

namespace App\Http\Api;

use App\Http\Middleware\AuthenticateLocalApi;
use Hospitality\Application\Auth\AuthenticationFailed;
use Hospitality\Application\Auth\LocalPrincipal;
use Hospitality\Application\Shared\CommandContext;
use Illuminate\Http\Request;

final class CommandContextFactory
{
    public function fromRequest(Request $request): CommandContext
    {
        $principal = $request->attributes->get(AuthenticateLocalApi::PRINCIPAL_ATTRIBUTE);
        if (!$principal instanceof LocalPrincipal) {
            throw new AuthenticationFailed('Operational command context requires an authenticated local principal.');
        }

        $deviceId = $request->header('X-Device-Id');

        return new CommandContext(
            $principal->tenantId,
            $principal->companyId,
            $principal->locationId,
            $principal->userId,
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
}
