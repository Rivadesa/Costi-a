<?php

declare(strict_types=1);

namespace App\Providers;

use App\Infrastructure\Persistence\LaravelIdempotencyStore;
use App\Infrastructure\Persistence\LaravelOutboxStore;
use App\Infrastructure\Persistence\LaravelTableServiceRepository;
use App\Infrastructure\Persistence\LaravelTransactionManager;
use Hospitality\Application\Contracts\IdempotencyStore;
use Hospitality\Application\Contracts\OutboxStore;
use Hospitality\Application\Contracts\TableServiceRepository;
use Hospitality\Application\Contracts\TransactionManager;
use Illuminate\Support\ServiceProvider;

final class AppServiceProvider extends ServiceProvider
{
    public function register(): void
    {
        $this->app->scoped(TableServiceRepository::class, LaravelTableServiceRepository::class);
        $this->app->scoped(TransactionManager::class, LaravelTransactionManager::class);
        $this->app->scoped(IdempotencyStore::class, LaravelIdempotencyStore::class);
        $this->app->scoped(OutboxStore::class, LaravelOutboxStore::class);
    }

    public function boot(): void
    {
    }
}
