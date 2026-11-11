// ============================================================
//  signalrService.js — Cliente de tiempo real hacia /hubs/pedidos
//  El backend (PedidosHub) une automáticamente la conexión al grupo
//  de su rol (Mozo/Cajero/Admin/Cocina); aquí solo se conecta con el
//  token y se reparten los eventos a los componentes suscritos.
//
//  Diseño (evita avisos duplicados):
//   - UNA sola conexión por pestaña, aunque varios componentes (o el
//     doble montaje de React StrictMode en desarrollo) llamen a
//     connectPedidosHub() a la vez: se comparte la misma promesa.
//   - Los componentes se suscriben con onHubEvent() de forma síncrona
//     (aunque la conexión aún no exista) y se desuscriben en el cleanup
//     del useEffect. Cada evento del hub tiene un único "despachador" en
//     la conexión que llama a los handlers registrados: un handler nunca
//     queda registrado dos veces ni sobrevive al componente.
// ============================================================

import * as signalR from '@microsoft/signalr';
import { API_BASE_URL, getStoredAuth } from './api';

// API_BASE_URL termina en algo como "http://localhost:5000/api/v1";
// el hub de SignalR cuelga de la raíz del host ("/hubs/pedidos"), no del prefijo /api/v1.
const HUB_URL = API_BASE_URL.replace(/\/api\/v\d+\/?$/, '') + '/hubs/pedidos';

let connection = null;
let startPromise = null;

/** evento → Set de handlers */
const handlers = new Map();
/** eventos que ya tienen despachador registrado en la conexión actual */
const eventosEnlazados = new Set();
/** listeners de estado de conexión: (conectado: boolean) => void */
const estadoListeners = new Set();

const notificarEstado = (conectado) => estadoListeners.forEach((cb) => cb(conectado));

const enlazarEvento = (eventName) => {
  if (!connection || eventosEnlazados.has(eventName)) return;
  connection.on(eventName, (payload) => {
    // Copia: un handler puede desuscribirse mientras se recorre el Set.
    [...(handlers.get(eventName) ?? [])].forEach((h) => {
      try {
        h(payload);
      } catch (e) {
        console.error(`Error en handler de ${eventName}:`, e);
      }
    });
  });
  eventosEnlazados.add(eventName);
};

/** listeners de "volvió la conexión" (para recargar lo que se pudo perder mientras tanto) */
const reconexionListeners = new Set();
/** ya hubo al menos una conexión exitosa (la primera no cuenta como "reconexión") */
let yaEstuvoConectado = false;
/** logout: no reintentar solo */
let detenidoAMano = false;
let reintento = null;

// Reintentos sin rendirse: 0 s, 2 s, 5 s, 10 s y luego cada 15 s. La librería, por defecto, se
// rinde a los ~40 s y la pantalla quedaba sin tiempo real hasta recargarla.
const politicaReintentos = {
  nextRetryDelayInMilliseconds: (ctx) => [0, 2000, 5000, 10000][ctx.previousRetryCount] ?? 15000,
};

const alConectar = () => {
  notificarEstado(true);
  if (yaEstuvoConectado) {
    reconexionListeners.forEach((cb) => {
      try {
        cb();
      } catch (e) {
        console.error('Error al recargar tras reconectar:', e);
      }
    });
  }
  yaEstuvoConectado = true;
};

const programarReintento = () => {
  if (detenidoAMano || reintento) return;
  reintento = setTimeout(() => {
    reintento = null;
    connectPedidosHub();
  }, 10000);
};

/**
 * Crea (si no existe) y arranca la conexión SignalR autenticada. Seguro de llamar muchas veces
 * y en paralelo: todas las llamadas comparten la misma conexión y la misma promesa de arranque.
 * Si la conexión ya está reconectándose no se toca (antes se creaba una segunda conexión y los
 * avisos llegaban dos veces).
 * @returns {Promise<signalR.HubConnection|null>}
 */
export const connectPedidosHub = async () => {
  const auth = getStoredAuth();
  if (!auth?.token) return null;
  detenidoAMano = false;

  if (startPromise) return startPromise;
  if (connection && connection.state !== signalR.HubConnectionState.Disconnected) return connection;

  if (!connection) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL, { accessTokenFactory: () => getStoredAuth()?.token ?? '' })
      .withAutomaticReconnect(politicaReintentos)
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    eventosEnlazados.clear();
    handlers.forEach((_, eventName) => enlazarEvento(eventName));

    const propia = connection;
    connection.onreconnecting(() => notificarEstado(false));
    connection.onreconnected(() => alConectar());
    connection.onclose(() => {
      notificarEstado(false);
      // Se cerró sin que nadie la detuviera (p. ej. el servidor se reinició): volver a intentar.
      if (connection === propia) programarReintento();
    });
  }

  // Arranca la MISMA conexión (con sus eventos ya enlazados); nunca se crea otra en paralelo.
  const actual = connection;
  startPromise = actual
    .start()
    .then(() => {
      alConectar();
      return actual;
    })
    .catch((e) => {
      console.error('No se pudo conectar al hub de pedidos (tiempo real):', e);
      notificarEstado(false);
      if (connection === actual) programarReintento();
      return null;
    })
    .finally(() => {
      startPromise = null;
    });

  return startPromise;
};

/**
 * Suscribe un handler a un evento del hub (NuevoPedido, PedidoEstadoCambiado, PagoRecibido,
 * PagoAnulado, MesaEstadoCambiado, PedidoActualizado, DetalleEstadoCambiado).
 * Puede llamarse antes de que exista la conexión. Retorna la función para desuscribirse.
 * @param {string} eventName
 * @param {(payload: any) => void} handler
 */
export const onHubEvent = (eventName, handler) => {
  if (!handlers.has(eventName)) handlers.set(eventName, new Set());
  handlers.get(eventName).add(handler);
  enlazarEvento(eventName);
  return () => handlers.get(eventName)?.delete(handler);
};

/**
 * Escucha cambios de estado de la conexión (true = en vivo). Llama de inmediato con el estado
 * actual. Retorna la función para dejar de escuchar.
 */
export const onHubConnectionState = (callback) => {
  estadoListeners.add(callback);
  callback(connection?.state === signalR.HubConnectionState.Connected);
  return () => estadoListeners.delete(callback);
};

/**
 * Avisa cuando la conexión VUELVE después de haberse cortado (no en la primera conexión), para
 * recargar los datos de la pantalla: los avisos que llegaron mientras tanto se perdieron.
 * Retorna la función para dejar de escuchar.
 */
export const onHubReconnected = (callback) => {
  reconexionListeners.add(callback);
  return () => reconexionListeners.delete(callback);
};

export const disconnectPedidosHub = async () => {
  detenidoAMano = true;
  if (reintento) {
    clearTimeout(reintento);
    reintento = null;
  }
  yaEstuvoConectado = false;
  const actual = connection;
  connection = null;
  startPromise = null;
  eventosEnlazados.clear();
  if (actual) {
    try {
      await actual.stop();
    } catch {
      // ya estaba detenida
    }
  }
  notificarEstado(false);
};
