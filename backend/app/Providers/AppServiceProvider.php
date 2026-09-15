<?php

declare(strict_types=1);

namespace App\Providers;

use App\Infrastructure\Auth\LaravelLocalAuthGateway;
use App\Infrastructure\Persistence\CatalogAwareTableServiceRepository;
use App\Infrastructure\Persistence\LaravelActiveTableServiceRepository;
use App\Infrastructure\Persistence\LaravelDiningTableRepository;
use App\Infrastructure\Persistence\LaravelIdempotencyStore;
use App\Infrastructure\Persistence\LaravelKitchenStationRepository;
use App\Infrastructure\Persistence\LaravelMenuTemplateRepository;
use App\Infrastructure\Persistence\LaravelOperationalCatalogRepository;
use App\Infrastructure\Persistence\LaravelOutboxStore;
use App\Infrastructure\Persistence\LaravelTableServiceRepository;
use App\Infrastructure\Persistence\LaravelTransactionManager;
use Hospitality\Application\Contracts\ActiveTableServiceRepository;
use Hospitality\Application\Contracts\DiningTableRepository;
use Hospitality\Application\Contracts\IdempotencyStore;
use Hospitality\Application\Contracts\KitchenStationRepository;
use Hospitality\Application\Contracts\LocalAuthGateway;
use Hospitality\Application\Contracts\MenuTemplateRepository;
use Hospitality\Application\Contracts\OperationalCatalogRepository;
use Hospitality\Application\Contracts\OutboxStore;
use Hospitality\Application\Contracts\TableServiceRepository;
use Hospitality\Application\Contracts\TransactionManager;
use Illuminate\Support\ServiceProvider;

final class AppServiceProvider extends ServiceProvider
{
    public function register(): void
    {
        $this->app->bind(\Hospitality\Application\Contracts\OpenAccountRepository::class, \App\Infrastructure\Persistence\LaravelOpenAccountRepository::class);
        $this->app->bind(\Hospitality\Application\Contracts\AuditStore::class, \App\Infrastructure\Persistence\LaravelAuditStore::class);
        $this->app->scoped(\Hospitality\Application\Contracts\CatalogAdminRepository::class, \App\Infrastructure\Persistence\LaravelCatalogAdminRepository::class);
        $this->app->scoped(LaravelTableServiceRepository::class);
        $this->app->scoped(TableServiceRepository::class, CatalogAwareTableServiceRepository::class);
        $this->app->scoped(ActiveTableServiceRepository::class, LaravelActiveTableServiceRepository::class);
        $this->app->scoped(MenuTemplateRepository::class, LaravelMenuTemplateRepository::class);
        $this->app->scoped(DiningTableRepository::class, LaravelDiningTableRepository::class);
        $this->app->scoped(KitchenStationRepository::class, LaravelKitchenStationRepository::class);
        $this->app->scoped(OperationalCatalogRepository::class, LaravelOperationalCatalogRepository::class);
        $this->app->scoped(TransactionManager::class, LaravelTransactionManager::class);
        $this->app->scoped(IdempotencyStore::class, LaravelIdempotencyStore::class);
        $this->app->scoped(OutboxStore::class, LaravelOutboxStore::class);
        $this->app->scoped(LocalAuthGateway::class, LaravelLocalAuthGateway::class);
    }

    public function boot(): void
    {
    }
}
