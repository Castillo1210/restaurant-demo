// ============================================================
//  descuentosService.js — Catálogo de descuentos (Fase 7, backend real)
//  Consume /descuentos. Usado por Admin (CRUD del catálogo) y por
//  Caja (selector de descuentos vigentes al cobrar un pedido).
// ============================================================

import api, { getApiErrorMessage } from './api';

const mapDescuento = (d) => ({
  id: d.descuentoId,
  nombre: d.nombre,
  esPorcentaje: d.esPorcentaje,
  valor: d.valor,
  estaActivo: d.estaActivo,
  fechaInicio: d.fechaInicio,
  fechaFin: d.fechaFin,
  creadoEn: d.creadoEn,
});

/**
 * Lista los descuentos del catálogo.
 * @param {{ soloVigentes?: boolean }} params soloVigentes=true trae solo los activos y
 * dentro de su rango de fechas (lo que usa Caja al cobrar).
 * @returns {Promise<Array>}
 */
export const getDescuentos = async ({ soloVigentes = false } = {}) => {
  try {
    const { data } = await api.get('/descuentos', { params: { soloVigentes } });
    return data.map(mapDescuento);
  } catch (error) {
    console.error('Error al cargar descuentos:', getApiErrorMessage(error));
    return [];
  }
};

/**
 * Crea un nuevo descuento en el catálogo.
 * @param {{ nombre: string, esPorcentaje: boolean, valor: number, fechaInicio?: string|null, fechaFin?: string|null }} payload
 */
export const createDescuento = async (payload) => {
  try {
    const { data } = await api.post('/descuentos', payload);
    return mapDescuento(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo crear el descuento'));
  }
};

/**
 * Actualiza un descuento existente del catálogo.
 * @param {number} id
 * @param {{ nombre: string, esPorcentaje: boolean, valor: number, estaActivo: boolean, fechaInicio?: string|null, fechaFin?: string|null }} payload
 */
export const updateDescuento = async (id, payload) => {
  try {
    const { data } = await api.put(`/descuentos/${id}`, payload);
    return mapDescuento(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo actualizar el descuento'));
  }
};

/**
 * Desactiva (soft-delete) un descuento del catálogo.
 * @param {number} id
 */
export const deleteDescuento = async (id) => {
  try {
    await api.delete(`/descuentos/${id}`);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo desactivar el descuento'));
  }
};
