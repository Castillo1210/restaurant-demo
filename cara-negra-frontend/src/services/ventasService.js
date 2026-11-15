// ============================================================
//  ventasService.js — Venta de mostrador ("Venta rápida")
//  POST /ventas/directa: registra la venta ya cobrada (pedido +
//  pagos) en la caja de quien vende. La boleta se emite después
//  con comprobantesService.emitirComprobante.
// ============================================================

import api, { getApiErrorMessage } from './api';

/** Código único de un intento de venta: si se reintenta por el Wi-Fi, no se cobra dos veces. */
export const nuevoCodigoOperacion = () =>
  (globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(36).slice(2)}`);

/**
 * @param {{ items: Array<{productoId:number, cantidad:number}>,
 *           pagos: Array<{metodoPagoId:number, monto:number, referencia?:string}>,
 *           operacionId?: string }} venta
 * @returns {Promise<{ pedidoId:number, caja:string, total:number, creadoEn:string, vendedor:string,
 *                     lineas: Array<{producto:string, cantidad:number, precioUnitario:number, importe:number}>,
 *                     pagos: Array<{metodoPago:string, monto:number}> }>}
 */
export const ventaDirecta = async (venta) => {
  try {
    const { data } = await api.post('/ventas/directa', venta);
    return data;
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo registrar la venta'));
  }
};
