/** Parse a euro input exactly; never multiply a fractional floating-point number. */
export function euroInputToCents(input) {
  const text = String(input).trim();
  if (!/^\d{1,7}(?:[.,]\d{1,2})?$/.test(text)) throw new Error('Introduce un precio válido con un máximo de dos decimales.');
  const [whole, fraction = ''] = text.replace(',', '.').split('.');
  const cents = Number(whole) * 100 + Number(fraction.padEnd(2, '0'));
  if (!Number.isSafeInteger(cents) || cents > 100000000) throw new Error('Precio fuera del límite permitido.');
  return cents;
}
