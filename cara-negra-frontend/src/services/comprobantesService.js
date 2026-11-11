// ============================================================
//  comprobantesService.js — Boletas / facturas electrónicas
//  Según el backend, la emisión es SIMULADA (demo) o se envía a
//  APISUNAT (sandbox o producción). Ver utils/estadoComprobante.
// ============================================================

import api, { getApiErrorMessage, TIMEOUT_SUNAT_MS } from './api';

/**
 * Emite la boleta o factura de un pedido ya pagado.
 * @param {{ pedidoId:number, tipoComprobante:'Boleta'|'Factura', clienteTipoDocumento:'SinDocumento'|'DNI'|'CarnetExtranjeria'|'RUC', clienteNumeroDocumento?:string, clienteNombre?:string, clienteDireccion?:string }} payload
 */
export const emitirComprobante = async (payload) => {
  try {
    const { data } = await api.post('/comprobantes', payload, { timeout: TIMEOUT_SUNAT_MS });
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

/**
 * Comprobantes emitidos hace más de una hora que SUNAT todavía no confirma.
 * @returns {Promise<{cantidad:number, masAntiguo:string|null}>}
 */
export const getComprobantesAtrasados = async () => {
  const { data } = await api.get('/comprobantes/atrasados');
  return data;
};

/**
 * Reintenta el envío a SUNAT (o consulta el estado) de un comprobante PorEnviar,
 * Pendiente o con error de envío. Devuelve el comprobante actualizado.
 */
export const reenviarComprobante = async (id) => {
  try {
    const { data } = await api.post(`/comprobantes/${id}/reenviar`, null, { timeout: TIMEOUT_SUNAT_MS });
    return data;
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo reenviar el comprobante'));
  }
};

/** Número que llevará el próximo comprobante de una serie: { serie, numero, texto: 'B001-00000015' }. */
export const getProximoNumero = async (serie) => {
  const { data } = await api.get('/comprobantes/proximo-numero', { params: { serie } });
  return data;
};

/** Dónde se emiten los comprobantes: { proveedor: 'Simulado'|'ApiSunat', ambiente: 'SIMULADO'|'SANDBOX'|'PRODUCCION' }. */
export const getAmbienteFacturacion = async () => {
  const { data } = await api.get('/comprobantes/ambiente');
  return data;
};
