<?php

declare(strict_types=1);

namespace Hospitality\Tests\Feature;

use App\Infrastructure\Provisioning\PilotProvisioner;
use Hospitality\Tests\TestCase;

final class PilotAcceptanceApiTest extends TestCase
{
    public function test_provisioned_admin_can_login_and_load_operational_configuration(): void
    {
        /** @var PilotProvisioner $provisioner */
        $provisioner = $this->app->make(PilotProvisioner::class);
        $scope = $provisioner->provision('retiro-pilot', 'pilot-password-2026');

        config([
            'hospitality.tenant_id' => $scope['tenant_id'],
            'hospitality.company_id' => $scope['company_id'],
            'hospitality.location_id' => $scope['location_id'],
            'hospitality.token_ttl_hours' => 24,
        ]);

        $login = $this->postJson('/api/v1/auth/login', [
            'email' => 'admin@hospitality.local',
            'password' => 'pilot-password-2026',
            'device_name' => 'Pilot acceptance test',
        ]);

        $login->assertOk()
            ->assertJsonPath('user.company_id', $scope['company_id'])
            ->assertJsonPath('user.location_id', $scope['location_id']);

        $token = (string) $login->json('access_token');
        $configuration = $this->withToken($token)->getJson('/api/v1/configuration');

        $configuration->assertOk();
        self::assertCount(8, $configuration->json('data.tables'));
        self::assertCount(1, $configuration->json('data.menus'));
        self::assertCount(6, $configuration->json('data.stations'));
    }
}
