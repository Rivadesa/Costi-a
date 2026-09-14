<?php

declare(strict_types=1);

use App\Infrastructure\Provisioning\PilotProvisioner;
use Illuminate\Support\Facades\Artisan;

Artisan::command('hospitality:about', function (): void {
    $this->info('Hospitality OS / Costi-a V1A local-primary backend');
})->purpose('Show Hospitality OS backend identity');

Artisan::command('hospitality:provision {--profile=retiro-pilot} {--password=} {--json}', function (PilotProvisioner $provisioner): int {
    $profile = (string) $this->option('profile');
    $password = (string) $this->option('password');

    if ($password === '') {
        $password = (string) env('HOSPITALITY_PROVISION_PASSWORD', '');
    }
    if ($password === '') {
        $password = (string) ($this->secret('Pilot password (12+ characters)') ?? '');
    }

    try {
        $result = $provisioner->provision($profile, $password);
    } catch (Throwable $exception) {
        if ((bool) $this->option('json')) {
            $this->line(json_encode([
                'ok' => false,
                'error' => $exception->getMessage(),
            ], JSON_THROW_ON_ERROR | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
        } else {
            $this->error($exception->getMessage());
        }
        return 1;
    }

    if ((bool) $this->option('json')) {
        $this->line(json_encode([
            'ok' => true,
            ...$result,
        ], JSON_THROW_ON_ERROR | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
        return 0;
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
