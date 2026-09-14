<?php

declare(strict_types=1);

namespace App\Http\Middleware;

use Closure;
use Hospitality\Application\Auth\AuthorizationDenied;
use Hospitality\Application\Auth\LocalPrincipal;
use Illuminate\Http\Request;
use Symfony\Component\HttpFoundation\Response;

final class RequirePermission
{
    public function handle(Request $request, Closure $next, string $permission): Response
    {
        $principal = $request->attributes->get(AuthenticateLocalApi::PRINCIPAL_ATTRIBUTE);
        if (!$principal instanceof LocalPrincipal) {
            throw new AuthorizationDenied('Authenticated principal is missing.');
        }
        if (!$principal->can($permission)) {
            throw new AuthorizationDenied('Permission denied: '.$permission);
        }

        return $next($request);
    }
}
