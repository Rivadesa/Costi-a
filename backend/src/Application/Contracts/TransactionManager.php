<?php

declare(strict_types=1);

namespace Hospitality\Application\Contracts;

interface TransactionManager
{
    /** @template T @param callable():T $callback @return T */
    public function run(callable $callback): mixed;
}
