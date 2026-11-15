// ============================================================
//  jornadaService.js — Jornada (día operativo): apertura,
//  movimientos de caja, cierre (con lo registrado) y reportes.
// ============================================================

import api, { getApiErrorMessage } from './api';
import { descargarArchivo } from './reportesService';

const llamar = async (promesa, mensaje) => {
  try {
    const { data } = await promesa;
    return data;
  } catch (error) {
    throw new Error(getApiErrorMessage(error, mensaje));
  }
};

/** { abierta: boolean, jornada: JornadaDto|null } */
export const getJornadaActual = () => llamar(api.get('/jornadas/actual'), 'No se pudo consultar la jornada');

/** @param {{ fechaOperativa?: string, fondoInicial: number, observacion?: string }} datos */
export const abrirJornada = (datos) => llamar(api.post('/jornadas/abrir', datos), 'No se pudo abrir la jornada');

export const getResumenActual = () => llamar(api.get('/jornadas/actual/resumen'), 'No se pudo cargar el resumen');

export const getPreCierre = () => llamar(api.get('/jornadas/actual/precierre'), 'No se pudo preparar el cierre');

/** @param {{ conteos: {metodoPagoId:number|null, nombre:string, contado:number}[], observacion?: string }} datos */
export const cerrarJornada = (datos) => llamar(api.post('/jornadas/actual/cerrar', datos), 'No se pudo cerrar la jornada');

/** @param {{ tipo: 'Ingreso'|'Egreso', monto: number, motivo: string }} datos */
export const registrarMovimiento = (datos) =>
  llamar(api.post('/jornadas/actual/movimientos', datos), 'No se pudo registrar el movimiento');

export const anularMovimiento = (id) =>
  llamar(api.post(`/jornadas/movimientos/${id}/anular`), 'No se pudo anular el movimiento');

/**
 * Pedidos de la jornada (abiertos, cobrados y anulados). Sin id: la jornada actual.
 * caja ('Restaurante' | 'Piscina'): solo la usa el ADMIN; el resto ve siempre la de su rol.
 */
export const getPedidosJornada = (jornadaId, caja) =>
  llamar(api.get('/jornadas/pedidos', { params: { jornadaId: jornadaId || undefined, caja: caja || undefined } }),
    'No se pudieron cargar los pedidos del día');

/**
 * Historial de pedidos (ADMIN) de las jornadas entre dos fechas "yyyy-MM-dd", p. ej. un mes entero.
 * Misma forma que getPedidosJornada, sin `jornada`.
 */
export const getPedidosRango = (fechaDesde, fechaHasta, caja) =>
  llamar(api.get('/jornadas/pedidos', { params: { fechaDesde, fechaHasta, caja: caja || undefined } }),
    'No se pudo cargar el historial de pedidos');

/** Descarga en Excel el historial de pedidos de las jornadas entre dos fechas "yyyy-MM-dd" (ADMIN). */
export const exportarPedidosRango = async (fechaDesde, fechaHasta, caja) => {
  try {
    const { data } = await api.get('/jornadas/pedidos/exportar', {
      params: { fechaDesde, fechaHasta, caja: caja || undefined },
      responseType: 'blob',
    });
    const rango = fechaDesde === fechaHasta ? fechaDesde : `${fechaDesde}_a_${fechaHasta}`;
    const sufijo = caja ? `_${caja.toLowerCase()}` : '';
    descargarArchivo(data, `pedidos_${rango}${sufijo}.xlsx`);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo exportar el historial de pedidos'));
  }
};

export const getHistorialJornadas = () => llamar(api.get('/jornadas'), 'No se pudo cargar el historial');

export const getResumenJornada = (id) => llamar(api.get(`/jornadas/${id}/resumen`), 'No se pudo cargar el reporte');
