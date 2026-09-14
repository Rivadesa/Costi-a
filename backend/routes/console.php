<?php

declare(strict_types=1);

use App\Infrastructure\Provisioning\PilotProvisioner;
use Illuminate\Support\Facades\Artisan;

Artisan::command('hospitality:about', function (): void {
    $this->info('Hospitality OS / Costi-a V1A local-primary backend');
})->purpose('Show Hospitality OS backend identity');

Artisan::command('hospitality:provision {--profile=retiro-pilot} {--password=}', function (PilotProvisioner $provisioner): int {
    $profile = (string) $this->option('profile');
    $password = (string) $this->option('password');

    if ($password === '') {
        $this->error('A pilot password is required. Use --password=<12+ characters>.');
        return 1;
    }

    try {
        $result = $provisioner->provision($profile, $password);
    } catch (Throwable $exception) {
        $this->error($exception->getMessage());
        return 1;
    }

    $this->info('Pilot profile provisioned successfully.');
    $this->warn((string) $result['warning']);
    $this->table(
        ['Key', 'Value'],
        [
            ['profile', (string) $result['profile']],
            ['tenant_id', (string) $result['tenant_id']],
            ['company_id', (string) $result['company_id']],
            ['location_id', (string) $result['location_id']],
            ['tables', (string) count($result['tables'])],
            ['stations', (string) count($result['stations'])],
            ['menu_id', (string) $result['menu_id']],
        ],
    );

    $this->newLine();
    $this->line('Pilot users (all use the supplied password):');
    foreach ($result['users'] as $email => $role) {
        $this->line(sprintf('  %-30s %s', $email, $role));
    }

    return 0;
})->purpose('Provision the idempotent Retiro V1A pilot profile');
