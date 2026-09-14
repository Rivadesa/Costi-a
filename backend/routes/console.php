<?php

declare(strict_types=1);

use Illuminate\Support\Facades\Artisan;

Artisan::command('hospitality:about', function (): void {
    $this->info('Hospitality OS / Costi-a V1A local-primary backend');
})->purpose('Show Hospitality OS backend identity');
