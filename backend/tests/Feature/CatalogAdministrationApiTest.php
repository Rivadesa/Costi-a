<?php

declare(strict_types=1);

namespace Hospitality\Tests\Feature;

use Database\Seeders\RetiroDemoSeeder;
use Hospitality\Domain\Shared\Ulid;
use Hospitality\Tests\TestCase;
use Illuminate\Foundation\Testing\DatabaseTransactions;
use Illuminate\Support\Facades\DB;

final class CatalogAdministrationApiTest extends TestCase
{
    use DatabaseTransactions;

    private function login(array $permissions = ['*']): string
    {
        $this->artisan('db:seed', ['--force' => true])->assertExitCode(0);
        config(['hospitality.tenant_id' => RetiroDemoSeeder::TENANT_ID,
            'hospitality.company_id' => RetiroDemoSeeder::COMPANY_ID,
            'hospitality.location_id' => RetiroDemoSeeder::LOCATION_ID]);
        DB::table('roles')->where('id', RetiroDemoSeeder::ROLE_ID)
            ->update(['permissions' => json_encode($permissions, JSON_THROW_ON_ERROR)]);
        return (string) $this->postJson('/api/v1/auth/login',
            ['email' => 'demo@hospitality.local', 'password' => 'demo1234'])->assertOk()->json('access_token');
    }

    private function input(array $changes = []): array
    {
        return array_replace(['id' => null, 'revision' => null,
            'price_list_id' => '01DEMO00000000000000000070', 'code' => 'WINE-'.Ulid::generate(),
            'name' => 'Vino de prueba', 'category_id' => '01DEMO00000000000000000061',
            'product_type' => 'wine', 'sale_unit' => 'glass', 'format_label' => '125 ml',
            'price_cents' => 950, 'available' => true], $changes);
    }

    public function test_waiter_cannot_read_prices_manage_catalog_or_fake_access_using_terminal_header(): void
    {
        $token = $this->login(['service.view', 'service.open', 'service.manage', 'service.edit']);
        $configuration = $this->withToken($token)->getJson('/api/v1/configuration')->assertOk()->json('data');
        self::assertArrayNotHasKey('sale_items', $configuration);
        foreach ($configuration['menus'] as $menu) self::assertArrayNotHasKey('price_cents', $menu);
        $this->withToken($token)->withHeader('X-Terminal-Mode', 'main')->getJson('/api/v1/checkout/catalog')->assertForbidden();
        $this->withToken($token)->getJson('/api/v1/admin/catalog')->assertForbidden();
        $this->withToken($token)->withHeader('Idempotency-Key', Ulid::generate())
            ->postJson('/api/v1/admin/catalog/products', $this->input())->assertForbidden();
        $serviceId = $this->withToken($token)->withHeader('Idempotency-Key', Ulid::generate())->postJson('/api/v1/services',
            ['table_id' => '01DEMO00000000000000000017', 'pax' => 2, 'menu_id' => RetiroDemoSeeder::MENU_ID])->assertCreated()->json('service_id');
        $started = $this->withToken($token)->withHeader('Idempotency-Key', Ulid::generate())
            ->postJson('/api/v1/services/'.$serviceId.'/start')->assertOk()->json();
        self::assertArrayNotHasKey('subtotal_cents', $started);
        self::assertArrayNotHasKey('paid_cents', $started);
    }

    public function test_catalog_create_is_idempotent_audited_and_outboxed(): void
    {
        $token = $this->login();
        $input = $this->input(); $key = Ulid::generate();
        $created = $this->withToken($token)->withHeader('Idempotency-Key', $key)
            ->postJson('/api/v1/admin/catalog/products', $input)->assertOk()->json('data');
        $replayed = $this->withToken($token)->withHeader('Idempotency-Key', $key)
            ->postJson('/api/v1/admin/catalog/products', $input)->assertOk()->json('data');
        // JSON object property order can change after PostgreSQL JSONB storage.
        // Normalize this flat object's keys; retain strict value/type equality.
        ksort($created, SORT_STRING);
        ksort($replayed, SORT_STRING);
        self::assertSame($created, $replayed);
        self::assertSame(1, DB::table('products')->where('id', $created['id'])->count());
        self::assertSame(1, DB::table('audit_log')->where('entity_id', $created['id'])->count());
        self::assertSame(1, DB::table('outbox_events')->where('aggregate_id', $created['id'])->count());
        $this->withToken($token)->withHeader('Idempotency-Key', $key)
            ->postJson('/api/v1/admin/catalog/products', array_replace($input, ['price_cents' => 1000]))->assertConflict();
    }

