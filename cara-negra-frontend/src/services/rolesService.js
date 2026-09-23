// ============================================================
//  rolesService.js — Lectura de roles (backend real, solo ADMIN)
//  Los roles (ADMIN/MOZO/CAJERO) son fijos: la API solo expone
//  lectura, para poblar el selector de rol al crear/editar un usuario.
// ============================================================

import api, { getApiErrorMessage } from './api';

const mapRol = (r) => ({
  id: r.rolId,
  nombre: r.nombre,
  descripcion: r.descripcion,
});

export const getRoles = async () => {
  try {
    const { data } = await api.get('/roles');
    return data.map(mapRol);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudieron cargar los roles'));
  }
};
