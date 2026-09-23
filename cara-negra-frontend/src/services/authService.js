// ============================================================
//  authService.js — Servicio de autenticación (backend real)
//  Login por usuario + contraseña contra POST /auth/login (JWT).
// ============================================================

import api, { getStoredAuth, setStoredAuth, getApiErrorMessage } from './api';
import { getUsuarioIdFromToken } from './jwt';

/**
 * Inicia sesión con nombre de usuario + contraseña.
 * @param {string} nombreUsuario
 * @param {string} password
 * @returns {Promise<Object>} usuario autenticado (incluye token)
 */
export const login = async (nombreUsuario, password) => {
  try {
    const { data } = await api.post('/auth/login', {
      nombreUsuario,
      password,
    });

    const usuarioId = getUsuarioIdFromToken(data.token);

    const auth = {
      token: data.token,
      usuarioId,
      nombreUsuario: data.nombreUsuario,
      nombreCompleto: data.nombreCompleto,
      rol: data.rol,
      expiracion: data.expiracion,
    };

    setStoredAuth(auth);
    return auth;
  } catch (error) {
    if (error.response?.status === 401) {
      throw new Error('Usuario o contraseña incorrectos');
    }
    throw new Error(getApiErrorMessage(error, 'No se pudo iniciar sesión'));
  }
};

/**
 * Obtiene la sesión actual desde localStorage (null si no hay sesión
 * o si el token ya expiró).
 * @returns {Object|null}
 */
export const getCurrentUser = () => {
  const auth = getStoredAuth();
  if (!auth) return null;

  if (auth.expiracion) {
    const expira = new Date(auth.expiracion).getTime();
    if (!Number.isNaN(expira) && expira <= Date.now()) {
      setStoredAuth(null);
      return null;
    }
  }

  return auth;
};

/**
 * Cierra la sesión del usuario actual.
 */
export const logout = () => {
  setStoredAuth(null);
};

/**
 * Cambia la contraseña del usuario autenticado.
 * @param {number} usuarioId
 * @param {{ currentPassword: string, newPassword: string, confirmPassword: string }} payload
 */
export const cambiarPassword = async (usuarioId, { currentPassword, newPassword, confirmPassword }) => {
  try {
    await api.post(`/usuarios/${usuarioId}/cambiar-password`, {
      currentPassword,
      newPassword,
      confirmPassword,
    });
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo cambiar la contraseña'));
  }
};
