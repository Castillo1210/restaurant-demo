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

// Tiempo máximo de espera por petición. Sin esto, con el Wi-Fi caído una petición puede quedar
// colgada indefinidamente y el botón se queda en "Enviando...". Emitir comprobantes usa más
// (ver comprobantesService): espera la respuesta de SUNAT.
export const TIMEOUT_MS = 20000;
export const TIMEOUT_SUNAT_MS = 60000;

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
  timeout: TIMEOUT_MS,
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

  // Errores de validación (FluentValidation / model binding): { errors: { Campo: ["msg", ...] } }.
  // Antes se mostraba solo el título genérico ("One or more validation errors occurred"), sin
  // decir QUÉ campo falló; ahora se muestran los mensajes concretos.
  const errores = error?.response?.data?.errors;
  if (errores && typeof errores === 'object') {
    const mensajes = Object.entries(errores)
      // "The dto field is required" es un efecto secundario del error real de conversión: se omite.
      .filter(([campo]) => !/^(dto|request)$/i.test(campo))
      .flatMap(([campo, lista]) =>
      (Array.isArray(lista) ? lista : [lista]).map((m) =>
        // Error de conversión de tipos (p.ej. un número llegó vacío): se muestra algo entendible.
        /could not be converted|is required|field is required/i.test(String(m))
          ? `Dato inválido en "${campo.replace(/^\$\./, '')}"`
          : String(m)
      )
    );
    const unicos = [...new Set(mensajes)].filter(Boolean);
    if (unicos.length) return unicos.slice(0, 3).join(' · ');
  }

  // Errores manejados por el middleware del servidor: { title, detail } (p. ej. 409 "otra
  // persona está trabajando sobre este pedido"). El detalle es el mensaje útil.
  const status = error?.response?.status;
  if (status && status < 500 && error?.response?.data?.detail) return error.response.data.detail;

  if (error?.response?.data?.title) return error.response.data.title;
  if (error?.code === 'ECONNABORTED' || error?.code === 'ETIMEDOUT') {
    return 'El servidor tardó demasiado en responder (¿Wi-Fi lento?). Antes de repetir, revisa si la operación sí se registró.';
  }
  if (error?.message === 'Network Error') return 'No se pudo conectar con el servidor. Revisa el Wi-Fi.';
  return fallback;
};

export default api;
