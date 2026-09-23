// ============================================================
//  signalrService.js — Cliente de tiempo real hacia /hubs/pedidos
//  El backend (PedidosHub) ya une automáticamente la conexión al
//  grupo correspondiente según el rol del JWT (Mozo/Cajero/Admin),
//  así que aquí solo hace falta conectar con el token y suscribirse
//  a los eventos (NuevoPedido, PedidoEstadoCambiado, PagoRecibido,
//  PagoAnulado, MesaEstadoCambiado, PedidoActualizado).
// ============================================================

import * as signalR from '@microsoft/signalr';
import { API_BASE_URL, getStoredAuth } from './api';

// API_BASE_URL termina en algo como "http://localhost:5000/api/v1";
// el hub de SignalR cuelga de la raíz del host ("/hubs/pedidos"), no del prefijo /api/v1.
const HUB_URL = API_BASE_URL.replace(/\/api\/v\d+\/?$/, '') + '/hubs/pedidos';

let connection = null;

/**
 * Crea (si no existe) y arranca la conexión SignalR autenticada.
 * Es seguro llamarla varias veces: si ya hay una conexión activa, la reutiliza.
 * @returns {Promise<signalR.HubConnection|null>}
 */
export const connectPedidosHub = async () => {
  const auth = getStoredAuth();
  if (!auth?.token) return null;

  if (connection && connection.state === signalR.HubConnectionState.Connected) {
    return connection;
  }

  connection = new signalR.HubConnectionBuilder()
    .withUrl(HUB_URL, { accessTokenFactory: () => auth.token })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  try {
    await connection.start();
  } catch (e) {
    console.error('No se pudo conectar al hub de pedidos (tiempo real):', e);
    connection = null;
  }

  return connection;
};

/**
 * Suscribe un handler a un evento del hub. Retorna una función para des-suscribirse.
 * @param {string} eventName
 * @param {(payload: any) => void} handler
 */
export const onHubEvent = (eventName, handler) => {
  if (!connection) return () => {};
  connection.on(eventName, handler);
  return () => connection?.off(eventName, handler);
};

export const disconnectPedidosHub = async () => {
  if (connection) {
    await connection.stop();
    connection = null;
  }
};
