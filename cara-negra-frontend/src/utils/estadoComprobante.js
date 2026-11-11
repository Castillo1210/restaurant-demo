// ============================================================
//  estadoComprobante.js — Cómo mostrar el estado de un comprobante
//  frente a SUNAT (ver EstadoComprobante en el backend).
// ============================================================

/**
 * @typedef {Object} InfoEstado
 * @property {string} titulo        Texto corto (badge / cabecera).
 * @property {string} descripcion   Explicación para el cajero.
 * @property {string} [nota]        Resumen de una línea (quién tiene la demora y qué hacer).
 * @property {string} [accion]      Texto del botón de reintento (si difiere de "Reintentar envío").
 * @property {string} [clases]      Colores propios en la cabecera del comprobante (si no, los del tono).
 * @property {string} [claseBadge]  Colores propios de la etiqueta en listados (si no, los del tono).
 * @property {'ok'|'espera'|'error'|'neutro'} tono
 * @property {boolean} valido       Cuenta como comprobante válido del pedido.
 * @property {boolean} reintentar   Se puede pulsar "Reintentar envío".
 * @property {boolean} reemitir     Hay que emitir un comprobante nuevo.
 */

/** @type {Record<string, InfoEstado>} */
const ESTADOS = {
  AceptadoSimulado: {
    titulo: 'Simulado (demo)',
    descripcion: 'Comprobante de demostración: no se envió a SUNAT.',
    tono: 'ok', valido: true, reintentar: false, reemitir: false,
  },
  Aceptado: {
    titulo: 'Aceptado por SUNAT',
    descripcion: 'SUNAT aceptó el comprobante (tiene su constancia, el CDR). No hay nada que hacer.',
    tono: 'ok', valido: true, reintentar: false, reemitir: false,
  },
  Pendiente: {
    titulo: 'Esperando a SUNAT',
    descripcion: 'El proveedor de facturación ya lo recibió y lo envió a SUNAT; falta que SUNAT lo confirme. La demora es de SUNAT, no de este equipo ni del internet: se actualiza solo y no hay que volver a emitirlo.',
    nota: 'Ya se envió · falta que SUNAT confirme (se actualiza solo)',
    accion: 'Consultar a SUNAT ahora',
    // Azul: es informativo (demora de SUNAT), distinto del amarillo de "Sin enviar" (revisar internet).
    clases: 'bg-sky-50 text-sky-700',
    claseBadge: 'text-sky-400 border-sky-800/60 bg-sky-950/30',
    tono: 'espera', valido: true, reintentar: true, reemitir: false,
  },
  PorEnviar: {
    titulo: 'Sin enviar',
    descripcion: 'No llegó al proveedor de facturación: sin internet en esta PC o el proveedor no respondió. El sistema lo reintenta solo cada pocos minutos.',
    nota: 'No llegó al proveedor · revisa el internet (se reintenta solo)',
    tono: 'espera', valido: true, reintentar: true, reemitir: false,
  },
  Rechazado: {
    titulo: 'Rechazado por SUNAT',
    descripcion: 'SUNAT lo rechazó por algún dato (el motivo aparece abajo). No es válido: corrige los datos y emite uno nuevo.',
    tono: 'error', valido: false, reintentar: false, reemitir: true,
  },
  ErrorEnvio: {
    titulo: 'No aceptado por el proveedor',
    descripcion: 'El proveedor de facturación no lo aceptó, así que no llegó a SUNAT (el motivo aparece abajo). Puedes reintentar o emitir uno nuevo con los datos corregidos.',
    tono: 'error', valido: false, reintentar: true, reemitir: true,
  },
  Anulado: {
    titulo: 'Anulado',
    descripcion: 'Comprobante anulado.',
    tono: 'neutro', valido: false, reintentar: false, reemitir: true,
  },
};

/** @returns {InfoEstado} */
export const infoEstado = (estado) => ESTADOS[estado] ?? ESTADOS.Pendiente;

/** Leyenda impresa según el ambiente (null = comprobante real, sin leyenda). */
export const leyendaAmbiente = (ambiente) => {
  if (ambiente === 'PRODUCCION') return null;
  if (ambiente === 'SANDBOX') return 'PRUEBA (SANDBOX) – SIN VALIDEZ TRIBUTARIA';
  return 'DEMO – SIN VALIDEZ TRIBUTARIA';
};

export const TONO_CLASES = {
  ok: 'bg-emerald-50 text-emerald-700',
  espera: 'bg-amber-50 text-amber-700',
  error: 'bg-red-50 text-red-700',
  neutro: 'bg-gray-100 text-gray-600',
};

export const TONO_BADGE = {
  ok: 'text-accentGreen border-emerald-800/60 bg-emerald-950/40',
  espera: 'text-accentYellow border-yellow-800/60 bg-yellow-950/30',
  error: 'text-red-400 border-red-800/60 bg-red-950/30',
  neutro: 'text-gray-400 border-gray-700 bg-gray-900',
};
