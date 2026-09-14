<?php

declare(strict_types=1);

namespace Hospitality\Tests\Feature;

use Hospitality\Tests\TestCase;

final class MetaApiTest extends TestCase
{
    public function test_meta_exposes_local_primary_version_and_build(): void
    {
        config()->set('hospitality.version', '0.1.0-test');
        config()->set('hospitality.build_sha', 'abc1234');

        $response = $this->getJson('/api/v1/meta');

        $response->assertOk()->assertJson([
            'application' => 'Costi-a / Hospitality OS',
            'version' => '0.1.0-test',
            'build_sha' => 'abc1234',
            'api_version' => 'v1',
            'phase' => 'V1A',
            'authority' => 'local-primary',
        ]);
    }
}
