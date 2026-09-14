<?php

declare(strict_types=1);

namespace Hospitality\Application\Shared;

final class RequestHasher
{
    private function __construct()
    {
    }

    /** @param array<string, mixed> $payload */
    public static function hash(array $payload): string
    {
        $normalized = self::normalize($payload);
        return hash('sha256', json_encode($normalized, JSON_THROW_ON_ERROR | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
    }

    private static function normalize(mixed $value): mixed
    {
        if (!is_array($value)) {
            return $value;
        }

        if (array_is_list($value)) {
            return array_map(self::normalize(...), $value);
        }

        ksort($value);
        foreach ($value as $key => $item) {
            $value[$key] = self::normalize($item);
        }

        return $value;
    }
}
