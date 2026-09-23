// ============================================================
//  mesasAdminService.js — CRUD de mesas (backend real), para el
//  panel de administración (Fase 4). tablesService.js sigue siendo
//  de solo lectura y es lo que usa el mozo para el mapa de mesas.
// ============================================================

import api, { getApiErrorMessage } from './api';

const mapMesa = (m) => ({
  id: m.mesaId,
  numeroMesa: m.numeroMesa,
  estado: m.estado, // 'Disponible' | 'Ocupada' | 'Reservada' | 'Mantenimiento'
  creadoEn: m.creadoEn,
});

/**
 * Lista TODAS las mesas (cualquier estado), para el panel de admin.
 */
export const getMesasAdmin = async () => {
  try {
    const { data } = await api.get('/mesas', { params: { soloDisponibles: false } });
    return data.map(mapMesa);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudieron cargar las mesas'));
  }
};

export const createMesa = async (numeroMesa) => {
  try {
    const { data } = await api.post('/mesas', { numeroMesa });
    return mapMesa(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo crear la mesa'));
  }
};

export const updateMesa = async (id, { numeroMesa, estado }) => {
  try {
    const { data } = await api.put(`/mesas/${id}`, { numeroMesa, estado });
    return mapMesa(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo actualizar la mesa'));
  }
};

/**
 * Desactiva una mesa (la pasa a estado Mantenimiento). Para reactivarla,
 * usa updateMesa(id, { numeroMesa, estado: 'Disponible' }).
 */
export const deleteMesa = async (id) => {
  try {
    await api.delete(`/mesas/${id}`);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo desactivar la mesa'));
  }
};
