<?php

declare(strict_types=1);

namespace Hospitality\Tests\Feature;

use Hospitality\Domain\Shared\Ulid;
use Hospitality\Tests\TestCase;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Hash;

final class LocalAuthApiTest extends TestCase
{
    public function test_local_login_me_board_and_logout_work_without_cloud_identity(): void
    {
        $scope = $this->seedUser(['service.view']);
        $this->fixLocalServerTenant($scope['tenant_id']);

        $login = $this->postJson('/api/v1/auth/login', [
            'email' => $scope['email'],
            'password' => 'secret-password',
            'company_id' => $scope['company_id'],
            'location_id' => $scope['location_id'],
            'device_name' => 'Test terminal',
        ]);

        $login->assertOk();
        $token = (string) $login->json('access_token');
        self::assertStringStartsWith('hos_', $token);
        self::assertSame(1, DB::table('api_tokens')->where('user_id', $scope['user_id'])->count());
        self::assertNotSame($token, DB::table('api_tokens')->where('user_id', $scope['user_id'])->value('token_hash'));

        $headers = $this->scopeHeaders($scope);
        $this->withToken($token)->getJson('/api/v1/auth/me', $headers)
            ->assertOk()
            ->assertJsonPath('user.user_id', $scope['user_id'])
            ->assertJsonPath('user.company_id', $scope['company_id'])
            ->assertJsonPath('user.location_id', $scope['location_id']);

        $this->withToken($token)->getJson('/api/v1/service-board', $headers)
            ->assertOk()
            ->assertJson(['data' => []]);

        $this->withToken($token)->postJson('/api/v1/auth/logout', [], $headers)
            ->assertOk()
            ->assertJson(['revoked' => true]);

        $this->withToken($token)->getJson('/api/v1/auth/me', $headers)
            ->assertStatus(401)
            ->assertJsonPath('error', 'authentication_failed');
    }

    public function test_server_side_permission_denies_action_even_if_client_calls_route_directly(): void
    {
        $scope = $this->seedUser(['service.view']);
        $this->fixLocalServerTenant($scope['tenant_id']);
        $token = $this->login($scope);

        $this->withToken($token)->postJson('/api/v1/services', [
            'table_id' => Ulid::generate(),
            'pax' => 2,
        ], $this->scopeHeaders($scope) + ['Idempotency-Key' => 'auth-permission-test'])
            ->assertStatus(403)
            ->assertJsonPath('error', 'authorization_denied');
    }

    public function test_user_cannot_switch_to_an_unassigned_company_location_by_changing_headers(): void
    {
        $scopeA = $this->seedUser(['service.view']);
        $scopeB = $this->seedCompanyLocation($scopeA['tenant_id']);
        $this->fixLocalServerTenant($scopeA['tenant_id']);
        $token = $this->login($scopeA);

        $this->withToken($token)->getJson('/api/v1/service-board', [
            'X-Company-Id' => $scopeB['company_id'],
            'X-Location-Id' => $scopeB['location_id'],
        ])
            ->assertStatus(403)
            ->assertJsonPath('error', 'authorization_denied');
    }

    public function test_wrong_password_does_not_issue_a_token(): void
    {
        $scope = $this->seedUser(['service.view']);
        $this->fixLocalServerTenant($scope['tenant_id']);

        $this->postJson('/api/v1/auth/login', [
            'email' => $scope['email'],
            'password' => 'wrong-password',
            'company_id' => $scope['company_id'],
            'location_id' => $scope['location_id'],
        ])
            ->assertStatus(401)
            ->assertJsonPath('error', 'authentication_failed');

        self::assertSame(0, DB::table('api_tokens')->where('user_id', $scope['user_id'])->count());
    }

    /** @param list<string> $permissions @return array<string, string> */
    private function seedUser(array $permissions): array
    {
        $tenantId = Ulid::generate();
        $companyLocation = $this->seedCompanyLocation($tenantId, true);
        $userId = Ulid::generate();
        $roleId = Ulid::generate();
        $email = strtolower(Ulid::generate()).'@example.test';
        $now = now();

        DB::table('users')->insert([
            'id' => $userId,
            'tenant_id' => $tenantId,
            'display_name' => 'API Test User',
            'email' => $email,
            'password_hash' => Hash::make('secret-password'),
            'active' => true,
            'created_at' => $now,
            'updated_at' => $now,
        ]);
        DB::table('roles')->insert([
            'id' => $roleId,
            'tenant_id' => $tenantId,
            'name' => 'Test Role '.Ulid::generate(),
            'permissions' => json_encode($permissions, JSON_THROW_ON_ERROR),
        ]);
        DB::table('user_roles')->insert([
            'id' => Ulid::generate(),
            'user_id' => $userId,
            'role_id' => $roleId,
            'company_id' => $companyLocation['company_id'],
            'location_id' => $companyLocation['location_id'],
        ]);

        return [
            'tenant_id' => $tenantId,
            'company_id' => $companyLocation['company_id'],
            'location_id' => $companyLocation['location_id'],
            'user_id' => $userId,
            'email' => $email,
        ];
    }

    /** @return array{company_id:string,location_id:string} */
    private function seedCompanyLocation(string $tenantId, bool $createTenant = false): array
    {
        $companyId = Ulid::generate();
        $locationId = Ulid::generate();
        $now = now();

        if ($createTenant) {
            DB::table('tenants')->insert([
                'id' => $tenantId,
                'name' => 'Auth Test Tenant',
                'created_at' => $now,
                'updated_at' => $now,
            ]);
        }

        DB::table('companies')->insert([
            'id' => $companyId,
            'tenant_id' => $tenantId,
            'legal_name' => 'Auth Test Company SL',
            'trade_name' => 'Auth Test',
            'tax_id' => null,
            'active' => true,
            'created_at' => $now,
            'updated_at' => $now,
        ]);
        DB::table('locations')->insert([
            'id' => $locationId,
            'tenant_id' => $tenantId,
            'company_id' => $companyId,
            'name' => 'Auth Test Location',
            'timezone' => 'Europe/Madrid',
            'active' => true,
            'created_at' => $now,
            'updated_at' => $now,
        ]);

        return ['company_id' => $companyId, 'location_id' => $locationId];
    }

    /** @param array<string, string> $scope */
    private function login(array $scope): string
    {
        $response = $this->postJson('/api/v1/auth/login', [
            'email' => $scope['email'],
            'password' => 'secret-password',
            'company_id' => $scope['company_id'],
            'location_id' => $scope['location_id'],
        ]);
        $response->assertOk();
        return (string) $response->json('access_token');
    }

    private function fixLocalServerTenant(string $tenantId): void
    {
        config([
            'hospitality.tenant_id' => $tenantId,
            'hospitality.company_id' => null,
            'hospitality.location_id' => null,
            'hospitality.token_ttl_hours' => 24,
        ]);
    }

    /** @param array<string, string> $scope @return array<string, string> */
    private function scopeHeaders(array $scope): array
    {
        return [
            'X-Company-Id' => $scope['company_id'],
            'X-Location-Id' => $scope['location_id'],
        ];
    }
}
