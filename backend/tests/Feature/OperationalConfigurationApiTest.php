<?php

declare(strict_types=1);

namespace Hospitality\Tests\Feature;

use Hospitality\Domain\Shared\Ulid;
use Hospitality\Tests\TestCase;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Hash;

final class OperationalConfigurationApiTest extends TestCase
{
    public function test_authenticated_terminal_receives_human_readable_tables_menus_and_stations(): void
    {
        $tenantId = Ulid::generate();
        $companyId = Ulid::generate();
        $locationId = Ulid::generate();
        $areaId = Ulid::generate();
        $tableId = Ulid::generate();
        $stationId = Ulid::generate();
        $menuId = Ulid::generate();
        $courseId = Ulid::generate();
        $userId = Ulid::generate();
        $roleId = Ulid::generate();
        $email = strtolower(Ulid::generate()).'@example.test';
        $now = now();

        DB::table('tenants')->insert(['id' => $tenantId, 'name' => 'UI Tenant', 'created_at' => $now, 'updated_at' => $now]);
        DB::table('companies')->insert([
            'id' => $companyId, 'tenant_id' => $tenantId, 'legal_name' => 'UI Company SL',
            'trade_name' => 'UI Company', 'active' => true, 'created_at' => $now, 'updated_at' => $now,
        ]);
        DB::table('locations')->insert([
            'id' => $locationId, 'tenant_id' => $tenantId, 'company_id' => $companyId,
            'name' => 'Retiro Test', 'timezone' => 'Europe/Madrid', 'active' => true,
            'created_at' => $now, 'updated_at' => $now,
        ]);
        DB::table('dining_areas')->insert([
            'id' => $areaId, 'tenant_id' => $tenantId, 'company_id' => $companyId,
            'location_id' => $locationId, 'name' => 'Sala principal', 'sequence' => 1, 'active' => true,
        ]);
        DB::table('dining_tables')->insert([
            'id' => $tableId, 'tenant_id' => $tenantId, 'company_id' => $companyId,
            'location_id' => $locationId, 'dining_area_id' => $areaId, 'code' => 'M1',
            'name' => 'Mesa 1', 'capacity' => 4, 'sequence' => 1, 'active' => true,
        ]);
        DB::table('kitchen_stations')->insert([
            'id' => $stationId, 'tenant_id' => $tenantId, 'company_id' => $companyId,
            'location_id' => $locationId, 'name' => 'Pescados', 'sequence' => 1, 'active' => true,
        ]);
        DB::table('menu_templates')->insert([
            'id' => $menuId, 'tenant_id' => $tenantId, 'company_id' => $companyId,
            'location_id' => $locationId, 'name' => 'Menú Experiencia', 'price_cents' => 15000,
            'currency' => 'EUR', 'active' => true, 'version' => 1, 'created_at' => $now, 'updated_at' => $now,
        ]);
        DB::table('course_templates')->insert([
            'id' => $courseId, 'menu_template_id' => $menuId, 'sequence' => 1, 'name' => 'Primer pase', 'active' => true,
        ]);
        DB::table('users')->insert([
            'id' => $userId, 'tenant_id' => $tenantId, 'display_name' => 'Operator Test',
            'email' => $email, 'password_hash' => Hash::make('secret-password'), 'active' => true,
            'created_at' => $now, 'updated_at' => $now,
        ]);
        DB::table('roles')->insert([
            'id' => $roleId, 'tenant_id' => $tenantId, 'name' => 'Operator',
            'permissions' => json_encode(['service.view'], JSON_THROW_ON_ERROR),
        ]);
        DB::table('user_roles')->insert([
            'id' => Ulid::generate(), 'user_id' => $userId, 'role_id' => $roleId,
            'company_id' => $companyId, 'location_id' => $locationId,
        ]);

        config([
            'hospitality.tenant_id' => $tenantId,
            'hospitality.company_id' => $companyId,
            'hospitality.location_id' => $locationId,
        ]);

        $login = $this->postJson('/api/v1/auth/login', [
            'email' => $email,
            'password' => 'secret-password',
        ])->assertOk();

        $token = (string) $login->json('access_token');
        $this->withToken($token)->getJson('/api/v1/configuration')
            ->assertOk()
            ->assertJsonPath('data.tables.0.id', $tableId)
            ->assertJsonPath('data.tables.0.name', 'Mesa 1')
            ->assertJsonPath('data.tables.0.area', 'Sala principal')
            ->assertJsonPath('data.menus.0.id', $menuId)
            ->assertJsonPath('data.menus.0.course_count', 1)
            ->assertJsonPath('data.stations.0.id', $stationId)
            ->assertJsonPath('data.stations.0.name', 'Pescados');
    }
}
