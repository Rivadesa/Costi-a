<?php

declare(strict_types=1);

use Illuminate\Support\Facades\Route;

Route::get('/', static fn (): array => [
    'application' => 'Costi-a / Hospitality OS',
    'status' => 'local backend online',
]);
