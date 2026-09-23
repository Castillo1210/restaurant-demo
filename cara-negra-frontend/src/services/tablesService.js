// ============================================================
//  tablesService.js — Servicio de mesas (backend real)
//  Consume GET /mesas. La entidad Mesa real solo tiene
//  MesaId / NumeroMesa / Estado — los campos "pax"/"name"/"location"
//  del mock original no existen en el backend y se dejaron de usar.
// ============================================================

import api, { getApiErrorMessage } from './api';

// Mapea el Estado del backend (string, gracias al JsonStringEnumConverter
// agregado en Program.cs) al estilo visual que usa el Dashboard.
const ESTADO_TO_STATUS = {
  Disponible: 'free',
  Ocupada: 'occupied',
  Reservada: 'reserved',
  Mantenimiento: 'maintenance',
};

const mapMesa = (m) => ({
  id: m.mesaId,
  numeroMesa: m.numeroMesa,
  status: ESTADO_TO_STATUS[m.estado] ?? 'free',
  estadoBackend: m.estado,
});

/**
 * Obtiene todas las mesas del restaurante.
 * @returns {Promise<Array>}
 */
export const getTables = async () => {
  try {
    const { data } = await api.get('/mesas');
    return data.map(mapMesa);
  } catch (error) {
    console.error('Error al cargar mesas:', getApiErrorMessage(error));
    return [];
  }
};

/**
 * Obtiene una mesa por su ID (MesaId).
 * @param {number} id
 * @returns {Promise<Object|null>}
 */
export const getTableById = async (id) => {
  try {
    const { data } = await api.get(`/mesas/${id}`);
    return mapMesa(data);
  } catch (error) {
    console.error('Error al cargar la mesa:', getApiErrorMessage(error));
    return null;
  }
};
