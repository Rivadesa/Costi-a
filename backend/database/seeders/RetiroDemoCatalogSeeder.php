<?php

declare(strict_types=1);

namespace Database\Seeders;

use Illuminate\Database\Seeder;
use Illuminate\Support\Facades\DB;

final class RetiroDemoCatalogSeeder extends Seeder
{
    private const PRICE_LIST_ID = '01DEMO00000000000000000070';

    /** @var list<array{id:string,code:string,name:string,sequence:int}> */
    private const CATEGORIES = [
        ['id' => '01DEMO00000000000000000060', 'code' => 'waters', 'name' => 'Aguas', 'sequence' => 10],
        ['id' => '01DEMO00000000000000000061', 'code' => 'wine-glass', 'name' => 'Vino por copa', 'sequence' => 20],
        ['id' => '01DEMO00000000000000000062', 'code' => 'wine-bottle', 'name' => 'Vino por botella', 'sequence' => 30],
        ['id' => '01DEMO00000000000000000063', 'code' => 'other-drinks', 'name' => 'Otras bebidas', 'sequence' => 40],
        ['id' => '01DEMO00000000000000000064', 'code' => 'extras', 'name' => 'Extras', 'sequence' => 50],
    ];

    /** @var list<array<string,mixed>> */
    private const PRODUCTS = [
        ['id' => '01DEMO00000000000000000071', 'category' => '01DEMO00000000000000000060', 'code' => 'water-still-075', 'sku' => 'AGUA-075', 'name' => 'Agua mineral', 'type' => 'beverage', 'unit' => 'bottle', 'format' => '0,75 L', 'price' => 400, 'sequence' => 10],
        ['id' => '01DEMO00000000000000000072', 'category' => '01DEMO00000000000000000060', 'code' => 'water-sparkling-075', 'sku' => 'AGUAGAS-075', 'name' => 'Agua con gas', 'type' => 'beverage', 'unit' => 'bottle', 'format' => '0,75 L', 'price' => 450, 'sequence' => 20],
        ['id' => '01DEMO00000000000000000073', 'category' => '01DEMO00000000000000000061', 'code' => 'wine-glass-white', 'sku' => 'COPA-BLANCO', 'name' => 'Copa de vino blanco selección', 'type' => 'wine', 'unit' => 'glass', 'format' => 'Copa', 'price' => 900, 'sequence' => 10],
        ['id' => '01DEMO00000000000000000074', 'category' => '01DEMO00000000000000000061', 'code' => 'wine-glass-red', 'sku' => 'COPA-TINTO', 'name' => 'Copa de vino tinto selección', 'type' => 'wine', 'unit' => 'glass', 'format' => 'Copa', 'price' => 950, 'sequence' => 20],
        ['id' => '01DEMO00000000000000000075', 'category' => '01DEMO00000000000000000062', 'code' => 'wine-bottle-white', 'sku' => 'BOT-BLANCO', 'name' => 'Botella vino blanco selección', 'type' => 'wine', 'unit' => 'bottle', 'format' => '0,75 L', 'price' => 3800, 'sequence' => 10],
        ['id' => '01DEMO00000000000000000076', 'category' => '01DEMO00000000000000000062', 'code' => 'wine-bottle-red', 'sku' => 'BOT-TINTO', 'name' => 'Botella vino tinto selección', 'type' => 'wine', 'unit' => 'bottle', 'format' => '0,75 L', 'price' => 4200, 'sequence' => 20],
        ['id' => '01DEMO00000000000000000077', 'category' => '01DEMO00000000000000000063', 'code' => 'beer', 'sku' => 'CERVEZA', 'name' => 'Cerveza', 'type' => 'beverage', 'unit' => 'unit', 'format' => 'Botella', 'price' => 500, 'sequence' => 10],
        ['id' => '01DEMO00000000000000000078', 'category' => '01DEMO00000000000000000064', 'code' => 'coffee', 'sku' => 'CAFE', 'name' => 'Café', 'type' => 'extra', 'unit' => 'unit', 'format' => null, 'price' => 300, 'sequence' => 10],
    ];

    public function run(): void
    {
        DB::transaction(function (): void {
            $now = now();

            foreach (self::CATEGORIES as $category) {
                DB::table('product_categories')->updateOrInsert(
                    ['id' => $category['id']],
                    [
                        'tenant_id' => RetiroDemoSeeder::TENANT_ID,
                        'code' => $category['code'],
                        'name' => $category['name'],
                        'sequence' => $category['sequence'],
                        'active' => true,
                        'created_at' => $now,
                        'updated_at' => $now,
                    ],
                );
            }

            DB::table('price_lists')->updateOrInsert(
                ['id' => self::PRICE_LIST_ID],
                [
                    'tenant_id' => RetiroDemoSeeder::TENANT_ID,
                    'company_id' => RetiroDemoSeeder::COMPANY_ID,
                    'location_id' => RetiroDemoSeeder::LOCATION_ID,
                    'code' => 'restaurant',
                    'name' => 'Tarifa Restaurante',
                    'is_default' => true,
                    'active' => true,
                    'created_at' => $now,
                    'updated_at' => $now,
                ],
            );

            foreach (self::PRODUCTS as $index => $product) {
                DB::table('products')->updateOrInsert(
                    ['id' => $product['id']],
                    [
                        'tenant_id' => RetiroDemoSeeder::TENANT_ID,
                        'product_category_id' => $product['category'],
                        'code' => $product['code'],
                        'sku' => $product['sku'],
                        'name' => $product['name'],
                        'product_type' => $product['type'],
                        'sale_unit' => $product['unit'],
                        'format_label' => $product['format'],
                        'active' => true,
                        'sequence' => $product['sequence'],
                        'created_at' => $now,
                        'updated_at' => $now,
                    ],
                );

                DB::table('product_prices')->updateOrInsert(
                    ['id' => sprintf('01DEMO000000000000000000%02d', 80 + $index)],
                    [
                        'product_id' => $product['id'],
                        'price_list_id' => self::PRICE_LIST_ID,
                        'price_cents' => $product['price'],
                        'currency' => 'EUR',
                        'active' => true,
                        'created_at' => $now,
                        'updated_at' => $now,
                    ],
                );
            }
        });
    }
}
