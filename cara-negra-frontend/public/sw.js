// ============================================================
//  sw.js — Service worker de Cara Negra (Fase 6, PWA)
// ------------------------------------------------------------
//  Objetivo: que la app se pueda "instalar" (icono en el celular/tablet
//  del mozo o de caja) y que la cáscara (shell) cargue rápido/offline.
//
//  A propósito NO cachea nada de /api/**: este es un sistema de pedidos,
//  caja e inventario en vivo — servir una respuesta vieja de stock, del
//  estado de una mesa o de un pedido sería peor que no tener red. Los
//  datos siempre van a la red; solo el "cascarón" estático (HTML/JS/CSS/
//  iconos) se sirve desde caché para que abra instantáneo.
// ============================================================

const CACHE_NAME = 'cara-negra-shell-v1';

const SHELL_ASSETS = [
  '/',
  '/manifest.webmanifest',
  '/icon-192.png',
  '/icon-512.png',
  '/favicon.svg',
];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => cache.addAll(SHELL_ASSETS))
  );
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(
        keys
          .filter((key) => key !== CACHE_NAME)
          .map((key) => caches.delete(key))
      )
    )
  );
  self.clients.claim();
});

self.addEventListener('fetch', (event) => {
  const { request } = event;

  // Solo GET, mismo origen, y nunca /api/**: los datos del negocio (pedidos,
  // mesas, caja, inventario) siempre deben ir a la red, nunca a caché.
  const url = new URL(request.url);
  const esMismoOrigen = url.origin === self.location.origin;
  const esApi = url.pathname.startsWith('/api/') || url.pathname.startsWith('/hubs/');

  if (request.method !== 'GET' || !esMismoOrigen || esApi) {
    return;
  }

  // Network-first para la navegación (el HTML principal): si hay red, se
  // usa la versión más reciente; si no, se cae al shell cacheado.
  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request)
        .then((response) => {
          const clone = response.clone();
          caches.open(CACHE_NAME).then((cache) => cache.put('/', clone));
          return response;
        })
        .catch(() => caches.match('/'))
    );
    return;
  }

  // Cache-first para el resto de assets estáticos (JS/CSS/imágenes/fuentes
  // ya construidos por Vite): son inmutables por build, así que priorizar
  // caché acelera cargas repetidas sin riesgo de mostrar datos viejos.
  event.respondWith(
    caches.match(request).then((cached) => {
      if (cached) return cached;
      return fetch(request).then((response) => {
        const clone = response.clone();
        caches.open(CACHE_NAME).then((cache) => cache.put(request, clone));
        return response;
      });
    })
  );
});
