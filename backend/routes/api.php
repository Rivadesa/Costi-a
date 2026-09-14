<?php

declare(strict_types=1);

use Illuminate\Support\Facades\Route;

Route::prefix('v1')->group(function (): void {
    Route::get('/meta', static fn (): array => [
        'application' => 'Costi-a / Hospitality OS',
        'api_version' => 'v1',
        'phase' => 'V1A',
        'authority' => 'local-primary',
    ]);
});
