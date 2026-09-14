<?php

declare(strict_types=1);

namespace Hospitality\Application\Service;

use Hospitality\Application\Contracts\IdempotencyStore;
use Hospitality\Application\Contracts\OutboxStore;
use Hospitality\Application\Contracts\TableServiceRepository;
use Hospitality\Application\Contracts\TransactionManager;
use Hospitality\Application\Shared\CommandContext;
use Hospitality\Application\Shared\IdempotencyConflict;
use Hospitality\Application\Shared\RequestHasher;
use Hospitality\Domain\Service\TableService;

final class ServiceMutationExecutor
{
    public function __construct(
        private readonly TableServiceRepository $services,
        private readonly TransactionManager $transactions,
        private readonly IdempotencyStore $idempotency,
        private readonly OutboxStore $outbox,
    ) {
    }

    /**
     * @param array<string, mixed> $requestPayload
     * @param callable(TableService):array<string, mixed> $mutation
     * @return array<string, mixed>
     */
    public function execute(
        CommandContext $context,
        string $serviceId,
        string $idempotencyKey,
        string $commandName,
        array $requestPayload,
        callable $mutation,
    ): array {
        if (trim($idempotencyKey) === '') {
            throw new \InvalidArgumentException('Idempotency key is required.');
        }

        $requestHash = RequestHasher::hash($requestPayload);

        return $this->transactions->run(function () use (
            $context,
            $serviceId,
            $idempotencyKey,
            $commandName,
            $requestHash,
            $mutation,
        ): array {
            $existing = $this->idempotency->find($context->tenantId, $idempotencyKey);
            if ($existing !== null) {
                if ($existing->commandName !== $commandName || $existing->requestHash !== $requestHash) {
                    throw new IdempotencyConflict('Idempotency key was already used for a different command or payload.');
                }

                return $existing->result;
            }

            $service = $this->services->get(
                $context->tenantId,
                $context->companyId,
                $context->locationId,
                $serviceId,
            );

            $result = $mutation($service);
            $this->services->save($service);

            foreach ($service->pullEvents() as $event) {
                $this->outbox->append(
                    $context->tenantId,
                    $context->companyId,
                    $context->locationId,
                    $event,
                );
            }

            $this->idempotency->remember(
                $context->tenantId,
                $idempotencyKey,
                $commandName,
                $requestHash,
                $result,
            );

            return $result;
        });
    }
}
