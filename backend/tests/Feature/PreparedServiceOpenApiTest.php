<?php

declare(strict_types=1);

namespace Hospitality\Tests\Feature;

use Hospitality\Domain\Shared\Ulid;
use Hospitality\Tests\TestCase;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Hash;

final class PreparedServiceOpenApiTest extends TestCase
{
    public function test_open_service_creates_menu_snapshot_and_all_pax_atomically(): void
    {
        $tenantId = Ulid::generate();
        $companyId = Ulid::generate();
        $locationId = Ulid::generate();
        $areaId = Ulid::generate();
        $tableId = Ulid::generate();
        $menuId = Ulid::generate();
        $userId = Ulid::generate();
        $roleId = Ulid::generate();
        $now = now();
        $email = strtolower(Ulid::generate()).'@example.test';

        DB::table('tenants')->insert(['id' => $tenantId, 'name' => 'Prepared Tenant', 'created_at' => $now, 'updated_at' => $now]);
        DB::table('companies')->insert(['id' => $companyId, 'tenant_id' => $tenantId, 'legal_name' => 'Prepared SL', 'active' => true, 'created_at' => $now, 'updated_at' => $now]);
        DB::table('locations')->insert(['id' => $locationId, 'tenant_id' => $tenantId, 'company_id' => $companyId, 'name' => 'Retiro', 'timezone' => 'Europe/Madrid', 'active' => true, 'created_at' => $now, 'updated_at' => $now]);
        DB::table('dining_areas')->insert(['id' => $areaId, 'tenant_id' => $tenantId, 'company_id' => $companyId, 'location_id' => $locationId, 'name' => 'Sala', 'sequence' => 1, 'active' => true]);
        DB::table('dining_tables')->insert(['id' => $tableId, 'tenant_id' => $tenantId, 'company_id' => $companyId, 'location_id' => $locationId, 'dining_area_id' => $areaId, 'code' => 'M1', 'name' => 'Mesa 1', 'capacity' => 6, 'sequence' => 1, 'active' => true]);
        DB::table('menu_templates')->insert(['id' => $menuId, 'tenant_id' => $tenantId, 'company_id' => $companyId, 'location_id' => $locationId, 'name' => 'Experiencia', 'price_cents' => 15000, 'currency' => 'EUR', 'active' => true, 'version' => 1, 'created_at' => $now, 'updated_at' => $now]);
        DB::table('course_templates')->insert(['id' => Ulid::generate(), 'menu_template_id' => $menuId, 'sequence' => 1, 'name' => 'Snack', 'active' => true]);
        DB::table('users')->insert(['id' => $userId, 'tenant_id' => $tenantId, 'display_name' => 'Maître', 'email' => $email, 'password_hash' => Hash::make('secret-password'), 'active' => true, 'created_at' => $now, 'updated_at' => $now]);
        DB::table('roles')->insert(['id' => $roleId, 'tenant_id' => $tenantId, 'name' => 'Maître', 'permissions' => json_encode(['service.open', 'service.view'], JSON_THROW_ON_ERROR)]);
        DB::table('user_roles')->insert(['id' => Ulid::generate(), 'user_id' => $userId, 'role_id' => $roleId, 'company_id' => $companyId, 'location_id' => $locationId]);

        config(['hospitality.tenant_id' => $tenantId, 'hospitality.company_id' => $companyId, 'hospitality.location_id' => $locationId]);
        $token = (string) $this->postJson('/api/v1/auth/login', ['email' => $email, 'password' => 'secret-password'])->assertOk()->json('access_token');

        $opened = $this->withToken($token)
            ->withHeader('Idempotency-Key', 'open-prepared-'.Ulid::generate())
            ->postJson('/api/v1/services', ['table_id' => $tableId, 'pax' => 3, 'menu_id' => $menuId])
            ->assertCreated()
            ->assertJsonPath('menu_id', $menuId)
            ->assertJsonPath('guest_count', 3);

        $serviceId = (string) $opened->json('service_id');
        $this->withToken($token)->getJson('/api/v1/services/'.$serviceId)
            ->assertOk()
            ->assertJsonPath('menu.id', $menuId)
            ->assertJsonCount(3, 'guests')
            ->assertJsonPath('guests.0.position', 1)
            ->assertJsonPath('guests.2.position', 3);

        self::assertSame(3, DB::table('service_guests')->where('table_service_id', $serviceId)->count());
        self::assertSame(1, DB::table('service_menus')->where('table_service_id', $serviceId)->count());
    }
}
