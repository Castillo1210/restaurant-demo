// ============================================================
//  metodosPagoService.js — Catálogo de métodos de pago (backend real)
//  Consume GET /metodos-pago. Usado por caja para poblar el selector
//  de método al registrar un cobro (Efectivo/Tarjeta/Yape/Plin/...).
// ============================================================

import api, { getApiErrorMessage } from './api';

const mapMetodoPago = (m) => ({
  id: m.metodoPagoId,
  nombre: m.nombre,
  estaActivo: m.estaActivo,
});

/**
 * Lista los métodos de pago activos disponibles para cobrar.
 * @returns {Promise<Array>}
 */
export const getMetodosPago = async () => {
  try {
    const { data } = await api.get('/metodos-pago', { params: { soloActivos: true } });
    return data.map(mapMetodoPago);
  } catch (error) {
    console.error('Error al cargar métodos de pago:', getApiErrorMessage(error));
    return [];
  }
};
