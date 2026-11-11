import { useState, useEffect, useCallback, useMemo } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { ShoppingCart, ChefHat, Settings, Banknote, WifiOff, RotateCw } from 'lucide-react';
import toast from 'react-hot-toast';
import { useCart } from '../CartContext';
import { useAuth } from '../context/AuthContext';
import { useClock } from '../hooks/useClock';
import { getTables } from '../services/tablesService';
import { connectPedidosHub, onHubEvent, onHubReconnected } from '../services/signalrService';
import UserSettingsModal from '../components/UserSettingsModal';
import MenuDrawer from '../components/MenuDrawer';
import { etiquetaMesa, prefijoSalon, nombreCorto } from '../utils/mesaLabel';

const STATUS_STYLES = {
  free: {
    dot: 'bg-accentGreen',
    bg: 'bg-emerald-950/40',
    border: 'border-emerald-900/40',
    shadow: 'shadow-glow-green',
    text: 'text-accentGreen',
    label: 'Libre',
  },
  occupied: {
    dot: 'bg-primary',
    bg: 'bg-orange-950/40',
    border: 'border-orange-900/40',
    shadow: 'shadow-glow-orange',
    text: 'text-primary',
    label: 'Ocupada',
  },
  reserved: {
    dot: 'bg-accentYellow',
    bg: 'bg-yellow-950/40',
    border: 'border-yellow-900/40',
    shadow: 'shadow-glow-yellow',
    text: 'text-accentYellow',
    label: 'Reservada',
  },
  maintenance: {
    dot: 'bg-gray-500',
    bg: 'bg-gray-800/40',
    border: 'border-gray-700/40',
    shadow: '',
    text: 'text-gray-400',
    label: 'Mantenimiento',
  },
};

const SALON_KEY = 'restaurant_salon_seleccionado';

const containerVariants = {
  hidden: {},
  // Entrada rápida: con 40 mesas, 0.05 s por mesa tardaba 2 s en mostrarlas todas.
  show: { transition: { staggerChildren: 0.012 } },
};

const cardVariants = {
  hidden: { opacity: 0, scale: 0.92, y: 10 },
  show: { opacity: 1, scale: 1, y: 0, transition: { type: 'spring', stiffness: 260, damping: 20 } },
};

