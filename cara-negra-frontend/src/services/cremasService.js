// ============================================================
//  cremasService.js — Catálogo de cremas/toppings (Fase 8, backend real)
//  Consume /cremas. Usado por Admin (CRUD del catálogo) y por CartPage
//  (chips de cremas disponibles al armar un pedido).
// ============================================================

import api, { getApiErrorMessage } from './api';

const mapCrema = (c) => ({
  id: c.cremaId,
  nombre: c.nombre,
  orden: c.orden,
  estaActivo: c.estaActivo,
  creadoEn: c.creadoEn,
});

/**
 * Lista las cremas del catálogo, ordenadas por su posición de despliegue.
 * @param {{ soloActivas?: boolean }} params soloActivas=true (default) trae solo las
 * habilitadas — lo que usa CartPage al armar un pedido.
 * @returns {Promise<Array>}
 */
export const getCremas = async ({ soloActivas = true } = {}) => {
  try {
    const { data } = await api.get('/cremas', { params: { soloActivas } });
    return data.map(mapCrema);
  } catch (error) {
    console.error('Error al cargar cremas:', getApiErrorMessage(error));
    return [];
  }
};

/**
 * Crea una nueva crema en el catálogo (se agrega al final del orden actual).
 * @param {string} nombre
 */
export const createCrema = async (nombre) => {
  try {
    const { data } = await api.post('/cremas', { nombre });
    return mapCrema(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo crear la crema'));
  }
};

/**
 * Actualiza una crema existente (nombre, orden o si está activa).
 * @param {number} id
 * @param {{ nombre: string, orden: number, estaActivo: boolean }} payload
 */
export const updateCrema = async (id, payload) => {
  try {
    const { data } = await api.put(`/cremas/${id}`, payload);
    return mapCrema(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo actualizar la crema'));
  }
};

/**
 * Desactiva (soft-delete) una crema del catálogo.
 * @param {number} id
 */
export const deleteCrema = async (id) => {
  try {
    await api.delete(`/cremas/${id}`);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo desactivar la crema'));
  }
};
