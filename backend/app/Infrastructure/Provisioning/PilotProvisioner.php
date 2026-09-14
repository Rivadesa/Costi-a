<?php

declare(strict_types=1);

namespace App\Infrastructure\Provisioning;

use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Hash;
use Illuminate\Support\Str;
use InvalidArgumentException;

final class PilotProvisioner
{
    /**
     * Provision a non-production pilot profile. Running the same profile twice is idempotent:
     * existing business records are updated in place instead of duplicated.
     *
     * @return array<string, mixed>
     */
    public function provision(string $profile, string $password): array
    {
        if ($profile !== 'retiro-pilot') {
            throw new InvalidArgumentException(sprintf('Unknown pilot profile "%s".', $profile));
        }

        if (mb_strlen($password) < 12) {
            throw new InvalidArgumentException('Pilot password must contain at least 12 characters.');
        }

        return DB::transaction(function () use ($password): array {
            $tenantId = $this->ensureTenant('Retiro da Costiña Pilot');
            $companyId = $this->ensureCompany($tenantId, 'Retiro da Costiña', 'Retiro da Costiña');
            $locationId = $this->ensureLocation($tenantId, $companyId, 'Restaurante');
            $areaId = $this->ensureDiningArea($tenantId, $companyId, $locationId, 'Sala principal');

            $tables = [];
            for ($number = 1; $number <= 8; $number++) {
                $tables[] = $this->ensureDiningTable(
                    $tenantId,
                    $companyId,
                    $locationId,
                    $areaId,
                    'M'.$number,
                    'Mesa '.$number,
                    8,
                    $number,
                );
            }

            $stationNames = ['Fríos', 'Calientes', 'Pescados', 'Carnes', 'Postres', 'Pase'];
            $stations = [];
            foreach ($stationNames as $sequence => $name) {
                $stations[$name] = $this->ensureKitchenStation(
                    $tenantId,
                    $companyId,
                    $locationId,
                    $name,
                    $sequence + 1,
                );
            }

            $menuId = $this->ensureMenu($tenantId, $companyId, $locationId);
            $this->ensureMenuCourses($menuId, $stations);

            $roles = $this->ensureRoles($tenantId);
            $users = $this->ensurePilotUsers($tenantId, $companyId, $locationId, $roles, $password);

            return [
                'profile' => 'retiro-pilot',
                'warning' => 'Pilot/demo data only. Review menu, prices, table capacities and users before any real service.',
                'tenant_id' => $tenantId,
                'company_id' => $companyId,
                'location_id' => $locationId,
                'tables' => $tables,
                'stations' => $stations,
                'menu_id' => $menuId,
                'users' => $users,
            ];
        });
    }

    private function ensureTenant(string $name): string
    {
        $existing = DB::table('tenants')->where('name', $name)->value('id');
        if (is_string($existing)) {
            DB::table('tenants')->where('id', $existing)->update(['updated_at' => now()]);
            return $existing;
        }

        $id = $this->id();
        DB::table('tenants')->insert([
            'id' => $id,
            'name' => $name,
            'created_at' => now(),
            'updated_at' => now(),
        ]);
        return $id;
    }

    private function ensureCompany(string $tenantId, string $legalName, string $tradeName): string
    {
        $existing = DB::table('companies')
            ->where('tenant_id', $tenantId)
            ->where('legal_name', $legalName)
            ->value('id');

        if (is_string($existing)) {
            DB::table('companies')->where('id', $existing)->update([
                'trade_name' => $tradeName,
                'active' => true,
                'updated_at' => now(),
            ]);
            return $existing;
        }

        $id = $this->id();
        DB::table('companies')->insert([
            'id' => $id,
            'tenant_id' => $tenantId,
            'legal_name' => $legalName,
            'trade_name' => $tradeName,
            'tax_id' => null,
            'active' => true,
            'created_at' => now(),
            'updated_at' => now(),
        ]);
        return $id;
    }

    private function ensureLocation(string $tenantId, string $companyId, string $name): string
    {
        $existing = DB::table('locations')
            ->where('tenant_id', $tenantId)
            ->where('company_id', $companyId)
            ->where('name', $name)
            ->value('id');

        if (is_string($existing)) {
            DB::table('locations')->where('id', $existing)->update([
                'timezone' => 'Europe/Madrid',
                'active' => true,
                'updated_at' => now(),
            ]);
            return $existing;
        }

        $id = $this->id();
        DB::table('locations')->insert([
            'id' => $id,
            'tenant_id' => $tenantId,
            'company_id' => $companyId,
            'name' => $name,
            'timezone' => 'Europe/Madrid',
            'active' => true,
            'created_at' => now(),
            'updated_at' => now(),
        ]);
        return $id;
    }

