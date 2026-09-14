<?php

declare(strict_types=1);

spl_autoload_register(function (string $class): void {
    $prefix = 'Hospitality\\';
    if (!str_starts_with($class, $prefix)) {
        return;
    }

    $relative = substr($class, strlen($prefix));
    $path = __DIR__ . '/../src/' . str_replace('\\', '/', $relative) . '.php';
    if (is_file($path)) {
        require $path;
    }
});

function ok(bool $condition, string $message): void
{
    if (!$condition) {
        throw new RuntimeException('FAIL: ' . $message);
    }
    echo "✓ {$message}\n";
}

function throws(callable $fn, string $message): void
{
    try {
        $fn();
    } catch (Throwable) {
        echo "✓ {$message}\n";
        return;
    }

    throw new RuntimeException('FAIL (expected exception): ' . $message);
}
