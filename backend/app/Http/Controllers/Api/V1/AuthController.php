<?php

declare(strict_types=1);

namespace App\Http\Controllers\Api\V1;

use App\Http\Middleware\AuthenticateLocalApi;
use Hospitality\Application\Auth\AuthenticationFailed;
use Hospitality\Application\Auth\LocalPrincipal;
use Hospitality\Application\Contracts\LocalAuthGateway;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

final class AuthController
{
    public function __construct(private readonly LocalAuthGateway $auth)
    {
    }

    public function login(Request $request): JsonResponse
    {
        $data = $request->validate([
            'email' => ['required', 'email', 'max:255'],
            'password' => ['required', 'string', 'max:1024'],
            'company_id' => ['nullable', 'string', 'max:26'],
            'location_id' => ['nullable', 'string', 'max:26'],
            'device_name' => ['nullable', 'string', 'max:160'],
        ]);

        $tenantId = config('hospitality.tenant_id');
        if (!is_string($tenantId) || trim($tenantId) === '') {
            throw new AuthenticationFailed('Local server tenant identity is not configured.');
        }

        $companyId = $this->scopeValue($data['company_id'] ?? null, 'hospitality.company_id', 'company_id');
        $locationId = $this->scopeValue($data['location_id'] ?? null, 'hospitality.location_id', 'location_id');

        $result = $this->auth->login(
            $tenantId,
            $data['email'],
            $data['password'],
            $companyId,
            $locationId,
            $data['device_name'] ?? 'local-device',
        );

        return response()->json([
            'token_type' => 'Bearer',
            'access_token' => $result->token,
            'expires_at' => $result->expiresAt->format(DATE_ATOM),
            'user' => $result->principal->toArray(),
        ]);
    }

    public function me(Request $request): JsonResponse
    {
        $principal = $this->principal($request);
        return response()->json(['user' => $principal->toArray()]);
    }

    public function logout(Request $request): JsonResponse
    {
        $token = $request->bearerToken();
        if (is_string($token)) {
            $this->auth->revoke($token);
        }

        return response()->json(['revoked' => true]);
    }

    private function principal(Request $request): LocalPrincipal
    {
        $principal = $request->attributes->get(AuthenticateLocalApi::PRINCIPAL_ATTRIBUTE);
        if (!$principal instanceof LocalPrincipal) {
            throw new AuthenticationFailed('Authenticated principal is missing.');
        }
        return $principal;
    }

    private function scopeValue(mixed $requestValue, string $configKey, string $field): string
    {
        $value = is_string($requestValue) && trim($requestValue) !== ''
            ? $requestValue
            : config($configKey);

        if (!is_string($value) || trim($value) === '') {
            throw new AuthenticationFailed(sprintf('%s is required because this local server has no fixed value.', $field));
        }
        return $value;
    }
}