    private function ensureDiningArea(string $tenantId, string $companyId, string $locationId, string $name): string
    {
        $existing = DB::table('dining_areas')
            ->where('location_id', $locationId)
            ->where('name', $name)
            ->value('id');

        if (is_string($existing)) {
            DB::table('dining_areas')->where('id', $existing)->update(['active' => true]);
            return $existing;
        }

        $id = $this->id();
        DB::table('dining_areas')->insert([
            'id' => $id,
            'tenant_id' => $tenantId,
            'company_id' => $companyId,
            'location_id' => $locationId,
            'name' => $name,
            'sequence' => 1,
            'active' => true,
        ]);
        return $id;
    }

    private function ensureDiningTable(
        string $tenantId,
        string $companyId,
        string $locationId,
        string $areaId,
        string $code,
        string $name,
        int $capacity,
        int $sequence,
    ): string {
        $existing = DB::table('dining_tables')
            ->where('location_id', $locationId)
            ->where('code', $code)
            ->value('id');

        $values = [
            'dining_area_id' => $areaId,
            'name' => $name,
            'capacity' => $capacity,
            'sequence' => $sequence,
            'active' => true,
        ];

        if (is_string($existing)) {
            DB::table('dining_tables')->where('id', $existing)->update($values);
            return $existing;
        }

        $id = $this->id();
        DB::table('dining_tables')->insert($values + [
            'id' => $id,
            'tenant_id' => $tenantId,
            'company_id' => $companyId,
            'location_id' => $locationId,
            'code' => $code,
        ]);
        return $id;
    }

    private function ensureKitchenStation(
        string $tenantId,
        string $companyId,
        string $locationId,
        string $name,
        int $sequence,
    ): string {
        $existing = DB::table('kitchen_stations')
            ->where('location_id', $locationId)
            ->where('name', $name)
            ->value('id');

        if (is_string($existing)) {
            DB::table('kitchen_stations')->where('id', $existing)->update([
                'sequence' => $sequence,
                'active' => true,
            ]);
            return $existing;
        }

        $id = $this->id();
        DB::table('kitchen_stations')->insert([
            'id' => $id,
            'tenant_id' => $tenantId,
            'company_id' => $companyId,
            'location_id' => $locationId,
            'name' => $name,
            'sequence' => $sequence,
            'active' => true,
        ]);
        return $id;
    }

    private function ensureMenu(string $tenantId, string $companyId, string $locationId): string
    {
        $name = 'Menú degustación piloto (NO REAL)';
        $existing = DB::table('menu_templates')
            ->where('tenant_id', $tenantId)
            ->where('company_id', $companyId)
            ->where('location_id', $locationId)
            ->where('name', $name)
            ->value('id');

        $values = [
            'price_cents' => 15000,
            'currency' => 'EUR',
            'active' => true,
            'version' => 1,
            'updated_at' => now(),
        ];

        if (is_string($existing)) {
            DB::table('menu_templates')->where('id', $existing)->update($values);
            return $existing;
        }

        $id = $this->id();
        DB::table('menu_templates')->insert($values + [
            'id' => $id,
            'tenant_id' => $tenantId,
            'company_id' => $companyId,
            'location_id' => $locationId,
            'name' => $name,
            'created_at' => now(),
        ]);
        return $id;
    }

    /** @param array<string, string> $stations */
    private function ensureMenuCourses(string $menuId, array $stations): void
    {
        $courses = [
            ['Snacks', [['Snacks', 'Fríos']]],
            ['Aperitivos', [['Aperitivos', 'Fríos']]],
            ['Entrante I', [['Entrante I', 'Calientes']]],
            ['Entrante II', [['Entrante II', 'Calientes']]],
            ['Pescado', [['Pescado', 'Pescados'], ['Guarnición pescado', 'Calientes']]],
            ['Carne', [['Carne', 'Carnes'], ['Guarnición carne', 'Calientes']]],
            ['Prepostre', [['Prepostre', 'Postres']]],
            ['Postre', [['Postre', 'Postres']]],
            ['Petit fours', [['Petit fours', 'Postres']]],
        ];

        foreach ($courses as $index => [$courseName, $preparations]) {
            $sequence = $index + 1;
            $courseId = DB::table('course_templates')
                ->where('menu_template_id', $menuId)
                ->where('sequence', $sequence)
                ->value('id');

            if (!is_string($courseId)) {
                $courseId = $this->id();
                DB::table('course_templates')->insert([
                    'id' => $courseId,
                    'menu_template_id' => $menuId,
                    'sequence' => $sequence,
                    'name' => $courseName,
                    'active' => true,
                ]);
            } else {
                DB::table('course_templates')->where('id', $courseId)->update([
                    'name' => $courseName,
                    'active' => true,
                ]);
            }

            foreach ($preparations as $preparationIndex => [$preparationName, $stationName]) {
                $stationId = $stations[$stationName];
                $existing = DB::table('preparation_templates')
                    ->where('course_template_id', $courseId)
                    ->where('station_id', $stationId)
                    ->where('name', $preparationName)
                    ->value('id');

                $values = [
                    'quantity_mode' => 'per_guest',
                    'fixed_quantity' => 1,
                    'mandatory' => true,
                    'sequence' => $preparationIndex + 1,
                ];

                if (is_string($existing)) {
                    DB::table('preparation_templates')->where('id', $existing)->update($values);
                    continue;
                }

                DB::table('preparation_templates')->insert($values + [
                    'id' => $this->id(),
                    'course_template_id' => $courseId,
                    'station_id' => $stationId,
                    'name' => $preparationName,
                ]);
            }
        }
    }

