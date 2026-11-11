// ============================================================
//  empresaService.js — Datos de la empresa (guardados en BD)
//  Se editan en Admin → Datos de la empresa. Así el sistema se
//  instala en otro restaurante sin cambiar código.
// ============================================================

import api, { getApiErrorMessage } from './api';

/** Nombre comercial, logo y mensaje al pie. No requiere sesión (pantalla de login). */
export const getEmpresaPublica = async () => {
  const { data } = await api.get('/empresa/publico');
  return data;
};

/** Datos completos (solo ADMIN). */
export const getEmpresa = async () => {
  try {
    const { data } = await api.get('/empresa');
    return data;
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudieron cargar los datos de la empresa'));
  }
};

export const updateEmpresa = async (datos) => {
  try {
    const { data } = await api.put('/empresa', datos);
    return data;
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudieron guardar los datos de la empresa'));
  }
};
