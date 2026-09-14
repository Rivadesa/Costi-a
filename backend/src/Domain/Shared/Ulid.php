<?php

declare(strict_types=1);

namespace Hospitality\Domain\Shared;

final class Ulid
{
    private const ALPHABET = '0123456789ABCDEFGHJKMNPQRSTVWXYZ';

    private function __construct()
    {
    }

    public static function generate(?\DateTimeImmutable $now = null): string
    {
        $now ??= new \DateTimeImmutable();
        $milliseconds = ((int) $now->format('U')) * 1000 + intdiv((int) $now->format('u'), 1000);

        $bytes = '';
        for ($shift = 40; $shift >= 0; $shift -= 8) {
            $bytes .= chr(($milliseconds >> $shift) & 0xff);
        }
        $bytes .= random_bytes(10);

        return self::encodeBase32($bytes);
    }

    private static function encodeBase32(string $bytes): string
    {
        $buffer = 0;
        $bits = 0;
        $encoded = '';

        foreach (str_split($bytes) as $byte) {
            $buffer = ($buffer << 8) | ord($byte);
            $bits += 8;

            while ($bits >= 5) {
                $bits -= 5;
                $encoded .= self::ALPHABET[($buffer >> $bits) & 0x1f];
                $buffer = $bits === 0 ? 0 : ($buffer & ((1 << $bits) - 1));
            }
        }

        if ($bits > 0) {
            $encoded .= self::ALPHABET[($buffer << (5 - $bits)) & 0x1f];
        }

        return str_pad($encoded, 26, '0', STR_PAD_LEFT);
    }
}