    /** @return array<string, string> */
    private function ensureRoles(string $tenantId): array
    {
        $definitions = [
            'Administrador' => [
                'service.view', 'service.open', 'service.edit', 'service.manage', 'service.close',
                'course.fire', 'course.serve', 'course.modify',
                'kitchen.view', 'kitchen.update', 'kitchen.pass',
                'consumption.add', 'consumption.cancel', 'payment.record',
            ],
            'Maître' => [
                'service.view', 'service.open', 'service.edit', 'service.manage', 'service.close',
                'course.fire', 'course.serve', 'course.modify',
                'kitchen.view', 'kitchen.pass',
                'consumption.add', 'consumption.cancel', 'payment.record',
            ],
            'Camarero' => [
                'service.view', 'service.open', 'service.edit',
                'course.fire', 'course.serve',
                'consumption.add', 'payment.record',
            ],
            'Chef' => [
                'service.view', 'service.manage',
                'course.fire', 'course.modify',
                'kitchen.view', 'kitchen.update', 'kitchen.pass',
            ],
            'Cocina' => ['service.view', 'kitchen.view', 'kitchen.update'],
        ];

        $result = [];
        foreach ($definitions as $name => $permissions) {
            $existing = DB::table('roles')
                ->where('tenant_id', $tenantId)
                ->where('name', $name)
                ->value('id');

            $encoded = json_encode($permissions, JSON_THROW_ON_ERROR);
            if (is_string($existing)) {
                DB::table('roles')->where('id', $existing)->update(['permissions' => $encoded]);
                $result[$name] = $existing;
                continue;
            }

            $id = $this->id();
            DB::table('roles')->insert([
                'id' => $id,
                'tenant_id' => $tenantId,
                'name' => $name,
                'permissions' => $encoded,
            ]);
            $result[$name] = $id;
        }

        return $result;
    }

    /**
     * @param array<string, string> $roles
     * @return array<string, string>
     */
    private function ensurePilotUsers(
        string $tenantId,
        string $companyId,
        string $locationId,
        array $roles,
        string $password,
    ): array {
        $definitions = [
            'admin@hospitality.local' => ['Administrador piloto', 'Administrador'],
            'maitre@hospitality.local' => ['Maître piloto', 'Maître'],
            'camarero@hospitality.local' => ['Camarero piloto', 'Camarero'],
            'chef@hospitality.local' => ['Chef piloto', 'Chef'],
            'cocina@hospitality.local' => ['Cocina piloto', 'Cocina'],
        ];

        $result = [];
        foreach ($definitions as $email => [$displayName, $roleName]) {
            $userId = DB::table('users')
                ->where('tenant_id', $tenantId)
                ->where('email', $email)
                ->value('id');

            $values = [
                'display_name' => $displayName,
                'password_hash' => Hash::make($password),
                'active' => true,
                'updated_at' => now(),
            ];

            if (is_string($userId)) {
                DB::table('users')->where('id', $userId)->update($values);
            } else {
                $userId = $this->id();
                DB::table('users')->insert($values + [
                    'id' => $userId,
                    'tenant_id' => $tenantId,
                    'email' => $email,
                    'created_at' => now(),
                ]);
            }

            $roleId = $roles[$roleName];
            $assignmentExists = DB::table('user_roles')
                ->where('user_id', $userId)
                ->where('role_id', $roleId)
                ->where('company_id', $companyId)
                ->where('location_id', $locationId)
                ->exists();

            if (!$assignmentExists) {
                DB::table('user_roles')->insert([
                    'id' => $this->id(),
                    'user_id' => $userId,
                    'role_id' => $roleId,
                    'company_id' => $companyId,
                    'location_id' => $locationId,
                ]);
            }

            $result[$email] = $roleName;
        }

        return $result;
    }

    private function id(): string
    {
        return (string) Str::ulid();
    }
}