    public function test_update_rejects_stale_revision_and_does_not_reprice_an_existing_consumption(): void
    {
        $token = $this->login();
        $input = $this->input();
        $created = $this->withToken($token)->withHeader('Idempotency-Key', Ulid::generate())
            ->postJson('/api/v1/admin/catalog/products', $input)->assertOk()->json('data');
        $serviceId = $this->withToken($token)->withHeader('Idempotency-Key', Ulid::generate())->postJson('/api/v1/services',
            ['table_id' => '01DEMO00000000000000000017', 'pax' => 1, 'menu_id' => RetiroDemoSeeder::MENU_ID])->assertCreated()->json('service_id');
        $this->withToken($token)->withHeader('Idempotency-Key', Ulid::generate())
            ->postJson('/api/v1/checkout/services/'.$serviceId.'/consumptions', ['product_id' => $created['id'], 'quantity' => 1])->assertOk();
        $update = array_replace($input, ['id' => $created['id'], 'revision' => $created['revision'],
            'name' => 'Vino renombrado', 'price_cents' => 1200, 'format_label' => '150 ml']);
        $saved = $this->withToken($token)->withHeader('Idempotency-Key', Ulid::generate())
            ->postJson('/api/v1/admin/catalog/products', $update)->assertOk()->json('data');
        $this->withToken($token)->withHeader('Idempotency-Key', Ulid::generate())
            ->postJson('/api/v1/admin/catalog/products', $update)->assertConflict();
        $account = $this->withToken($token)->getJson('/api/v1/checkout/services/'.$serviceId)->assertOk()->json();
        self::assertSame(950, $account['consumptions'][0]['unit_price_cents']);
        self::assertSame('Vino de prueba · Copa · 125 ml', $account['consumptions'][0]['name']);
        $this->withToken($token)->withHeader('Idempotency-Key', Ulid::generate())
            ->postJson('/api/v1/admin/catalog/products', array_replace($update, ['revision' => $saved['revision'], 'available' => false]))->assertOk();
        $this->withToken($token)->withHeader('Idempotency-Key', Ulid::generate())
            ->postJson('/api/v1/checkout/services/'.$serviceId.'/consumptions', ['product_id' => $created['id'], 'quantity' => 1])->assertConflict();
        self::assertTrue((bool) DB::table('products')->where('id', $created['id'])->value('active'));
        self::assertSame(1, DB::table('consumptions')->where('table_service_id', $serviceId)->count());
    }

    public function test_foreign_category_and_tariff_are_rejected_without_writes(): void
    {
        $token = $this->login();
        $foreignTenant = Ulid::generate(); $foreignCategory = Ulid::generate();
        DB::table('tenants')->insert(['id' => $foreignTenant, 'name' => 'Other tenant', 'created_at' => now(), 'updated_at' => now()]);
        DB::table('product_categories')->insert(['id' => $foreignCategory, 'tenant_id' => $foreignTenant, 'code' => 'FOREIGN', 'name' => 'Private', 'sequence' => 1, 'active' => true]);
        $before = DB::table('products')->count();
        $this->withToken($token)->withHeader('Idempotency-Key', Ulid::generate())
            ->postJson('/api/v1/admin/catalog/products', $this->input(['category_id' => $foreignCategory]))->assertConflict();
        $this->withToken($token)->withHeader('Idempotency-Key', Ulid::generate())
            ->postJson('/api/v1/admin/catalog/products', $this->input(['price_list_id' => Ulid::generate()]))->assertConflict();
        $this->withToken($token)->withHeader('Idempotency-Key', Ulid::generate())
            ->postJson('/api/v1/admin/catalog/products', $this->input(['id' => Ulid::generate(), 'revision' => str_repeat('a', 64)]))->assertConflict();
        self::assertSame($before, DB::table('products')->count());
    }

    public function test_duplicate_codes_case_insensitive_and_negative_price_are_rejected(): void
    {
        $token = $this->login();
        $this->withToken($token)->withHeader('Idempotency-Key', Ulid::generate())
            ->postJson('/api/v1/admin/catalog/products', $this->input(['code' => 'WATER-STILL-075']))->assertConflict();
        $this->withToken($token)->withHeader('Idempotency-Key', Ulid::generate())
            ->postJson('/api/v1/admin/catalog/products', $this->input(['price_cents' => -1]))->assertUnprocessable();
    }
}
