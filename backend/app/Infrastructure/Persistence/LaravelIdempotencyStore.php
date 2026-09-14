<?php

declare(strict_types=1);

namespace App\Infrastructure\Persistence;

use Hospitality\Application\Contracts\IdempotencyStore;
use Hospitality\Application\Shared\IdempotencyRecord;
use Hospitality\Domain\Shared\Ulid;
use Illuminate\Support\Facades\DB;

final class LaravelIdempotencyStore implements IdempotencyStore
{
    public function find(string $tenantId, string $idempotencyKey): ?IdempotencyRecord
    {
        $row = DB::table('idempotency_keys')
            ->where('tenant_id', $tenantId)
            ->where('idempotency_key', $idempotencyKey)
            ->first();

        if ($row === null) {
            return null;
        }

        $body = $row->response_body;
        if (is_string($body)) {
            $decoded = json_decode($body, true, flags: JSON_THROW_ON_ERROR);
            $body = is_array($decoded) ? $decoded : [];
        }

        return new IdempotencyRecord(
            (string) $row->tenant_id,
            (string) $row->idempotency_key,
            (string) $row->command_name,
            (string) $row->request_hash,
            is_array($body) ? $body : [],
        );
    }

    public function remember(
        string $tenantId,
        string $idempotencyKey,
        string $commandName,
        string $requestHash,
        array $result,
    ): void {
        DB::table('idempotency_keys')->insert([
            'id' => Ulid::generate(),
            'tenant_id' => $tenantId,
            'idempotency_key' => $idempotencyKey,
            'command_name' => $commandName,
            'request_hash' => $requestHash,
            'response_status' => 200,
            'response_body' => json_encode($result, JSON_THROW_ON_ERROR),
            'created_at' => now(),
            'expires_at' => null,
        ]);
    }
}
