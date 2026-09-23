// ============================================================
//  pagosService.js — Servicio de cobros (backend real)
//  Registra pagos (soporta pago mixto: varios pagos por pedido) y
//  permite anular un pago ya registrado (reversa con auditoría).
// ============================================================

import api, { getApiErrorMessage } from './api';

const mapPago = (p) => ({
  id: p.pagoId,
  pedidoId: p.pedidoId,
  mesaNumero: p.mesaNumero,
  monto: p.monto,
  metodoPagoId: p.metodoPagoId,
  metodoPagoNombre: p.metodoPagoNombre,
  referencia: p.referencia,
  anulado: p.estaAnulado,
  motivoAnulacion: p.motivoAnulacion,
  anuladoEn: p.anuladoEn,
  creadoEn: p.creadoEn,
});

/**
 * Registra un cobro sobre un pedido. Soporta pagos parciales/mixtos:
 * si el monto no cubre el saldo total, el pedido queda igual de "Listo"
 * hasta que se complete con más pagos.
 * @param {{ pedidoId: number, monto: number, metodoPagoId: number, referencia?: string }} payload
 */
export const createPago = async ({ pedidoId, monto, metodoPagoId, referencia }) => {
  try {
    const { data } = await api.post('/pagos', {
      pedidoId,
      monto,
      metodoPagoId,
      referencia: referencia || null,
    });
    return mapPago(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo registrar el cobro'));
  }
};

/**
 * Anula un pago ya registrado (reversa con auditoría, nunca se borra).
 * Recalcula el estado del pedido y, si corresponde, vuelve a ocupar la mesa.
 * @param {number} pagoId
 * @param {string} motivo
 */
export const anularPago = async (pagoId, motivo) => {
  try {
    const { data } = await api.delete(`/pagos/${pagoId}`, { data: { motivo } });
    return mapPago(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo anular el pago'));
  }
};
