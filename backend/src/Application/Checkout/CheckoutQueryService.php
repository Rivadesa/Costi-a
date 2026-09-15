<?php

declare(strict_types=1);

namespace Hospitality\Application\Checkout;

use Hospitality\Application\Contracts\TableServiceRepository;
use Hospitality\Application\Shared\CommandContext;

final class CheckoutQueryService
{
    public function __construct(
        private readonly TableServiceRepository $services,
        private readonly CheckoutDetailProjector $projector,
        private readonly \Hospitality\Application\Contracts\OpenAccountRepository $accounts,
    ) {
    }

    /** Released tables must not hide unpaid/open accounts from the authorized office. */
    public function openAccounts(CommandContext $context, int $page = 1): array
    {
        if ($page < 1) throw new \InvalidArgumentException('Page must be positive.');
        $ids = $this->accounts->ids($context->tenantId, $context->companyId, $context->locationId, ($page - 1) * 50, 51);
        return ['data' => array_map(fn (string $id): array => $this->detail($context, $id), array_slice($ids, 0, 50)),
            'page' => $page, 'has_more' => count($ids) > 50];
    }

    /** @return array<string, mixed> */
    public function detail(CommandContext $context, string $serviceId): array
    {
        $service = $this->services->get(
            $context->tenantId,
            $context->companyId,
            $context->locationId,
            $serviceId,
        );

        return $this->projector->project($service);
    }
}
