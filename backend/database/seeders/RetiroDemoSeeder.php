<?php

declare(strict_types=1);

namespace Database\Seeders;

use Illuminate\Database\Seeder;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Hash;

final class RetiroDemoSeeder extends Seeder
{
    public const TENANT_ID = '01DEMO00000000000000000000';
    public const COMPANY_ID = '01DEMO00000000000000000001';
    public const LOCATION_ID = '01DEMO00000000000000000002';
    public const USER_ID = '01DEMO00000000000000000003';
    public const ROLE_ID = '01DEMO00000000000000000004';
    public const AREA_ID = '01DEMO00000000000000000005';
    public const MENU_ID = '01DEMO00000000000000000030';

    /** @var array<string,string> */
    private const STATIONS = [
        'cold' => '01DEMO00000000000000000020',
        'hot' => '01DEMO00000000000000000021',
        'fish' => '01DEMO00000000000000000022',
        'meat' => '01DEMO00000000000000000023',
        'pastry' => '01DEMO00000000000000000024',
        'pass' => '01DEMO00000000000000000025',
    ];

    /** @var list<array{name:string,station:string}> */
    private const COURSES = [
        ['name' => 'Aperitivos', 'station' => 'cold'],
        ['name' => 'Entrante frío', 'station' => 'cold'],
        ['name' => 'Entrante caliente', 'station' => 'hot'],
        ['name' => 'Pescado', 'station' => 'fish'],
        ['name' => 'Carne', 'station' => 'meat'],
        ['name' => 'Prepostre', 'station' => 'pastry'],
        ['name' => 'Postre', 'station' => 'pastry'],
    ];

    public function run(): void
    {
        DB::transaction(function (): void {
            $now = now();

            DB::table('tenants')->updateOrInsert(
                ['id' => self::TENANT_ID],
                ['name' => 'Retiro Demo', 'created_at' => $now, 'updated_at' => $now],
            );

            DB::table('companies')->updateOrInsert(
                ['id' => self::COMPANY_ID],
                [
                    'tenant_id' => self::TENANT_ID,
                    'legal_name' => 'Retiro Demo SL',
                    'trade_name' => 'Retiro da Costiña · Demo',
                    'tax_id' => 'DEMO00000',
                    'active' => true,
                    'created_at' => $now,
                    'updated_at' => $now,
                ],
            );

            DB::table('locations')->updateOrInsert(
                ['id' => self::LOCATION_ID],
                [
                    'tenant_id' => self::TENANT_ID,
                    'company_id' => self::COMPANY_ID,
                    'name' => 'Retiro da Costiña · Demo',
                    'timezone' => 'Europe/Madrid',
                    'active' => true,
                    'created_at' => $now,
                    'updated_at' => $now,
                ],
            );

            DB::table('users')->updateOrInsert(
                ['id' => self::USER_ID],
                [
                    'tenant_id' => self::TENANT_ID,
                    'display_name' => 'Administrador Demo',
                    'email' => 'demo@hospitality.local',
                    'password_hash' => Hash::make('demo1234'),
                    'active' => true,
                    'created_at' => $now,
                    'updated_at' => $now,
                ],
            );

            DB::table('roles')->updateOrInsert(
                ['id' => self::ROLE_ID],
                [
                    'tenant_id' => self::TENANT_ID,
                    'name' => 'Administrador Demo',
                    'permissions' => json_encode(['*'], JSON_THROW_ON_ERROR),
                ],
            );

            DB::table('user_roles')->updateOrInsert(
                [
                    'id' => '01DEMO00000000000000000006',
                ],
                [
                    'user_id' => self::USER_ID,
                    'role_id' => self::ROLE_ID,
                    'company_id' => self::COMPANY_ID,
                    'location_id' => self::LOCATION_ID,
                ],
            );

            DB::table('dining_areas')->updateOrInsert(
                ['id' => self::AREA_ID],
                [
                    'tenant_id' => self::TENANT_ID,
                    'company_id' => self::COMPANY_ID,
                    'location_id' => self::LOCATION_ID,
                    'name' => 'Sala principal',
                    'sequence' => 1,
                    'active' => true,
                ],
            );

            foreach (range(1, 8) as $number) {
                DB::table('dining_tables')->updateOrInsert(
                    ['id' => sprintf('01DEMO000000000000000000%02d', 10 + $number - 1)],
                    [
                        'tenant_id' => self::TENANT_ID,
                        'company_id' => self::COMPANY_ID,
                        'location_id' => self::LOCATION_ID,
                        'dining_area_id' => self::AREA_ID,
                        'code' => (string) $number,
                        'name' => "Mesa {$number}",
                        'capacity' => $number === 8 ? 8 : 6,
                        'sequence' => $number,
                        'active' => true,
                    ],
                );
            }

            $stationNames = [
                'cold' => 'Fríos',
                'hot' => 'Calientes',
                'fish' => 'Pescados',
                'meat' => 'Carnes',
                'pastry' => 'Postres',
                'pass' => 'Pase',
            ];

            $stationSequence = 1;
            foreach (self::STATIONS as $key => $stationId) {
                DB::table('kitchen_stations')->updateOrInsert(
                    ['id' => $stationId],
                    [
                        'tenant_id' => self::TENANT_ID,
                        'company_id' => self::COMPANY_ID,
                        'location_id' => self::LOCATION_ID,
                        'name' => $stationNames[$key],
                        'sequence' => $stationSequence++,
                        'active' => true,
                    ],
                );
            }

            DB::table('menu_templates')->updateOrInsert(
                ['id' => self::MENU_ID],
                [
                    'tenant_id' => self::TENANT_ID,
                    'company_id' => self::COMPANY_ID,
                    'location_id' => self::LOCATION_ID,
                    'name' => 'Menú Experiencia Demo',
                    'price_cents' => 15000,
                    'currency' => 'EUR',
                    'active' => true,
                    'version' => 1,
                    'created_at' => $now,
                    'updated_at' => $now,
                ],
            );

            foreach (self::COURSES as $index => $definition) {
                $sequence = $index + 1;
                $courseId = sprintf('01DEMO000000000000000000%02d', 31 + $index);
                $preparationId = sprintf('01DEMO000000000000000000%02d', 41 + $index);

                DB::table('course_templates')->updateOrInsert(
                    ['id' => $courseId],
                    [
                        'menu_template_id' => self::MENU_ID,
                        'sequence' => $sequence,
                        'name' => $definition['name'],
                        'active' => true,
                    ],
                );

                DB::table('preparation_templates')->updateOrInsert(
                    ['id' => $preparationId],
                    [
                        'course_template_id' => $courseId,
                        'station_id' => self::STATIONS[$definition['station']],
                        'name' => $definition['name'],
                        'quantity_mode' => 'per_guest',
                        'fixed_quantity' => 1,
                        'mandatory' => true,
                        'sequence' => 1,
                    ],
                );
            }

            // Pescado y carne necesitan además una elaboración del pase para comprobar multiestación.
            foreach ([4 => 55, 5 => 56] as $courseSequence => $suffix) {
                $courseId = sprintf('01DEMO000000000000000000%02d', 30 + $courseSequence);
                DB::table('preparation_templates')->updateOrInsert(
                    ['id' => sprintf('01DEMO000000000000000000%02d', $suffix)],
                    [
                        'course_template_id' => $courseId,
                        'station_id' => self::STATIONS['pass'],
                        'name' => 'Guarnición / pase',
                        'quantity_mode' => 'fixed',
                        'fixed_quantity' => 1,
                        'mandatory' => true,
                        'sequence' => 2,
                    ],
                );
            }
        });
    }
}
