// ============================================================
//  salonesService.js — Salones / áreas del restaurante
//  (Terraza, Jardín, Salón principal...). Cada mesa pertenece
//  a un salón y la numeración de mesas es POR salón.
// ============================================================

import api, { getApiErrorMessage } from './api';

const mapSalon = (s) => ({
  id: s.salonId,
  nombre: s.nombre,
  descripcion: s.descripcion ?? '',
  orden: s.orden,
  estaActivo: s.estaActivo,
  cantidadMesas: s.cantidadMesas,
  creadoEn: s.creadoEn,
});

/** @param {{ soloActivos?: boolean }} opts */
export const getSalones = async ({ soloActivos = false } = {}) => {
  try {
    const { data } = await api.get('/salones', { params: { soloActivos } });
    return data.map(mapSalon);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudieron cargar los salones'));
  }
};

export const createSalon = async ({ nombre, descripcion, orden }) => {
  try {
    const { data } = await api.post('/salones', { nombre, descripcion: descripcion || null, orden: Number(orden) || 0 });
    return mapSalon(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo crear el salón'));
  }
};

export const updateSalon = async (id, { nombre, descripcion, orden, estaActivo }) => {
  try {
    const { data } = await api.put(`/salones/${id}`, {
      nombre,
      descripcion: descripcion || null,
      orden: Number(orden) || 0,
      estaActivo,
    });
    return mapSalon(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo actualizar el salón'));
  }
};

/** Desactiva el salón (sus mesas dejan de mostrarse al mozo). Se reactiva con updateSalon. */
export const deleteSalon = async (id) => {
  try {
    await api.delete(`/salones/${id}`);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo desactivar el salón'));
  }
};
