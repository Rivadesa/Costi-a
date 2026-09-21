// ADR-007: comandero y KDS son superficies OPERATIVAS. Esta PWA no debe recibir precios, cuentas,
// saldos ni pagos. El servidor ya lo garantiza por proyeccion y por rol; esto es defensa en
// profundidad: si una respuesta trae una clave economica, se rechaza entera en vez de pintarla.
const MONEY_KEY = /(price|cents|amount|balance|subtotal|total|paid|payment|refund|charge|credit|tariff)/i

export class MoneyLeakError extends Error {
  constructor(public readonly path: string) {
    super(`Respuesta con datos economicos (${path}): esta aplicacion es solo operativa.`)
  }
}

export function assertNoMoney(value: unknown, path = '$'): void {
  if (Array.isArray(value)) {
    value.forEach((item, index) => assertNoMoney(item, `${path}[${index}]`))
    return
  }
  if (value !== null && typeof value === 'object') {
    for (const [key, inner] of Object.entries(value as Record<string, unknown>)) {
      if (MONEY_KEY.test(key)) throw new MoneyLeakError(`${path}.${key}`)
      assertNoMoney(inner, `${path}.${key}`)
    }
  }
}
