// ============================================================
//  dispositivosService.js — Dispositivos habilitados para el
//  ingreso con PIN (solo ADMIN). El registro se hace desde el
//  propio celular/tablet: el token que devuelve el backend se
//  guarda en ESTE navegador (ver authService.setTokenDispositivo).
// ============================================================

import api, { getApiErrorMessage } from './api';
import { setTokenDispositivo } from './authService';

export const getDispositivos = async () => {
  try {
    const { data } = await api.get('/dispositivos');
    return data ?? [];
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudieron cargar los dispositivos'));
  }
};

/**
 * Registra ESTE navegador como dispositivo del restaurante y guarda su token.
 * @param {{ nombre: string, cierreInactividadMinutos: number }} datos
 */
export const registrarEsteDispositivo = async ({ nombre, cierreInactividadMinutos }) => {
  try {
    const { data } = await api.post('/dispositivos', { nombre, cierreInactividadMinutos });
    setTokenDispositivo(data.token);
    return data.dispositivo;
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo registrar el dispositivo'));
  }
};

export const revocarDispositivo = async (id) => {
  try {
    await api.delete(`/dispositivos/${id}`);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo revocar el dispositivo'));
  }
};
