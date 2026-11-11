import { defineConfig, type ProxyOptions } from 'vite'
import react from '@vitejs/plugin-react'

// Backend .NET (perfil "http" de CaraNegra.API/Properties/launchSettings.json).
// Se usa 127.0.0.1 y no "localhost" porque en Windows "localhost" puede resolverse a ::1 (IPv6).
const BACKEND = 'http://127.0.0.1:5014'

// Proxy: el navegador solo habla con Vite (mismo origen), y Vite reenvía la API y el hub de
// SignalR al backend. Así la app funciona abierta desde OTRA máquina (la laptop, los celulares)
// a través de un túnel, sin exponer el backend ni tocar CORS.
// xfwd: agrega X-Forwarded-For con la IP real del cliente (la usa el rate limiting del backend).
const proxy: Record<string, ProxyOptions> = {
  '/api': { target: BACKEND, changeOrigin: true, xfwd: true },
  '/hubs': { target: BACKEND, changeOrigin: true, xfwd: true, ws: true },
}

// Dominios de los túneles con los que se presenta la demo (Cloudflare quick tunnel y
// Tailscale Funnel). Vite rechaza por seguridad cualquier otro nombre de host.
const allowedHosts = ['.trycloudflare.com', '.ts.net']

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  // "npm run demo" compila con --mode demo: la app llama a la API con rutas relativas
  // (/api/v1 y /hubs/pedidos), que resuelve el proxy de arriba. Se fija aquí para que un
  // .env.local con VITE_API_URL=http://localhost:5014/... no lo pise (las variables que ya
  // existen en process.env tienen prioridad sobre los archivos .env).
  if (mode === 'demo') {
    process.env.VITE_API_URL = '/api/v1'
  }

  return {
    plugins: [react()],
    server: { proxy, allowedHosts },
    preview: {
      host: '127.0.0.1',
      port: 4173,
      strictPort: true,
      proxy,
      allowedHosts,
    },
  }
})
