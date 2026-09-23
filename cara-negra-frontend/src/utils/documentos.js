// Validaciones de documentos peruanos (mismas reglas que el backend,
// ver DocumentoIdentidadHelper.cs) para dar feedback inmediato en pantalla.

const PESOS_RUC = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];
const PREFIJOS_RUC = ['10', '15', '16', '17', '20'];

export const esDniValido = (dni) => /^\d{8}$/.test(dni ?? '');

export const esCarnetExtranjeriaValido = (ce) => /^[A-Za-z0-9]{8,12}$/.test(ce ?? '');

/** RUC: 11 dígitos, prefijo válido y dígito verificador (módulo 11) correcto. */
export const esRucValido = (ruc) => {
  if (!/^\d{11}$/.test(ruc ?? '')) return false;
  if (!PREFIJOS_RUC.includes(ruc.slice(0, 2))) return false;
  const suma = PESOS_RUC.reduce((acc, peso, i) => acc + Number(ruc[i]) * peso, 0);
  let digito = 11 - (suma % 11);
  if (digito === 10) digito = 0;
  else if (digito === 11) digito = 1;
  return digito === Number(ruc[10]);
};

/**
 * El backend guarda fechas en UTC y, al leerlas de MySQL, a veces las serializa sin la "Z".
 * Sin la zona, el navegador las interpretaría como hora local (5 horas de diferencia en Lima).
 */
export const parseFechaUtc = (valor) => {
  if (!valor) return null;
  if (valor instanceof Date) return valor;
  const s = String(valor);
  return new Date(/[zZ]|[+-]\d{2}:?\d{2}$/.test(s) ? s : `${s}Z`);
};
