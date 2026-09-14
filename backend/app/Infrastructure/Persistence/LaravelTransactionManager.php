<?php

declare(strict_types=1);

namespace App\Infrastructure\Persistence;

use Hospitality\Application\Contracts\TransactionManager;
use Illuminate\Support\Facades\DB;

final class LaravelTransactionManager implements TransactionManager
{
    public function run(callable $callback): mixed
    {
        return DB::transaction($callback, 3);
    }
}
