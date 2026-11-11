import { parseFechaUtc } from './documentos';

export const soles = (n) => `S/ ${Number(n ?? 0).toFixed(2)}`;

/** "lun 30/09" a partir de "2026-09-30" o "2026-09-30T00:00:00" (fecha operativa, sin hora). */
export const fechaOperativaTexto = (valor, conAnio = false) => {
  if (!valor) return '';
  const [a, m, d] = String(valor).slice(0, 10).split('-').map(Number);
  const fecha = new Date(a, m - 1, d);
  return fecha.toLocaleDateString('es-PE', {
    weekday: 'short', day: '2-digit', month: '2-digit', ...(conAnio ? { year: 'numeric' } : {}),
  });
};

/** Hora de Lima de una fecha UTC del backend: "10:05 a. m." */
export const horaLima = (valorUtc) =>
  parseFechaUtc(valorUtc)?.toLocaleTimeString('es-PE', { timeZone: 'America/Lima', hour: '2-digit', minute: '2-digit' }) ?? '';

/** Fecha y hora de Lima: "30/09/26, 10:05 p. m." */
export const fechaHoraLima = (valorUtc) =>
  parseFechaUtc(valorUtc)?.toLocaleString('es-PE', { timeZone: 'America/Lima', dateStyle: 'short', timeStyle: 'short' }) ?? '';

export const hoyLimaISO = () => new Date().toLocaleDateString('en-CA', { timeZone: 'America/Lima' });
