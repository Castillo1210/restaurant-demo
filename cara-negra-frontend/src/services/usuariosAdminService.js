// ============================================================
//  usuariosAdminService.js — CRUD de usuarios (backend real, solo ADMIN)
//  Panel de administración de personal (Fase 5): crear, editar,
//  desactivar/reactivar y resetear contraseña de mozos/cajeros/admins.
// ============================================================

import api, { getApiErrorMessage } from './api';

const mapUsuario = (u) => ({
  id: u.usuarioId,
  nombreUsuario: u.nombreUsuario,
  nombreCompleto: u.nombreCompleto,
  rolId: u.rolId,
  rolNombre: u.rolNombre,
  estaActivo: u.esVerificado,
  ultimoAccesoEn: u.ultimoAccesoEn,
  creadoEn: u.creadoEn,
});

/**
 * Lista usuarios paginados, con búsqueda opcional por nombre de usuario/completo.
 * @param {{ page?: number, pageSize?: number, search?: string }} params
 */
export const getUsuarios = async ({ page = 1, pageSize = 50, search = '' } = {}) => {
  try {
    const { data } = await api.get('/usuarios', { params: { page, pageSize, search: search || undefined } });
    return {
      items: (data.items ?? []).map(mapUsuario),
      totalCount: data.totalCount,
      page: data.page,
      pageSize: data.pageSize,
      totalPages: data.totalPages,
    };
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudieron cargar los usuarios'));
  }
};

export const createUsuario = async ({ nombreUsuario, nombreCompleto, password, rolId }) => {
  try {
    const { data } = await api.post('/usuarios', { nombreUsuario, nombreCompleto, password, rolId });
    return mapUsuario(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo crear el usuario'));
  }
};

export const updateUsuario = async (id, { nombreCompleto, rolId, estaActivo }) => {
  try {
    const { data } = await api.put(`/usuarios/${id}`, {
      nombreCompleto,
      rolId,
      esVerificado: estaActivo,
    });
    return mapUsuario(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo actualizar el usuario'));
  }
};

/**
 * Desactiva un usuario (ya no puede iniciar sesión). Para reactivarlo, usa
 * updateUsuario(id, { ..., estaActivo: true }).
 */
export const deleteUsuario = async (id) => {
  try {
    await api.delete(`/usuarios/${id}`);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo desactivar el usuario'));
  }
};

/**
 * Resetea la contraseña de un usuario (el ADMIN no necesita la contraseña actual).
 * @param {number} id
 * @param {string} newPassword
 */
export const resetPassword = async (id, newPassword) => {
  try {
    await api.post(`/usuarios/${id}/reset-password`, {
      newPassword,
      confirmPassword: newPassword,
    });
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo resetear la contraseña'));
  }
};
