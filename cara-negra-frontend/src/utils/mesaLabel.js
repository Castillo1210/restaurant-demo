/**
 * Etiquetas de mesa. La numeración es POR SALÓN ("Mesa 1" existe en Terraza y en Jardín), así
 * que cada mesa se identifica con la PRIMERA PALABRA del nombre de su salón + su número:
 *   Salón "Terraza"          + mesa 1 → "Terraza 1"
 *   Salón "Salón principal"  + mesa 3 → "Salón 3"
 *   Salón "Zona de piscina"  + mesa 2 → "Zona 2"
 *   Sin salón                + mesa 5 → "Mesa 5"
 */

/** Primera palabra del nombre del salón ("Salón principal" → "Salón"). */
export const prefijoSalon = (salonNombre) => (salonNombre ?? '').trim().split(/\s+/)[0] || '';

/** Etiqueta completa de la mesa: "Terraza 1" (o "Mesa 1" si no tiene salón). */
export const etiquetaMesa = (salonNombre, mesaNumero) => {
  const numero = mesaNumero != null && mesaNumero !== '' ? mesaNumero : '—';
  const prefijo = prefijoSalon(salonNombre);
  return `${prefijo || 'Mesa'} ${numero}`;
};

/** true si el nombre del salón tiene más de una palabra (entonces vale la pena mostrarlo completo). */
export const salonTieneNombreCompuesto = (salonNombre) => (salonNombre ?? '').trim().split(/\s+/).length > 1;

/** Nombre corto para rótulos: "Carlos Ramos Díaz" → "Carlos R." */
export const nombreCorto = (nombreCompleto) => {
  const partes = (nombreCompleto ?? '').trim().split(/\s+/).filter(Boolean);
  if (partes.length === 0) return '';
  return partes.length > 1 ? `${partes[0]} ${partes[1][0]}.` : partes[0];
};
