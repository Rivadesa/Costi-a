<?php

declare(strict_types=1);

use Hospitality\Application\Shared\ConcurrencyConflict;
use Hospitality\Application\Shared\IdempotencyConflict;
use Illuminate\Foundation\Application;
use Illuminate\Foundation\Configuration\Exceptions;
use Illuminate\Foundation\Configuration\Middleware;
use Illuminate\Http\Request;
use Symfony\Component\HttpFoundation\Response;

return Application::configure(basePath: dirname(__DIR__))
    ->withRouting(
        web: __DIR__.'/../routes/web.php',
        api: __DIR__.'/../routes/api.php',
        commands: __DIR__.'/../routes/console.php',
        health: '/up',
    )
    ->withMiddleware(function (Middleware $middleware): void {
        // Local authentication + role authorization is introduced under Issue #5.
    })
    ->withExceptions(function (Exceptions $exceptions): void {
        $exceptions->shouldRenderJsonWhen(
            fn (Request $request): bool => $request->is('api/*') || $request->expectsJson(),
        );

        $exceptions->render(function (ConcurrencyConflict $exception, Request $request): ?Response {
            if (!$request->is('api/*')) {
                return null;
            }
            return response()->json([
                'error' => 'concurrency_conflict',
                'message' => $exception->getMessage(),
                'retryable' => true,
            ], 409);
        });

        $exceptions->render(function (IdempotencyConflict $exception, Request $request): ?Response {
            if (!$request->is('api/*')) {
                return null;
            }
            return response()->json([
                'error' => 'idempotency_conflict',
                'message' => $exception->getMessage(),
                'retryable' => false,
            ], 409);
        });

        $exceptions->render(function (\DomainException $exception, Request $request): ?Response {
            if (!$request->is('api/*')) {
                return null;
            }
            return response()->json([
                'error' => 'domain_conflict',
                'message' => $exception->getMessage(),
                'retryable' => false,
            ], 409);
        });

        $exceptions->render(function (\InvalidArgumentException $exception, Request $request): ?Response {
            if (!$request->is('api/*')) {
                return null;
            }
            return response()->json([
                'error' => 'invalid_argument',
                'message' => $exception->getMessage(),
                'retryable' => false,
            ], 422);
        });
    })
    ->create();
