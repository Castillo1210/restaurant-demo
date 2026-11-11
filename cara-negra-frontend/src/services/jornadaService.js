// ============================================================
//  jornadaService.js — Jornada (día operativo): apertura,
//  movimientos de caja, cierre (con lo registrado) y reportes.
// ============================================================

import api, { getApiErrorMessage } from './api';

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

/** Pedidos de la jornada (abiertos, cobrados y anulados). Sin id: la jornada actual. */
export const getPedidosJornada = (jornadaId) =>
  llamar(api.get('/jornadas/pedidos', { params: jornadaId ? { jornadaId } : {} }), 'No se pudieron cargar los pedidos del día');

export const getHistorialJornadas = () => llamar(api.get('/jornadas'), 'No se pudo cargar el historial');

export const getResumenJornada = (id) => llamar(api.get(`/jornadas/${id}/resumen`), 'No se pudo cargar el reporte');
