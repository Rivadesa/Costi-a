<?php

return [
    'version' => env('HOSPITALITY_VERSION', '0.1.0'),
    'build_sha' => env('HOSPITALITY_BUILD_SHA', 'dev'),
    'tenant_id' => env('HOSPITALITY_TENANT_ID'),
    'company_id' => env('HOSPITALITY_COMPANY_ID'),
    'location_id' => env('HOSPITALITY_LOCATION_ID'),
    'token_ttl_hours' => (int) env('HOSPITALITY_TOKEN_TTL_HOURS', 24),
];
