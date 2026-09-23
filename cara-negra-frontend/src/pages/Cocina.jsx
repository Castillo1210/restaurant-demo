import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import {
  ChefHat, Clock, CheckCircle2, Circle, Flame, Volume2, VolumeX, Maximize, Minimize,
  LogOut, ChevronLeft, Wifi, WifiOff, StickyNote, Printer,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { useAuth } from '../context/AuthContext';
import { useClock } from '../hooks/useClock';
import { getPedidosCocina, cambiarEstadoItem, cambiarEstadoPedidoCocina } from '../services/cocinaService';
import { connectPedidosHub, onHubEvent } from '../services/signalrService';
import { parseFechaUtc } from '../utils/documentos';

/**
 * Pantalla de cocina (KDS) en tiempo real.
 *
 * - Los pedidos llegan al instante por SignalR (con aviso sonoro) y se ordenan del más antiguo
 *   al más nuevo: cocina siempre ve primero lo que lleva más tiempo esperando.
 * - Cada ítem se toca para avanzar: Pendiente → En preparación → Listo. El pedido cambia de
 *   columna solo cuando corresponde (ver CocinaEstadoHelper en el backend).
 * - Convive con la comanda impresa: la ticketera sigue imprimiendo cada pedido.
 */

const COLUMNAS = [
  { estado: 'Pendiente', titulo: 'Nuevos', color: 'text-accentYellow', borde: 'border-yellow-700/50', fondo: 'bg-yellow-950/20' },
  { estado: 'EnPreparacion', titulo: 'En preparación', color: 'text-primary', borde: 'border-orange-700/50', fondo: 'bg-orange-950/20' },
  { estado: 'Listo', titulo: 'Listos para servir', color: 'text-accentGreen', borde: 'border-emerald-700/50', fondo: 'bg-emerald-950/20' },
];

const SIGUIENTE_ESTADO_ITEM = {
  Pendiente: 'EnPreparacion',
  EnPreparacion: 'Listo',
  Listo: 'EnPreparacion', // deshacer
};

const MINUTOS_ALERTA = 10;
const MINUTOS_CRITICO = 20;
const POLLING_MS = 20000; // respaldo por si se cae la conexión en tiempo real

// Aviso sonoro corto generado con Web Audio (no requiere archivos de audio).
const reproducirAviso = () => {
  try {
    const Ctx = window.AudioContext || window.webkitAudioContext;
    if (!Ctx) return;
    const ctx = new Ctx();
    [0, 0.18].forEach((delay, i) => {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.type = 'sine';
      osc.frequency.value = i === 0 ? 880 : 1175;
      gain.gain.setValueAtTime(0.0001, ctx.currentTime + delay);
      gain.gain.exponentialRampToValueAtTime(0.35, ctx.currentTime + delay + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + delay + 0.16);
      osc.connect(gain).connect(ctx.destination);
      osc.start(ctx.currentTime + delay);
      osc.stop(ctx.currentTime + delay + 0.18);
    });
    setTimeout(() => ctx.close(), 600);
  } catch {
    // Sin audio disponible: no es crítico.
  }
};

const minutosDesde = (fecha, ahora) => {
  const d = parseFechaUtc(fecha);
  return d ? Math.max(0, Math.floor((ahora - d.getTime()) / 60000)) : 0;
};

const estiloTiempo = (min) => {
  if (min >= MINUTOS_CRITICO) return 'bg-red-600 text-white animate-pulse';
  if (min >= MINUTOS_ALERTA) return 'bg-accentYellow text-black';
  return 'bg-white/10 text-gray-200';
};

function IconoEstadoItem({ estado }) {
  if (estado === 'Listo') return <CheckCircle2 size={22} className="text-accentGreen shrink-0" />;
  if (estado === 'EnPreparacion') return <Flame size={22} className="text-primary shrink-0" />;
  return <Circle size={22} className="text-gray-500 shrink-0" />;
}

function TarjetaPedido({ pedido, ahora, esNuevo, ocupado, onItem, onPedido }) {
  const minutos = minutosDesde(pedido.creadoEn, ahora);
  const creado = parseFechaUtc(pedido.creadoEn);
  const listos = pedido.items.filter((i) => i.estado === 'Listo').length;
  const esListo = pedido.estadoPedido === 'Listo';

  return (
    <motion.div
      layout
      initial={{ opacity: 0, scale: 0.9, y: -10 }}
      animate={{ opacity: 1, scale: 1, y: 0 }}
      exit={{ opacity: 0, scale: 0.9 }}
      transition={{ type: 'spring', stiffness: 300, damping: 26 }}
      className={`rounded-2xl border bg-card overflow-hidden ${
        esNuevo ? 'border-accentYellow shadow-glow-yellow ring-2 ring-accentYellow/60' : minutos >= MINUTOS_CRITICO && !esListo ? 'border-red-600' : 'border-gray-800'
      }`}
    >
      {/* Encabezado */}
      <div className="px-4 pt-3 pb-2 flex items-start justify-between gap-2 border-b border-gray-800/80">
        <div>
          <p className="text-2xl font-black tracking-tight leading-none">Mesa {pedido.mesaNumero}</p>
          <p className="text-xs text-gray-500 mt-1">
            #{pedido.pedidoId} · {pedido.mozoNombre?.split(' ')[0]} ·{' '}
            {creado?.toLocaleTimeString('es-PE', { hour: '2-digit', minute: '2-digit' })}
          </p>
        </div>
        <div className="flex flex-col items-end gap-1">
          <span className={`px-2.5 py-1 rounded-lg text-sm font-black tabular-nums flex items-center gap-1 ${estiloTiempo(esListo ? 0 : minutos)}`}>
            <Clock size={14} /> {minutos} min
          </span>
          {esNuevo && <span className="text-[10px] font-black text-accentYellow tracking-widest">¡NUEVO!</span>}
        </div>
      </div>

      {/* Ítems */}
      <div className="p-2 space-y-1">
        {pedido.items.map((item) => {
          const creadoItem = parseFechaUtc(item.creadoEn);
          const esAdicional = creado && creadoItem && creadoItem.getTime() - creado.getTime() > 60000;
          return (
            <button
              key={item.detallePedidoId}
              disabled={ocupado}
              onClick={() => onItem(pedido, item)}
              className={`w-full flex items-start gap-3 text-left px-2.5 py-2 rounded-xl transition-colors disabled:opacity-60 ${
                item.estado === 'Listo' ? 'bg-emerald-950/30 hover:bg-emerald-950/50' : item.estado === 'EnPreparacion' ? 'bg-orange-950/30 hover:bg-orange-950/50' : 'hover:bg-white/5'
              }`}
            >
              <IconoEstadoItem estado={item.estado} />
              <div className="flex-1 min-w-0">
                <p className={`text-lg font-bold leading-tight ${item.estado === 'Listo' ? 'line-through text-gray-500' : 'text-white'}`}>
                  <span className="text-primary font-black mr-1.5">{item.cantidad}×</span>
                  {item.productoNombre}
                  {esAdicional && (
                    <span className="ml-2 align-middle text-[10px] font-black px-1.5 py-0.5 rounded bg-sky-600 text-white no-underline">ADICIONAL</span>
                  )}
                </p>
                {item.notas && (
                  <p className="mt-1 text-sm font-semibold text-yellow-300 bg-yellow-950/40 border border-yellow-800/40 rounded-lg px-2 py-1 flex items-start gap-1.5">
                    <StickyNote size={14} className="mt-0.5 shrink-0" /> {item.notas}
                  </p>
                )}
              </div>
            </button>
          );
        })}
      </div>

      {/* Acción principal */}
      <div className="px-3 pb-3">
        {pedido.estadoPedido === 'Pendiente' && (
          <button
            disabled={ocupado}
            onClick={() => onPedido(pedido, 'EnPreparacion')}
            className="w-full py-3 rounded-xl bg-primary hover:bg-primaryHover text-white font-black text-base flex items-center justify-center gap-2 disabled:opacity-60"
          >
            <Flame size={18} /> Empezar
          </button>
        )}
        {pedido.estadoPedido === 'EnPreparacion' && (
          <button
            disabled={ocupado}
            onClick={() => onPedido(pedido, 'Listo')}
            className="w-full py-3 rounded-xl bg-accentGreen hover:bg-emerald-600 text-white font-black text-base flex items-center justify-center gap-2 disabled:opacity-60"
          >
            <CheckCircle2 size={18} /> Pedido listo ({listos}/{pedido.items.length})
          </button>
        )}
        {esListo && (
          <p className="text-center text-sm font-bold text-accentGreen py-2">Listo · esperando al mozo</p>
        )}
      </div>
    </motion.div>
  );
}

export default function Cocina() {
  const navigate = useNavigate();
  const { user, logout } = useAuth();
  const { time } = useClock('es-PE');

  const [pedidos, setPedidos] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [conectado, setConectado] = useState(false);
  const [sonido, setSonido] = useState(true);
  const [pantallaCompleta, setPantallaCompleta] = useState(false);
  const [nuevos, setNuevos] = useState(() => new Set());
  const [ocupados, setOcupados] = useState(() => new Set());
  const [ahora, setAhora] = useState(Date.now());

  const sonidoRef = useRef(sonido);
  sonidoRef.current = sonido;
  const refrescoPendiente = useRef(null);

  const cargar = useCallback(() => {
    return getPedidosCocina()
      .then(setPedidos)
      .catch(() => toast.error('No se pudieron cargar los pedidos de cocina'))
      .finally(() => setIsLoading(false));
  }, []);

  // Agrupa ráfagas de eventos (p.ej. varios ítems marcados seguidos) en un solo refresco.
  const refrescar = useCallback(() => {
    clearTimeout(refrescoPendiente.current);
    refrescoPendiente.current = setTimeout(cargar, 250);
  }, [cargar]);

  useEffect(() => {
    cargar();
    const polling = setInterval(cargar, POLLING_MS);
    const reloj = setInterval(() => setAhora(Date.now()), 15000);
    return () => {
      clearInterval(polling);
      clearInterval(reloj);
      clearTimeout(refrescoPendiente.current);
    };
  }, [cargar]);

  // Tiempo real
  useEffect(() => {
    let unsubs = [];
    connectPedidosHub().then((connection) => {
      setConectado(!!connection);
      if (!connection) return;

      connection.onreconnecting?.(() => setConectado(false));
      connection.onreconnected?.(() => {
        setConectado(true);
        cargar();
      });

      unsubs = [
        onHubEvent('NuevoPedido', (evento) => {
          if (sonidoRef.current) reproducirAviso();
          toast(`Nuevo pedido · Mesa ${evento?.mesaNumero ?? ''}`, { icon: '🔔' });
          const id = evento?.pedidoId;
          if (id) {
            setNuevos((prev) => new Set(prev).add(id));
            setTimeout(() => {
              setNuevos((prev) => {
                const next = new Set(prev);
                next.delete(id);
                return next;
              });
            }, 15000);
          }
          refrescar();
        }),
        onHubEvent('PedidoActualizado', (evento) => {
          if (sonidoRef.current) reproducirAviso();
          toast(`Mesa ${evento?.mesaNumero ?? ''}: pedido modificado`, { icon: '✏️' });
          refrescar();
        }),
        onHubEvent('PedidoEstadoCambiado', refrescar),
        onHubEvent('DetalleEstadoCambiado', refrescar),
        onHubEvent('PagoRecibido', refrescar),
      ];
    });
    return () => unsubs.forEach((u) => u());
  }, [cargar, refrescar]);

  useEffect(() => {
    const onFs = () => setPantallaCompleta(!!document.fullscreenElement);
    document.addEventListener('fullscreenchange', onFs);
    return () => document.removeEventListener('fullscreenchange', onFs);
  }, []);

  const conOcupado = async (pedidoId, accion) => {
    setOcupados((prev) => new Set(prev).add(pedidoId));
    try {
      const actualizado = await accion();
      // Actualización optimista con la respuesta del servidor (sin esperar al evento).
      setPedidos((prev) => prev.map((p) => (p.pedidoId === actualizado.pedidoId ? actualizado : p)));
    } catch (err) {
      toast.error(err.message);
      cargar();
    } finally {
      setOcupados((prev) => {
        const next = new Set(prev);
        next.delete(pedidoId);
        return next;
      });
    }
  };

  const handleItem = (pedido, item) =>
    conOcupado(pedido.pedidoId, () =>
      cambiarEstadoItem(pedido.pedidoId, item.detallePedidoId, SIGUIENTE_ESTADO_ITEM[item.estado])
    );

  const handlePedido = (pedido, estado) =>
    conOcupado(pedido.pedidoId, () => cambiarEstadoPedidoCocina(pedido.pedidoId, estado));

  const togglePantallaCompleta = () => {
    if (document.fullscreenElement) document.exitFullscreen?.();
    else document.documentElement.requestFullscreen?.();
  };

  const toggleSonido = () => {
    setSonido((s) => {
      // Al activar, se reproduce un aviso de prueba: además "desbloquea" el audio del
      // navegador, que exige una interacción del usuario antes de poder sonar.
      if (!s) reproducirAviso();
      return !s;
    });
  };

  const porColumna = useMemo(() => {
    const grupos = { Pendiente: [], EnPreparacion: [], Listo: [] };
    pedidos.forEach((p) => grupos[p.estadoPedido]?.push(p));
    return grupos;
  }, [pedidos]);

  const esAdmin = user?.rol === 'ADMIN';

  return (
    <div className="min-h-screen bg-[#070707] text-white flex flex-col">
      {/* Barra superior */}
      <header className="px-4 sm:px-6 py-3 flex items-center gap-3 border-b border-gray-800/80 bg-black/60 sticky top-0 z-20 backdrop-blur">
        {esAdmin && (
          <button onClick={() => navigate('/admin')} className="p-2 rounded-full bg-card border border-gray-800 hover:bg-cardHighlight" title="Volver">
            <ChevronLeft size={20} />
          </button>
        )}
        <ChefHat size={28} className="text-primary" />
        <div className="flex-1 min-w-0">
          <h1 className="text-xl sm:text-2xl font-black tracking-tight leading-none">Cocina</h1>
          <p className="text-xs text-gray-500 mt-0.5 flex items-center gap-1.5">
            {conectado ? (
              <><Wifi size={12} className="text-accentGreen" /> En vivo</>
            ) : (
              <><WifiOff size={12} className="text-red-500" /> Sin conexión en vivo · actualizando cada {POLLING_MS / 1000}s</>
            )}
            <span className="hidden sm:inline">· <Printer size={12} className="inline -mt-0.5" /> comanda impresa activa</span>
          </p>
        </div>
        <p className="text-2xl sm:text-3xl font-black tabular-nums">{time}</p>
        <button onClick={toggleSonido} className="p-2.5 rounded-full bg-card border border-gray-800 hover:bg-cardHighlight" title={sonido ? 'Silenciar' : 'Activar sonido'}>
          {sonido ? <Volume2 size={20} /> : <VolumeX size={20} className="text-gray-500" />}
        </button>
        <button onClick={togglePantallaCompleta} className="p-2.5 rounded-full bg-card border border-gray-800 hover:bg-cardHighlight hidden sm:block" title="Pantalla completa">
          {pantallaCompleta ? <Minimize size={20} /> : <Maximize size={20} />}
        </button>
        {!esAdmin && (
          <button onClick={logout} className="p-2.5 rounded-full bg-card border border-gray-800 hover:bg-cardHighlight" title="Cerrar sesión">
            <LogOut size={20} />
          </button>
        )}
      </header>

      {/* Tablero */}
      <main className="flex-1 grid grid-cols-1 md:grid-cols-3 gap-3 sm:gap-4 p-3 sm:p-4">
        {COLUMNAS.map((col) => {
          const lista = porColumna[col.estado];
          return (
            <section key={col.estado} className={`rounded-3xl border ${col.borde} ${col.fondo} flex flex-col min-h-[30vh] md:min-h-0`}>
              <div className="px-4 py-3 flex items-center justify-between">
                <h2 className={`text-lg font-black uppercase tracking-wide ${col.color}`}>{col.titulo}</h2>
                <span className={`min-w-8 h-8 px-2 rounded-full flex items-center justify-center text-base font-black bg-black/40 ${col.color}`}>
                  {lista.length}
                </span>
              </div>
              <div className="flex-1 overflow-y-auto px-3 pb-3 space-y-3 md:max-h-[calc(100vh-9rem)] no-scrollbar">
                <AnimatePresence mode="popLayout">
                  {lista.map((p) => (
                    <TarjetaPedido
                      key={p.pedidoId}
                      pedido={p}
                      ahora={ahora}
                      esNuevo={nuevos.has(p.pedidoId)}
                      ocupado={ocupados.has(p.pedidoId)}
                      onItem={handleItem}
                      onPedido={handlePedido}
                    />
                  ))}
                </AnimatePresence>
                {!isLoading && lista.length === 0 && (
                  <p className="text-center text-sm text-gray-600 py-10">Sin pedidos</p>
                )}
              </div>
            </section>
          );
        })}
      </main>
    </div>
  );
}