export default function Dashboard() {
  const navigate = useNavigate();
  const location = useLocation();
  const { setActiveTable, activeTable, cart, clearCart } = useCart();
  const { user } = useAuth();
  const { time } = useClock();

  const [tables, setTables] = useState([]);
  // Salón seleccionado en las pestañas (se recuerda por dispositivo: cada mozo suele atender
  // siempre la misma área).
  const [salonSel, setSalonSel] = useState(() => {
    try {
      return Number(localStorage.getItem(SALON_KEY)) || null;
    } catch {
      return null;
    }
  });
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const [errorMesas, setErrorMesas] = useState(null);
  // Mesa que se tocó mientras hay platos sin enviar de OTRA mesa (se pregunta qué hacer con ellos).
  const [cambioMesa, setCambioMesa] = useState(null);

  const loadTables = useCallback(() => {
    getTables()
      .then((mesas) => {
        setTables(mesas);
        setErrorMesas(null);
      })
      // Se conserva el último mapa conocido y se avisa (antes un corte dejaba el mapa vacío).
      .catch((e) => setErrorMesas(e.message));
  }, []);

  // El personal de cocina no usa el mapa de mesas: su inicio es la pantalla de cocina.
  useEffect(() => {
    if (user?.rol === 'COCINA') navigate('/cocina', { replace: true });
  }, [user?.rol, navigate]);

  // Cargar mesas al montar
  useEffect(() => {
    loadTables();
  }, [loadTables]);

  // Tiempo real: refrescar el mapa de mesas cuando cambia el estado de una mesa
  // o cuando se crea/actualiza un pedido (libera u ocupa mesas indirectamente).
  useEffect(() => {
    // Suscripción síncrona (ver signalrService): se limpia siempre en el cleanup, así el doble
    // montaje de StrictMode o volver a esta pantalla nunca deja handlers duplicados.
    connectPedidosHub();
    const unsubscribers = [
      onHubEvent('MesaEstadoCambiado', loadTables),
      onHubEvent('NuevoPedido', loadTables),
      onHubEvent('PedidoEstadoCambiado', (evento) => {
        loadTables();
        // Aviso al mozo cuando cocina termina un pedido (pantalla de cocina en vivo).
        // El id evita duplicados: si el mismo aviso llegara dos veces, se reemplaza.
        if (evento?.estadoNuevo === 'Listo') {
          toast.success(`${etiquetaMesa(evento.salonNombre, evento.mesaNumero)}: pedido listo para servir`, {
            id: `pedido-listo-${evento.pedidoId}`,
            icon: '🍽️',
            duration: 6000,
          });
        }
      }),
      onHubEvent('PagoRecibido', loadTables),
      onHubEvent('PagoAnulado', loadTables),
      // Al volver la conexión pudo haberse perdido algún aviso: se recarga el mapa.
      onHubReconnected(loadTables),
    ];

    return () => {
      unsubscribers.forEach((unsub) => unsub());
    };
  }, [loadTables]);

  // Al volver al dashboard el drawer se cierra, salvo que se venga de "Revisar y enviar" con la
  // flecha atrás: entonces se reabre la carta de la misma mesa para seguir agregando platos.
  useEffect(() => {
    const reabrir = !!location.state?.abrirCarta && !!activeTable;
    setIsMenuOpen(reabrir);
    // Que un F5 no vuelva a abrir la carta.
    if (location.state?.abrirCarta) window.history.replaceState({ ...window.history.state, usr: null }, '');
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [location.key]);

  const abrirMesa = (table) => {
    setActiveTable(table);
    setIsMenuOpen(true);
  };

  const handleTableClick = (table) => {
    if (table.status === 'maintenance') return;
    // Hay platos sin enviar cargados para OTRA mesa: preguntar antes de cambiar, para que no
    // terminen en la cuenta (y en el ticket de cocina) de la mesa equivocada.
    if (cart.length > 0 && activeTable && activeTable.id !== table.id) {
      setCambioMesa(table);
      return;
    }
    abrirMesa(table);
  };

  const firstName = user?.nombreCompleto?.split(' ')[0] ?? 'Camarero';
  const initial = firstName[0]?.toUpperCase() ?? 'C';
  const puedeCobrar = user?.rol === 'CAJERO' || user?.rol === 'ADMIN';
  const esAdmin = user?.rol === 'ADMIN';

  // Salones activos que tienen mesas, en el orden configurado por el administrador.
  const salones = useMemo(() => {
    const vistos = new Map();
    tables
      .filter((t) => t.salonActivo)
      .forEach((t) => {
        const key = t.salonId ?? 0;
        if (!vistos.has(key)) {
          vistos.set(key, { id: key, nombre: t.salonNombre || 'Sin salón', orden: t.salonOrden ?? 0, ocupadas: 0 });
        }
        if (t.status === 'occupied') vistos.get(key).ocupadas += 1;
      });
    return [...vistos.values()].sort((a, b) => a.orden - b.orden || a.nombre.localeCompare(b.nombre));
  }, [tables]);

  const salonActivoId = salones.some((s) => s.id === salonSel) ? salonSel : salones[0]?.id;
  const mesasDelSalon = tables.filter((t) => t.salonActivo && (t.salonId ?? 0) === salonActivoId);

  const elegirSalon = (id) => {
    setSalonSel(id);
    try {
      localStorage.setItem(SALON_KEY, String(id));
    } catch {
      // sin almacenamiento local: solo no se recuerda la pestaña
    }
  };

  // Contadores dinámicos (del salón seleccionado)
  const freeCount = mesasDelSalon.filter((t) => t.status === 'free').length;
  const occupiedCount = mesasDelSalon.filter((t) => t.status === 'occupied').length;
  const reservedCount = mesasDelSalon.filter((t) => t.status === 'reserved').length;

  const cartItemCount = cart.reduce((acc, item) => acc + item.quantity, 0);

  return (
    <div className="min-h-screen bg-background pb-28 relative overflow-x-hidden">
      {/* ── Header ──────────────────────────────────────── */}
      <div className="px-4 pt-4 pb-3 flex justify-between items-center gap-3">
        <div className="min-w-0">
          <p className="text-gray-500 text-sm font-medium tabular-nums">{time}</p>
          <h1 className="text-xl font-extrabold mt-0.5 tracking-tight truncate">
            Hola, {firstName} 👋
          </h1>
          {user?.rol && (
            <p className="text-xs text-gray-500 mt-0.5">{user.rol}</p>
          )}
        </div>
        {/* Accesos con nombre (no solo ícono): se entiende qué hace cada botón sin adivinar.
            «Cobrar» va en naranja porque es la acción principal de la caja. En celular, Cocina y
            Administrar quedan solo con ícono para que entren junto al saludo. */}
        <div className="flex items-center gap-2 shrink-0">
          {esAdmin && (
            <motion.button
              whileTap={{ scale: 0.94 }}
              onClick={() => navigate('/cocina')}
              className="h-12 min-w-12 px-3 sm:px-4 bg-card rounded-full flex items-center justify-center gap-2 border-2 border-gray-700/50 hover:bg-cardHighlight transition-colors"
              title="Pantalla de cocina"
            >
              <ChefHat size={20} className="text-gray-300 shrink-0" />
              <span className="hidden sm:inline text-sm font-bold text-gray-200">Cocina</span>
            </motion.button>
          )}
          {esAdmin && (
            <motion.button
              whileTap={{ scale: 0.94 }}
              onClick={() => navigate('/admin')}
              className="h-12 min-w-12 px-3 sm:px-4 bg-card rounded-full flex items-center justify-center gap-2 border-2 border-gray-700/50 hover:bg-cardHighlight transition-colors"
              title="Panel de administración"
            >
              <Settings size={20} className="text-gray-300 shrink-0" />
              <span className="hidden sm:inline text-sm font-bold text-gray-200">Administrar</span>
            </motion.button>
          )}
          {puedeCobrar && (
            <motion.button
              whileTap={{ scale: 0.94 }}
              onClick={() => navigate('/caja')}
              className={`h-12 min-w-12 ${esAdmin ? 'px-3' : 'px-4'} sm:px-5 bg-primary hover:bg-primaryHover rounded-full flex items-center justify-center gap-2 border-2 border-primary text-white shadow-glow-orange transition-colors`}
              title="Ir a caja"
            >
              <Banknote size={20} className="shrink-0" />
              {/* En celular el admin tiene 4 botones: ahí «Cobrar» queda solo con ícono para no tapar el saludo. */}
              <span className={`${esAdmin ? 'hidden sm:inline' : ''} text-sm font-extrabold`}>Cobrar</span>
            </motion.button>
          )}
          <motion.button
            whileTap={{ scale: 0.9 }}
            onClick={() => setIsSettingsOpen(true)}
            className="w-12 h-12 bg-primary/20 rounded-full flex items-center justify-center border-2 border-primary/40 hover:bg-primary/30 transition-colors"
          >
            <span className="font-extrabold text-lg text-primary">{initial}</span>
          </motion.button>
        </div>
      </div>

      {/* ── Pestañas de salones / áreas ─────────────────── */}
      {salones.length > 0 && (
        <div className="px-4 mb-3 flex gap-2 overflow-x-auto no-scrollbar">
          {salones.map((s) => {
            const activo = s.id === salonActivoId;
            return (
              <motion.button
                key={s.id}
                whileTap={{ scale: 0.95 }}
                onClick={() => elegirSalon(s.id)}
                className={`shrink-0 flex items-center gap-2 px-3.5 py-2 rounded-2xl border text-sm font-bold transition-colors ${
                  activo ? 'bg-primary text-white border-primary shadow-glow-orange' : 'bg-card border-gray-800 text-gray-400 hover:text-ink'
                }`}
              >
                {s.nombre}
                {s.ocupadas > 0 && (
                  <span className={`min-w-5 h-5 px-1.5 rounded-full text-[11px] flex items-center justify-center ${activo ? 'bg-white/25 text-ink' : 'bg-orange-950/60 text-primary'}`}>
                    {s.ocupadas}
                  </span>
                )}
              </motion.button>
            );
          })}
        </div>
      )}

      {/* ── Resumen (compacto: con 40 mesas el mapa debe entrar en una pantalla) ── */}
      <div className="px-4 grid grid-cols-3 gap-2 mb-2">
        {[
          { count: freeCount, label: 'Libres', color: 'text-accentGreen', border: 'border-emerald-900/30', bg: 'bg-emerald-950/30' },
          { count: occupiedCount, label: 'Ocupadas', color: 'text-primary', border: 'border-orange-900/30', bg: 'bg-orange-950/30' },
          { count: reservedCount, label: 'Reservadas', color: 'text-accentYellow', border: 'border-yellow-900/30', bg: 'bg-yellow-950/30' },
        ].map(({ count, label, color, border, bg }) => (
          <motion.div
            key={label}
            initial={{ opacity: 0, y: -8 }}
            animate={{ opacity: 1, y: 0 }}
            className={`${bg} border ${border} px-2 py-1.5 rounded-xl flex items-center justify-center gap-1.5`}
          >
            <p className={`text-lg font-extrabold leading-none ${color}`}>{count}</p>
            <p className={`text-[10px] font-bold uppercase tracking-wider ${color} opacity-80`}>
              {label}
            </p>
          </motion.div>
        ))}
      </div>

      {/* ── Legend ──────────────────────────────────────── */}
      <div className="px-4 flex gap-x-4 gap-y-1 mb-3 text-[11px] font-medium text-gray-500 flex-wrap">
        {Object.entries(STATUS_STYLES).map(([key, s]) => (
          <div key={key} className="flex items-center gap-1.5">
            <span className={`w-2 h-2 rounded-full ${s.dot}`} />
            {s.label}
          </div>
        ))}
      </div>

      {/* ── Tables grid ─────────────────────────────────── */}
      <motion.div
        variants={containerVariants}
        initial="hidden"
        animate="show"
        key={salonActivoId ?? 'todas'}
        // Mesas compactas: 5 por fila en celular; en tablet/PC tantas como entren (~110 px cada una).
        className="px-4 grid grid-cols-5 sm:grid-cols-[repeat(auto-fill,minmax(110px,1fr))] gap-2"
      >
        {mesasDelSalon.map((table) => {
          const style = STATUS_STYLES[table.status] ?? STATUS_STYLES.free;
          return (
            <motion.button
              key={table.id}
              variants={cardVariants}
              whileTap={{ scale: table.status === 'maintenance' ? 1 : 0.93 }}
              onClick={() => handleTableClick(table)}
              disabled={table.status === 'maintenance'}
              title={`${table.salonNombre ? `${table.salonNombre} · ` : ''}Mesa ${table.numeroMesa} · ${style.label}${table.atendidoPor ? ` · ${table.atendidoPor}` : ''}`}
              className={`relative p-1 rounded-xl ${style.bg} border ${style.border} flex flex-col items-center justify-center aspect-square transition-shadow ${
                table.status === 'maintenance' ? 'opacity-60 cursor-not-allowed' : `hover:${style.shadow}`
              }`}
            >
              {/* Status dot */}
              <span className={`absolute top-1 right-1 sm:top-1.5 sm:right-1.5 w-1.5 h-1.5 sm:w-2 sm:h-2 rounded-full ${style.dot}`} />

              {/* Etiqueta: primera palabra del salón + número (p.ej. "Terraza 1"). Grande y oscura
                  para leerse de lejos en el monitor de caja. */}
              {prefijoSalon(table.salonNombre) && (
                <p className="text-[10px] sm:text-sm font-extrabold uppercase sm:tracking-wide text-gray-300 leading-none max-w-full truncate px-0.5">
                  {prefijoSalon(table.salonNombre)}
                </p>
              )}
              {/* El estado se ve por el color de la mesa y su punto (ver leyenda) */}
              <h2 className={`text-xl sm:text-2xl font-extrabold leading-tight tracking-tight ${table.status === 'free' ? 'text-ink' : style.text}`}>{table.numeroMesa}</h2>

              {/* Rótulo: quién atiende la mesa ocupada */}
              {table.status === 'occupied' && table.atendidoPor && (
                <span
                  className={`absolute bottom-1 left-1 right-1 truncate text-[8px] sm:text-[9px] leading-tight font-bold px-1 py-px rounded ${
                    table.atendidoPorUsuarioId === user?.usuarioId
                      ? 'bg-primary text-white'
                      : 'bg-gray-950/40 text-gray-200'
                  }`}
                  title={`Atendida por ${table.atendidoPor}`}
                >
                  {table.atendidoPorUsuarioId === user?.usuarioId ? 'Tú' : nombreCorto(table.atendidoPor)}
                </span>
              )}
            </motion.button>
          );
        })}
      </motion.div>

      {errorMesas && (
        <div className="mx-4 mt-4 flex items-center gap-3 rounded-2xl border border-red-500/40 bg-red-500/10 px-4 py-3 text-sm text-red-300">
          <WifiOff size={18} className="shrink-0" />
          <span className="flex-1 min-w-0">Sin conexión con el servidor: el mapa puede no estar al día. ({errorMesas})</span>
          <button type="button" onClick={loadTables} className="shrink-0 flex items-center gap-1 font-bold text-red-200">
            <RotateCw size={14} /> Reintentar
          </button>
        </div>
      )}

      {mesasDelSalon.length === 0 && !errorMesas && (
        <p className="px-5 text-center text-sm text-gray-600 mt-10">
          Aún no hay mesas configuradas. Pídele al administrador que las cree desde el panel.
        </p>
      )}

      <AnimatePresence>
        {cambioMesa && activeTable && (
          <motion.div
            key="cambio-mesa"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-[70] bg-black/70 flex items-end sm:items-center justify-center p-4"
            onClick={() => setCambioMesa(null)}
          >
            <motion.div
              initial={{ y: 30 }}
              animate={{ y: 0 }}
              onClick={(e) => e.stopPropagation()}
              role="dialog"
              aria-label="Platos sin enviar de otra mesa"
              className="w-full max-w-sm bg-card border border-gray-700/50 rounded-3xl p-5 space-y-3"
            >
              <p className="text-xs font-bold uppercase tracking-wider text-amber-400">Platos sin enviar</p>
              <h3 className="text-lg font-extrabold text-ink leading-tight">
                Tienes {cartItemCount} plato{cartItemCount === 1 ? '' : 's'} cargado{cartItemCount === 1 ? '' : 's'} para{' '}
                {etiquetaMesa(activeTable.salonNombre, activeTable.numeroMesa)}
              </h3>
              <p className="text-sm text-gray-400">
                Todavía no se enviaron a cocina. ¿Qué hacemos con ellos antes de abrir{' '}
                {etiquetaMesa(cambioMesa.salonNombre, cambioMesa.numeroMesa)}?
              </p>
              <button
                type="button"
                onClick={() => { setCambioMesa(null); setIsMenuOpen(true); }}
                className="w-full py-3 rounded-2xl bg-primary text-white font-bold text-sm"
              >
                Volver a {etiquetaMesa(activeTable.salonNombre, activeTable.numeroMesa)} y enviarlos
              </button>
              <button
                type="button"
                onClick={() => { const destino = cambioMesa; setCambioMesa(null); abrirMesa(destino); }}
                className="w-full py-3 rounded-2xl border border-gray-700 text-ink font-bold text-sm"
              >
                Llevarlos a {etiquetaMesa(cambioMesa.salonNombre, cambioMesa.numeroMesa)}
              </button>
              <button
                type="button"
                onClick={() => { const destino = cambioMesa; setCambioMesa(null); clearCart(); abrirMesa(destino); }}
                className="w-full py-3 rounded-2xl text-red-400 font-bold text-sm"
              >
                Descartarlos y abrir {etiquetaMesa(cambioMesa.salonNombre, cambioMesa.numeroMesa)}
              </button>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* ── Modals ──────────────────────────────────────── */}
      <AnimatePresence>
        {isSettingsOpen && <UserSettingsModal onClose={() => setIsSettingsOpen(false)} />}
      </AnimatePresence>
      <MenuDrawer isOpen={isMenuOpen} onClose={() => setIsMenuOpen(false)} />

      {/* ── Floating cart button ─────────────────────────── */}
      <motion.button
        whileTap={{ scale: 0.9 }}
        onClick={() => navigate('/cart')}
        className={`fixed bottom-8 left-5 rounded-full w-16 h-16 flex items-center justify-center shadow-xl z-40 border-2 border-background transition-all ${
          cart.length > 0
            ? 'bg-primary shadow-glow-orange'
            : 'bg-card border-gray-700/50'
        }`}
      >
        <ShoppingCart size={24} className={cart.length > 0 ? "text-white" : "text-ink"} />
        {cartItemCount > 0 && (
          <motion.span
            key={cartItemCount}
            initial={{ scale: 0.5 }}
            animate={{ scale: 1 }}
            className="absolute -top-1 -left-1 bg-red-500 text-white rounded-full w-6 h-6 text-xs flex items-center justify-center font-bold border-2 border-background"
          >
            {cartItemCount}
          </motion.span>
        )}
      </motion.button>
    </div>
  );
}
