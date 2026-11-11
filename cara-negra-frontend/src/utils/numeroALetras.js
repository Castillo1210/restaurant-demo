// Convierte un importe a letras en el formato usado en comprobantes peruanos:
//   45.5  → "CUARENTA Y CINCO CON 50/100 SOLES"
//   1     → "UNO CON 00/100 SOLES"

const UNIDADES = ['', 'UNO', 'DOS', 'TRES', 'CUATRO', 'CINCO', 'SEIS', 'SIETE', 'OCHO', 'NUEVE'];
const ESPECIALES = {
  10: 'DIEZ', 11: 'ONCE', 12: 'DOCE', 13: 'TRECE', 14: 'CATORCE', 15: 'QUINCE',
  16: 'DIECISEIS', 17: 'DIECISIETE', 18: 'DIECIOCHO', 19: 'DIECINUEVE',
  20: 'VEINTE', 21: 'VEINTIUNO', 22: 'VEINTIDOS', 23: 'VEINTITRES', 24: 'VEINTICUATRO',
  25: 'VEINTICINCO', 26: 'VEINTISEIS', 27: 'VEINTISIETE', 28: 'VEINTIOCHO', 29: 'VEINTINUEVE',
};
const DECENAS = ['', '', '', 'TREINTA', 'CUARENTA', 'CINCUENTA', 'SESENTA', 'SETENTA', 'OCHENTA', 'NOVENTA'];
const CENTENAS = ['', 'CIENTO', 'DOSCIENTOS', 'TRESCIENTOS', 'CUATROCIENTOS', 'QUINIENTOS',
  'SEISCIENTOS', 'SETECIENTOS', 'OCHOCIENTOS', 'NOVECIENTOS'];

function decenas(n) {
  if (n < 10) return UNIDADES[n];
  if (ESPECIALES[n]) return ESPECIALES[n];
  const d = Math.floor(n / 10);
  const u = n % 10;
  return u === 0 ? DECENAS[d] : `${DECENAS[d]} Y ${UNIDADES[u]}`;
}

function centenas(n) {
  if (n === 100) return 'CIEN';
  const c = Math.floor(n / 100);
  const resto = n % 100;
  return [CENTENAS[c], decenas(resto)].filter(Boolean).join(' ');
}

function miles(n) {
  const m = Math.floor(n / 1000);
  const resto = n % 1000;
  let prefijo = '';
  if (m === 1) prefijo = 'MIL';
  else if (m > 1) prefijo = `${centenas(m).replace(/UNO$/, 'UN')} MIL`;
  return [prefijo, centenas(resto)].filter(Boolean).join(' ');
}

function millones(n) {
  const mm = Math.floor(n / 1000000);
  const resto = n % 1000000;
  let prefijo = '';
  if (mm === 1) prefijo = 'UN MILLON';
  else if (mm > 1) prefijo = `${miles(mm).replace(/UNO$/, 'UN')} MILLONES`;
  return [prefijo, miles(resto)].filter(Boolean).join(' ');
}

export function numeroALetras(monto) {
  const valor = Math.round(Number(monto || 0) * 100) / 100;
  const entero = Math.floor(valor);
  const centimos = Math.round((valor - entero) * 100);
  const letras = entero === 0 ? 'CERO' : millones(entero);
  return `${letras} CON ${String(centimos).padStart(2, '0')}/100 SOLES`;
}
