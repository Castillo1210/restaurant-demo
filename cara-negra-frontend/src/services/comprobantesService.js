// ============================================================
//  comprobantesService.js — Boletas / facturas electrónicas
//  DEMO: la emisión es SIMULADA en el backend (formato SUNAT,
//  serie-correlativo, IGV y QR), sin envío real a SUNAT/OSE.
// ============================================================

import api, { getApiErrorMessage } from './api';

/**
 * Emite la boleta o factura de un pedido ya pagado.
 * @param {{ pedidoId:number, tipoComprobante:'Boleta'|'Factura', clienteTipoDocumento:'SinDocumento'|'DNI'|'CarnetExtranjeria'|'RUC', clienteNumeroDocumento?:string, clienteNombre?:string, clienteDireccion?:string }} payload
 */
export const emitirComprobante = async (payload) => {
  try {
    const { data } = await api.post('/comprobantes', payload);
    return data;
  } catch (error) {
    // Los errores de FluentValidation vienen como { errors: { Campo: [msg] } }.
    const errores = error?.response?.data?.errors;
    if (errores && typeof errores === 'object') {
      const primero = Object.values(errores).flat()[0];
      if (primero) throw new Error(primero);
    }
    throw new Error(getApiErrorMessage(error, 'No se pudo emitir el comprobante'));
  }
};

/** Comprobante vigente de un pedido, o null si aún no tiene. */
export const getComprobanteDePedido = async (pedidoId) => {
  const res = await api.get(`/comprobantes/pedido/${pedidoId}`);
  return res.status === 204 ? null : res.data;
};

export const getComprobanteById = async (id) => {
  const { data } = await api.get(`/comprobantes/${id}`);
  return data;
};

/**
 * Listado de comprobantes por fecha (calendario de Lima).
 * @param {{ fechaDesde?:string, fechaHasta?:string, tipo?:'Boleta'|'Factura' }} params
 */
export const getComprobantes = async (params = {}) => {
  const { data } = await api.get('/comprobantes', { params });
  return data;
};
