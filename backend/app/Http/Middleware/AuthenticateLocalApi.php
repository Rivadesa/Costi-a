<?php

declare(strict_types=1);

namespace App\Http\Middleware;

use Closure;
use Hospitality\Application\Auth\AuthenticationFailed;
use Hospitality\Application\Contracts\LocalAuthGateway;
use Illuminate\Http\Request;
use Symfony\Component\HttpFoundation\Response;

final class AuthenticateLocalApi
{
    public const PRINCIPAL_ATTRIBUTE = 'hospitality.principal';

    public function __construct(private readonly LocalAuthGateway $auth)
    {
    }

    public function handle(Request $request, Closure $next): Response
    {
        $rawToken = $request->bearerToken();
        if (!is_string($rawToken) || $rawToken === '') {
            throw new AuthenticationFailed('Bearer token is required.');
        }

        $companyId = $this->scope($request, 'X-Company-Id', 'hospitality.company_id');
        $locationId = $this->scope($request, 'X-Location-Id', 'hospitality.location_id');
        $principal = $this->auth->authenticate($rawToken, $companyId, $locationId);
        $request->attributes->set(self::PRINCIPAL_ATTRIBUTE, $principal);

        return $next($request);
    }

    private function scope(Request $request, string $header, string $configKey): string
    {
        $value = $request->header($header);
        if (!is_string($value) || trim($value) === '') {
            $configured = config($configKey);
            $value = is_string($configured) ? $configured : '';
        }
        if (trim($value) === '') {
            throw new AuthenticationFailed(sprintf('%s is required for this local session.', $header));
        }
        return $value;
    }
}
