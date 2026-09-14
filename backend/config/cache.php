<?php

use Illuminate\Support\Str;

return [
    'default' => env('CACHE_STORE', 'array'),
    'stores' => [
        'array' => ['driver' => 'array', 'serialize' => false],
        'database' => ['driver' => 'database', 'connection' => null, 'table' => 'cache', 'lock_connection' => null, 'lock_table' => null],
        'redis' => ['driver' => 'redis', 'connection' => 'cache', 'lock_connection' => 'default'],
    ],
    'prefix' => env('CACHE_PREFIX', Str::slug((string) env('APP_NAME', 'hospitality-os'), '_').'_cache_'),
];
