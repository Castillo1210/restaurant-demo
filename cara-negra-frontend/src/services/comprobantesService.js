// ============================================================
//  comprobantesService.js — Boletas / facturas electrónicas
//  Según el backend, la emisión es SIMULADA (demo) o se envía a
//  APISUNAT (sandbox o producción). Ver utils/estadoComprobante.
// ============================================================

import api, { getApiErrorMessage, TIMEOUT_SUNAT_MS } from './api';
import { descargarArchivo } from './reportesService';

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
 * Descarga un .xlsx con los comprobantes del rango y tipo indicados (mismos filtros que la pantalla).
 * @param {{ fechaDesde:string, fechaHasta:string, tipo?:'Boleta'|'Factura' }} params fechas "yyyy-MM-dd" (calendario de Lima)
 */
export const exportarComprobantes = async ({ fechaDesde, fechaHasta, tipo }) => {
  try {
    const { data } = await api.get('/comprobantes/exportar', {
      params: { fechaDesde, fechaHasta, tipo: tipo || undefined },
      responseType: 'blob',
    });
    const rango = fechaDesde === fechaHasta ? fechaDesde : `${fechaDesde}_a_${fechaHasta}`;
    const sufijo = tipo === 'Boleta' ? '_boletas' : tipo === 'Factura' ? '_facturas' : '';
    descargarArchivo(data, `comprobantes_${rango}${sufijo}.xlsx`);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo exportar los comprobantes'));
  }
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

// ── Anulación y notas de crédito (solo ADMIN) ──

const postSunat = async (url, body, mensaje) => {
  try {
    const { data } = await api.post(url, body, { timeout: TIMEOUT_SUNAT_MS });
    return data;
  } catch (error) {
    throw new Error(getApiErrorMessage(error, mensaje));
  }
};

/**
 * Anula el comprobante ante SUNAT. El backend elige el camino: comunicación de baja (factura,
 * hasta 3 días), resumen diario (boleta, hasta 5 días) o nota de crédito (fuera de plazo).
 * @param {number} id
 * @param {{ motivo:string, usarNotaCredito?:boolean }} datos
 */
export const anularComprobante = (id, datos) =>
  postSunat(`/comprobantes/${id}/anular`, datos, 'No se pudo anular el comprobante');

/** Reenvía o consulta una anulación en proceso. */
export const reintentarAnulacion = (id) =>
  postSunat(`/comprobantes/${id}/anulacion/reintentar`, null, 'No se pudo consultar la anulación');

/** Marca como aceptada una baja / resumen pendiente (verificada en el panel de APISUNAT). */
export const confirmarAnulacion = (id) =>
  postSunat(`/comprobantes/${id}/anulacion/confirmar`, null, 'No se pudo confirmar la anulación');

/**
 * Nota de crédito por corrección de descripción (importes en cero).
 * @param {number} id
 * @param {{ motivo?:string, correcciones:string[] }} datos
 */
export const emitirNotaCorreccion = (id, datos) =>
  postSunat(`/comprobantes/${id}/notas-credito`, datos, 'No se pudo emitir la nota de crédito');

/** Reenvía o consulta una nota de crédito. Devuelve el comprobante actualizado. */
export const reenviarNotaCredito = (notaId) =>
  postSunat(`/comprobantes/notas-credito/${notaId}/reenviar`, null, 'No se pudo reenviar la nota de crédito');

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
