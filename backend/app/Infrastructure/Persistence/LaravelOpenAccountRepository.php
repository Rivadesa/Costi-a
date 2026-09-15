<?php

declare(strict_types=1);
namespace App\Infrastructure\Persistence;
use Hospitality\Application\Contracts\OpenAccountRepository;
use Illuminate\Support\Facades\DB;
final class LaravelOpenAccountRepository implements OpenAccountRepository
{
    public function ids(string $tenantId, string $companyId, string $locationId, int $offset, int $limit): array
    {
        return DB::table('table_services')->where('tenant_id', $tenantId)->where('company_id', $companyId)
            ->where('location_id', $locationId)->whereNull('account_closed_at')
            ->orderByDesc('opened_at')->orderByDesc('id')->offset($offset)->limit($limit)->pluck('id')->all();
    }
}
