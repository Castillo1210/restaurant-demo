// ============================================================
//  authService.js — Servicio de autenticación (backend real)
//  Login por usuario + contraseña contra POST /auth/login (JWT), o con
//  PIN de 4 dígitos desde un dispositivo registrado (POST /auth/pin/login).
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

    const auth = crearSesion(data, { metodo: 'password' });
    setStoredAuth(auth);
    return auth;
  } catch (error) {
    if (error.response?.status === 401) {
      throw new Error('Usuario o contraseña incorrectos');
    }
    throw new Error(getApiErrorMessage(error, 'No se pudo iniciar sesión'));
  }
};

/** Arma el objeto de sesión que se guarda en localStorage a partir de la respuesta del login. */
const crearSesion = (data, extra = {}) => ({
  token: data.token,
  usuarioId: getUsuarioIdFromToken(data.token),
  nombreUsuario: data.nombreUsuario,
  nombreCompleto: data.nombreCompleto,
  rol: data.rol,
  expiracion: data.expiracion,
  ...extra,
});

// ─── Ingreso rápido con PIN (dispositivos registrados) ──────────────────────
//  El ADMIN registra el celular/tablet en Admin → Dispositivos con PIN; el token
//  queda guardado SOLO en este navegador y se envía en la cabecera X-Dispositivo-Token.

const DISPOSITIVO_KEY = 'caraNegra_dispositivo';
const HEADER_DISPOSITIVO = 'X-Dispositivo-Token';

export const getTokenDispositivo = () => {
  try {
    return localStorage.getItem(DISPOSITIVO_KEY) || null;
  } catch {
    return null;
  }
};

export const setTokenDispositivo = (token) => {
  try {
    if (token) localStorage.setItem(DISPOSITIVO_KEY, token);
    else localStorage.removeItem(DISPOSITIVO_KEY);
  } catch {
    // Sin almacenamiento (modo privado): simplemente no habrá ingreso con PIN.
  }
};

/** Error de "este dispositivo ya no está habilitado" (revocado): se borra el token local. */
const esDispositivoNoAutorizado = (error) =>
  error?.response?.status === 403 && error.response?.data?.codigo === 'DISPOSITIVO_NO_AUTORIZADO';

/**
 * Datos de la pantalla de PIN de este dispositivo: { dispositivoId, dispositivo,
 * cierreInactividadMinutos, usuarios: [{ usuarioId, nombreCompleto, rol }] }.
 * Devuelve null si este navegador no está registrado (o fue revocado).
 */
export const getPantallaPin = async () => {
  const token = getTokenDispositivo();
  if (!token) return null;
  try {
    const { data } = await api.get('/auth/pin/pantalla', { headers: { [HEADER_DISPOSITIVO]: token } });
    return data;
  } catch (error) {
    if (esDispositivoNoAutorizado(error)) {
      setTokenDispositivo(null);
      return null;
    }
    throw new Error(getApiErrorMessage(error, 'No se pudo cargar el ingreso con PIN'));
  }
};

/**
 * Inicia sesión con el PIN de 4 dígitos del usuario elegido.
 * @param {number} usuarioId
 * @param {string} pin
 */
export const loginConPin = async (usuarioId, pin) => {
  const token = getTokenDispositivo();
  try {
    const { data } = await api.post(
      '/auth/pin/login',
      { usuarioId, pin },
      { headers: { [HEADER_DISPOSITIVO]: token ?? '' } }
    );
    const auth = crearSesion(data, {
      metodo: 'pin',
      cierreInactividadMinutos: data.cierreInactividadMinutos ?? 0,
    });
    setStoredAuth(auth);
    return auth;
  } catch (error) {
    if (esDispositivoNoAutorizado(error)) {
      setTokenDispositivo(null);
      const err = new Error(getApiErrorMessage(error));
      err.dispositivoNoAutorizado = true;
      throw err;
    }
    throw new Error(getApiErrorMessage(error, 'PIN incorrecto'));
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
