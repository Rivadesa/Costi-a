<?php

declare(strict_types=1);

namespace App\Infrastructure\Auth;

use Hospitality\Application\Auth\AuthenticationFailed;
use Hospitality\Application\Auth\AuthorizationDenied;
use Hospitality\Application\Auth\LocalLoginResult;
use Hospitality\Application\Auth\LocalPrincipal;
use Hospitality\Application\Contracts\LocalAuthGateway;
use Hospitality\Domain\Shared\Ulid;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Hash;

final class LaravelLocalAuthGateway implements LocalAuthGateway
{
    public function login(
        string $tenantId,
        string $email,
        string $password,
        string $companyId,
        string $locationId,
        string $deviceName,
    ): LocalLoginResult {
        $user = DB::table('users')
            ->where('tenant_id', $tenantId)
            ->where('active', true)
            ->whereRaw('lower(email) = ?', [mb_strtolower(trim($email))])
            ->first();

        if ($user === null || !is_string($user->password_hash) || !Hash::check($password, $user->password_hash)) {
            throw new AuthenticationFailed('Invalid local credentials.');
        }

        $permissions = $this->permissionsForScope(
            (string) $user->id,
            (string) $user->tenant_id,
            $companyId,
            $locationId,
        );

        $rawToken = $this->generateToken();
        $tokenId = Ulid::generate();
        $ttlHours = max(1, (int) config('hospitality.token_ttl_hours', 24));
        $expiresAt = new \DateTimeImmutable(sprintf('+%d hours', $ttlHours));

        DB::table('api_tokens')->insert([
            'id' => $tokenId,
            'user_id' => (string) $user->id,
            'name' => trim($deviceName) !== '' ? substr($deviceName, 0, 160) : 'local-device',
            'token_hash' => $this->hashToken($rawToken),
            'last_used_at' => null,
            'expires_at' => $expiresAt,
            'revoked_at' => null,
            'created_at' => now(),
        ]);

        return new LocalLoginResult(
            $rawToken,
            $expiresAt,
            new LocalPrincipal(
                (string) $user->id,
                (string) $user->tenant_id,
                $companyId,
                $locationId,
                (string) $user->display_name,
                $permissions,
                $tokenId,
            ),
        );
    }

    public function authenticate(
        string $rawToken,
        string $companyId,
        string $locationId,
    ): LocalPrincipal {
        if (trim($rawToken) === '') {
            throw new AuthenticationFailed('Missing bearer token.');
        }

        $row = DB::table('api_tokens as tokens')
            ->join('users', 'users.id', '=', 'tokens.user_id')
            ->where('tokens.token_hash', $this->hashToken($rawToken))
            ->whereNull('tokens.revoked_at')
            ->where('users.active', true)
            ->where(function ($query): void {
                $query->whereNull('tokens.expires_at')->orWhere('tokens.expires_at', '>', now());
            })
            ->select([
                'tokens.id as token_id',
                'users.id as user_id',
                'users.tenant_id',
                'users.display_name',
            ])
            ->first();

        if ($row === null) {
            throw new AuthenticationFailed('Invalid, expired or revoked local token.');
        }

        $permissions = $this->permissionsForScope(
            (string) $row->user_id,
            (string) $row->tenant_id,
            $companyId,
            $locationId,
        );

        DB::table('api_tokens')->where('id', (string) $row->token_id)->update([
            'last_used_at' => now(),
        ]);

        return new LocalPrincipal(
            (string) $row->user_id,
            (string) $row->tenant_id,
            $companyId,
            $locationId,
            (string) $row->display_name,
            $permissions,
            (string) $row->token_id,
        );
    }

    public function revoke(string $rawToken): void
    {
        if (trim($rawToken) === '') {
            return;
        }

        DB::table('api_tokens')
            ->where('token_hash', $this->hashToken($rawToken))
            ->whereNull('revoked_at')
            ->update(['revoked_at' => now()]);
    }

    /** @return list<string> */
    private function permissionsForScope(
        string $userId,
        string $tenantId,
        string $companyId,
        string $locationId,
    ): array {
        $scopeExists = DB::table('locations')
            ->join('companies', 'companies.id', '=', 'locations.company_id')
            ->where('locations.id', $locationId)
            ->where('locations.tenant_id', $tenantId)
            ->where('locations.company_id', $companyId)
            ->where('locations.active', true)
            ->where('companies.tenant_id', $tenantId)
            ->where('companies.active', true)
            ->exists();

        if (!$scopeExists) {
            throw new AuthorizationDenied('Requested company/location is outside the authenticated tenant scope.');
        }

        $roles = DB::table('user_roles as user_roles')
            ->join('roles', 'roles.id', '=', 'user_roles.role_id')
            ->where('user_roles.user_id', $userId)
            ->where('roles.tenant_id', $tenantId)
            ->where(function ($query) use ($companyId): void {
                $query->whereNull('user_roles.company_id')->orWhere('user_roles.company_id', $companyId);
            })
            ->where(function ($query) use ($locationId): void {
                $query->whereNull('user_roles.location_id')->orWhere('user_roles.location_id', $locationId);
            })
            ->pluck('roles.permissions');

        if ($roles->isEmpty()) {
            throw new AuthorizationDenied('User has no role for the requested company/location.');
        }

        $permissions = [];
        foreach ($roles as $encodedPermissions) {
            $values = is_array($encodedPermissions)
                ? $encodedPermissions
                : json_decode((string) $encodedPermissions, true, flags: JSON_THROW_ON_ERROR);

            foreach (is_array($values) ? $values : [] as $permission) {
                if (is_string($permission) && $permission !== '') {
                    $permissions[$permission] = true;
                }
            }
        }

        return array_keys($permissions);
    }

    private function generateToken(): string
    {
        return 'hos_'.rtrim(strtr(base64_encode(random_bytes(32)), '+/', '-_'), '=');
    }

    private function hashToken(string $rawToken): string
    {
        return hash('sha256', $rawToken);
    }
}
