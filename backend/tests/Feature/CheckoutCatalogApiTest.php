<?php

declare(strict_types=1);

namespace Hospitality\Tests\Feature;

use Database\Seeders\RetiroDemoSeeder;
use Hospitality\Domain\Shared\Ulid;
use Hospitality\Tests\TestCase;
use Illuminate\Support\Facades\DB;

final class CheckoutCatalogApiTest extends TestCase
{
    public function test_service_tracking_hides_money_while_checkout_snapshots_catalog_price(): void
    {
        $this->artisan('db:seed', ['--force' => true])->assertExitCode(0);
        config(['hospitality.tenant_id' => RetiroDemoSeeder::TENANT_ID,
            'hospitality.company_id' => RetiroDemoSeeder::COMPANY_ID,
            'hospitality.location_id' => RetiroDemoSeeder::LOCATION_ID]);
        $token = (string) $this->postJson('/api/v1/auth/login', [
            'email' => 'demo@hospitality.local', 'password' => 'demo1234',
        ])->assertOk()->json('access_token');
        $configuration = $this->withToken($token)->getJson('/api/v1/checkout/catalog')->assertOk();
        self::assertNotEmpty($configuration->json('data.sale_categories'));
        self::assertNotEmpty($configuration->json('data.sale_items'));
        $tableId = '01DEMO00000000000000000010';
        $productId = '01DEMO00000000000000000071';
        $opened = $this->withToken($token)->withHeader('Idempotency-Key', 'checkout-open-'.Ulid::generate())
            ->postJson('/api/v1/services', ['table_id' => $tableId, 'pax' => 2, 'menu_id' => RetiroDemoSeeder::MENU_ID])->assertCreated();
        $serviceId = (string) $opened->json('service_id');
        $operational = $this->withToken($token)->getJson('/api/v1/services/'.$serviceId)->assertOk()->json();
        self::assertArrayNotHasKey('consumptions', $operational);
        self::assertArrayNotHasKey('payments', $operational);
        self::assertArrayNotHasKey('subtotal_cents', $operational);
        self::assertArrayNotHasKey('paid_cents', $operational);
        self::assertArrayNotHasKey('balance_cents', $operational);
        self::assertArrayNotHasKey('unit_price_cents', $operational['menu']);
        $added = $this->withToken($token)->withHeader('Idempotency-Key', 'checkout-item-'.Ulid::generate())
            ->postJson('/api/v1/checkout/services/'.$serviceId.'/consumptions', ['product_id' => $productId, 'quantity' => 2])
            ->assertOk()->assertJsonPath('product_id', $productId)->assertJsonPath('quantity', 2)
            ->assertJsonPath('unit_price_cents', 400)->assertJsonPath('subtotal_cents', 30800);
        self::assertNotEmpty((string) $added->json('price_list_id'));
        DB::table('product_prices')->where('product_id', $productId)->update(['price_cents' => 999, 'updated_at' => now()]);
        $checkout = $this->withToken($token)->getJson('/api/v1/checkout/services/'.$serviceId)->assertOk()
            ->assertJsonPath('menu.unit_price_cents', 15000)->assertJsonPath('consumptions.0.product_id', $productId)
            ->assertJsonPath('consumptions.0.unit_price_cents', 400)->assertJsonPath('consumptions.0.total_cents', 800)
            ->assertJsonPath('subtotal_cents', 30800);
        self::assertSame(999, (int) DB::table('product_prices')->where('product_id', $productId)->value('price_cents'));
        self::assertSame(400, (int) $checkout->json('consumptions.0.unit_price_cents'));
        $boardRow = collect($this->withToken($token)->getJson('/api/v1/service-board')->assertOk()->json('data'))->firstWhere('service_id', $serviceId);
        self::assertIsArray($boardRow);
        self::assertArrayNotHasKey('subtotal_cents', $boardRow);
        self::assertArrayNotHasKey('paid_cents', $boardRow);
    }
}
