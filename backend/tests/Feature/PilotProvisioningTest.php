<?php

declare(strict_types=1);

namespace Hospitality\Tests\Feature;

use App\Infrastructure\Provisioning\PilotProvisioner;
use Hospitality\Tests\TestCase;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Hash;

final class PilotProvisioningTest extends TestCase
{
    public function test_retiro_pilot_profile_is_complete_and_idempotent(): void
    {
        /** @var PilotProvisioner $provisioner */
        $provisioner = $this->app->make(PilotProvisioner::class);
        $password = 'pilot-password-2026';

        $first = $provisioner->provision('retiro-pilot', $password);
        $second = $provisioner->provision('retiro-pilot', $password);

        self::assertSame($first['tenant_id'], $second['tenant_id']);
        self::assertSame($first['company_id'], $second['company_id']);
        self::assertSame($first['location_id'], $second['location_id']);
        self::assertSame($first['menu_id'], $second['menu_id']);

        self::assertSame(8, DB::table('dining_tables')->where('location_id', $first['location_id'])->count());
        self::assertSame(6, DB::table('kitchen_stations')->where('location_id', $first['location_id'])->count());
        self::assertSame(9, DB::table('course_templates')->where('menu_template_id', $first['menu_id'])->count());
        self::assertSame(5, DB::table('users')->where('tenant_id', $first['tenant_id'])->count());
        self::assertSame(5, DB::table('roles')->where('tenant_id', $first['tenant_id'])->count());

        $admin = DB::table('users')
            ->where('tenant_id', $first['tenant_id'])
            ->where('email', 'admin@hospitality.local')
            ->first();

        self::assertNotNull($admin);
        self::assertTrue(Hash::check($password, (string) $admin->password_hash));

        $menu = DB::table('menu_templates')->where('id', $first['menu_id'])->first();
        self::assertNotNull($menu);
        self::assertSame('Menú degustación piloto (NO REAL)', $menu->name);
        self::assertSame(15000, $menu->price_cents);
    }

    public function test_short_password_is_rejected(): void
    {
        $this->expectException(\InvalidArgumentException::class);

        /** @var PilotProvisioner $provisioner */
        $provisioner = $this->app->make(PilotProvisioner::class);
        $provisioner->provision('retiro-pilot', 'short');
    }
}
