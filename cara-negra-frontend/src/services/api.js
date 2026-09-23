// ============================================================
//  api.js — Cliente HTTP centralizado hacia el backend real
//  Todos los servicios (auth, mesas, menú, pedidos, pagos...)
//  deben usar esta instancia de axios en lugar de fetch/mocks.
// ============================================================

import axios from 'axios';

// Configurable vía variable de entorno de Vite (.env / .env.local):
//   VITE_API_URL=https://localhost:7108/api/v1
// Si no se define, se asume el puerto HTTP de desarrollo definido en
// CaraNegra.API/Properties/launchSettings.json (perfil "http": localhost:5014).
export const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5014/api/v1';

const AUTH_KEY = 'caraNegra_auth';

/**
 * Forma guardada en localStorage tras un login exitoso:
 * { token, usuarioId, nombreUsuario, nombreCompleto, rol, expiracion }
 */
export const getStoredAuth = () => {
  try {
    const raw = localStorage.getItem(AUTH_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    if (!parsed?.token) return null;
    return parsed;
  } catch (e) {
    console.error('Error leyendo sesión guardada:', e);
    return null;
  }
};

export const setStoredAuth = (auth) => {
  if (auth) {
    localStorage.setItem(AUTH_KEY, JSON.stringify(auth));
  } else {
    localStorage.removeItem(AUTH_KEY);
  }
};

export const AUTH_STORAGE_KEY = AUTH_KEY;

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
});

api.interceptors.request.use((config) => {
  const auth = getStoredAuth();
  if (auth?.token) {
    config.headers.Authorization = `Bearer ${auth.token}`;
  }
  return config;
});

let onUnauthorized = null;
/**
 * Permite que AuthContext se suscriba a un 401 global (token vencido/ inválido)
 * para limpiar el estado de sesión y redirigir a /login.
 */
export const setUnauthorizedHandler = (handler) => {
  onUnauthorized = handler;
};

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      setStoredAuth(null);
      if (typeof onUnauthorized === 'function') {
        onUnauthorized();
      }
    }
    return Promise.reject(error);
  }
);

/**
 * Extrae un mensaje de error legible de una respuesta de la API
 * (el backend responde { mensaje: "..." } en los errores controlados).
 */
export const getApiErrorMessage = (error, fallback = 'Ocurrió un error inesperado. Intenta de nuevo.') => {
  if (error?.response?.data?.mensaje) return error.response.data.mensaje;
  if (error?.response?.data?.title) return error.response.data.title;
  if (error?.message === 'Network Error') return 'No se pudo conectar con el servidor.';
  return fallback;
};

export default api;
